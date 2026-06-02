using System.IO;
using System.Text.Json;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Persists <see cref="AppSettings"/> to <c>%AppData%\ElBruno.PresenterTimer\settings.json</c>
/// via System.Text.Json. Missing or corrupt files fall back to defaults (PRD §10.3).
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly string SettingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ElBruno.PresenterTimer");

    private static readonly string SettingsFilePath =
        Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

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

        if (!File.Exists(SettingsFilePath))
        {
            Settings = new AppSettings();
            Save(); // create the file with defaults on first run
            return;
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // Corrupt or unreadable — fall back to defaults per PRD §10.3
            Settings = new AppSettings();
        }
    }

    /// <inheritdoc />
    public void Save()
    {
        EnsureFolderExists();
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    private static void EnsureFolderExists()
    {
        if (!Directory.Exists(SettingsFolder))
            Directory.CreateDirectory(SettingsFolder);
    }
}
