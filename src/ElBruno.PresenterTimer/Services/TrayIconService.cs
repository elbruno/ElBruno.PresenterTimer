using System.Drawing;
using System.Windows.Forms;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.ViewModels;
using ElBruno.PresenterTimer.Views;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Implements <see cref="ITrayIconService"/> using a WinForms <see cref="NotifyIcon"/>.
/// The app has no main window; the tray icon is the primary UI entry point (PRD §7.1, §8.1).
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private NotifyIcon?       _notifyIcon;
    private ContextMenuStrip? _contextMenu;

    // ── Injected services ─────────────────────────────────────────────────────
    private readonly ISessionLoaderService     _loaderService;
    private readonly ISessionValidationService _validationService;
    private readonly IFileDialogService        _fileDialogService;
    private readonly ISettingsService          _settingsService;
    private readonly IRecentSessionsService    _recentSessionsService;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private SessionPlan?            _currentPlan;
    private ISessionTimerService?   _timerService;

    // ── Overlay window callbacks (set by App after overlay is created) ────────
    /// <summary>Show the timeline overlay window (called on WPF dispatcher).</summary>
    public Action? ShowOverlayAction { get; set; }

    /// <summary>Hide the timeline overlay window (called on WPF dispatcher).</summary>
    public Action? HideOverlayAction { get; set; }

    /// <summary>Toggle the timeline overlay window visibility.</summary>
    public Action? ToggleOverlayAction { get; set; }

    /// <summary>Open the settings window (marshals to WPF dispatcher). Set by App.</summary>
    public Action? OpenSettingsAction { get; set; }

    /// <summary>Open the session summary window for the last result. Set by App.</summary>
    public Action? OpenSessionSummaryAction { get; set; }

    /// <summary>Open the Session Plan Editor window. Set by App.</summary>
    public Action? OpenSessionPlanEditorAction { get; set; }

    /// <summary>Open the About window. Set by App.</summary>
    public Action? OpenAboutAction { get; set; }

    // ── Pause/Resume menu item reference (for text toggling) ──────────────────
    private ToolStripMenuItem? _pauseResumeItem;
    // ── Recent Sessions submenu item (rebuilt dynamically on open) ────────────
    private ToolStripMenuItem? _recentSessionsItem;

    /// <summary>
    /// Exposes the underlying <see cref="NotifyIcon"/> so other services (e.g.
    /// <c>SystemNotificationService</c>) can reuse it for balloon tips without
    /// creating a second notification-area entry.
    /// Valid after <see cref="Initialize()"/>; <c>null</c> before that.
    /// </summary>
    public NotifyIcon? NotifyIcon => _notifyIcon;

    // ---------------------------------------------------------------------------
    // State → color map (PRD §7.1)
    // ---------------------------------------------------------------------------
    private static readonly Dictionary<TrayState, Color> StateColors = new()
    {
        [TrayState.NoSession] = Color.Gray,
        [TrayState.Loaded]    = Color.RoyalBlue,
        [TrayState.Running]   = Color.SeaGreen,
        [TrayState.Warning]   = Color.Goldenrod,
        [TrayState.Overtime]  = Color.Crimson,
        [TrayState.Paused]    = Color.SlateBlue,
    };

    private static readonly Dictionary<TrayState, string> StateTooltips = new()
    {
        [TrayState.NoSession] = "Session Timeline Overlay — No session loaded",
        [TrayState.Loaded]    = "Session Timeline Overlay — Session loaded",
        [TrayState.Running]   = "Session Timeline Overlay — Running",
        [TrayState.Warning]   = "Session Timeline Overlay — Warning: section almost done",
        [TrayState.Overtime]  = "Session Timeline Overlay — Overtime!",
        [TrayState.Paused]    = "Session Timeline Overlay — Paused",
    };

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    public TrayIconService(
        ISessionLoaderService     loaderService,
        ISessionValidationService validationService,
        IFileDialogService        fileDialogService,
        ISettingsService          settingsService,
        IRecentSessionsService    recentSessionsService)
    {
        _loaderService          = loaderService;
        _validationService      = validationService;
        _fileDialogService      = fileDialogService;
        _settingsService        = settingsService;
        _recentSessionsService  = recentSessionsService;
    }

    // ---------------------------------------------------------------------------
    // Timer service wiring
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Registers the live <see cref="ISessionTimerService"/> so tray menu handlers
    /// can forward user actions to it (called by App after the session is started).
    /// </summary>
    public void SetTimerService(ISessionTimerService timerService)
    {
        _timerService = timerService;
        RefreshPauseResumeLabel();
    }

    private void RefreshPauseResumeLabel()
    {
        if (_pauseResumeItem is null || _timerService is null) return;
        _pauseResumeItem.Text = _timerService.IsPaused ? "Resume Session" : "Pause Session";
    }

    // ---------------------------------------------------------------------------
    // ITrayIconService
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public void Initialize()
    {
        _contextMenu = BuildContextMenu();

        _notifyIcon = new NotifyIcon
        {
            Text    = "Session Timeline Overlay",
            Icon    = CreateColoredIcon(StateColors[TrayState.NoSession]),
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OnShowHideOverlay();
        };
    }

    /// <inheritdoc />
    public void SetState(TrayState state)
    {
        if (_notifyIcon == null) return;

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = CreateColoredIcon(StateColors[state]);
        _notifyIcon.Text = StateTooltips[state];
        oldIcon?.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
    }

    // ---------------------------------------------------------------------------
    // Icon generation — draws a filled circle onto a 16×16 bitmap, no external assets
    // ---------------------------------------------------------------------------
    private static Icon CreateColoredIcon(Color color)
    {
        const int size = 16;
        using var bmp = new Bitmap(size, size);
        using var g   = Graphics.FromImage(bmp);

        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, size - 2, size - 2);

        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    // ---------------------------------------------------------------------------
    // Context menu construction (PRD §7.1)
    // ---------------------------------------------------------------------------
    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var header = new ToolStripMenuItem("Session Timeline Overlay") { Enabled = false };
        menu.Items.Add(header);
        menu.Items.Add(new ToolStripSeparator());

        var sessionMenu = new ToolStripMenuItem("Session");
        sessionMenu.DropDownItems.Add(MakeItem("Start Session", OnStartSession));
        _pauseResumeItem = MakeItem("Pause Session", OnPauseSession);
        sessionMenu.DropDownItems.Add(_pauseResumeItem);
        sessionMenu.DropDownItems.Add(MakeItem("Reset Session", OnResetSession));
        menu.Items.Add(sessionMenu);

        var sectionMenu = new ToolStripMenuItem("Sections");
        sectionMenu.DropDownItems.Add(MakeItem("Next Section", OnNextSection));
        sectionMenu.DropDownItems.Add(MakeItem("Previous Section", OnPreviousSection));
        sectionMenu.DropDownItems.Add(MakeItem("Restart Current Section", OnRestartCurrentSection));
        var extend = new ToolStripMenuItem("Extend Current Section");
        extend.DropDownItems.Add(MakeItem("+1 minute",  OnExtendOneMinute));
        extend.DropDownItems.Add(MakeItem("+5 minutes", OnExtendFiveMinutes));
        sectionMenu.DropDownItems.Add(extend);
        menu.Items.Add(sectionMenu);

        var planMenu = new ToolStripMenuItem("Plan / JSON");
        planMenu.DropDownItems.Add(MakeItem("Import Session JSON", OnImportSessionJson));
        planMenu.DropDownItems.Add(MakeItem("Reload Last Session", OnReloadLastSession));
        _recentSessionsItem = new ToolStripMenuItem("Recent Sessions");
        _recentSessionsItem.DropDownOpening += OnRecentSessionsDropDownOpening;
        planMenu.DropDownItems.Add(_recentSessionsItem);
        planMenu.DropDownItems.Add(MakeItem("Export Sample JSON", OnExportSampleJson));
        menu.Items.Add(planMenu);

        var overlayMenu = new ToolStripMenuItem("Overlay");
        overlayMenu.DropDownItems.Add(MakeItem("Show Timeline Overlay", OnShowTimeline));
        overlayMenu.DropDownItems.Add(MakeItem("Hide Timeline Overlay", OnHideTimeline));
        menu.Items.Add(overlayMenu);

        var windowsAppMenu = new ToolStripMenuItem("Windows / App");
        windowsAppMenu.DropDownItems.Add(MakeItem("Open Session Preview", OnOpenSessionPreview));
        windowsAppMenu.DropDownItems.Add(MakeItem("Open Session Plan Editor", OnOpenSessionPlanEditor));
        windowsAppMenu.DropDownItems.Add(MakeItem("Open Session Summary", OnOpenSessionSummary));
        windowsAppMenu.DropDownItems.Add(MakeItem("Settings", OnOpenSettings));
        windowsAppMenu.DropDownItems.Add(MakeItem("About", OnAbout));
        windowsAppMenu.DropDownItems.Add(new ToolStripSeparator());
        windowsAppMenu.DropDownItems.Add(MakeItem("Exit", OnExit));
        menu.Items.Add(windowsAppMenu);

        return menu;
    }

    private static ToolStripMenuItem MakeItem(string text, EventHandler handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += handler;
        return item;
    }

    // ---------------------------------------------------------------------------
    // Phase 2 — timer control (wired to ISessionTimerService)
    // ---------------------------------------------------------------------------

    private void OnStartSession(object? sender, EventArgs e)
    {
        if (_currentPlan is null)
        {
            // No plan loaded: prompt the user to import first
            var answer = MessageBox.Show(
                "No session is loaded.\nWould you like to import a session JSON file?",
                "No Session Loaded", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer == DialogResult.Yes)
                OnImportSessionJson(sender, e);
            return;
        }

        if (_timerService is null)
        {
            // Plan loaded but timer not yet wired → open preview so user goes through Start Session
            OpenPreviewWindow(_currentPlan);
            return;
        }

        if (!_timerService.IsRunning)
        {
            _timerService.Start();
            SetState(TrayState.Running);
        }
    }

    private void OnPauseSession(object? sender, EventArgs e)
    {
        if (_timerService is null) return;

        if (_timerService.IsPaused)
        {
            _timerService.Resume();
            SetState(TrayState.Running);
        }
        else if (_timerService.IsRunning)
        {
            _timerService.Pause();
            SetState(TrayState.Paused);
        }

        RefreshPauseResumeLabel();
    }

    private void OnResetSession(object? sender, EventArgs e)
    {
        if (_timerService is null) return;

        if (_settingsService.Settings.General.ConfirmBeforeReset)
        {
            var answer = MessageBox.Show(
                "Reset the session timer to the beginning?",
                "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
        }

        _timerService.Reset();
        SetState(TrayState.Running);
        RefreshPauseResumeLabel();
    }

    private void OnNextSection(object? sender, EventArgs e)
        => _timerService?.NextSection();

    private void OnPreviousSection(object? sender, EventArgs e)
        => _timerService?.PreviousSection();

    private void OnRestartCurrentSection(object? sender, EventArgs e)
        => _timerService?.RestartCurrentSection();

    private void OnExtendOneMinute(object? sender, EventArgs e)
        => _timerService?.ExtendCurrentSection(TimeSpan.FromMinutes(1));

    private void OnExtendFiveMinutes(object? sender, EventArgs e)
        => _timerService?.ExtendCurrentSection(TimeSpan.FromMinutes(5));

    // ---------------------------------------------------------------------------
    // Phase 3/4 — Session file operations (implemented)
    // ---------------------------------------------------------------------------

    private void OnImportSessionJson(object? sender, EventArgs e)
    {
        var path = _fileDialogService.ShowOpenJsonDialog();
        if (path is null) return;
        LoadSessionFromPath(path);
    }

    private void OnReloadLastSession(object? sender, EventArgs e)
    {
        var path = _settingsService.Settings.General.LastSessionPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(
                "No session has been loaded previously.\nUse 'Import Session JSON' to load a session.",
                "No Last Session", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_recentSessionsService.Exists(path))
        {
            MessageBox.Show(
                $"The last session file could not be found:\n{path}\n\nPlease import a new session.",
                "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _recentSessionsService.Remove(path);
            _settingsService.Settings.General.LastSessionPath = null;
            _settingsService.Save();
            return;
        }

        LoadSessionFromPath(path);
    }

    /// <summary>Dynamically populates the Recent Sessions submenu each time it opens.</summary>
    private void OnRecentSessionsDropDownOpening(object? sender, EventArgs e)
    {
        if (_recentSessionsItem is null) return;
        _recentSessionsItem.DropDownItems.Clear();

        var paths = _recentSessionsService.GetExisting();
        if (paths.Count == 0)
        {
            _recentSessionsItem.DropDownItems.Add(
                new ToolStripMenuItem("(No recent sessions)") { Enabled = false });
            return;
        }

        foreach (var path in paths)
        {
            var caption = $"{Path.GetFileName(path)}  —  {path}";
            var item = new ToolStripMenuItem(caption);
            var capturedPath = path;
            item.Click += (_, _) => LoadRecentSession(capturedPath);
            _recentSessionsItem.DropDownItems.Add(item);
        }
    }

    /// <summary>Loads a session from the recent list, checking file existence first.</summary>
    private void LoadRecentSession(string path)
    {
        if (!_recentSessionsService.Exists(path))
        {
            MessageBox.Show(
                $"File not found:\n{path}\n\nThe entry will be removed from recent sessions.",
                "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _recentSessionsService.Remove(path);
            return;
        }

        LoadSessionFromPath(path);
    }

    private void OnExportSampleJson(object? sender, EventArgs e)
    {
        var json     = _loaderService.ExportSampleJson();
        var savePath = _fileDialogService.ShowSaveJsonDialog("sample-session.json");
        if (savePath is null) return;

        try
        {
            File.WriteAllText(savePath, json);
            MessageBox.Show(
                $"Sample session JSON exported to:\n{savePath}",
                "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not save file:\n\n{ex.Message}",
                "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---------------------------------------------------------------------------
    // Phase 4 — overlay visibility (wired to overlay window via callbacks)
    // ---------------------------------------------------------------------------

    private void OnShowTimeline(object? sender, EventArgs e)
        => ShowOverlayAction?.Invoke();

    private void OnHideTimeline(object? sender, EventArgs e)
        => HideOverlayAction?.Invoke();

    private void OnShowHideOverlay()
        => ToggleOverlayAction?.Invoke();

    private void OnOpenSessionPreview(object? sender, EventArgs e)
    {
        if (_currentPlan is null)
        {
            var answer = MessageBox.Show(
                "No session is currently loaded.\nWould you like to import a session JSON file?",
                "No Session Loaded", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer == DialogResult.Yes)
                OnImportSessionJson(sender, e);
            return;
        }
        OpenPreviewWindow(_currentPlan);
    }

    /// <summary>Opens the Session Plan Editor window.</summary>
    private void OnOpenSessionPlanEditor(object? sender, EventArgs e)
    {
        OpenSessionPlanEditorAction?.Invoke();
    }

    /// <summary>Opens the Session Summary window for the last completed result.</summary>
    private void OnOpenSessionSummary(object? sender, EventArgs e)
    {
        if (OpenSessionSummaryAction is not null)
        {
            OpenSessionSummaryAction();
            return;
        }

        MessageBox.Show(
            "No session has been completed yet.\nRun a session to completion to see the summary.",
            "No Summary Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>TODO Phase 8: open the settings window.</summary>
    private void OnOpenSettings(object? sender, EventArgs e)
    {
        if (OpenSettingsAction is not null)
            OpenSettingsAction();
    }

    /// <summary>Opens the About window.</summary>
    private void OnAbout(object? sender, EventArgs e)
    {
        if (OpenAboutAction is not null)
            OpenAboutAction();
    }

    /// <summary>
    /// Exits the application cleanly: hides the tray icon, disposes resources,
    /// then calls Application.Current.Shutdown (PRD §10.3).
    /// </summary>
    private void OnExit(object? sender, EventArgs e)
    {
        if (_notifyIcon != null)
            _notifyIcon.Visible = false;

        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    // ---------------------------------------------------------------------------
    // Import pipeline helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Core import pipeline: load → validate → persist → set state → open preview.
    /// Used by both <see cref="OnImportSessionJson"/> and <see cref="OnReloadLastSession"/>.
    /// </summary>
    private void LoadSessionFromPath(string path)
    {
        SessionPlan plan;
        try
        {
            plan = _loaderService.Load(path);
        }
        catch (SessionLoadException ex)
        {
            MessageBox.Show(
                $"Could not load session file:\n\n{ex.Message}",
                "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var result = _validationService.Validate(plan);
        if (!result.IsValid)
        {
            var errors = string.Join("\n• ", result.Errors);
            MessageBox.Show(
                $"Invalid session file.\n\n• {errors}",
                "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return; // do NOT open preview for invalid sessions (PRD §8.2)
        }

        // Persist last-session path + recent list via the service (dedupe + cap handled there)
        _recentSessionsService.Add(path);

        _currentPlan = plan;
        SetState(TrayState.Loaded); // tray icon turns blue

        OpenPreviewWindow(plan);
    }

    /// <summary>
    /// Marshals to the WPF UI thread and shows <see cref="SessionPreviewWindow"/>
    /// bound to a fresh <see cref="SessionPreviewViewModel"/>.
    /// </summary>
    private void OpenPreviewWindow(SessionPlan plan)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            var vm = new SessionPreviewViewModel(
                plan,
                _loaderService,
                _validationService,
                _fileDialogService,
                _settingsService,
                onStartSession: OnSessionStartRequested);

            var window = new SessionPreviewWindow { DataContext = vm };
            vm.RequestClose += () => window.Close();
            window.Show();
        }));
    }

    /// <summary>
    /// Called when the user confirms "▶ Start Session" in the preview window.
    /// Delegates to <see cref="App.StartLoadedSession"/> which owns the timer + overlay lifecycle.
    /// </summary>
    private void OnSessionStartRequested(SessionPlan plan)
    {
        _currentPlan = plan;
        App.StartLoadedSession(plan);
    }
}
