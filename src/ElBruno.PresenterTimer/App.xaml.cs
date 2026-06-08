using Application = System.Windows.Application;
using System.Windows;
using System.Windows.Forms;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;
using ElBruno.PresenterTimer.ViewModels;
using ElBruno.PresenterTimer.Views;

namespace ElBruno.PresenterTimer;

/// <summary>
/// Application entry point. Starts minimised to tray (no main window).
/// Owns the lifetime of all root-level services.
/// </summary>
public partial class App : Application
{
    private TrayIconService?        _trayIconService;
    private SettingsService?        _settingsService;
    private FileDialogService?      _fileDialogService;
    private SessionTimerService?    _timerService;
    private TimelineOverlayWindow?  _overlayWindow;
    private Window?                 _miniOverlayWindow;
    private SettingsWindow?         _settingsWindow;
    private SessionPlanEditorWindow? _sessionPlanEditorWindow;
    private string                  _currentOverlayMode = "FullTimeline"; // tracks active overlay mode

    // ── Phase 10 services ─────────────────────────────────────────────────────
    private RecentSessionsService?   _recentSessionsService;
    private WindowPlacementService?  _windowPlacementService;

    // ── Alert services (per-session) ─────────────────────────────────────────
    private AlertService?               _alertService;
    private SoundAlertService?          _soundAlertService;
    private SystemNotificationService?  _notificationService;

    // ── Speech analysis service (app-lifetime; disabled by default in Phase 1) ──
    private SpeechAnalysisService?      _speechAnalysisService;

    // ── Session summary state ─────────────────────────────────────────────────
    /// <summary>Result from the most recently completed session; used by "Open Session Summary".</summary>
    private SessionResult? _lastSessionResult;

    // Last computed tray state from timer ticks (used to suppress redundant SetState calls).
    private volatile int _lastTrayStateOrdinal = (int)TrayState.NoSession;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Must not show a main window on startup (PRD §8.1).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // WinForms interop: enable visual styles for the NotifyIcon/ContextMenuStrip.
        System.Windows.Forms.Application.EnableVisualStyles();

        // ── Settings ────────────────────────────────────────────────────────────
        _settingsService = new SettingsService();
        _settingsService.Load(); // creates settings.json with defaults on first run
        _settingsService.SettingsApplied += OnSettingsApplied;

        // ── Session services (Parker's implementations) ─────────────────────────
        var loaderService     = new SessionLoaderService();
        var validationService = new SessionValidationService();
        _fileDialogService    = new FileDialogService();

        // ── Recent sessions + window placement (Phase 10) ───────────────────────
        _recentSessionsService  = new RecentSessionsService(_settingsService);
        _windowPlacementService = new WindowPlacementService();

        // ── Sound service (app-lifetime; PlayTestSound ignores IsEnabled flag) ───
        _soundAlertService = new SoundAlertService(_settingsService.Settings.Alerts);

        // ── Speech analysis service (app-lifetime; disabled by default in Phase 1) ──
        _speechAnalysisService = new SpeechAnalysisService();

        // ── Tray icon ───────────────────────────────────────────────────────────
        _trayIconService = new TrayIconService(
            loaderService,
            validationService,
            _fileDialogService,
            _settingsService,
            _recentSessionsService);

        _trayIconService.Initialize();
        _trayIconService.SetState(TrayState.NoSession); // initial gray state

        // ── Wire tray callbacks ──────────────────────────────────────────────
        _trayIconService.OpenSettingsAction      = OpenSettingsWindow;
        _trayIconService.OpenSessionPlanEditorAction = OpenSessionPlanEditorWindow;
        _trayIconService.OpenSessionSummaryAction = OpenLastSessionSummary;
        _trayIconService.OpenAboutAction          = OpenAboutWindow;

