using System.IO;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElBruno.PresenterTimer.Tests;

public sealed class SpeechAnalysisServiceTests
{
    [Fact]
    public async Task StartListeningAsync_WhenDisabled_RaisesAlertAndStaysStopped()
    {
        var sut = CreateService(new AppSettings { SpeechAnalysis = { Enabled = false } }, out var alerts);

        await sut.StartListeningAsync();

        Assert.False(sut.IsListening);
        Assert.Contains(alerts, alert => alert.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CircularPcmBuffer_KeepsLatestBytes()
    {
        var buffer = new CircularPcmBuffer(4);

        buffer.Write([1, 2, 3]);
        buffer.Write([4, 5]);

        Assert.Equal(new byte[] { 2, 3, 4, 5 }, buffer.Snapshot());
    }

    [Fact]
    public void CalculateNormalizedRms_ReturnsZeroForSilence()
        => Assert.Equal(0d, SpeechAnalysisService.CalculateNormalizedRms(new byte[SpeechAnalysisService.BytesPerChunk]));

    [Fact]
    public void CalculateNormalizedRms_ReturnsPositiveForAudio()
    {
        byte[] chunk = new byte[SpeechAnalysisService.BytesPerChunk];
        for (int i = 0; i < chunk.Length; i += 2)
            BitConverter.GetBytes((short)3000).CopyTo(chunk, i);

        Assert.True(SpeechAnalysisService.CalculateNormalizedRms(chunk) > 0.01d);
    }

    [Fact]
    public async Task AudioProcessing_EmitsTranscriptionAndAnalysisAtUtteranceBoundary()
    {
        var whisper = new FakeWhisperTranscriber
        {
            OnTranscribeAsync = (_, _) => Task.FromResult(new SpeechTranscription("intro architecture roadmap", 0.91d, TimeSpan.FromSeconds(2)))
        };
        var analyzer = new FakeRelevanceAnalyzer
        {
            OnAnalyzeAsync = (_, _) => Task.FromResult(new SpeechAnalysisResult(0.95d, true, "📊 95% on-topic", ["Demo", "Wrap-up"]))
        };

        var sut = CreateService(CreateEnabledSettings(), out _, null, whisper, analyzer);
        var transcriptions = new List<TranscriptionEventArgs>();
        var analyses = new List<AnalysisEventArgs>();
        sut.TranscriptionReceived += (_, e) => transcriptions.Add(e);
        sut.AnalysisReceived += (_, e) => analyses.Add(e);

        await sut.ProcessAudioChunkForTestsAsync(CreateAudioChunk(sampleValue: 4000));
        await sut.ProcessAudioChunkForTestsAsync(new byte[SpeechAnalysisService.BytesPerChunk]);
        await sut.ProcessAudioChunkForTestsAsync(new byte[SpeechAnalysisService.BytesPerChunk]);

        Assert.Single(transcriptions);
        Assert.Single(analyses);
        Assert.Equal("intro architecture roadmap", transcriptions[0].TranscribedText);
        Assert.Equal("📊 95% on-topic", analyses[0].Insight);
    }

    [Fact]
    public async Task AnalyzeTimeout_SkipsAnalysisEvent()
    {
        var whisper = new FakeWhisperTranscriber
        {
            OnTranscribeAsync = (_, _) => Task.FromResult(new SpeechTranscription("speaker content", 0.8d, TimeSpan.FromSeconds(1)))
        };
        var analyzer = new FakeRelevanceAnalyzer
        {
            OnAnalyzeAsync = async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new SpeechAnalysisResult(0.8d, true, "never reached", []);
            }
        };

        var sut = CreateService(CreateEnabledSettings(), out _, null, whisper, analyzer);
        int analysisCount = 0;
        sut.AnalysisReceived += (_, _) => analysisCount++;

        await sut.ProcessAudioChunkForTestsAsync(CreateAudioChunk(sampleValue: 4000));
        await sut.ProcessAudioChunkForTestsAsync(new byte[SpeechAnalysisService.BytesPerChunk]);
        await sut.ProcessAudioChunkForTestsAsync(new byte[SpeechAnalysisService.BytesPerChunk]);

        Assert.Equal(0, analysisCount);
    }

    [Fact]
    public void ParseAnalysisJson_ReadsExpectedFields()
    {
        const string raw = """
            ```json
            {"relevance_score":0.42,"on_topic":false,"insight":"⚠️ Drifting from topic","next_section_preview":["Q&A"]}
            ```
            """;

        SpeechAnalysisResult result = SpeechAnalysisService.ParseAnalysisJson(raw, ["Fallback"]);

        Assert.Equal(0.42d, result.TopicRelevanceScore, 3);
        Assert.False(result.IsOnTopic);
        Assert.Equal("⚠️ Drifting from topic", result.Insight);
        Assert.Equal(["Q&A"], result.NextSectionPreview);
    }

    [Fact]
    public void HeuristicAnalyze_UsesPresentationContext()
    {
        SpeechAnalysisResult result = SpeechAnalysisService.HeuristicAnalyze(
            new SpeechAnalysisRequest(
                "Architecture deployment demo architecture",
                "Architecture",
                "Discuss deployment and architecture",
                ["Demo"],
                "Medium"));

        Assert.True(result.TopicRelevanceScore > 0.5d);
        Assert.True(result.IsOnTopic);
    }

    private static AppSettings CreateEnabledSettings() => new()
    {
        SpeechAnalysis = new SpeechAnalysisSettings
        {
            Enabled = true,
            ModelType = "Local",
            LocalModelPath = string.Empty,
            AnalysisSensitivity = "Medium"
        }
    };

    private static byte[] CreateAudioChunk(short sampleValue)
    {
        byte[] chunk = new byte[SpeechAnalysisService.BytesPerChunk];
        byte[] sample = BitConverter.GetBytes(sampleValue);
        for (int i = 0; i < chunk.Length; i += 2)
            sample.CopyTo(chunk, i);

        return chunk;
    }

    private static SpeechAnalysisService CreateService(
        AppSettings settings,
        out List<AlertEventArgs> alerts,
        FakeAudioInputFactory? audioFactory = null,
        IWhisperTranscriber? whisperTranscriber = null,
        IRelevanceAnalyzer? analyzer = null)
    {
        var collectedAlerts = new List<AlertEventArgs>();
        alerts = collectedAlerts;
        var alertService = new FakeAlertService();
        var sut = new SpeechAnalysisService(
            settings,
            alertService,
            NullLogger<SpeechAnalysisService>.Instance,
            audioFactory ?? new FakeAudioInputFactory(),
            whisperTranscriber ?? new FakeWhisperTranscriber(),
            analyzer ?? new FakeRelevanceAnalyzer());
        sut.AlertRaised += (_, e) => collectedAlerts.Add(e);
        return sut;
    }

    private sealed class FakeAlertService : IAlertService
    {
        public event EventHandler<AlertEventArgs>? AlertRaised;
        public void Attach(ISessionTimerService timer) { }
        public void Detach() { }
        public void Reset() { }
    }

    private sealed class FakeAudioInputFactory : ISpeechAudioInputFactory
    {
        public FakeAudioInput? LastInput { get; private set; }

        public ISpeechAudioInput Create()
        {
            LastInput = new FakeAudioInput();
            return LastInput;
        }
    }

    private sealed class FakeAudioInput : ISpeechAudioInput
    {
        public event EventHandler<SpeechAudioChunkEventArgs>? AudioChunkReceived;

        public void Start() { }
        public void Stop() { }
        public void Dispose() { }

        public void Push(byte[] chunk)
            => AudioChunkReceived?.Invoke(this, new SpeechAudioChunkEventArgs { Buffer = chunk });
    }

    private sealed class FakeWhisperTranscriber : IWhisperTranscriber
    {
        public Func<Stream, CancellationToken, Task<SpeechTranscription>> OnTranscribeAsync { get; set; }
            = (_, _) => Task.FromResult(new SpeechTranscription(string.Empty, 0d, TimeSpan.Zero));

        public ValueTask EnsureLoadedAsync(SpeechAnalysisSettings settings, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public Task<SpeechTranscription> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken)
            => OnTranscribeAsync(audioStream, cancellationToken);

        public void Dispose() { }
    }

    private sealed class FakeRelevanceAnalyzer : IRelevanceAnalyzer
    {
        public Func<SpeechAnalysisRequest, CancellationToken, Task<SpeechAnalysisResult>> OnAnalyzeAsync { get; set; }
            = (request, _) => Task.FromResult(SpeechAnalysisService.HeuristicAnalyze(request));

        public ValueTask EnsureLoadedAsync(SpeechAnalysisSettings settings, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public Task<SpeechAnalysisResult> AnalyzeAsync(SpeechAnalysisRequest request, CancellationToken cancellationToken)
            => OnAnalyzeAsync(request, cancellationToken);

        public void Dispose() { }
    }
}
