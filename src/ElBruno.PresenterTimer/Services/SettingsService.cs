using System.IO;
using System.Text.Json;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Persists <see cref="AppSettings"/> to <c>%AppData%\ElBruno.PresenterTimer\settings.json</c>
/// via System.Text.Json. Missing or corrupt files fall back to defaults (PRD §10.3).
/// </summary>
/// <remarks>
/// An injection seam is available via <see cref="SettingsService(string)"/>: pass a full file
/// path to redirect storage (e.g., a temp directory in tests).  The parameterless constructor
/// preserves the production <c>%AppData%</c> path so callers in <c>App</c> are unchanged.
/// </remarks>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly string _settingsFolder;

    /// <summary>
    /// Production constructor — stores settings at
    /// <c>%AppData%\ElBruno.PresenterTimer\settings.json</c>.
    /// </summary>
    public SettingsService()
        : this(Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
               "ElBruno.PresenterTimer",
               "settings.json"))
    { }

    /// <summary>
    /// Injection-seam constructor — stores settings at <paramref name="settingsFilePath"/>.
    /// Use in tests to point at a temporary directory without touching the real
    /// <c>%AppData%</c> file.
    /// </summary>
    /// <param name="settingsFilePath">Full path to the <c>settings.json</c> file.</param>
    public SettingsService(string settingsFilePath)
    {
        if (string.IsNullOrWhiteSpace(settingsFilePath))
            throw new ArgumentException("Settings file path must not be empty.", nameof(settingsFilePath));

        _settingsFilePath = settingsFilePath;
        _settingsFolder   = Path.GetDirectoryName(settingsFilePath)
                            ?? throw new ArgumentException(
                                   "Cannot determine directory from the supplied path.",
                                   nameof(settingsFilePath));
    }

    /// <summary>The currently loaded settings. Always non-null after <see cref="Load"/>.</summary>
    public AppSettings Settings { get; private set; } = new();

    /// <inheritdoc />
    public event EventHandler? SettingsApplied;

    /// <inheritdoc />
    public void RaiseSettingsApplied() => SettingsApplied?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void Load()
    {
        EnsureFolderExists();

        if (!File.Exists(_settingsFilePath))
        {
            Settings = new AppSettings();
            Save(); // create the file with defaults on first run
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // Corrupt or unreadable — fall back to defaults per PRD §10.3
            Settings = new AppSettings();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uses an atomic-ish write: serialises to a <c>.tmp</c> sibling file first, then
    /// replaces the real file with <see cref="File.Move"/>.  A partial write to the temp
    /// file therefore never corrupts the live settings.
    /// </remarks>
    public void Save()
    {
        EnsureFolderExists();
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        var tmp  = _settingsFilePath + ".tmp";

        File.WriteAllText(tmp, json);
        File.Move(tmp, _settingsFilePath, overwrite: true);
    }

    private void EnsureFolderExists()
    {
        if (!Directory.Exists(_settingsFolder))
            Directory.CreateDirectory(_settingsFolder);
    }
}
