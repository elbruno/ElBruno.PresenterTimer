using System.Windows.Forms;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

public class TrayIconServiceTests
{
    [Fact]
    public void Initialize_BuildsGroupedTrayMenu()
    {
        var sut = CreateSut();
        try
        {
            sut.Initialize();

            var menu = sut.NotifyIcon!.ContextMenuStrip!;
            var topLevelLabels = menu.Items
                .OfType<ToolStripMenuItem>()
                .Where(i => i.Enabled)
                .Select(i => i.Text)
                .ToList();

            Assert.Equal(
                ["Session", "Sections", "Plan / JSON", "Overlay", "Windows / App"],
                topLevelLabels);
        }
        finally
        {
            sut.Dispose();
        }
    }

    [Fact]
    public void PauseResume_ClickingItemTogglesTimerAndLabel()
    {
        var timer = new FakeSessionTimerService { IsRunning = true, IsPaused = false };
        var sut = CreateSut();
        try
        {
            sut.Initialize();
            sut.SetTimerService(timer);

            var pauseResume = FindMenuItem(sut, "Session", "Pause Session");
            Assert.Equal("Pause Session", pauseResume.Text);

            pauseResume.PerformClick();
            Assert.Equal(1, timer.PauseCalls);
            Assert.Equal("Resume Session", pauseResume.Text);

            pauseResume.PerformClick();
            Assert.Equal(1, timer.ResumeCalls);
            Assert.Equal("Pause Session", pauseResume.Text);
        }
        finally
        {
            sut.Dispose();
        }
    }

    [Fact]
    public void RecentSessions_SubmenuIsBuiltDynamicallyOnOpen()
    {
        var recent = new FakeRecentSessionsService
        {
            Existing =
            [
                @"C:\sessions\demo-a.json",
                @"C:\sessions\demo-b.json"
            ]
        };

        var sut = CreateSut(recent);
        try
        {
            sut.Initialize();

            var recentMenu = FindMenuItem(sut, "Plan / JSON", "Recent Sessions");
            InvokeRecentSessionsRefresh(sut, recentMenu);

            Assert.Equal(2, recentMenu.DropDownItems.Count);
            Assert.Equal("demo-a.json  —  C:\\sessions\\demo-a.json", recentMenu.DropDownItems[0].Text);
            Assert.Equal("demo-b.json  —  C:\\sessions\\demo-b.json", recentMenu.DropDownItems[1].Text);

            recent.Existing = [];
            InvokeRecentSessionsRefresh(sut, recentMenu);

            Assert.Single(recentMenu.DropDownItems);
            Assert.Equal("(No recent sessions)", recentMenu.DropDownItems[0].Text);
            Assert.False(recentMenu.DropDownItems[0].Enabled);
        }
        finally
        {
            sut.Dispose();
        }
    }

    [Fact]
    public void WindowsAppMenu_ClickingItemsInvokesHooks()
    {
        var sut = CreateSut();
        try
        {
            sut.Initialize();

            int settingsCalls = 0;
            int summaryCalls = 0;
            int aboutCalls = 0;

            sut.OpenSettingsAction = () => settingsCalls++;
            sut.OpenSessionSummaryAction = () => summaryCalls++;
            sut.OpenAboutAction = () => aboutCalls++;

            FindMenuItem(sut, "Windows / App", "Settings").PerformClick();
            FindMenuItem(sut, "Windows / App", "Open Session Summary").PerformClick();
            FindMenuItem(sut, "Windows / App", "About").PerformClick();

            Assert.Equal(1, settingsCalls);
            Assert.Equal(1, summaryCalls);
            Assert.Equal(1, aboutCalls);
        }
        finally
        {
            sut.Dispose();
        }
    }

    [Fact]
    public void OverlayMenu_ClickingItemsInvokesOverlayHooks()
    {
        var sut = CreateSut();
        try
        {
            sut.Initialize();

            int showCalls = 0;
            int hideCalls = 0;

            sut.ShowOverlayAction = () => showCalls++;
            sut.HideOverlayAction = () => hideCalls++;

            FindMenuItem(sut, "Overlay", "Show Timeline Overlay").PerformClick();
            FindMenuItem(sut, "Overlay", "Hide Timeline Overlay").PerformClick();

            Assert.Equal(1, showCalls);
            Assert.Equal(1, hideCalls);
        }
        finally
        {
            sut.Dispose();
        }
    }

