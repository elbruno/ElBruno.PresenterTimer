using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ElBruno.LocalLLMs;
using ElBruno.PresenterTimer.Models;
using ElBruno.Whisper;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace ElBruno.PresenterTimer.Services;

internal sealed class CircularPcmBuffer(int capacityBytes)
{
    private readonly byte[] _buffer = new byte[Math.Max(1, capacityBytes)];
    private int _writeIndex;
    private int _count;

    public int Count => _count;

    public void Clear()
    {
        _writeIndex = 0;
        _count = 0;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        foreach (byte sample in data)
        {
            _buffer[_writeIndex] = sample;
            _writeIndex = (_writeIndex + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }
    }

    public byte[] Snapshot()
    {
        if (_count == 0)
            return [];

        byte[] result = new byte[_count];
        int start = (_writeIndex - _count + _buffer.Length) % _buffer.Length;

        for (int i = 0; i < _count; i++)
            result[i] = _buffer[(start + i) % _buffer.Length];

        return result;
    }
}

internal sealed class SpeechAudioChunkEventArgs : EventArgs
{
    public required byte[] Buffer { get; init; }
}

internal interface ISpeechAudioInput : IDisposable
{
    event EventHandler<SpeechAudioChunkEventArgs>? AudioChunkReceived;
    void Start();
    void Stop();
}

internal interface ISpeechAudioInputFactory
{
    ISpeechAudioInput Create();
}

internal sealed class NAudioSpeechAudioInputFactory : ISpeechAudioInputFactory
{
    public ISpeechAudioInput Create() => new NAudioSpeechAudioInput();
}

internal sealed class NAudioSpeechAudioInput : ISpeechAudioInput
{
    private readonly WaveInEvent _waveIn;

    public NAudioSpeechAudioInput()
    {
        if (WaveInEvent.DeviceCount <= 0)
            throw new InvalidOperationException("No microphone input device is available.");

        _waveIn = new WaveInEvent
        {
            BufferMilliseconds = SpeechAnalysisService.ChunkMilliseconds,
            NumberOfBuffers = 3,
            WaveFormat = new WaveFormat(SpeechAnalysisService.TargetSampleRate, 16, 1)
        };
        _waveIn.DataAvailable += OnDataAvailable;
    }

    public event EventHandler<SpeechAudioChunkEventArgs>? AudioChunkReceived;

    public void Start() => _waveIn.StartRecording();

    public void Stop() => _waveIn.StopRecording();

    public void Dispose()
    {
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
            return;

        byte[] copy = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, copy, 0, e.BytesRecorded);
        AudioChunkReceived?.Invoke(this, new SpeechAudioChunkEventArgs { Buffer = copy });
    }
}

internal sealed record SpeechTranscription(string Text, double Confidence, TimeSpan Duration);

internal sealed record SpeechAnalysisRequest(
    string SpeakerText,
    string CurrentSection,
    string CurrentSectionNotes,
    IReadOnlyList<string> NextSections,
    string Sensitivity);

internal sealed record SpeechAnalysisResult(
    double TopicRelevanceScore,
    bool IsOnTopic,
    string Insight,
    IReadOnlyList<string> NextSectionPreview);

internal interface IWhisperTranscriber : IDisposable
{
    ValueTask EnsureLoadedAsync(SpeechAnalysisSettings settings, CancellationToken cancellationToken);
    Task<SpeechTranscription> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken);
}

internal interface IRelevanceAnalyzer : IDisposable
{
    ValueTask EnsureLoadedAsync(SpeechAnalysisSettings settings, CancellationToken cancellationToken);
    Task<SpeechAnalysisResult> AnalyzeAsync(SpeechAnalysisRequest request, CancellationToken cancellationToken);
}

internal sealed class WhisperTranscriber(ILogger<WhisperTranscriber> logger) : IWhisperTranscriber
{
    private WhisperClient? _client;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public async ValueTask EnsureLoadedAsync(SpeechAnalysisSettings settings, CancellationToken cancellationToken)
    {
        if (_client is not null)
            return;

        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is not null)
                return;

            var options = new WhisperOptions
            {
                Model = KnownWhisperModels.WhisperTinyEn,
                Language = "en"
            };

