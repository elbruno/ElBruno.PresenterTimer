using ElBruno.PresenterTimer.Abstractions;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Stub implementation of <see cref="ISpeechAnalysisService"/>.
/// Phase 1 (v0.9.0): No-op implementation. UI only.
/// Phase 2: Full implementation with Whisper/Azure/Foundry models.
/// </summary>
public sealed class SpeechAnalysisService : ISpeechAnalysisService
{
    private bool _isListening;

    public bool IsListening => _isListening;

    public event EventHandler<TranscriptionEventArgs>? TranscriptionReceived;
    public event EventHandler<AnalysisEventArgs>? AnalysisReceived;
    public event EventHandler<AlertEventArgs>? AlertRaised;

    /// <summary>Initializes a new instance of the <see cref="SpeechAnalysisService"/> stub.</summary>
    public SpeechAnalysisService()
    {
        _isListening = false;
    }

    /// <summary>Phase 1: No-op. Phase 2 will start microphone capture.</summary>
    public Task StartListeningAsync()
    {
        _isListening = true;
        return Task.CompletedTask;
    }

    /// <summary>Phase 1: No-op. Phase 2 will stop microphone capture.</summary>
    public Task StopListeningAsync()
    {
        _isListening = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _isListening = false;
        TranscriptionReceived = null;
        AnalysisReceived = null;
        AlertRaised = null;
    }
}
