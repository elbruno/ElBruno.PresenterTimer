using System.IO;
using System.Text.Json;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// PRD §11 — Settings persistence tests using the injectable-path constructor
/// <see cref="SettingsService(string)"/>.
///
/// <para>Each test receives its own unique temp directory under <see cref="Path.GetTempPath()"/>
/// so it never touches the real <c>%AppData%\ElBruno.PresenterTimer</c> folder.
/// The directory is removed in <see cref="Dispose"/>.</para>
///
/// <para>Five groups are covered:
/// <list type="bullet">
///   <item>Save→Load round-trip across all setting categories</item>
///   <item>Missing file → defaults without throwing</item>
///   <item>Corrupt JSON → defaults without throwing (PRD §10.3)</item>
///   <item>Auto-create parent directory on first Save()</item>
///   <item>Atomic write — no leftover .tmp and valid JSON on disk</item>
/// </list></para>
/// </summary>
public sealed class SettingsServicePathTests : IDisposable
{
    // ── Per-test isolated temp directory ─────────────────────────────────────

    private readonly string _tempDir;
    private readonly string _settingsFilePath;

    public SettingsServicePathTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PresenterTimer_Tests_{Guid.NewGuid():N}");
        _settingsFilePath = Path.Combine(_tempDir, "settings.json");
        // Do NOT pre-create the directory — individual tests decide whether it exists.
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; never fail a test in Dispose.
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SettingsService CreateService() => new(_settingsFilePath);

