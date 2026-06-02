namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Identifies the kind of alert raised by <see cref="IAlertService"/> (PRD §7.8).
/// </summary>
public enum AlertType
{
    /// <summary>Current section has X time remaining (threshold from section or global settings).</summary>
    SectionWarning,

    /// <summary>Current section has reached its planned duration.</summary>
    SectionEnd,

    /// <summary>Whole session has X time remaining (threshold from global settings).</summary>
    SessionWarning,

    /// <summary>Whole session has reached its planned total duration.</summary>
    SessionEnd,

    /// <summary>Current section has exceeded its effective (planned + extended) duration.</summary>
    SectionOvertime,

    /// <summary>Whole session has exceeded its planned total duration.</summary>
    SessionOvertime,

    /// <summary>User manually navigated to the next or previous section.</summary>
    ManualSectionChange
}
