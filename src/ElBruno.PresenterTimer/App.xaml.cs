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
    private SettingsWindow?         _settingsWindow;

    // ── Alert services (per-session) ─────────────────────────────────────────
    private AlertService?               _alertService;
    private SoundAlertService?          _soundAlertService;
    private SystemNotificationService?  _notificationService;

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

        // ── Sound service (app-lifetime; PlayTestSound ignores IsEnabled flag) ───
        _soundAlertService = new SoundAlertService(_settingsService.Settings.Alerts);

        // ── Tray icon ───────────────────────────────────────────────────────────
        _trayIconService = new TrayIconService(
            loaderService,
            validationService,
            _fileDialogService,
            _settingsService);

        _trayIconService.Initialize();
        _trayIconService.SetState(TrayState.NoSession); // initial gray state

        // ── Wire Settings window callback ────────────────────────────────────
        _trayIconService.OpenSettingsAction = OpenSettingsWindow;
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
        _timerService.AutoAdvanceSections = settings.Behavior.AutoAdvanceSections;
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

        // ── Create overlay window ────────────────────────────────────────────
        var vm = new TimelineOverlayViewModel(
            _timerService,
            plan,
            settings,
            Dispatcher,
            OnOverlayPositionChanged);

        _overlayWindow = new TimelineOverlayWindow { DataContext = vm };
        PositionOverlay(_overlayWindow);

        // Wire tray overlay-visibility callbacks
        _trayIconService.ShowOverlayAction   = () => Dispatcher.BeginInvoke(() => _overlayWindow?.Show());
        _trayIconService.HideOverlayAction   = () => Dispatcher.BeginInvoke(() => _overlayWindow?.Hide());
        _trayIconService.ToggleOverlayAction = () => Dispatcher.BeginInvoke(() =>
        {
            if (_overlayWindow?.IsVisible == true)
                _overlayWindow.Hide();
            else
                _overlayWindow?.Show();
        });

        // Show overlay automatically if the behavior setting is on (PRD §7.1 / §8.3)
        if (settings.Behavior.ShowOverlayWhenSessionStarts)
            _overlayWindow.Show();

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
    // Overlay positioning helpers
    // ---------------------------------------------------------------------------

    private void PositionOverlay(TimelineOverlayWindow window)
    {
        var layout = _settingsService!.Settings.OverlayLayout;
        var screens = Screen.AllScreens;
        var screen  = (layout.Monitor >= 0 && layout.Monitor < screens.Length)
            ? screens[layout.Monitor]
            : Screen.PrimaryScreen!;

        double width = screen.Bounds.Width * Math.Clamp(layout.WidthFraction, 0.2, 1.0);
        window.Width = width;

        // Restore saved position if available (PRD §7.7)
        if (layout.RememberCustomPosition && layout.CustomX.HasValue && layout.CustomY.HasValue)
        {
            window.Left = layout.CustomX.Value;
            window.Top  = layout.CustomY.Value;
        }
        else
        {
            ApplyNamedPosition(window, layout.Position, screen, width);
        }
    }

    private static void ApplyNamedPosition(
        Window window, string position, Screen screen, double width)
    {
        double left = screen.Bounds.Left;
        double top  = screen.Bounds.Top;
        double sw   = screen.Bounds.Width;
        double sh   = screen.Bounds.Height;

        (window.Left, window.Top) = position switch
        {
            "TopLeft"      => (left + 4,             top + 4),
            "TopRight"     => (left + sw - width - 4, top + 4),
            "BottomCenter" => (left + (sw - width) / 2.0, top + sh - 90),
            "BottomLeft"   => (left + 4,             top + sh - 90),
            "BottomRight"  => (left + sw - width - 4, top + sh - 90),
            _              => (left + (sw - width) / 2.0, top + 4), // TopCenter (default)
        };
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
        if (_overlayWindow is null) return;

        if (_overlayWindow.DataContext is IDisposable disposable)
            disposable.Dispose();

        _overlayWindow.Close();
        _overlayWindow = null;

        // Clear overlay callbacks on the tray service
        if (_trayIconService is not null)
        {
            _trayIconService.ShowOverlayAction   = null;
            _trayIconService.HideOverlayAction   = null;
            _trayIconService.ToggleOverlayAction = null;
        }
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
                playTestSound: soundService.PlayTestSound);

            _settingsWindow = new SettingsWindow { DataContext = vm };
            vm.RequestClose += () => _settingsWindow?.Close();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }));
    }

    private void OnSettingsApplied(object? sender, EventArgs e)
    {
        // Apply overlay opacity (and any other live-updatable style) to the running overlay
        if (_overlayWindow?.DataContext is TimelineOverlayViewModel overlayVm)
            overlayVm.ApplyStyleSettings(_settingsService!.Settings.OverlayStyle);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TimeSpan ParseTimeSpanOrDefault(string? value, TimeSpan fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return TimeSpan.TryParseExact(value, @"hh\:mm\:ss", null, out var ts) ? ts : fallback;
    }
}