            string? modelPath = NormalizeWhisperModelPath(settings.LocalModelPath);
            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                options.ModelPath = modelPath;
                options.EnsureModelDownloaded = false;
                logger.LogDebug("Loading Whisper model from {ModelPath}", modelPath);
            }

            _client = await WhisperClient.CreateAsync(options, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public async Task<SpeechTranscription> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken)
    {
        if (_client is null)
            throw new InvalidOperationException("Whisper client is not loaded.");

        audioStream.Position = 0;
        var result = await _client.TranscribeAsync(audioStream, cancellationToken).ConfigureAwait(false);
        string text = result.Text?.Trim() ?? string.Empty;

        return new SpeechTranscription(
            text,
            SpeechAnalysisService.EstimateConfidence(text),
            result.Duration);
    }

    public void Dispose() => _client?.Dispose();

    private static string? NormalizeWhisperModelPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        if (Directory.Exists(configuredPath))
            return configuredPath;

        if (File.Exists(configuredPath))
            return Path.GetDirectoryName(configuredPath);

        throw new FileNotFoundException("The configured Whisper model path does not exist.", configuredPath);
    }
}

internal sealed class LocalLlmRelevanceAnalyzer(ILoggerFactory loggerFactory) : IRelevanceAnalyzer
{
    private readonly ILogger<LocalLlmRelevanceAnalyzer> _logger = loggerFactory.CreateLogger<LocalLlmRelevanceAnalyzer>();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private LocalChatClient? _client;

    public async ValueTask EnsureLoadedAsync(SpeechAnalysisSettings settings, CancellationToken cancellationToken)
    {
        if (_client is not null)
            return;

        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is not null)
                return;

            string? modelPath = NormalizeLlmModelPath(settings.LocalModelPath);
            var options = new LocalLLMsOptions
            {
                Model = KnownModels.Qwen25_05B_ToolCalling,
                EnsureModelDownloaded = string.IsNullOrWhiteSpace(modelPath),
                ModelPath = modelPath,
                ExecutionProvider = ExecutionProvider.Cpu,
                Temperature = 0.1f,
                TopP = 0.2f,
                SystemPrompt = "You evaluate presentation relevance. Return strict JSON only."
            };

            _client = await LocalChatClient.CreateAsync(options, progress: null, loggerFactory, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public async Task<SpeechAnalysisResult> AnalyzeAsync(SpeechAnalysisRequest request, CancellationToken cancellationToken)
    {
        if (_client is null)
            throw new InvalidOperationException("Local LLM client is not loaded.");

        string prompt = BuildPrompt(request);
        ChatMessage[] messages =
        [
            new ChatMessage(ChatRole.System, "Evaluate whether the speaker is on topic. Return JSON only."),
            new ChatMessage(ChatRole.User, prompt)
        ];

        var response = await _client.GetResponseAsync(messages, options: null, cancellationToken).ConfigureAwait(false);
        string raw = response.Text?.Trim() ?? string.Empty;
        _logger.LogDebug("Speech analysis LLM response: {Response}", raw);

        return SpeechAnalysisService.ParseAnalysisJson(raw, request.NextSections);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _loadGate.Dispose();
    }

    private static string BuildPrompt(SpeechAnalysisRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Return a compact JSON object with properties:");
        builder.AppendLine("relevance_score (0.0-1.0), on_topic (bool), insight (string), next_section_preview (array of strings).");
        builder.AppendLine();
        builder.AppendLine($"Sensitivity: {request.Sensitivity}");
        builder.AppendLine($"Current section: {request.CurrentSection}");
        if (!string.IsNullOrWhiteSpace(request.CurrentSectionNotes))
            builder.AppendLine($"Current section notes: {request.CurrentSectionNotes}");

        builder.AppendLine("Upcoming sections:");
        foreach (string section in request.NextSections)
            builder.AppendLine($"- {section}");

        builder.AppendLine();
        builder.AppendLine("Speaker text:");
        builder.AppendLine(request.SpeakerText);
        return builder.ToString();
    }

    private static string? NormalizeLlmModelPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath) || !Directory.Exists(configuredPath))
            return null;

        string genAiConfig = Path.Combine(configuredPath, "genai_config.json");
        return File.Exists(genAiConfig) ? configuredPath : null;
    }
}
