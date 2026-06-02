using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Manages the Windows system-tray icon: icon state, tooltip, and context menu.
/// </summary>
public interface ITrayIconService
{
    /// <summary>Initialises the tray icon and registers the context menu.</summary>
    void Initialize();

    /// <summary>
    /// Updates the tray icon color and tooltip to reflect the given state per PRD §7.1.
    /// </summary>
    void SetState(TrayState state);

    /// <summary>Releases tray-icon resources on app exit.</summary>
    void Dispose();
}