    [Fact]
    public void OverlayToggle_InternalToggleInvokesHook()
    {
        var sut = CreateSut();
        try
        {
            sut.Initialize();

            int toggleCalls = 0;
            sut.ToggleOverlayAction = () => toggleCalls++;

            var method = typeof(TrayIconService).GetMethod(
                "OnShowHideOverlay",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(method);
            method!.Invoke(sut, []);

            Assert.Equal(1, toggleCalls);
        }
        finally
        {
            sut.Dispose();
        }
    }

    private static TrayIconService CreateSut(FakeRecentSessionsService? recentSessions = null)
    {
        return new TrayIconService(
            new FakeSessionLoaderService(),
            new FakeSessionValidationService(),
            new FakeFileDialogService(),
            new FakeSettingsService(),
            recentSessions ?? new FakeRecentSessionsService());
    }

    private static ToolStripMenuItem FindMenuItem(TrayIconService sut, string parent, string child)
    {
        var menu = sut.NotifyIcon!.ContextMenuStrip!;
        var parentItem = menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == parent);
        return parentItem.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == child);
    }

    private static void InvokeRecentSessionsRefresh(TrayIconService sut, ToolStripMenuItem recentMenu)
    {
        var method = typeof(TrayIconService).GetMethod(
            "OnRecentSessionsDropDownOpening",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(sut, [recentMenu, EventArgs.Empty]);
    }

    private sealed class FakeSessionLoaderService : ISessionLoaderService
    {
        public SessionPlan Load(string path) => throw new NotImplementedException();
        public SessionPlan TryParse(string json) => throw new NotImplementedException();
        public string ExportJson(SessionPlan plan) => "{}";
        public string ExportSampleJson() => "{}";
        public TimeSpan GetTotalDuration(SessionPlan plan) => TimeSpan.Zero;
    }

    private sealed class FakeSessionValidationService : ISessionValidationService
    {
        public ValidationResult Validate(SessionPlan plan) => ValidationResult.Success;
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        public string? ShowOpenJsonDialog() => null;
        public string? ShowSaveJsonDialog(string? defaultFileName = null) => null;
        public string? ShowSaveMarkdownDialog(string? defaultFileName = null) => null;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public event EventHandler? SettingsApplied;
        public void Load() { }
        public void Save() { }
        public void RaiseSettingsApplied() => SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeRecentSessionsService : IRecentSessionsService
    {
        public IReadOnlyList<string> Existing { get; set; } = [];

        public void Add(string path) { }
        public IReadOnlyList<string> GetAll() => Existing;
        public IReadOnlyList<string> GetExisting() => Existing;
        public bool Exists(string path) => Existing.Contains(path, StringComparer.OrdinalIgnoreCase);
        public void Remove(string path) { }
        public void Clear() { }
    }

    private sealed class FakeSessionTimerService : ISessionTimerService
    {
        public bool IsRunning { get; set; }
        public bool IsPaused { get; set; }
        public int PauseCalls { get; private set; }
        public int ResumeCalls { get; private set; }

        public SessionPlan? Plan => null;
        public bool IsSessionComplete => false;
        public int CurrentSectionIndex => -1;
        public SessionSection? CurrentSection => null;
        public TimeSpan TotalPlannedDuration => TimeSpan.Zero;
        public TimeSpan SessionElapsed => TimeSpan.Zero;
        public TimeSpan SessionRemaining => TimeSpan.Zero;
        public TimeSpan SessionOvertime => TimeSpan.Zero;
        public TimeSpan CurrentSectionElapsed => TimeSpan.Zero;
        public TimeSpan CurrentSectionRemaining => TimeSpan.Zero;
        public TimeSpan CurrentSectionOvertime => TimeSpan.Zero;
        public TimeSpan BehindSchedule => TimeSpan.Zero;
        public bool AutoAdvanceSections { get; set; }
        public event EventHandler<TimerTickEventArgs>? Tick;
        public event EventHandler<SectionChangedEventArgs>? SectionChanged;
        public event EventHandler? StateChanged;

        public void LoadPlan(SessionPlan plan) { }
        public void Start() => IsRunning = true;
        public void Pause()
        {
            PauseCalls++;
            IsPaused = true;
        }
        public void Resume()
        {
            ResumeCalls++;
            IsPaused = false;
        }
        public void Reset() { }
        public void NextSection() { }
        public void PreviousSection() { }
        public void RestartCurrentSection() { }
        public void ExtendCurrentSection(TimeSpan extension) { }
        public SessionResult GetResult() => new();
        public void Dispose() { }
    }
}
