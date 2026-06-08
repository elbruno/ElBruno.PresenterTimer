using System.Collections.Concurrent;
using System.Text.Json;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Real-time speech analysis service backed by NAudio microphone capture, Whisper transcription,
/// and local LLM relevance analysis with graceful fallbacks.
/// </summary>
public sealed class SpeechAnalysisService : ISpeechAnalysisService
{
    internal const int TargetSampleRate = 16000;
    internal const int ChunkMilliseconds = 500;
    internal const int SamplesPerChunk = TargetSampleRate * ChunkMilliseconds / 1000;
    internal const int BytesPerChunk = SamplesPerChunk * sizeof(short);
    internal const int CircularBufferCapacityBytes = BytesPerChunk * 20;
    internal const int SilenceChunksBeforeTranscription = 2;
    internal const int MinimumUtteranceBytes = BytesPerChunk;
    internal const double SilenceThreshold = 0.015d;

    private readonly AppSettings _settings;
    private readonly IAlertService _alertService;
    private readonly ILogger<SpeechAnalysisService> _logger;
    private readonly ISpeechAudioInputFactory _audioInputFactory;
    private readonly IWhisperTranscriber _whisperTranscriber;
    private readonly IRelevanceAnalyzer _relevanceAnalyzer;
    private readonly ConcurrentQueue<byte[]> _audioQueue = new();
    private readonly SemaphoreSlim _audioSignal = new(0, int.MaxValue);
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly CircularPcmBuffer _circularBuffer = new(CircularBufferCapacityBytes);
    private readonly List<byte> _utteranceBuffer = [];
    private readonly object _contextGate = new();

    private CancellationTokenSource? _listeningCts;
    private Task? _captureTask;
    private ISpeechAudioInput? _audioInput;
    private bool _isListening;
    private bool _disposed;
    private int _silentChunkCount;
    private SessionPlan? _currentPlan;
    private int _currentSectionIndex = -1;

    public SpeechAnalysisService(
        AppSettings settings,
        IAlertService alertService,
        ILogger<SpeechAnalysisService> logger)
        : this(
            settings,
            alertService,
            logger,
            new NAudioSpeechAudioInputFactory(),
            new WhisperTranscriber(LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<WhisperTranscriber>()),
            new LocalLlmRelevanceAnalyzer(LoggerFactory.Create(builder => builder.AddDebug())))
    {
    }

