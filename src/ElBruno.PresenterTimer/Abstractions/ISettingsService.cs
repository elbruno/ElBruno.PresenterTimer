using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Loads and persists application settings to/from <c>%AppData%\ElBruno.PresenterTimer\settings.json</c>.
/// Members will be refined in Phase 1 (app shell) and Phase 8 (settings UI).
/// </summary>
public interface ISettingsService
{
    /// <summary>The currently loaded settings object. Always non-null after <see cref="Load"/>.</summary>
    AppSettings Settings { get; }

    /// <summary>Loads settings from disk, applying defaults for any missing values.</summary>
    void Load();

    /// <summary>Persists the current settings to disk.</summary>
    void Save();

    /// <summary>
    /// Fired when settings are explicitly applied by the user via the Settings UI.
    /// Not raised on every <see cref="Save"/> (e.g., position auto-saves are excluded).
    /// </summary>
    event EventHandler? SettingsApplied;

    /// <summary>
    /// Signals that the user has deliberately applied/saved settings.
    /// Called by <c>SettingsViewModel</c> after <see cref="Save"/>.
    /// </summary>
    void RaiseSettingsApplied();
}
