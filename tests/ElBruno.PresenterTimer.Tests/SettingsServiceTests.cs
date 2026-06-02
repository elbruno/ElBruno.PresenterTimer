using System.IO;
using System.Text.Json;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// PRD §11 — Settings persistence tests for <see cref="SettingsService"/>.
///
/// <para><b>Path strategy:</b><br/>
/// <see cref="SettingsService"/> hard-codes its storage to
/// <c>%AppData%\ElBruno.PresenterTimer\settings.json</c> (private <c>static readonly</c>
/// fields — no constructor injection point).  Tests therefore operate against the real path,
/// employing a per-test backup/restore pattern implemented via <see cref="IDisposable"/>
/// so that an existing developer settings file is never destroyed or corrupted by the suite.
/// The backup is stored in the same folder with a <c>.test-backup</c> suffix and is removed
/// on teardown.</para>
///
/// <para><b>Limitation documented:</b><br/>
/// Because the path cannot be injected, each test must physically manipulate the real
/// <c>settings.json</c> file.  Tests are therefore I/O-dependent and can in theory collide
/// if run in parallel against the same machine user.  xUnit's default sequential-per-class
/// execution model prevents this during normal <c>dotnet test</c> runs.</para>
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    // ── Real %AppData% paths ───────────────────────────────────────────────────

    private static readonly string SettingsFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ElBruno.PresenterTimer");

    private static readonly string SettingsFilePath =
        Path.Combine(SettingsFolder, "settings.json");

    private static readonly string BackupFilePath =
        SettingsFilePath + ".test-backup";

    // ── Constructor (backup) / Dispose (restore) ───────────────────────────────

    public SettingsServiceTests()
    {
        // xUnit constructs a new instance per [Fact].
        // Backup any existing settings so tests don't destroy developer state.
        if (File.Exists(SettingsFilePath))
            File.Copy(SettingsFilePath, BackupFilePath, overwrite: true);

        // Start each test with no settings file so state is predictable.
        if (File.Exists(SettingsFilePath))
            File.Delete(SettingsFilePath);
    }

    public void Dispose()
    {
        // Restore the original settings file (or remove one left by a test).
        if (File.Exists(BackupFilePath))
        {
            File.Copy(BackupFilePath, SettingsFilePath, overwrite: true);
            File.Delete(BackupFilePath);
        }
        else if (File.Exists(SettingsFilePath))
        {
            // No original existed; clean up whatever the test created.
            File.Delete(SettingsFilePath);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Writes arbitrary text directly to the settings file path.</summary>
    private static void WriteRawSettings(string content)
    {
        Directory.CreateDirectory(SettingsFolder);
        File.WriteAllText(SettingsFilePath, content);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 1 — AppSettings default values (no SettingsService I/O required)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AppSettings_Defaults_General_LaunchMinimizedToTray_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.General.LaunchMinimizedToTray);
    }

    [Fact]
    public void AppSettings_Defaults_General_RememberLastSession_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.General.RememberLastSession);
    }

    [Fact]
    public void AppSettings_Defaults_General_AutoLoadLastSession_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.General.AutoLoadLastSessionOnStartup);
    }

    [Fact]
    public void AppSettings_Defaults_General_RecentSessionPaths_IsEmpty()
    {
        var settings = new AppSettings();
        Assert.Empty(settings.General.RecentSessionPaths);
    }

    [Fact]
    public void AppSettings_Defaults_General_LastSessionPath_IsNull()
    {
        var settings = new AppSettings();
        Assert.Null(settings.General.LastSessionPath);
    }

    [Fact]
    public void AppSettings_Defaults_Alerts_EnableSoundAlerts_IsFalse()
    {
        // PRD §7.8: sound alerts OFF by default.
        var settings = new AppSettings();
        Assert.False(settings.Alerts.EnableSoundAlerts);
    }

    [Fact]
    public void AppSettings_Defaults_Alerts_EnableWindowsNotifications_IsFalse()
    {
        // PRD §7.8: Windows notifications OFF by default.
        var settings = new AppSettings();
        Assert.False(settings.Alerts.EnableWindowsNotifications);
    }

    [Fact]
    public void AppSettings_Defaults_Alerts_EnableSectionWarningAlerts_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.Alerts.EnableSectionWarningAlerts);
    }

    [Fact]
    public void AppSettings_Defaults_Alerts_SectionWarningThreshold_IsOneMinute()
    {
        var settings = new AppSettings();
        Assert.Equal("00:01:00", settings.Alerts.SectionWarningThreshold);
    }

    [Fact]
    public void AppSettings_Defaults_Alerts_SessionWarningThreshold_IsThreeMinutes()
    {
        var settings = new AppSettings();
        Assert.Equal("00:03:00", settings.Alerts.SessionWarningThreshold);
    }

    [Fact]
    public void AppSettings_Defaults_OverlayLayout_Position_IsTopCenter()
    {
        var settings = new AppSettings();
        Assert.Equal("TopCenter", settings.OverlayLayout.Position);
    }

    [Fact]
    public void AppSettings_Defaults_OverlayLayout_WidthFraction_Is080()
    {
        var settings = new AppSettings();
        Assert.Equal(0.80, settings.OverlayLayout.WidthFraction);
    }

    [Fact]
    public void AppSettings_Defaults_OverlayStyle_Theme_IsSystem()
    {
        var settings = new AppSettings();
        Assert.Equal("System", settings.OverlayStyle.Theme);
    }

    [Fact]
    public void AppSettings_Defaults_Hotkeys_Enabled_IsFalse()
    {
        // PRD §12: global hotkeys default disabled to avoid conflicts.
        var settings = new AppSettings();
        Assert.False(settings.Hotkeys.Enabled);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 2 — Load when file is absent → defaults
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Load_WhenFileAbsent_Settings_IsNotNull()
    {
        // Constructor deleted the file; Load should create defaults without throwing.
        var svc = new SettingsService();
        svc.Load();

        Assert.NotNull(svc.Settings);
    }

    [Fact]
    public void Load_WhenFileAbsent_General_HasExpectedDefaults()
    {
        var svc = new SettingsService();
        svc.Load();

        Assert.True(svc.Settings.General.LaunchMinimizedToTray);
        Assert.True(svc.Settings.General.RememberLastSession);
        Assert.False(svc.Settings.General.AutoLoadLastSessionOnStartup);
    }

    [Fact]
    public void Load_WhenFileAbsent_CreatesSettingsFile()
    {
        // PRD §10.3: first-run creates the file with defaults.
        var svc = new SettingsService();
        svc.Load();

        Assert.True(File.Exists(SettingsFilePath));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 3 — Save → Load round-trip
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SaveThenLoad_RoundTrips_LastSessionPath()
    {
        const string path = @"C:\sessions\my-talk.json";

        var writer = new SettingsService();
        writer.Load();
        writer.Settings.General.LastSessionPath = path;
        writer.Save();

        var reader = new SettingsService();
        reader.Load();

        Assert.Equal(path, reader.Settings.General.LastSessionPath);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_RecentSessionPaths()
    {
        var paths = new List<string>
        {
            @"C:\sessions\talk-a.json",
            @"C:\sessions\talk-b.json"
        };

        var writer = new SettingsService();
        writer.Load();
        writer.Settings.General.RecentSessionPaths = paths;
        writer.Save();

        var reader = new SettingsService();
        reader.Load();

        Assert.Equal(paths, reader.Settings.General.RecentSessionPaths);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_EnableSoundAlerts_True()
    {
        var writer = new SettingsService();
        writer.Load();
        writer.Settings.Alerts.EnableSoundAlerts = true;
        writer.Save();

        var reader = new SettingsService();
        reader.Load();

        Assert.True(reader.Settings.Alerts.EnableSoundAlerts);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_AccentColor()
    {
        const string color = "#FF5722";

        var writer = new SettingsService();
        writer.Load();
        writer.Settings.OverlayStyle.AccentColor = color;
        writer.Save();

        var reader = new SettingsService();
        reader.Load();

        Assert.Equal(color, reader.Settings.OverlayStyle.AccentColor);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_OverlayOpacity()
    {
        const int opacity = 60;

        var writer = new SettingsService();
        writer.Load();
        writer.Settings.OverlayStyle.OverlayOpacity = opacity;
        writer.Save();

        var reader = new SettingsService();
        reader.Load();

        Assert.Equal(opacity, reader.Settings.OverlayStyle.OverlayOpacity);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_CustomXY_Position()
    {
        var writer = new SettingsService();
        writer.Load();
        writer.Settings.OverlayLayout.CustomX = 123.5;
        writer.Settings.OverlayLayout.CustomY = 456.0;
        writer.Save();

        var reader = new SettingsService();
        reader.Load();

        Assert.Equal(123.5, reader.Settings.OverlayLayout.CustomX);
        Assert.Equal(456.0, reader.Settings.OverlayLayout.CustomY);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_Behavior_AutoAdvanceSections()
    {
        var writer = new SettingsService();
        writer.Load();
        writer.Settings.Behavior.AutoAdvanceSections = true;
        writer.Save();

        var reader = new SettingsService();
        reader.Load();

        Assert.True(reader.Settings.Behavior.AutoAdvanceSections);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_HotkeysEnabled_True()
    {
        var writer = new SettingsService();
        writer.Load();
        writer.Settings.Hotkeys.Enabled = true;
        writer.Save();

        var reader = new SettingsService();
        reader.Load();

        Assert.True(reader.Settings.Hotkeys.Enabled);
    }

    [Fact]
    public void Save_ProducesValidJson()
    {
        var svc = new SettingsService();
        svc.Load();
        svc.Save();

        var json = File.ReadAllText(SettingsFilePath);
        // Should not throw — verifies the serialised output is well-formed.
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 4 — Corrupt / invalid file → fall back to defaults without throwing
    // PRD §10.3: "Missing or corrupt files fall back to defaults."
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Load_WithCorruptJson_DoesNotThrow()
    {
        WriteRawSettings("<<< this is not JSON >>>");

        var svc = new SettingsService();
        // Must not propagate any exception per PRD §10.3.
        var ex = Record.Exception(() => svc.Load());
        Assert.Null(ex);
    }

    [Fact]
    public void Load_WithCorruptJson_FallsBackToDefaultSettings()
    {
        WriteRawSettings("{ broken json !!!}");

        var svc = new SettingsService();
        svc.Load();

        // Verify key defaults are restored, not whatever garbage was in the file.
        Assert.True(svc.Settings.General.LaunchMinimizedToTray);
        Assert.False(svc.Settings.Alerts.EnableSoundAlerts);
        Assert.Equal("TopCenter", svc.Settings.OverlayLayout.Position);
    }

    [Fact]
    public void Load_WithEmptyFile_FallsBackToDefaults_WithoutThrowing()
    {
        WriteRawSettings(string.Empty);

        var svc = new SettingsService();
        var ex = Record.Exception(() => svc.Load());

        Assert.Null(ex);
        Assert.NotNull(svc.Settings);
    }

    [Fact]
    public void Load_WithNullLiteralJson_FallsBackToDefaults()
    {
        WriteRawSettings("null");

        var svc = new SettingsService();
        svc.Load();

        Assert.NotNull(svc.Settings);
        Assert.True(svc.Settings.General.LaunchMinimizedToTray);
    }

    [Fact]
    public void Load_WithPartiallyValidJson_FallsBackToDefaults_WithoutThrowing()
    {
        // JSON that is structurally invalid mid-object.
        WriteRawSettings(@"{ ""General"": { ""LaunchMinimizedToTray"": true,");

        var svc = new SettingsService();
        var ex = Record.Exception(() => svc.Load());

        Assert.Null(ex);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 5 — RaiseSettingsApplied event
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RaiseSettingsApplied_Fires_SettingsAppliedEvent()
    {
        var svc = new SettingsService();
        var fired = false;
        svc.SettingsApplied += (_, _) => fired = true;

        svc.RaiseSettingsApplied();

        Assert.True(fired);
    }

    [Fact]
    public void RaiseSettingsApplied_WithNoSubscribers_DoesNotThrow()
    {
        var svc = new SettingsService();
        var ex = Record.Exception(() => svc.RaiseSettingsApplied());
        Assert.Null(ex);
    }
}
