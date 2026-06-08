using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Service for real-time speech analysis.
/// Phase 1 (v0.9.0): UI stubs only.
/// Phase 2: Full implementation with Whisper/Azure/Foundry integration.
/// </summary>
public interface ISpeechAnalysisService : IDisposable
{
    /// <summary>Whether the speech analysis service is currently listening to the microphone.</summary>
    bool IsListening { get; }

    /// <summary>Start listening for speech input.</summary>
    Task StartListeningAsync();

    /// <summary>Stop listening for speech input.</summary>
    Task StopListeningAsync();

    /// <summary>Fired when speech has been transcribed.</summary>
    event EventHandler<TranscriptionEventArgs>? TranscriptionReceived;

    /// <summary>Fired when speech has been analyzed for topic relevance.</summary>
    event EventHandler<AnalysisEventArgs>? AnalysisReceived;

    /// <summary>Fired when an alert is raised (e.g., off-topic detected).</summary>
    event EventHandler<AlertEventArgs>? AlertRaised;

    /// <summary>Updates the active presentation context used to evaluate topic relevance.</summary>
    void UpdatePresentationContext(SessionPlan? plan, int currentSectionIndex);
}

/// <summary>Arguments for transcription events.</summary>
public sealed class TranscriptionEventArgs : EventArgs
{
    /// <summary>The transcribed text from speech.</summary>
    public required string TranscribedText { get; init; }

    /// <summary>Confidence score (0.0-1.0).</summary>
    public required double Confidence { get; init; }

    /// <summary>Timestamp of transcription.</summary>
    public required DateTime Timestamp { get; init; }
}

/// <summary>Arguments for analysis events.</summary>
public sealed class AnalysisEventArgs : EventArgs
{
    /// <summary>The transcribed text that was analyzed.</summary>
    public required string TranscribedText { get; init; }

    /// <summary>Topic relevance score (0.0-1.0). Higher = more on-topic.</summary>
    public required double TopicRelevanceScore { get; init; }

    /// <summary>Whether the speech is considered on-topic.</summary>
    public required bool IsOnTopic { get; init; }

    /// <summary>Brief insight about the speech (e.g., "On track", "Drifting", "Off topic").</summary>
    public required string Insight { get; init; }

    /// <summary>List of next section names to preview.</summary>
    public required IReadOnlyList<string> NextSectionPreview { get; init; }

    /// <summary>Timestamp of analysis.</summary>
    public required DateTime Timestamp { get; init; }
}
