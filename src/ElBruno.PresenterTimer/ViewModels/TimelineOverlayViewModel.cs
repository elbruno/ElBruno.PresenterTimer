using System.Collections.ObjectModel;
using System.Windows.Threading;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// ViewModel for <c>Views\TimelineOverlayWindow.xaml</c> (PRD §7.6, §7.7).
/// Subscribes to <see cref="ISessionTimerService"/> events on the thread-pool and marshals
/// all property updates to the WPF <see cref="Dispatcher"/> so bindings stay safe.
/// </summary>
public sealed class TimelineOverlayViewModel : ViewModelBase, IDisposable
{
    private readonly ISessionTimerService    _timerService;
    private readonly SessionPlan             _plan;
    private readonly AppSettings             _settings;
    private readonly Dispatcher              _dispatcher;
    private readonly Action<double, double>? _onPositionChanged;
    private readonly double                  _totalDurationSeconds;

    // ── Backing fields ────────────────────────────────────────────────────────
    private string _sessionTitle                  = string.Empty;
    private string _currentSectionTitle           = string.Empty;
    private string _nextSectionTitle              = string.Empty;
    private string _currentSectionElapsedDisplay  = "00:00";
    private string _currentSectionRemainingDisplay = "00:00";
    private string _sessionElapsedDisplay         = "00:00";
    private string _sessionRemainingDisplay       = "00:00";
    private string _overtimeDisplay               = string.Empty;
    private bool   _isSessionOvertime;
    private bool   _isSectionWarning;
    private bool   _isPaused;
    private bool   _isRunning;
    private double _overlayOpacity                = 0.85;
    private double _progressFillOpacity           = 0.20;

    // Alert message (transient, clears after AlertMessageDurationSeconds)
    private string _alertMessage        = string.Empty;
    private bool   _isAlertMessageVisible;
    private readonly DispatcherTimer _alertMessageTimer;

    // ── Alert pulse event (subscribed by code-behind to trigger storyboard) ──
    /// <summary>
    /// Fired on the WPF dispatcher when an alert arrives and
    /// <c>AlertSettings.EnableOverlayPulse</c> is <c>true</c>.
    /// The view code-behind starts its pulse <see cref="System.Windows.Media.Animation.Storyboard"/>
    /// when this fires (PRD §7.8).
    /// </summary>
    public event EventHandler? PulseRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public TimelineOverlayViewModel(
        ISessionTimerService    timerService,
        SessionPlan             plan,
        AppSettings             settings,
        Dispatcher              dispatcher,
        Action<double, double>? onPositionChanged = null)
    {
        _timerService       = timerService;
        _plan               = plan;
        _settings           = settings;
        _dispatcher         = dispatcher;
        _onPositionChanged  = onPositionChanged;

        _totalDurationSeconds = plan.Sections.Sum(s => s.Duration.TotalSeconds);
        _overlayOpacity       = Math.Clamp(settings.OverlayStyle.OverlayOpacity / 100.0, 0.1, 1.0);
        _progressFillOpacity  = Math.Clamp(settings.OverlayStyle.ProgressFillOpacity / 100.0, 0.0, 1.0);

        SessionTitle        = plan.Title;
        CurrentSectionTitle = plan.Sections.Count > 0 ? plan.Sections[0].Title : string.Empty;
        NextSectionTitle    = plan.Sections.Count > 1 ? plan.Sections[1].Title : "—";

        InitializeSections();

        // Alert message auto-clear timer (runs on WPF dispatcher — created on UI thread)
        _alertMessageTimer          = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _alertMessageTimer.Tick    += OnAlertMessageTimerTick;

        _timerService.Tick           += OnTimerTick;
        _timerService.SectionChanged += OnSectionChanged;
        _timerService.StateChanged   += OnStateChanged;
    }

    // ── Bound properties ──────────────────────────────────────────────────────

    public ObservableCollection<OverlaySectionViewModel> Sections { get; } = [];

    public string SessionTitle
    {
        get => _sessionTitle;
        private set => SetProperty(ref _sessionTitle, value);
    }

    public string CurrentSectionTitle
    {
        get => _currentSectionTitle;
        private set => SetProperty(ref _currentSectionTitle, value);
    }

    public string NextSectionTitle
    {
        get => _nextSectionTitle;
        private set => SetProperty(ref _nextSectionTitle, value);
    }

    public string CurrentSectionElapsedDisplay
    {
        get => _currentSectionElapsedDisplay;
        private set => SetProperty(ref _currentSectionElapsedDisplay, value);
    }

    public string CurrentSectionRemainingDisplay
    {
        get => _currentSectionRemainingDisplay;
        private set => SetProperty(ref _currentSectionRemainingDisplay, value);
    }

