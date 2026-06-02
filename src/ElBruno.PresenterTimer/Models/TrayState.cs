namespace ElBruno.PresenterTimer.Models;

/// <summary>
/// Visual state of the system-tray icon per PRD §7.1.
/// </summary>
public enum TrayState
{
    /// <summary>No session loaded.</summary>
    NoSession,

    /// <summary>Session loaded but not started.</summary>
    Loaded,

    /// <summary>Session timer is running.</summary>
    Running,

    /// <summary>Current section is almost done (warning threshold reached).</summary>
    Warning,

    /// <summary>Session has gone into overtime.</summary>
    Overtime,

    /// <summary>Session is paused.</summary>
    Paused
}
