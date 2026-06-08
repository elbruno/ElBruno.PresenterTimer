using System.Windows.Threading;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// ViewModel for <c>Views\MiniOverlayWindow.xaml</c>.
/// Simplified version of TimelineOverlayViewModel focused on:
/// - Current section title (prominent)
/// - Remaining section time
/// - Remaining session time
/// - Overall session progress (0.0–1.0)
/// 
/// Subscribes to <see cref="ISessionTimerService"/> events on the thread-pool and marshals
/// all property updates to the WPF <see cref="Dispatcher"/> so bindings stay safe.
/// </summary>
public sealed class MiniOverlayViewModel : ViewModelBase, IDisposable
{
    private readonly ISessionTimerService    _timerService;
    private readonly SessionPlan             _plan;
    private readonly AppSettings             _settings;
    private readonly Dispatcher              _dispatcher;
    private readonly Action<double, double>? _onPositionChanged;
    private readonly double                  _totalDurationSeconds;

    // ── Backing fields ────────────────────────────────────────────────────────
    private string _sessionTitle                     = string.Empty;
    private string _currentSectionTitle              = string.Empty;
    private string _currentSectionRemainingDisplay   = "00:00";
    private string _sessionRemainingDisplay          = "00:00";
    private bool   _isSessionOvertime;
    private bool   _isSectionWarning;
    private bool   _isPaused;
    private bool   _isRunning;
    private double _overlayOpacity                   = 0.85;
    private double _sessionProgressFraction          = 0.0;
    private string _overtimeDisplay                  = string.Empty;

    // Next sections (for display in mini window)
    private List<SessionSection> _nextSections = [];

    // Alert message (transient, clears after AlertMessageDurationSeconds)
    private string _alertMessage                     = string.Empty;
    private bool   _isAlertMessageVisible;
    private readonly DispatcherTimer _alertMessageTimer;

    // ── Alert pulse event (subscribed by code-behind to trigger storyboard) ──
    /// <summary>
    /// Fired on the WPF dispatcher when an alert arrives and
    /// <c>AlertSettings.EnableOverlayPulse</c> is <c>true</c>.
    /// The view code-behind starts its pulse <see cref="System.Windows.Media.Animation.Storyboard"/>
    /// when this fires.
    /// </summary>
    public event EventHandler? PulseRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MiniOverlayViewModel(
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

        SessionTitle        = plan.Title;
        CurrentSectionTitle = plan.Sections.Count > 0 ? plan.Sections[0].Title : string.Empty;

        // Alert message auto-clear timer (runs on WPF dispatcher — created on UI thread)
        _alertMessageTimer          = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _alertMessageTimer.Tick    += OnAlertMessageTimerTick;

        _timerService.Tick           += OnTimerTick;
        _timerService.SectionChanged += OnSectionChanged;
        _timerService.StateChanged   += OnStateChanged;
    }

    // ── Bound properties ──────────────────────────────────────────────────────

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

    public string CurrentSectionRemainingDisplay
    {
        get => _currentSectionRemainingDisplay;
        private set => SetProperty(ref _currentSectionRemainingDisplay, value);
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

    /// <summary>Session progress as a fraction (0.0–1.0). Used for mini progress bar width.</summary>
    public double SessionProgressFraction
    {
        get => _sessionProgressFraction;
        private set => SetProperty(ref _sessionProgressFraction, value);
    }

    /// <summary>
    /// Transient alert message shown for <c>AlertSettings.AlertMessageDurationSeconds</c>
    /// then cleared. E.g. "⚠️ 1:00 left in &quot;Demo&quot;".
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

    /// <summary>List of the next 2-4 upcoming sections (after the current one) for display in mini window.</summary>
    public IReadOnlyList<SessionSection> NextSections
    {
        get => _nextSections;
        private set => SetProperty(ref _nextSections, (List<SessionSection>)value);
    }

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>Called when the user drags the overlay window to persist its position.</summary>
    public void SavePosition(double left, double top)
        => _onPositionChanged?.Invoke(left, top);

    /// <summary>Pauses the current running session.</summary>
    public void PauseSession()
        => _dispatcher.BeginInvoke(() => _timerService.Pause());

    /// <summary>Resumes a paused session.</summary>
    public void ResumeSession()
        => _dispatcher.BeginInvoke(() => _timerService.Resume());

    /// <summary>Restarts the current section from the beginning.</summary>
    public void RestartCurrentSection()
        => _dispatcher.BeginInvoke(() => _timerService.RestartCurrentSection());

    /// <summary>
    /// Applies updated overlay style settings live without restarting the session.
    /// Called by <c>App</c> when the Settings window saves/applies.
    /// </summary>
    public void ApplyStyleSettings(OverlayStyleSettings style)
        => _dispatcher.BeginInvoke(() =>
        {
            OverlayOpacity = Math.Clamp(style.OverlayOpacity / 100.0, 0.1, 1.0);
        });

    /// <summary>
    /// Shows a transient alert message on the overlay and (optionally) fires a pulse animation.
    /// Called by <c>App</c> on the thread-pool <see cref="IAlertService.AlertRaised"/> thread;
    /// marshals internally to the WPF dispatcher.
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
        // Session-level updates
        SessionRemainingDisplay = e.IsSessionOvertime
            ? $"+{FormatTime(e.SessionOvertime)}"
            : FormatTime(e.SessionRemaining);

        IsSessionOvertime = e.IsSessionOvertime;
        OvertimeDisplay   = e.IsSessionOvertime ? $"+{FormatTime(e.SessionOvertime)}" : string.Empty;

        // Session progress for mini bar (0.0 to 1.0)
        if (_totalDurationSeconds > 0)
            SessionProgressFraction = Math.Min(1.0, e.SessionElapsed.TotalSeconds / _totalDurationSeconds);

        // Current section updates
        int idx = e.CurrentSectionIndex;
        if (idx < 0 || idx >= _plan.Sections.Count) return;

        var section = _plan.Sections[idx];
        CurrentSectionTitle = section.Title;

        CurrentSectionRemainingDisplay = e.IsSectionOvertime
            ? $"+{FormatTime(e.CurrentSectionOvertime)}"
            : FormatTime(e.CurrentSectionRemaining);

        // Section warning state
        var warnThreshold = section.WarningAt
            ?? ParseTimeSpanOrDefault(_settings.Alerts.SectionWarningThreshold, TimeSpan.FromMinutes(1));

        bool inWarning = !e.IsSectionOvertime && e.CurrentSectionRemaining <= warnThreshold;
        IsSectionWarning = inWarning;
    }

    private void ApplySectionChange(int currentIdx)
    {
        if (currentIdx >= 0 && currentIdx < _plan.Sections.Count)
        {
            CurrentSectionTitle = _plan.Sections[currentIdx].Title;
            
            // Populate next sections (up to 4 upcoming sections after the current)
            var upcomingSections = new List<SessionSection>();
            for (int i = currentIdx + 1; i < _plan.Sections.Count && upcomingSections.Count < 4; i++)
            {
                upcomingSections.Add(_plan.Sections[i]);
            }
            NextSections = upcomingSections;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

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
