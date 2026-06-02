namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Payload for <see cref="IAlertService.AlertRaised"/> events (PRD §7.8).
/// </summary>
public sealed class AlertEventArgs : EventArgs
{
    /// <summary>What kind of alert this is.</summary>
    public AlertType AlertType { get; init; }

    /// <summary>Human-readable alert message suitable for overlay display.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Zero-based index of the section relevant to this alert.
    /// Use <c>-1</c> for session-level alerts that have no specific section.
    /// </summary>
    public int SectionIndex { get; init; }

    /// <summary>
    /// True when <c>AlertSettings.EnableSoundAlerts</c> is on and sound is applicable to this alert.
    /// Actual audio playback is a UI-layer concern; this is the gate signal.
    /// Sound is disabled by default (PRD §7.8).
    /// </summary>
    public bool ShouldPlaySound { get; init; }

    /// <summary>
    /// True when <c>AlertSettings.EnableWindowsNotifications</c> is on and a toast is applicable.
    /// Actual toast dispatch is a UI-layer concern; this is the gate signal.
    /// Windows notifications are disabled by default (PRD §7.8).
    /// </summary>
    public bool ShouldShowNotification { get; init; }
}
