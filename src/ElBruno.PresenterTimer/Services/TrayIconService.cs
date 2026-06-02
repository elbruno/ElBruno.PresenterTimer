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

    // ── Pause/Resume menu item reference (for text toggling) ──────────────────
    private ToolStripMenuItem? _pauseResumeItem;

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
        ISettingsService          settingsService)
    {
        _loaderService     = loaderService;
        _validationService = validationService;
        _fileDialogService = fileDialogService;
        _settingsService   = settingsService;
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

        // ── Session controls ────────────────────────────────────────────────────
        menu.Items.Add(MakeItem("Start Session",   OnStartSession));
        _pauseResumeItem = MakeItem("Pause Session", OnPauseSession);
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(MakeItem("Reset Session",   OnResetSession));
        menu.Items.Add(new ToolStripSeparator());

        // ── Section navigation ──────────────────────────────────────────────────
        menu.Items.Add(MakeItem("Next Section",             OnNextSection));
        menu.Items.Add(MakeItem("Previous Section",         OnPreviousSection));
        menu.Items.Add(MakeItem("Restart Current Section",  OnRestartCurrentSection));

        var extend = new ToolStripMenuItem("Extend Current Section");
        extend.DropDownItems.Add(MakeItem("+1 minute",  OnExtendOneMinute));
        extend.DropDownItems.Add(MakeItem("+5 minutes", OnExtendFiveMinutes));
        menu.Items.Add(extend);
        menu.Items.Add(new ToolStripSeparator());

        // ── Session file operations ─────────────────────────────────────────────
        menu.Items.Add(MakeItem("Import Session JSON",   OnImportSessionJson));
        menu.Items.Add(MakeItem("Reload Last Session",   OnReloadLastSession));
        menu.Items.Add(MakeItem("Recent Sessions",       OnRecentSessions));
        menu.Items.Add(MakeItem("Export Sample JSON",    OnExportSampleJson));
        menu.Items.Add(new ToolStripSeparator());

        // ── Overlay visibility ──────────────────────────────────────────────────
        menu.Items.Add(MakeItem("Show Timeline Overlay", OnShowTimeline));
        menu.Items.Add(MakeItem("Hide Timeline Overlay", OnHideTimeline));
        menu.Items.Add(new ToolStripSeparator());

        // ── Windows ────────────────────────────────────────────────────────────
        menu.Items.Add(MakeItem("Open Session Preview", OnOpenSessionPreview));
        menu.Items.Add(MakeItem("Open Session Summary", OnOpenSessionSummary));
        menu.Items.Add(MakeItem("Settings",             OnOpenSettings));
        menu.Items.Add(MakeItem("About",                OnAbout));
        menu.Items.Add(new ToolStripSeparator());

        // ── Exit ───────────────────────────────────────────────────────────────
        menu.Items.Add(MakeItem("Exit", OnExit));

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
        LoadSessionFromPath(path);
    }

    /// <summary>TODO Phase 3: show a recent-sessions sub-menu from settings.</summary>
    private void OnRecentSessions(object? sender, EventArgs e)
    {
        // TODO Phase 3 — build dynamic sub-menu from settings.General.RecentSessionPaths.
        var recent = _settingsService.Settings.General.RecentSessionPaths;
        if (recent.Count == 0)
        {
            MessageBox.Show("No recent sessions found.", "Recent Sessions",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var list = string.Join("\n", recent.Select((p, i) => $"{i + 1}. {p}"));
        MessageBox.Show(list, "Recent Sessions", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    /// <summary>TODO Phase 6: open the session summary window.</summary>
    private void OnOpenSessionSummary(object? sender, EventArgs e)
    {
        // TODO Phase 6 — SessionSummaryWindow.Show().
    }

    /// <summary>TODO Phase 8: open the settings window.</summary>
    private void OnOpenSettings(object? sender, EventArgs e)
    {
        if (OpenSettingsAction is not null)
            OpenSettingsAction();
    }

    /// <summary>TODO Phase 9: show an About dialog with version information.</summary>
    private void OnAbout(object? sender, EventArgs e)
    {
        // TODO Phase 9 — AboutWindow or simple MessageBox with version/credits.
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

        // Persist last-session path + recent list
        _settingsService.Settings.General.LastSessionPath = path;
        AddToRecentSessions(path);
        _settingsService.Save();

        _currentPlan = plan;
        SetState(TrayState.Loaded); // tray icon turns blue

        OpenPreviewWindow(plan);
    }

    private void AddToRecentSessions(string path)
    {
        var recent = _settingsService.Settings.General.RecentSessionPaths;
        recent.Remove(path); // deduplicate
        recent.Insert(0, path);
        if (recent.Count > 10)
            recent.RemoveRange(10, recent.Count - 10);
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