    private void WriteRawFile(string content)
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsFilePath, content);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 1 — Save → Load round-trip (injectable path)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InjectablePath_SaveThenLoad_RoundTrips_MultipleCategories()
    {
        // Mutate settings across every category, save, reload and verify all values.
        var writer = CreateService();
        writer.Load();

        writer.Settings.General.LastSessionPath          = @"C:\talks\keynote.json";
        writer.Settings.General.AutoLoadLastSessionOnStartup = true;
        writer.Settings.General.RecentSessionPaths       = [@"C:\talks\a.json", @"C:\talks\b.json"];

        writer.Settings.Behavior.AutoAdvanceSections     = true;
        writer.Settings.Behavior.PauseTimerWhenComputerLocks = false;

        writer.Settings.OverlayStyle.Theme               = "Dark";
        writer.Settings.OverlayStyle.AccentColor         = "#FF5722";
        writer.Settings.OverlayStyle.OverlayOpacity      = 72;
        writer.Settings.OverlayStyle.FontSize             = "Large";

        writer.Settings.OverlayLayout.Position           = "BottomCenter";
        writer.Settings.OverlayLayout.WidthFraction      = 0.65;
        writer.Settings.OverlayLayout.CustomX            = 42.5;
        writer.Settings.OverlayLayout.CustomY            = 100.0;
        writer.Settings.OverlayLayout.Monitor            = 2;

        writer.Settings.Alerts.EnableSoundAlerts         = true;
        writer.Settings.Alerts.SectionWarningThreshold   = "00:02:00";
        writer.Settings.Alerts.SessionWarningThreshold   = "00:05:00";

        writer.Settings.Hotkeys.Enabled                  = true;
        writer.Settings.Hotkeys.PauseResume              = "Ctrl+Shift+P";
        writer.Save();

        var reader = CreateService();
        reader.Load();

        // General
        Assert.Equal(@"C:\talks\keynote.json", reader.Settings.General.LastSessionPath);
        Assert.True(reader.Settings.General.AutoLoadLastSessionOnStartup);
        Assert.Equal([@"C:\talks\a.json", @"C:\talks\b.json"],
                     reader.Settings.General.RecentSessionPaths);

        // Behavior
        Assert.True(reader.Settings.Behavior.AutoAdvanceSections);
        Assert.False(reader.Settings.Behavior.PauseTimerWhenComputerLocks);

        // OverlayStyle
        Assert.Equal("Dark",    reader.Settings.OverlayStyle.Theme);
        Assert.Equal("#FF5722", reader.Settings.OverlayStyle.AccentColor);
        Assert.Equal(72,        reader.Settings.OverlayStyle.OverlayOpacity);
        Assert.Equal("Large",   reader.Settings.OverlayStyle.FontSize);

        // OverlayLayout
        Assert.Equal("BottomCenter", reader.Settings.OverlayLayout.Position);
        Assert.Equal(0.65,           reader.Settings.OverlayLayout.WidthFraction);
        Assert.Equal(42.5,           reader.Settings.OverlayLayout.CustomX);
        Assert.Equal(100.0,          reader.Settings.OverlayLayout.CustomY);
        Assert.Equal(2,              reader.Settings.OverlayLayout.Monitor);

        // Alerts
        Assert.True(reader.Settings.Alerts.EnableSoundAlerts);
        Assert.Equal("00:02:00", reader.Settings.Alerts.SectionWarningThreshold);
        Assert.Equal("00:05:00", reader.Settings.Alerts.SessionWarningThreshold);

        // Hotkeys
        Assert.True(reader.Settings.Hotkeys.Enabled);
        Assert.Equal("Ctrl+Shift+P", reader.Settings.Hotkeys.PauseResume);
    }

    [Fact]
    public void InjectablePath_SaveThenLoad_PreservesMonitorDeviceName()
    {
        var writer = CreateService();
        writer.Load();
        writer.Settings.OverlayLayout.MonitorDeviceName = @"\\.\DISPLAY2";
        writer.Save();

        var reader = CreateService();
        reader.Load();

        Assert.Equal(@"\\.\DISPLAY2", reader.Settings.OverlayLayout.MonitorDeviceName);
    }

    [Fact]
    public void InjectablePath_SaveThenLoad_PreservesAllHotkeyBindings()
    {
        var writer = CreateService();
        writer.Load();
        writer.Settings.Hotkeys.Enabled               = true;
        writer.Settings.Hotkeys.NextSection           = "Ctrl+Alt+N";
        writer.Settings.Hotkeys.PreviousSection       = "Ctrl+Alt+B";
        writer.Settings.Hotkeys.ResetSession          = "Ctrl+Alt+Del";
        writer.Settings.Hotkeys.ShowHideOverlay       = "Ctrl+Alt+O";
        writer.Settings.Hotkeys.ExtendSectionOneMinute = "Ctrl+Alt+E";
        writer.Save();

        var reader = CreateService();
        reader.Load();

        Assert.True(reader.Settings.Hotkeys.Enabled);
        Assert.Equal("Ctrl+Alt+N",   reader.Settings.Hotkeys.NextSection);
        Assert.Equal("Ctrl+Alt+B",   reader.Settings.Hotkeys.PreviousSection);
        Assert.Equal("Ctrl+Alt+Del", reader.Settings.Hotkeys.ResetSession);
        Assert.Equal("Ctrl+Alt+O",   reader.Settings.Hotkeys.ShowHideOverlay);
        Assert.Equal("Ctrl+Alt+E",   reader.Settings.Hotkeys.ExtendSectionOneMinute);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 2 — Missing file → defaults without throwing
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InjectablePath_Load_WhenFileAbsent_DoesNotThrow()
    {
        // _tempDir does not exist yet; Load() must not throw.
        var svc = CreateService();
        var ex  = Record.Exception(() => svc.Load());
        Assert.Null(ex);
    }

    [Fact]
    public void InjectablePath_Load_WhenFileAbsent_ReturnsDefaultSettings()
    {
        var svc = CreateService();
        svc.Load();

        Assert.NotNull(svc.Settings);
        Assert.True(svc.Settings.General.LaunchMinimizedToTray);
        Assert.True(svc.Settings.General.RememberLastSession);
        Assert.False(svc.Settings.Alerts.EnableSoundAlerts);
        Assert.Equal("TopCenter", svc.Settings.OverlayLayout.Position);
        Assert.Equal("System",    svc.Settings.OverlayStyle.Theme);
        Assert.False(svc.Settings.Hotkeys.Enabled);
    }

    [Fact]
    public void InjectablePath_Load_WhenFileAbsent_CreatesSettingsFileOnDisk()
    {
        // PRD §10.3: first-run creates the file.
        var svc = CreateService();
        svc.Load();

        Assert.True(File.Exists(_settingsFilePath));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 3 — Corrupt JSON → defaults without throwing (PRD §10.3)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InjectablePath_Load_WithGarbageFile_DoesNotThrow()
    {
        WriteRawFile("<<< this is not JSON at all >>>");

        var svc = CreateService();
        var ex  = Record.Exception(() => svc.Load());
        Assert.Null(ex);
    }

    [Fact]
    public void InjectablePath_Load_WithGarbageFile_FallsBackToDefaults()
    {
        WriteRawFile("{ broken json !!!}");

        var svc = CreateService();
        svc.Load();

        Assert.True(svc.Settings.General.LaunchMinimizedToTray);
        Assert.False(svc.Settings.Alerts.EnableSoundAlerts);
        Assert.Equal("TopCenter", svc.Settings.OverlayLayout.Position);
    }

    [Fact]
    public void InjectablePath_Load_WithEmptyFile_DoesNotThrow_AndReturnsDefaults()
    {
        WriteRawFile(string.Empty);

        var svc = CreateService();
        var ex  = Record.Exception(() => svc.Load());

        Assert.Null(ex);
        Assert.NotNull(svc.Settings);
        Assert.True(svc.Settings.General.LaunchMinimizedToTray);
    }

    [Fact]
    public void InjectablePath_Load_WithNullLiteralJson_FallsBackToDefaults()
    {
        WriteRawFile("null");

        var svc = CreateService();
        svc.Load();

        Assert.NotNull(svc.Settings);
        Assert.True(svc.Settings.General.LaunchMinimizedToTray);
    }

    [Fact]
    public void InjectablePath_Load_WithTruncatedJson_DoesNotThrow()
    {
        WriteRawFile(@"{ ""General"": { ""LaunchMinimizedToTray"": true,");

        var svc = CreateService();
        var ex  = Record.Exception(() => svc.Load());
        Assert.Null(ex);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 4 — Save() creates the parent directory if it doesn't exist
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InjectablePath_Save_CreatesParentDirectory_WhenMissing()
    {
        // Use a two-level-deep path that definitely does not exist.
        var deepDir  = Path.Combine(_tempDir, "sub", "deep");
        var deepPath = Path.Combine(deepDir, "settings.json");
        var svc = new SettingsService(deepPath);

        svc.Settings.General.LastSessionPath = "marker";
        svc.Save();

        Assert.True(Directory.Exists(deepDir));
        Assert.True(File.Exists(deepPath));
    }

    [Fact]
    public void InjectablePath_Load_CreatesParentDirectory_WhenMissing()
    {
        // Load() calls EnsureFolderExists() then Save() on first-run path, so the dir
        // must be created even if the caller never calls Save() explicitly.
        Assert.False(Directory.Exists(_tempDir));

        var svc = CreateService();
        svc.Load();

        Assert.True(Directory.Exists(_tempDir));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 5 — Atomic write: no leftover .tmp and valid JSON on disk
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InjectablePath_Save_LeavesNoTmpFile()
    {
        var svc = CreateService();
        svc.Load();
        svc.Save();

        var tmpPath = _settingsFilePath + ".tmp";
        Assert.False(File.Exists(tmpPath),
            $"Leftover .tmp file should not exist after Save() but found: {tmpPath}");
    }

    [Fact]
    public void InjectablePath_Save_WritesValidJson()
    {
        var svc = CreateService();
        svc.Load();
        svc.Settings.OverlayStyle.AccentColor = "#ABCDEF";
        svc.Save();

        var json = File.ReadAllText(_settingsFilePath);
        var doc  = JsonDocument.Parse(json); // throws if not valid JSON
        Assert.NotNull(doc);
    }

    [Fact]
    public void InjectablePath_Save_JsonContains_MutatedValues()
    {
        var svc = CreateService();
        svc.Load();
        svc.Settings.OverlayStyle.Theme    = "Dark";
        svc.Settings.Hotkeys.Enabled       = true;
        svc.Save();

        var json = File.ReadAllText(_settingsFilePath);
        Assert.Contains("Dark",  json, StringComparison.Ordinal);
        Assert.Contains("true",  json, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectablePath_MultipleSaves_LastValueWins()
    {
        var svc = CreateService();
        svc.Load();
        svc.Settings.General.LastSessionPath = "first-value.json";
        svc.Save();
        svc.Settings.General.LastSessionPath = "second-value.json";
        svc.Save();

        var reader = CreateService();
        reader.Load();

        Assert.Equal("second-value.json", reader.Settings.General.LastSessionPath);
    }
}