        // ── Auto-load last session on startup (General setting, PRD §7.16) ──────
        if (_settingsService.Settings.General.AutoLoadLastSessionOnStartup)
        {
            var lastPath = _settingsService.Settings.General.LastSessionPath;
            if (!string.IsNullOrWhiteSpace(lastPath))
            {
                if (_recentSessionsService.Exists(lastPath))
                {
                    try
                    {
                        var plan   = loaderService.Load(lastPath);
                        var result = validationService.Validate(plan);
                        if (result.IsValid)
                        {
                            _recentSessionsService.Add(lastPath);
                            // Open preview (matches normal import UX) via dispatcher
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var vm = new ViewModels.SessionPreviewViewModel(
                                    plan,
                                    loaderService,
                                    validationService,
                                    _fileDialogService,
                                    _settingsService,
                                    onStartSession: p => StartSessionInternal(p));
                                var win = new SessionPreviewWindow { DataContext = vm };
                                vm.RequestClose += () => win.Close();
                                win.Show();
                            }));
                        }
                    }
                    catch
                    {
                        // Non-fatal: missing/corrupt auto-load file; proceed silently
                    }
                }
                else
                {
                    // File no longer exists — clean up quietly
                    _recentSessionsService.Remove(lastPath);
                    _settingsService.Settings.General.LastSessionPath = null;
                    _settingsService.Save();
                }
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TearDownAlertServices();
        _timerService?.Dispose();
        _trayIconService?.Dispose();
        base.OnExit(e);
    }

    // ---------------------------------------------------------------------------
    // StartLoadedSession — Phase 6 + Phase 7 implementation
    // Called from SessionPreviewViewModel → TrayIconService.OnSessionStartRequested
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates and starts a <see cref="SessionTimerService"/> for the given plan,
    /// shows the timeline overlay (when the setting is enabled), and wires all
    /// tray-menu controls and alert services to the live timer (PRD §8.3, §7.8).
    /// </summary>
    public static void StartLoadedSession(SessionPlan plan)
        => ((App)Current).StartSessionInternal(plan);

    private void StartSessionInternal(SessionPlan plan)
    {
        // Tear down any previous session
        if (_timerService is not null)
        {
            _timerService.Tick         -= OnTimerTick;
            _timerService.StateChanged -= OnTimerStateChanged;
            _timerService.Dispose();
            _timerService = null;
        }

        TearDownAlertServices();
        CloseOverlay();

        var settings = _settingsService!.Settings;

        // ── Create timer ─────────────────────────────────────────────────────
        _timerService = new SessionTimerService();
        // Always guide the presenter to the current topic by advancing sections automatically.
        _timerService.AutoAdvanceSections = true;
        _timerService.LoadPlan(plan);

        _timerService.Tick         += OnTimerTick;
        _timerService.StateChanged += OnTimerStateChanged;

        // ── Wire tray menu to timer ──────────────────────────────────────────
        _trayIconService!.SetTimerService(_timerService);

        // ── Create alert services ────────────────────────────────────────────
        _alertService        = new AlertService(settings.Alerts);
        _soundAlertService   = new SoundAlertService(settings.Alerts);
        _notificationService = new SystemNotificationService(settings.Alerts, _trayIconService.NotifyIcon);

        _alertService.Attach(_timerService);
        _alertService.AlertRaised += OnAlertRaised;

        // ── Create overlay window based on current mode setting ──────────────
        _currentOverlayMode = settings.OverlayLayout.OverlayMode;
        
        if (_currentOverlayMode == "Mini")
        {
            CreateMiniOverlay(plan, settings);
        }
        else
        {
            CreateFullTimelineOverlay(plan, settings);
        }

        // Wire tray overlay-visibility callbacks
        _trayIconService.ShowOverlayAction   = () => Dispatcher.BeginInvoke(ShowOverlayWindow);
        _trayIconService.HideOverlayAction   = () => Dispatcher.BeginInvoke(HideOverlayWindow);
        _trayIconService.ToggleOverlayAction = () => Dispatcher.BeginInvoke(ToggleOverlayWindow);

        // Show overlay automatically if the behavior setting is on (PRD §7.1 / §8.3)
        if (settings.Behavior.ShowOverlayWhenSessionStarts)
        {
            if (_overlayWindow is not null)
                _overlayWindow.Show();
            else if (_miniOverlayWindow is not null)
                _miniOverlayWindow.Show();
        }

        // ── Start the timer ──────────────────────────────────────────────────
        _timerService.Start();
        _lastTrayStateOrdinal = (int)TrayState.Running;
        _trayIconService.SetState(TrayState.Running);
    }

    // ---------------------------------------------------------------------------
    // Alert event handler (PRD §7.8)
    // Raised on a thread-pool thread; marshal UI work to the WPF dispatcher.
    // ---------------------------------------------------------------------------

    private void OnAlertRaised(object? sender, AlertEventArgs e)
    {
        // ── Visual: overlay pulse + transient message ────────────────────────
        if (_overlayWindow?.DataContext is TimelineOverlayViewModel overlayVm)
        {
            var settings    = _settingsService!.Settings;
            bool enablePulse = settings.Alerts.EnableOverlayPulse;
            int  duration   = settings.Alerts.AlertMessageDurationSeconds;
            overlayVm.TriggerAlert(e, duration, enablePulse);
        }
        else if (_miniOverlayWindow?.DataContext is MiniOverlayViewModel miniVm)
        {
            var settings    = _settingsService!.Settings;
            bool enablePulse = settings.Alerts.EnableOverlayPulse;
            int  duration   = settings.Alerts.AlertMessageDurationSeconds;
            miniVm.TriggerAlert(e, duration, enablePulse);
        }

        // ── Tray color update (Warning/Overtime alert types reinforce tray state) ──
        // The existing OnTimerTick handler already drives tray transitions; no
        // extra SetState call is needed here to avoid double-dispatching.

        // ── Sound (gated by ShouldPlaySound which already checks EnableSoundAlerts) ──
        if (e.ShouldPlaySound && _soundAlertService is not null)
        {
            _ = e.AlertType switch
            {
                AlertType.SectionWarning  => PlayAndReturn(_soundAlertService.PlaySectionWarning),
                AlertType.SectionEnd      => PlayAndReturn(_soundAlertService.PlaySectionEnd),
                AlertType.SectionOvertime => PlayAndReturn(_soundAlertService.PlaySectionEnd),
                AlertType.SessionWarning  => PlayAndReturn(_soundAlertService.PlaySectionWarning),
                AlertType.SessionEnd      => PlayAndReturn(_soundAlertService.PlaySessionEnd),
                AlertType.SessionOvertime => PlayAndReturn(_soundAlertService.PlaySessionEnd),
                _                         => true
            };
        }

        // ── Windows balloon notification (gated by ShouldShowNotification) ───
        if (e.ShouldShowNotification)
            _notificationService?.Notify(e.AlertType.ToString(), e.Message);
    }

    private static bool PlayAndReturn(Action play) { play(); return true; }

    // ---------------------------------------------------------------------------
    // Timer event handlers (drive tray icon state)
    // ---------------------------------------------------------------------------

    private void OnTimerTick(object? sender, TimerTickEventArgs e)
    {
        // Compute which tray state the current tick implies
        TrayState desired;

        if (e.IsSectionOvertime || e.IsSessionOvertime)
        {
            desired = TrayState.Overtime;
        }
        else
        {
            // Check section warning threshold
            var plan = _timerService?.Plan;
            if (plan is not null && e.CurrentSectionIndex >= 0 && e.CurrentSectionIndex < plan.Sections.Count)
            {
                var section = plan.Sections[e.CurrentSectionIndex];
                var threshold = section.WarningAt
                    ?? ParseTimeSpanOrDefault(
                        _settingsService!.Settings.Alerts.SectionWarningThreshold,
                        TimeSpan.FromMinutes(1));

                desired = e.CurrentSectionRemaining <= threshold
                    ? TrayState.Warning
                    : TrayState.Running;
            }
            else
            {
                desired = TrayState.Running;
            }
        }

        // Only update when state transitions to avoid redundant WinForms calls
        int desiredOrdinal = (int)desired;
        if (System.Threading.Interlocked.Exchange(ref _lastTrayStateOrdinal, desiredOrdinal) != desiredOrdinal)
            Dispatcher.BeginInvoke(() => _trayIconService?.SetState(desired));
    }

    private void OnTimerStateChanged(object? sender, EventArgs e)
    {
        if (_timerService is null) return;

        Dispatcher.BeginInvoke(() =>
        {
            TrayState state;
            if (_timerService.IsPaused)
            {
                state = TrayState.Paused;
            }
            else if (_timerService.IsSessionComplete)
            {
                state = TrayState.Loaded; // completed, plan still in memory

                // Capture result immediately on the dispatcher (timer is done, no race)
                _lastSessionResult = _timerService.GetResult();

                var settings = _settingsService!.Settings;

                // Hide overlay if the behavior setting says so (PRD §7.14 / §8.5)
                if (settings.Behavior.HideOverlayWhenSessionEnds)
                {
                    _overlayWindow?.Hide();
                    _miniOverlayWindow?.Hide();
                }

                // Show summary window if the general setting allows it (PRD §7.14)
                if (settings.General.ShowSummaryOnSessionEnd)
                    ShowSessionSummary(_lastSessionResult);
            }
            else if (_timerService.IsRunning)
            {
                // Re-evaluate from current timer values
                state = ComputeTrayStateFromTimer();
            }
            else
            {
                state = _timerService.Plan is not null ? TrayState.Loaded : TrayState.NoSession;
            }

            _lastTrayStateOrdinal = (int)state;
            _trayIconService?.SetState(state);

            // Keep Pause/Resume label in sync
            _trayIconService?.SetTimerService(_timerService);
        });
    }

    private TrayState ComputeTrayStateFromTimer()
    {
        if (_timerService is null) return TrayState.Loaded;

        if (_timerService.CurrentSectionOvertime > TimeSpan.Zero ||
            _timerService.SessionOvertime > TimeSpan.Zero)
            return TrayState.Overtime;

        var plan = _timerService.Plan;
        if (plan is not null)
        {
            int idx = _timerService.CurrentSectionIndex;
            if (idx >= 0 && idx < plan.Sections.Count)
            {
                var section = plan.Sections[idx];
                var threshold = section.WarningAt
                    ?? ParseTimeSpanOrDefault(
                        _settingsService!.Settings.Alerts.SectionWarningThreshold,
                        TimeSpan.FromMinutes(1));

                if (_timerService.CurrentSectionRemaining <= threshold &&
                    _timerService.CurrentSectionRemaining > TimeSpan.Zero)
                    return TrayState.Warning;
            }
        }

        return TrayState.Running;
    }

    // ---------------------------------------------------------------------------
    // Alert service teardown
    // ---------------------------------------------------------------------------

    private void TearDownAlertServices()
    {
        if (_alertService is not null)
        {
            _alertService.AlertRaised -= OnAlertRaised;
            _alertService.Detach();
            _alertService.Dispose();
            _alertService = null;
        }

        _notificationService?.Dispose();
        _notificationService = null;
        // _soundAlertService is recreated per-session; dispose only the per-session instance.
        // The app-startup instance is the same reference after first session — clear regardless.
        _soundAlertService = null;
    }

    // ---------------------------------------------------------------------------
    // Overlay positioning helpers (uses WindowPlacementService — PRD §7.7 / §7.18)
    // ---------------------------------------------------------------------------

    private void PositionOverlay(TimelineOverlayWindow window)
    {
        var layout = _settingsService!.Settings.OverlayLayout;

        // Resolve the target monitor (falls back to primary if saved monitor is disconnected)
        var monitor = _windowPlacementService!.ResolveMonitor(layout.MonitorDeviceName, layout.Monitor);

        // Compute overlay pixel width
        double widthFraction = Math.Clamp(layout.WidthFraction, 0.2, 1.0);
        int    overlayWidth  = (int)(monitor.WorkingArea.Width * widthFraction);
        int    overlayHeight = 90; // compact default height in pixels

        window.Width = overlayWidth;

        // Determine the named position enum
        var position = _windowPlacementService.ParsePosition(layout.Position);

        // If RememberCustomPosition is on and we have saved coords, treat as Custom
        if (layout.RememberCustomPosition && layout.CustomX.HasValue && layout.CustomY.HasValue)
            position = OverlayPosition.Custom;

        var pt = _windowPlacementService.ResolvePlacement(
            position,
            monitor,
            new System.Drawing.Size(overlayWidth, overlayHeight),
            layout.CustomX,
            layout.CustomY);

        // Clamp so the overlay cannot escape the working area
        var clamped = _windowPlacementService.ClampToWorkingArea(
            pt,
            new System.Drawing.Size(overlayWidth, overlayHeight),
            monitor);

        window.Left = clamped.X;
        window.Top  = clamped.Y;
    }

    private void CreateFullTimelineOverlay(SessionPlan plan, AppSettings settings)
    {
        if (_overlayWindow is not null)
        {
            if (_overlayWindow.DataContext is IDisposable disposable)
                disposable.Dispose();
            _overlayWindow.ClosePermanently();
        }

        var vm = new TimelineOverlayViewModel(
            _timerService!,
            plan,
            settings,
            Dispatcher,
            OnOverlayPositionChanged);

        _overlayWindow = new TimelineOverlayWindow { DataContext = vm };
        PositionOverlay(_overlayWindow);
        _currentOverlayMode = "FullTimeline";
    }

    private void CreateMiniOverlay(SessionPlan plan, AppSettings settings)
    {
        if (_miniOverlayWindow is not null)
        {
            if (_miniOverlayWindow.DataContext is IDisposable disposable)
                disposable.Dispose();
            _miniOverlayWindow.Close();
        }

        var vm = new MiniOverlayViewModel(
            _timerService!,
            plan,
            settings,
            Dispatcher,
            OnMiniOverlayPositionChanged);

        _miniOverlayWindow = new MiniOverlayWindow { DataContext = vm };
        PositionMiniOverlay(_miniOverlayWindow);
        _currentOverlayMode = "Mini";
    }

    private void PositionMiniOverlay(Window window)
    {
        var layout = _settingsService!.Settings.OverlayLayout;

        // Resolve the target monitor (falls back to primary if saved monitor is disconnected)
        var monitor = _windowPlacementService!.ResolveMonitor(layout.MonitorDeviceName, layout.Monitor);

        // Mini window default dimensions (user can resize)
        int miniWidth  = 320;
        int miniHeight = 200;

        // Check if custom position is saved for mini window
        if (layout.RememberCustomPosition && layout.CustomX.HasValue && layout.CustomY.HasValue)
        {
            window.Left = layout.CustomX.Value;
            window.Top  = layout.CustomY.Value;
        }
        else
        {
            // Default to TopRight corner
            var position = OverlayPosition.TopRight;
            var pt = _windowPlacementService.ResolvePlacement(
                position,
                monitor,
                new System.Drawing.Size(miniWidth, miniHeight),
                null,
                null);

            // Clamp so the overlay cannot escape the working area
            var clamped = _windowPlacementService.ClampToWorkingArea(
                pt,
                new System.Drawing.Size(miniWidth, miniHeight),
                monitor);

            window.Left = clamped.X;
            window.Top  = clamped.Y;
        }
    }

    private void OnMiniOverlayPositionChanged(double left, double top)
    {
        if (_settingsService is null) return;
        var layout = _settingsService.Settings.OverlayLayout;
        if (!layout.RememberCustomPosition) return;

        layout.CustomX = left;
        layout.CustomY = top;
        _settingsService.Save();
    }

    private void OnOverlayPositionChanged(double left, double top)
    {
        if (_settingsService is null) return;
        var layout = _settingsService.Settings.OverlayLayout;
        if (!layout.RememberCustomPosition) return;

        layout.CustomX = left;
        layout.CustomY = top;
        _settingsService.Save();
    }

    private void CloseOverlay()
    {
        // Close timeline overlay
        if (_overlayWindow is not null)
        {
            if (_overlayWindow.DataContext is IDisposable disposable)
                disposable.Dispose();

            _overlayWindow.ClosePermanently();
            _overlayWindow = null;
        }

        // Close mini overlay
        if (_miniOverlayWindow is not null)
        {
            if (_miniOverlayWindow.DataContext is IDisposable disposable)
                disposable.Dispose();

            _miniOverlayWindow.Close();
            _miniOverlayWindow = null;
        }

        // Clear overlay callbacks on the tray service
        if (_trayIconService is not null)
        {
            _trayIconService.ShowOverlayAction   = null;
            _trayIconService.HideOverlayAction   = null;
            _trayIconService.ToggleOverlayAction = null;
        }

        _currentOverlayMode = "FullTimeline"; // reset to default
    }

    // ---------------------------------------------------------------------------
    // Session Summary window (Kane's API — PRD §7.14 / §8.5)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Shows the SessionSummaryWindow for the given result on the WPF dispatcher.
    /// </summary>
    private void ShowSessionSummary(SessionResult result)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var vm  = new SessionSummaryViewModel(result, _fileDialogService!);
            var win = new SessionSummaryWindow();
            win.SetViewModel(vm);
            win.Show();
        }));
    }

    /// <summary>
    /// Action wired to <c>TrayIconService.OpenSessionSummaryAction</c>.
    /// Shows the summary for the last completed session, or a friendly message if none.
    /// </summary>
    private void OpenLastSessionSummary()
    {
        if (_lastSessionResult is null)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Windows.MessageBox.Show(
                    "No session has been completed yet.\nRun a session to completion to see the summary.",
                    "No Summary Available",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }));
            return;
        }

        ShowSessionSummary(_lastSessionResult);
    }

    // ---------------------------------------------------------------------------
    // About window (PRD §7.1)
    // ---------------------------------------------------------------------------

    private void OpenAboutWindow()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var win = new AboutWindow();
            win.Show();
        }));
    }

    // ---------------------------------------------------------------------------
    // Settings window (Phase 8)
    // ---------------------------------------------------------------------------

    private void OpenSettingsWindow()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // Single-instance: bring existing window to front if already open
            if (_settingsWindow is { IsLoaded: true })
            {
                _settingsWindow.Activate();
                return;
            }

            // Pass a test-sound action so the Alerts tab "Test Sound" button always works,
            // even before a session has been started.
            var soundService = _soundAlertService
                ?? new SoundAlertService(_settingsService!.Settings.Alerts);

            var vm = new SettingsViewModel(
                _settingsService!,
                _fileDialogService!,
                _windowPlacementService!,
                playTestSound: soundService.PlayTestSound);

            _settingsWindow = new SettingsWindow { DataContext = vm };
            vm.RequestClose += () => _settingsWindow?.Close();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }));
    }

    private void OpenSessionPlanEditorWindow()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_sessionPlanEditorWindow is { IsLoaded: true })
            {
                _sessionPlanEditorWindow.Activate();
                return;
            }

            var vm = new SessionPlanEditorViewModel(
                new SessionLoaderService(),
                new SessionValidationService(),
                _fileDialogService!);

            _sessionPlanEditorWindow = new SessionPlanEditorWindow { DataContext = vm };
            vm.RequestClose += () => _sessionPlanEditorWindow?.Close();
            _sessionPlanEditorWindow.Closed += (_, _) => _sessionPlanEditorWindow = null;
            _sessionPlanEditorWindow.Show();
        }));
    }

    private void OnSettingsApplied(object? sender, EventArgs e)
    {
        // Apply overlay opacity (and any other live-updatable style) to the running overlay
        if (_overlayWindow?.DataContext is TimelineOverlayViewModel overlayVm)
            overlayVm.ApplyStyleSettings(_settingsService!.Settings.OverlayStyle);
        else if (_miniOverlayWindow?.DataContext is MiniOverlayViewModel miniVm)
            miniVm.ApplyStyleSettings(_settingsService!.Settings.OverlayStyle);

        // Re-position the overlay if layout settings changed (PRD §7.7 / §7.18)
        if (_overlayWindow is not null)
            PositionOverlay(_overlayWindow);
        else if (_miniOverlayWindow is not null)
            PositionMiniOverlay(_miniOverlayWindow);

        // Check if overlay mode changed — if so, switch windows
        var newMode = _settingsService!.Settings.OverlayLayout.OverlayMode;
        if (newMode != _currentOverlayMode && _timerService is not null)
        {
            // Close current overlay and create new one of different type
            CloseOverlay();
            if (newMode == "Mini")
            {
                CreateMiniOverlay(_timerService.Plan, _settingsService.Settings);
            }
            else
            {
                CreateFullTimelineOverlay(_timerService.Plan, _settingsService.Settings);
            }

            // Show the new overlay if it should be visible
            if ((_overlayWindow?.IsVisible ?? false) || (_miniOverlayWindow?.IsVisible ?? false))
            {
                if (_overlayWindow is not null)
                    _overlayWindow.Show();
                else if (_miniOverlayWindow is not null)
                    _miniOverlayWindow.Show();
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TimeSpan ParseTimeSpanOrDefault(string? value, TimeSpan fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return TimeSpan.TryParseExact(value, @"hh\:mm\:ss", null, out var ts) ? ts : fallback;
    }

    private void ShowOverlayWindow()
    {
        if (_overlayWindow is not null)
        {
            if (!_overlayWindow.IsVisible)
                _overlayWindow.Show();

            if (_overlayWindow.WindowState == WindowState.Minimized)
                _overlayWindow.WindowState = WindowState.Normal;

            _overlayWindow.ShowInTaskbar = false;
            _overlayWindow.Activate();
        }
        else if (_miniOverlayWindow is not null)
        {
            if (!_miniOverlayWindow.IsVisible)
                _miniOverlayWindow.Show();

            if (_miniOverlayWindow.WindowState == WindowState.Minimized)
                _miniOverlayWindow.WindowState = WindowState.Normal;

            _miniOverlayWindow.ShowInTaskbar = false;
            _miniOverlayWindow.Activate();
        }
    }

    private void HideOverlayWindow()
    {
        if (_overlayWindow is not null)
        {
            _overlayWindow.ShowInTaskbar = false;
            _overlayWindow.Hide();
        }

        if (_miniOverlayWindow is not null)
        {
            _miniOverlayWindow.ShowInTaskbar = false;
            _miniOverlayWindow.Hide();
        }
    }

    private void ToggleOverlayWindow()
    {
        bool overlayVisible = (_overlayWindow?.IsVisible == true) || (_miniOverlayWindow?.IsVisible == true);
        bool overlayMinimized = (_overlayWindow?.WindowState == WindowState.Minimized) ||
                               (_miniOverlayWindow?.WindowState == WindowState.Minimized);

        if (!overlayVisible || overlayMinimized)
        {
            ShowOverlayWindow();
            return;
        }

        HideOverlayWindow();
    }
}
