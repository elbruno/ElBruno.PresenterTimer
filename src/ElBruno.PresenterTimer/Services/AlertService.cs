using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Alert detection and deduplication engine driven by <see cref="ISessionTimerService"/> events
/// (PRD §7.8).  Attach to a timer, subscribe to <see cref="AlertRaised"/>, and this service
/// handles the rest.
///
/// <para><b>Deduplication contract (PRD §7.8 "Alert Deduplication")</b></para>
/// <list type="bullet">
///   <item>Each alert type fires at most once per section lifetime.</item>
///   <item>Section dedup state is cleared when entering a section fresh
///         (navigation, auto-advance) or after <see cref="ISessionTimerService.RestartCurrentSection"/>.</item>
///   <item>Full Reset clears all dedup state.</item>
///   <item>Pause/resume does NOT clear dedup; an alert that already fired will not refire
///         unless the section is restarted and the threshold is crossed again.</item>
///   <item>Session-level alerts (Warning, End, Overtime) use a dedicated session bucket
///         that is only cleared on full Reset.</item>
/// </list>
///
/// <para><b>Testability</b></para>
/// <see cref="ProcessState"/> is <c>internal</c> and directly callable in unit tests
/// (the main project exposes internals to <c>ElBruno.PresenterTimer.Tests</c>).
/// Tests can drive the service with hand-crafted <see cref="TimerTickEventArgs"/> instances
/// without a running timer to verify dedup, threshold, and settings-gate logic.
/// </summary>
public sealed class AlertService : IAlertService, IDisposable
{
    // Keyed by 0-based section index.  Session-level alerts use SessionBucket (-1).
    private readonly Dictionary<int, HashSet<AlertType>> _fired = new();
    private readonly AlertSettings _settings;
    private readonly object _lock = new();
    private ISessionTimerService? _timer;

    /// <summary>
    /// Sentinel key used in <c>_fired</c> for session-level alerts
    /// (SessionWarning, SessionEnd, SessionOvertime).
    /// </summary>
    internal const int SessionBucket = -1;

    /// <inheritdoc/>
    public event EventHandler<AlertEventArgs>? AlertRaised;