    public string SessionElapsedDisplay
    {
        get => _sessionElapsedDisplay;
        private set => SetProperty(ref _sessionElapsedDisplay, value);
    }

    public string SessionRemainingDisplay
    {
        get => _sessionRemainingDisplay;
        private set => SetProperty(ref _sessionRemainingDisplay, value);
    }

    /// <summary>Formatted overtime string, e.g. "+01:42". Empty when not overtime.</summary>
    public string OvertimeDisplay
    {
        get => _overtimeDisplay;
        private set => SetProperty(ref _overtimeDisplay, value);
    }

    public bool IsSessionOvertime
    {
        get => _isSessionOvertime;
        private set => SetProperty(ref _isSessionOvertime, value);
    }

    public bool IsSectionWarning
    {
        get => _isSectionWarning;
        private set => SetProperty(ref _isSectionWarning, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set => SetProperty(ref _isPaused, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    /// <summary>Overall overlay window opacity (0.0–1.0) read from <see cref="AppSettings"/>.</summary>
    public double OverlayOpacity
    {
        get => _overlayOpacity;
        set => SetProperty(ref _overlayOpacity, value);
    }

    public double ProgressFillOpacity
    {
        get => _progressFillOpacity;
        set => SetProperty(ref _progressFillOpacity, value);
    }

    /// <summary>
    /// Transient alert message shown for <c>AlertSettings.AlertMessageDurationSeconds</c>
    /// then cleared.  E.g. "⚠️ 1:00 left in &quot;Demo&quot;".
    /// </summary>
    public string AlertMessage
    {
        get => _alertMessage;
        private set => SetProperty(ref _alertMessage, value);
    }

    /// <summary><c>true</c> while the transient alert message is visible on the overlay.</summary>
    public bool IsAlertMessageVisible
    {
        get => _isAlertMessageVisible;
        private set => SetProperty(ref _isAlertMessageVisible, value);
    }

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the view's <c>SizeChanged</c> handler to distribute proportional pixel widths
    /// across section segments (PRD §7.6 — "segment width proportional to duration").
    /// </summary>
    public void UpdateSectionWidths(double availableWidth)
    {
        if (availableWidth <= 0) return;
        foreach (var s in Sections)
            s.PixelWidth = Math.Max(2.0, availableWidth * s.DurationFraction);
    }

    /// <summary>Called when the user drags the overlay window to persist its position (PRD §7.7).</summary>
    public void SavePosition(double left, double top)
        => _onPositionChanged?.Invoke(left, top);

    /// <summary>
    /// Applies updated overlay style settings live without restarting the session (PRD §7.13).
    /// Called by <c>App</c> when the Settings window saves/applies.
    /// </summary>
    public void ApplyStyleSettings(OverlayStyleSettings style)
        => _dispatcher.BeginInvoke(() =>
        {
            OverlayOpacity      = Math.Clamp(style.OverlayOpacity / 100.0, 0.1, 1.0);
            ProgressFillOpacity = Math.Clamp(style.ProgressFillOpacity / 100.0, 0.0, 1.0);
        });

    /// <summary>
    /// Shows a transient alert message on the overlay and (optionally) fires a pulse animation.
    /// Called by <c>App</c> on the thread-pool <see cref="IAlertService.AlertRaised"/> thread;
    /// marshals internally to the WPF dispatcher (PRD §7.8, §10.2).
    /// </summary>
    /// <param name="e">Alert event args from <see cref="IAlertService.AlertRaised"/>.</param>
    /// <param name="durationSeconds">How long to show the message before fading it out.</param>
    /// <param name="enablePulse">
    /// When <c>true</c> and the current alert warrants visual attention,
    /// fires <see cref="PulseRequested"/> so the view can start its pulse animation.
    /// </param>
    public void TriggerAlert(AlertEventArgs e, int durationSeconds, bool enablePulse)
    {
        _dispatcher.BeginInvoke(() =>
        {
            AlertMessage          = e.Message;
            IsAlertMessageVisible = true;

            if (enablePulse)
                PulseRequested?.Invoke(this, EventArgs.Empty);

            // Restart the auto-clear timer
            _alertMessageTimer.Stop();
            _alertMessageTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, durationSeconds));
            _alertMessageTimer.Start();
        });
    }

    private void OnAlertMessageTimerTick(object? sender, EventArgs e)
    {
        _alertMessageTimer.Stop();
        IsAlertMessageVisible = false;
        AlertMessage          = string.Empty;
    }

    // ── Timer event handlers (thread-pool → dispatcher) ───────────────────────

    private void OnTimerTick(object? sender, TimerTickEventArgs e)
        => _dispatcher.BeginInvoke(() => ApplyTick(e));

    private void OnSectionChanged(object? sender, SectionChangedEventArgs e)
        => _dispatcher.BeginInvoke(() => ApplySectionChange(e.CurrentSectionIndex));

    private void OnStateChanged(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(() =>
        {
            IsPaused  = _timerService.IsPaused;
            IsRunning = _timerService.IsRunning;
        });
    }

    // ── Tick logic ────────────────────────────────────────────────────────────

    private void ApplyTick(TimerTickEventArgs e)
    {
        SessionElapsedDisplay = FormatTime(e.SessionElapsed);
        SessionRemainingDisplay = e.IsSessionOvertime
            ? $"+{FormatTime(e.SessionOvertime)}"
            : FormatTime(e.SessionRemaining);

        IsSessionOvertime = e.IsSessionOvertime;
        OvertimeDisplay   = e.IsSessionOvertime ? $"+{FormatTime(e.SessionOvertime)}" : string.Empty;

        int idx = e.CurrentSectionIndex;
        if (idx < 0 || idx >= _plan.Sections.Count) return;

        var section = _plan.Sections[idx];
        CurrentSectionTitle = section.Title;

        CurrentSectionElapsedDisplay = FormatTime(e.CurrentSectionElapsed);
        CurrentSectionRemainingDisplay = e.IsSectionOvertime
            ? $"+{FormatTime(e.CurrentSectionOvertime)}"
            : FormatTime(e.CurrentSectionRemaining);

        int nextIdx = idx + 1;
        NextSectionTitle = nextIdx < _plan.Sections.Count ? _plan.Sections[nextIdx].Title : "—";

        var warnThreshold = section.WarningAt
            ?? ParseTimeSpanOrDefault(_settings.Alerts.SectionWarningThreshold, TimeSpan.FromMinutes(1));

        bool inWarning = !e.IsSectionOvertime && e.CurrentSectionRemaining <= warnThreshold;
        IsSectionWarning = inWarning;

        // Update per-section visual states
        for (int i = 0; i < Sections.Count; i++)
        {
            var vm = Sections[i];
            if (i < idx)
            {
                vm.State            = SectionVisualState.Completed;
                vm.ProgressFraction = 0.0;
            }
            else if (i > idx)
            {
                vm.State            = SectionVisualState.Upcoming;
                vm.ProgressFraction = 0.0;
            }
            else // current
            {
                vm.State = e.IsSectionOvertime    ? SectionVisualState.Overtime
                         : inWarning              ? SectionVisualState.Warning
                         :                         SectionVisualState.Current;

                double effectiveSecs = section.Duration.TotalSeconds;
                vm.ProgressFraction = effectiveSecs > 0
                    ? Math.Min(1.0, e.CurrentSectionElapsed.TotalSeconds / effectiveSecs)
                    : 0.0;
            }
        }
    }

    private void ApplySectionChange(int currentIdx)
    {
        for (int i = 0; i < Sections.Count; i++)
        {
            Sections[i].State = i < currentIdx  ? SectionVisualState.Completed
                              : i > currentIdx  ? SectionVisualState.Upcoming
                              :                   SectionVisualState.Current;

            if (i != currentIdx)
                Sections[i].ProgressFraction = 0.0;
        }

        if (currentIdx >= 0 && currentIdx < _plan.Sections.Count)
        {
            CurrentSectionTitle = _plan.Sections[currentIdx].Title;
            int nextIdx = currentIdx + 1;
            NextSectionTitle = nextIdx < _plan.Sections.Count ? _plan.Sections[nextIdx].Title : "—";
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void InitializeSections()
    {
        Sections.Clear();
        double total = _totalDurationSeconds > 0 ? _totalDurationSeconds : 1.0;

        for (int i = 0; i < _plan.Sections.Count; i++)
        {
            var s = _plan.Sections[i];
            Sections.Add(new OverlaySectionViewModel
            {
                Title            = s.Title,
                DurationFraction = s.Duration.TotalSeconds / total,
                State            = i == 0 ? SectionVisualState.Current : SectionVisualState.Upcoming,
            });
        }
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
    }

    private static TimeSpan ParseTimeSpanOrDefault(string? value, TimeSpan fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return TimeSpan.TryParseExact(value, @"hh\:mm\:ss", null, out var ts) ? ts : fallback;
    }

    public void Dispose()
    {
        _alertMessageTimer.Stop();
        _alertMessageTimer.Tick -= OnAlertMessageTimerTick;

        _timerService.Tick           -= OnTimerTick;
        _timerService.SectionChanged -= OnSectionChanged;
        _timerService.StateChanged   -= OnStateChanged;
    }
}
