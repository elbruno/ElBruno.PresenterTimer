namespace ElBruno.PresenterTimer.Models;

/// <summary>
/// Root settings object persisted to %AppData%\ElBruno.PresenterTimer\settings.json.
/// Composed of nested category classes per PRD §7.12 / §9.3.
/// </summary>
public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public OverlayStyleSettings OverlayStyle { get; set; } = new();
    public OverlayLayoutSettings OverlayLayout { get; set; } = new();
    public AlertSettings Alerts { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
}

/// <summary>PRD §7.12 General settings.</summary>
public sealed class GeneralSettings
{
    public bool LaunchMinimizedToTray { get; set; } = true;
    public bool RememberLastSession { get; set; } = true;
    public bool AutoLoadLastSessionOnStartup { get; set; } = false;
    public bool ShowSessionPreviewAfterImport { get; set; } = true;
    public bool ConfirmBeforeReset { get; set; } = true;
    public bool ConfirmBeforeExitWhileRunning { get; set; } = true;

    /// <summary>Show the Session Summary window automatically when a session ends (PRD §7.14).</summary>
    public bool ShowSummaryOnSessionEnd { get; set; } = true;

    /// <summary>File path of the most recently loaded session JSON (PRD §7.2).</summary>
    public string? LastSessionPath { get; set; }

    /// <summary>Ordered list of recently loaded session file paths, newest first (max 10).</summary>
    public List<string> RecentSessionPaths { get; set; } = [];
}

/// <summary>PRD §7.12 Behavior settings.</summary>
public sealed class BehaviorSettings
{
    public bool ShowOverlayWhenSessionStarts { get; set; } = true;
    public bool HideOverlayWhenSessionEnds { get; set; } = false;
    public bool AutoAdvanceSections { get; set; } = false;
    public bool KeepCountingOvertimeAfterSectionEnd { get; set; } = true;
    public bool KeepCountingOvertimeAfterSessionEnd { get; set; } = true;
    public bool EnableGlobalHotkeys { get; set; } = false;
    public bool EnableOverlayClickThrough { get; set; } = false;
    public bool PauseTimerWhenComputerLocks { get; set; } = true;
}

/// <summary>PRD §7.12 Overlay style settings.</summary>
public sealed class OverlayStyleSettings
{
    /// <summary>"System", "Light", or "Dark".</summary>
    public string Theme { get; set; } = "System";

    /// <summary>Hex color string, e.g. "#0078D4".</summary>
    public string AccentColor { get; set; } = "#0078D4";

    public string WarningColor { get; set; } = "#FFC107";
    public string OvertimeColor { get; set; } = "#E53935";

    /// <summary>0–100.</summary>
    public int CompletedSectionOpacity { get; set; } = 45;
    public int UpcomingSectionOpacity { get; set; } = 55;
    public int CurrentSectionOpacity { get; set; } = 100;
    public int OverlayOpacity { get; set; } = 85;

    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>"Small", "Medium", or "Large".</summary>
    public string FontSize { get; set; } = "Medium";

    /// <summary>"None", "Small", "Medium", or "Large".</summary>
    public string BorderRadius { get; set; } = "Medium";

    public bool ShowSectionLabels { get; set; } = true;
    public bool ShowSessionTitle { get; set; } = true;
    public bool ShowCurrentSectionTitle { get; set; } = true;
    public bool ShowNextSectionTitle { get; set; } = true;
    public bool ShowTimeRemaining { get; set; } = true;
    public bool ShowElapsedTime { get; set; } = true;
}

/// <summary>PRD §7.12 Overlay layout settings.</summary>
public sealed class OverlayLayoutSettings
{
    /// <summary>"FullTimeline" or "Compact".</summary>
    public string OverlayMode { get; set; } = "FullTimeline";

    /// <summary>"TopCenter", "TopLeft", "TopRight", "BottomCenter", etc.</summary>
    public string Position { get; set; } = "TopCenter";

    /// <summary>Monitor index; 0 = primary (legacy / fallback).</summary>
    public int Monitor { get; set; } = 0;

    /// <summary>
    /// Device name of the preferred monitor (e.g. "\\.\DISPLAY1").
    /// Used by <c>WindowPlacementService</c>; falls back to primary if the device is disconnected (PRD §7.18).
    /// </summary>
    public string? MonitorDeviceName { get; set; }

    /// <summary>Width as a fraction of monitor width (0.0–1.0).</summary>
    public double WidthFraction { get; set; } = 0.80;

    /// <summary>"Compact" or "Expanded".</summary>
    public string Height { get; set; } = "Compact";

    public bool RememberCustomPosition { get; set; } = true;
    public bool EnableDragToMove { get; set; } = true;
    public bool SnapToScreenEdges { get; set; } = true;

    /// <summary>Custom X position in pixels (used when RememberCustomPosition is true).</summary>
    public double? CustomX { get; set; }

    /// <summary>Custom Y position in pixels (used when RememberCustomPosition is true).</summary>
    public double? CustomY { get; set; }
}

/// <summary>PRD §7.12 Alert settings.</summary>
public sealed class AlertSettings
{
    public bool EnableSectionWarningAlerts { get; set; } = true;

    /// <summary>hh:mm:ss string, default 1 minute.</summary>
    public string SectionWarningThreshold { get; set; } = "00:01:00";

    public bool EnableSessionWarningAlerts { get; set; } = true;

    /// <summary>hh:mm:ss string, default 3 minutes.</summary>
    public string SessionWarningThreshold { get; set; } = "00:03:00";

    public bool EnableSectionEndAlerts { get; set; } = true;
    public bool EnableSessionEndAlerts { get; set; } = true;
    public bool EnableOvertimeAlerts { get; set; } = true;
    public bool EnableOverlayPulse { get; set; } = true;
    public bool EnableSoundAlerts { get; set; } = false;
    public bool EnableWindowsNotifications { get; set; } = false;

    /// <summary>Alert message duration in seconds.</summary>
    public int AlertMessageDurationSeconds { get; set; } = 5;
}

/// <summary>PRD §7.12 Hotkey settings. All disabled by default to avoid conflicts.</summary>
public sealed class HotkeySettings
{
    public bool Enabled { get; set; } = false;

    /// <summary>Suggested: Ctrl+Alt+Space</summary>
    public string PauseResume { get; set; } = "Ctrl+Alt+Space";

    /// <summary>Suggested: Ctrl+Alt+Right</summary>
    public string NextSection { get; set; } = "Ctrl+Alt+Right";

    /// <summary>Suggested: Ctrl+Alt+Left</summary>
    public string PreviousSection { get; set; } = "Ctrl+Alt+Left";

    /// <summary>Suggested: Ctrl+Alt+R</summary>
    public string ResetSession { get; set; } = "Ctrl+Alt+R";

    /// <summary>Suggested: Ctrl+Alt+H</summary>
    public string ShowHideOverlay { get; set; } = "Ctrl+Alt+H";

    /// <summary>Suggested: Ctrl+Alt+Up</summary>
    public string ExtendSectionOneMinute { get; set; } = "Ctrl+Alt+Up";
}