    /// <param name="settings">
    /// Alert configuration snapshot.  Toggles and thresholds are read on every tick so
    /// changes to the object are picked up immediately without re-attaching.
    /// </param>
    public AlertService(AlertSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    // ── Attach / Detach ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Attach(ISessionTimerService timer)
    {
        ArgumentNullException.ThrowIfNull(timer);
        Detach();
        _timer = timer;
        _timer.Tick += OnTimerTick;
        _timer.SectionChanged += OnSectionChanged;
    }

    /// <inheritdoc/>
    public void Detach()
    {
        if (_timer is null) return;
        _timer.Tick -= OnTimerTick;
        _timer.SectionChanged -= OnSectionChanged;
        _timer = null;
    }

    // ── Reset ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Reset()
    {
        lock (_lock)
        {
            _fired.Clear();
        }
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnTimerTick(object? sender, TimerTickEventArgs e)
    {
        var plan = _timer?.Plan;
        if (plan is null) return;
        ProcessState(e, plan);
    }

    private void OnSectionChanged(object? sender, SectionChangedEventArgs e)
    {
        lock (_lock)
        {
            switch (e.Reason)
            {
                case SectionChangeReason.Reset:
                    // Full reset: wipe everything.
                    _fired.Clear();
                    return;

                case SectionChangeReason.ManualRestart:
                    // Restarting current section: clear its dedup so alerts can refire.
                    _fired.Remove(e.CurrentSectionIndex);
                    return;

                case SectionChangeReason.ManualNext:
                case SectionChangeReason.ManualPrevious:
                case SectionChangeReason.AutoAdvance:
                    // Entering a section fresh: clear its prior dedup state.
                    _fired.Remove(e.CurrentSectionIndex);
                    break;
            }
        }

        // ManualSectionChange alert fires for explicit user navigation only.
        if (e.Reason is SectionChangeReason.ManualNext or SectionChangeReason.ManualPrevious)
        {
            var plan = _timer?.Plan;
            if (plan is null) return;

            int si = e.CurrentSectionIndex;
            string title = si >= 0 && si < plan.Sections.Count
                ? plan.Sections[si].Title
                : string.Empty;

            string message = string.IsNullOrEmpty(title)
                ? "Section changed"
                : $"Moved to \"{title}\"";

            Fire(new AlertEventArgs
            {
                AlertType = AlertType.ManualSectionChange,
                Message = message,
                SectionIndex = si,
                ShouldPlaySound = false,
                ShouldShowNotification = false
            });
        }
    }

    // ── Core evaluation (internal for unit tests) ────────────────────────────

    /// <summary>
    /// Evaluates <paramref name="tick"/> state against all enabled alert thresholds, applies
    /// deduplication, fires any due alerts via <see cref="AlertRaised"/>, and records fired
    /// state so the same alert will not fire again within the same section lifetime.
    ///
    /// <para>This method is <c>internal</c> so tests can call it directly with synthetic
    /// <see cref="TimerTickEventArgs"/> instances — no running timer required.</para>
    /// </summary>
    /// <param name="tick">Timer-state snapshot, e.g. from a <see cref="ISessionTimerService.Tick"/> payload.</param>
    /// <param name="plan">The session plan currently loaded in the timer.</param>
    internal void ProcessState(TimerTickEventArgs tick, SessionPlan plan)
    {
        int si = tick.CurrentSectionIndex;
        bool sectionValid = si >= 0 && si < plan.Sections.Count;
        var section = sectionValid ? plan.Sections[si] : null;

        // ── Section Warning ──────────────────────────────────────────────────
        if (_settings.EnableSectionWarningAlerts && section is not null)
        {
            // Per-section WarningAt takes priority over the global threshold.
            var threshold = section.WarningAt
                ?? ParseThreshold(_settings.SectionWarningThreshold, TimeSpan.FromMinutes(1));

            if (tick.CurrentSectionRemaining > TimeSpan.Zero
                && tick.CurrentSectionRemaining <= threshold
                && TryMarkFired(si, AlertType.SectionWarning))
            {
                Fire(new AlertEventArgs
                {
                    AlertType = AlertType.SectionWarning,
                    Message = $"⚠️ {Format(tick.CurrentSectionRemaining)} left in \"{section.Title}\"",
                    SectionIndex = si,
                    ShouldPlaySound = _settings.EnableSoundAlerts,
                    ShouldShowNotification = _settings.EnableWindowsNotifications
                });
            }
        }

        // ── Section End ──────────────────────────────────────────────────────
        if (_settings.EnableSectionEndAlerts && section is not null)
        {
            if (tick.CurrentSectionRemaining == TimeSpan.Zero
                && TryMarkFired(si, AlertType.SectionEnd))
            {
                Fire(new AlertEventArgs
                {
                    AlertType = AlertType.SectionEnd,
                    Message = $"Section ended: \"{section.Title}\"",
                    SectionIndex = si,
                    ShouldPlaySound = _settings.EnableSoundAlerts,
                    ShouldShowNotification = _settings.EnableWindowsNotifications
                });
            }
        }

        // ── Section Overtime ─────────────────────────────────────────────────
        if (_settings.EnableOvertimeAlerts && section is not null)
        {
            if (tick.IsSectionOvertime
                && TryMarkFired(si, AlertType.SectionOvertime))
            {
                Fire(new AlertEventArgs
                {
                    AlertType = AlertType.SectionOvertime,
                    Message = $"Overtime in \"{section.Title}\": +{Format(tick.CurrentSectionOvertime)}",
                    SectionIndex = si,
                    ShouldPlaySound = _settings.EnableSoundAlerts,
                    ShouldShowNotification = _settings.EnableWindowsNotifications
                });
            }
        }

        // ── Session Warning ──────────────────────────────────────────────────
        if (_settings.EnableSessionWarningAlerts)
        {
            var threshold = ParseThreshold(_settings.SessionWarningThreshold, TimeSpan.FromMinutes(3));

            if (tick.SessionRemaining > TimeSpan.Zero
                && tick.SessionRemaining <= threshold
                && TryMarkFired(SessionBucket, AlertType.SessionWarning))
            {
                Fire(new AlertEventArgs
                {
                    AlertType = AlertType.SessionWarning,
                    Message = $"⚠️ {Format(tick.SessionRemaining)} remaining in session",
                    SectionIndex = si,
                    ShouldPlaySound = _settings.EnableSoundAlerts,
                    ShouldShowNotification = _settings.EnableWindowsNotifications
                });
            }
        }

        // ── Session End ──────────────────────────────────────────────────────
        if (_settings.EnableSessionEndAlerts)
        {
            if (tick.SessionRemaining == TimeSpan.Zero
                && TryMarkFired(SessionBucket, AlertType.SessionEnd))
            {
                Fire(new AlertEventArgs
                {
                    AlertType = AlertType.SessionEnd,
                    Message = "Session time reached",
                    SectionIndex = si,
                    ShouldPlaySound = _settings.EnableSoundAlerts,
                    ShouldShowNotification = _settings.EnableWindowsNotifications
                });
            }
        }

        // ── Session Overtime ─────────────────────────────────────────────────
        if (_settings.EnableOvertimeAlerts)
        {
            if (tick.IsSessionOvertime
                && TryMarkFired(SessionBucket, AlertType.SessionOvertime))
            {
                Fire(new AlertEventArgs
                {
                    AlertType = AlertType.SessionOvertime,
                    Message = $"Session overtime: +{Format(tick.SessionOvertime)}",
                    SectionIndex = si,
                    ShouldPlaySound = _settings.EnableSoundAlerts,
                    ShouldShowNotification = _settings.EnableWindowsNotifications
                });
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void Fire(AlertEventArgs args) => AlertRaised?.Invoke(this, args);

    /// <summary>
    /// Atomically checks whether <paramref name="type"/> has already fired for
    /// <paramref name="bucketKey"/> and, if not, records it and returns <c>true</c>.
    /// Returns <c>false</c> if already fired (dedup suppression).
    /// </summary>
    private bool TryMarkFired(int bucketKey, AlertType type)
    {
        lock (_lock)
        {
            if (!_fired.TryGetValue(bucketKey, out var set))
            {
                set = new HashSet<AlertType>();
                _fired[bucketKey] = set;
            }
            return set.Add(type); // Add returns false if already present.
        }
    }

    private static TimeSpan ParseThreshold(string raw, TimeSpan fallback)
        => TimeSpan.TryParse(raw, out var ts) ? ts : fallback;

    private static string Format(TimeSpan ts)
        => ts.ToString(ts.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss");

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose() => Detach();
}