    internal SpeechAnalysisService(
        AppSettings settings,
        IAlertService alertService,
        ILogger<SpeechAnalysisService> logger,
        ISpeechAudioInputFactory audioInputFactory,
        IWhisperTranscriber whisperTranscriber,
        IRelevanceAnalyzer relevanceAnalyzer)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _audioInputFactory = audioInputFactory ?? throw new ArgumentNullException(nameof(audioInputFactory));
        _whisperTranscriber = whisperTranscriber ?? throw new ArgumentNullException(nameof(whisperTranscriber));
        _relevanceAnalyzer = relevanceAnalyzer ?? throw new ArgumentNullException(nameof(relevanceAnalyzer));
    }

    public bool IsListening => _isListening;

    public event EventHandler<TranscriptionEventArgs>? TranscriptionReceived;
    public event EventHandler<AnalysisEventArgs>? AnalysisReceived;
    public event EventHandler<AlertEventArgs>? AlertRaised;

    public async Task StartListeningAsync()
    {
        ThrowIfDisposed();

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isListening)
                return;

            if (!_settings.SpeechAnalysis.Enabled)
            {
                RaiseSpeechAlert("Speech analysis is disabled in Settings.");
                return;
            }

            using var loadCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await _whisperTranscriber.EnsureLoadedAsync(_settings.SpeechAnalysis, loadCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                RaiseSpeechAlert("Whisper model loading timed out. Speech analysis was disabled.");
                return;
            }
            catch (Exception ex)
            {
                HandleSpeechError("Unable to load the Whisper model.", ex, userFacing: true);
                return;
            }

            _listeningCts = new CancellationTokenSource();
            _captureTask = Task.Run(() => _CaptureAudioLoop(_listeningCts.Token));
            _isListening = true;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StopListeningAsync()
    {
        ThrowIfDisposed();

        CancellationTokenSource? cts;
        Task? captureTask;

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isListening)
                return;

            _isListening = false;
            cts = _listeningCts;
            captureTask = _captureTask;
            _listeningCts = null;
            _captureTask = null;
        }
        finally
        {
            _stateLock.Release();
        }

        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _audioSignal.Release();

        if (captureTask is not null)
        {
            try
            {
                await captureTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public void UpdatePresentationContext(SessionPlan? plan, int currentSectionIndex)
    {
        lock (_contextGate)
        {
            _currentPlan = plan;
            _currentSectionIndex = currentSectionIndex;
        }
    }

    private async Task _CaptureAudioLoop(CancellationToken cancellationToken)
    {
        try
        {
            _audioInput = _audioInputFactory.Create();
            _audioInput.AudioChunkReceived += OnAudioChunkReceived;
            _audioInput.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                await _audioSignal.WaitAsync(cancellationToken).ConfigureAwait(false);

                while (_audioQueue.TryDequeue(out byte[]? chunk))
                    await _ProcessAudioBuffer(chunk, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Speech capture loop cancelled.");
        }
        catch (Exception ex)
        {
            HandleSpeechError("Microphone capture failed.", ex, userFacing: true);
            _isListening = false;
        }
        finally
        {
            try
            {
                await FlushPendingUtteranceAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to flush the final utterance.");
            }

            if (_audioInput is not null)
            {
                _audioInput.AudioChunkReceived -= OnAudioChunkReceived;
                _audioInput.Stop();
                _audioInput.Dispose();
                _audioInput = null;
            }
        }
    }

    private async Task _ProcessAudioBuffer(byte[] chunk, CancellationToken cancellationToken)
    {
        _circularBuffer.Write(chunk);

        bool containsSpeech = CalculateNormalizedRms(chunk) >= SilenceThreshold;
        if (containsSpeech)
        {
            _utteranceBuffer.AddRange(chunk);
            _silentChunkCount = 0;
            return;
        }

        if (_utteranceBuffer.Count == 0)
            return;

        _silentChunkCount++;
        if (_silentChunkCount < SilenceChunksBeforeTranscription)
            return;

        await FlushPendingUtteranceAsync(cancellationToken).ConfigureAwait(false);
    }

    internal Task ProcessAudioChunkForTestsAsync(byte[] chunk, CancellationToken cancellationToken = default)
        => _ProcessAudioBuffer(chunk, cancellationToken);

    private async Task FlushPendingUtteranceAsync(CancellationToken cancellationToken)
    {
        if (_utteranceBuffer.Count < MinimumUtteranceBytes)
        {
            _utteranceBuffer.Clear();
            _silentChunkCount = 0;
            return;
        }

        byte[] utterance = _utteranceBuffer.ToArray();
        _utteranceBuffer.Clear();
        _silentChunkCount = 0;

        try
        {
            using MemoryStream wavStream = CreateWaveStream(utterance);
            SpeechTranscription transcription = await _whisperTranscriber.TranscribeAsync(wavStream, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(transcription.Text))
                return;

            TranscriptionReceived?.Invoke(this, new TranscriptionEventArgs
            {
                TranscribedText = transcription.Text,
                Confidence = transcription.Confidence,
                Timestamp = DateTime.UtcNow
            });

            SpeechAnalysisResult? analysis = await AnalyzeTranscriptionAsync(transcription.Text, cancellationToken).ConfigureAwait(false);
            if (analysis is null)
                return;

            AnalysisReceived?.Invoke(this, new AnalysisEventArgs
            {
                TranscribedText = transcription.Text,
                TopicRelevanceScore = analysis.TopicRelevanceScore,
                IsOnTopic = analysis.IsOnTopic,
                Insight = analysis.Insight,
                NextSectionPreview = analysis.NextSectionPreview,
                Timestamp = DateTime.UtcNow
            });

            if (!analysis.IsOnTopic)
            {
                RaiseSpeechAlert(
                    analysis.TopicRelevanceScore < 0.35
                        ? "Speech analysis detected off-topic content."
                        : "Speech analysis detected topic drift.");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Speech transcription cancelled.");
        }
        catch (Exception ex)
        {
            HandleSpeechError("Speech transcription failed.", ex, userFacing: true);
        }
    }

    private async Task<SpeechAnalysisResult?> AnalyzeTranscriptionAsync(string transcription, CancellationToken cancellationToken)
    {
        SpeechAnalysisRequest request = BuildAnalysisRequest(transcription);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            await _relevanceAnalyzer.EnsureLoadedAsync(_settings.SpeechAnalysis, timeoutCts.Token).ConfigureAwait(false);
            return await _relevanceAnalyzer.AnalyzeAsync(request, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("LLM analysis timed out. Skipping analysis for transcription.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LLM analysis unavailable. Falling back to heuristic relevance scoring.");
            return HeuristicAnalyze(request);
        }
    }

    private SpeechAnalysisRequest BuildAnalysisRequest(string transcription)
    {
        lock (_contextGate)
        {
            if (_currentPlan is null || _currentPlan.Sections.Count == 0)
            {
                return new SpeechAnalysisRequest(
                    transcription,
                    "General presentation",
                    string.Empty,
                    [],
                    _settings.SpeechAnalysis.AnalysisSensitivity);
            }

            int currentIndex = Math.Clamp(_currentSectionIndex, 0, _currentPlan.Sections.Count - 1);
            SessionSection currentSection = _currentPlan.Sections[currentIndex];

            List<string> nextSections = [];
            for (int i = currentIndex + 1; i < _currentPlan.Sections.Count && nextSections.Count < 3; i++)
            {
                SessionSection next = _currentPlan.Sections[i];
                nextSections.Add(string.IsNullOrWhiteSpace(next.Notes)
                    ? next.Title
                    : $"{next.Title}: {next.Notes}");
            }

            return new SpeechAnalysisRequest(
                transcription,
                currentSection.Title,
                currentSection.Notes ?? string.Empty,
                nextSections,
                _settings.SpeechAnalysis.AnalysisSensitivity);
        }
    }

    internal static SpeechAnalysisResult HeuristicAnalyze(SpeechAnalysisRequest request)
    {
        HashSet<string> contextWords = Tokenize($"{request.CurrentSection} {request.CurrentSectionNotes} {string.Join(' ', request.NextSections)}");
        HashSet<string> transcriptWords = Tokenize(request.SpeakerText);

        double overlap = transcriptWords.Count == 0
            ? 0d
            : transcriptWords.Count(word => contextWords.Contains(word)) / (double)transcriptWords.Count;

        double sensitivityModifier = request.Sensitivity switch
        {
            "High" => 0.10d,
            "Low" => -0.10d,
            _ => 0d
        };

        double score = Math.Clamp(overlap + sensitivityModifier, 0d, 1d);
        bool onTopic = score >= 0.55d;

        string insight = score switch
        {
            >= 0.85d => "📊 95% on-topic",
            >= 0.55d => "📊 Staying on-topic",
            >= 0.35d => "⚠️ Drifting from topic",
            _ => "🛑 Off-topic content detected"
        };

        return new SpeechAnalysisResult(score, onTopic, insight, request.NextSections.Take(3).ToArray());
    }

    internal static SpeechAnalysisResult ParseAnalysisJson(string rawResponse, IReadOnlyList<string> fallbackPreview)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return new SpeechAnalysisResult(0d, false, "⚠️ No analysis returned", fallbackPreview);

        string json = ExtractJsonPayload(rawResponse);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        double score = root.TryGetProperty("relevance_score", out JsonElement scoreElement)
            ? Math.Clamp(scoreElement.GetDouble(), 0d, 1d)
            : 0d;

        bool isOnTopic = root.TryGetProperty("on_topic", out JsonElement onTopicElement) && onTopicElement.GetBoolean();
        string insight = root.TryGetProperty("insight", out JsonElement insightElement)
            ? insightElement.GetString() ?? string.Empty
            : string.Empty;

        List<string> preview = [];
        if (root.TryGetProperty("next_section_preview", out JsonElement previewElement) &&
            previewElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in previewElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    preview.Add(item.GetString()!);
            }
        }

        if (preview.Count == 0)
            preview.AddRange(fallbackPreview);

        if (string.IsNullOrWhiteSpace(insight))
        {
            insight = isOnTopic
                ? $"📊 {Math.Round(score * 100)}% on-topic"
                : score >= 0.35d
                    ? "⚠️ Drifting from topic"
                    : "🛑 Off-topic content detected";
        }

        return new SpeechAnalysisResult(score, isOnTopic, insight, preview);
    }

    internal static string ExtractJsonPayload(string rawResponse)
    {
        int start = rawResponse.IndexOf('{');
        int end = rawResponse.LastIndexOf('}');
        if (start >= 0 && end > start)
            return rawResponse[start..(end + 1)];

        return rawResponse;
    }

    internal static MemoryStream CreateWaveStream(byte[] pcmData)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        int subChunk2Size = pcmData.Length;
        int chunkSize = 36 + subChunk2Size;
        short channels = 1;
        short bitsPerSample = 16;
        int byteRate = TargetSampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(chunkSize);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(TargetSampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(subChunk2Size);
        writer.Write(pcmData);
        writer.Flush();

        stream.Position = 0;
        return stream;
    }

    internal static double CalculateNormalizedRms(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length < sizeof(short))
            return 0d;

        double sumSquares = 0d;
        int sampleCount = chunk.Length / sizeof(short);

        for (int i = 0; i < chunk.Length - 1; i += 2)
        {
            short sample = BitConverter.ToInt16(chunk[i..(i + 2)]);
            double normalized = sample / (double)short.MaxValue;
            sumSquares += normalized * normalized;
        }

        return Math.Sqrt(sumSquares / sampleCount);
    }

    internal static double EstimateConfidence(string transcription)
    {
        if (string.IsNullOrWhiteSpace(transcription))
            return 0d;

        int wordCount = transcription
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;

        return Math.Clamp(0.55d + (wordCount * 0.04d), 0.55d, 0.98d);
    }

    private static HashSet<string> Tokenize(string text) =>
        text.Split([' ', '\r', '\n', '\t', '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 4)
            .Select(token => token.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    private void OnAudioChunkReceived(object? sender, SpeechAudioChunkEventArgs e)
    {
        _audioQueue.Enqueue(e.Buffer);
        _audioSignal.Release();
    }

    private void HandleSpeechError(string userMessage, Exception ex, bool userFacing)
    {
        _logger.LogDebug(ex, "{Message}", userMessage);
        if (userFacing)
            RaiseSpeechAlert(userMessage);
    }

    private void RaiseSpeechAlert(string message)
    {
        AlertRaised?.Invoke(this, new AlertEventArgs
        {
            AlertType = AlertType.ManualSectionChange,
            Message = message,
            SectionIndex = _currentSectionIndex,
            ShouldPlaySound = false,
            ShouldShowNotification = false
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopListeningAsync().GetAwaiter().GetResult();
        _audioSignal.Dispose();
        _stateLock.Dispose();
        _whisperTranscriber.Dispose();
        _relevanceAnalyzer.Dispose();
        TranscriptionReceived = null;
        AnalysisReceived = null;
        AlertRaised = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SpeechAnalysisService));
    }
}
