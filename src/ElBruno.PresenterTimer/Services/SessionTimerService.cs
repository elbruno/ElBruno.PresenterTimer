using System.Diagnostics;
using System.Timers;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Drives a <see cref="SessionPlan"/> through time and implements all manual controls
/// described in PRD §7.9–§7.11 and §10.1.
///
/// <para><b>Monotonic timing (PRD §10.1):</b> a single <see cref="Stopwatch"/> acts as
/// the monotonic source.  <see cref="_sessionElapsedBase"/> accumulates elapsed time across
/// pause/resume cycles.  All display values — section elapsed, session elapsed, remaining,
/// overtime, behind-schedule — are computed from this base at read time.  UI ticks
/// (~250 ms via <see cref="System.Timers.Timer"/>) only trigger recompute and event
/// dispatch; they do <em>not</em> accumulate time themselves.</para>
///
/// <para><b>Threading:</b> all state mutations are guarded by <c>_sync</c>.
/// Events are raised outside the lock on the calling/timer-pool thread.
/// WPF consumers must marshal via <c>Dispatcher</c>.</para>
/// </summary>
public sealed class SessionTimerService : ISessionTimerService
{
    // ── Synchronisation ───────────────────────────────────────────────────────
    private readonly object _sync = new();

    // ── Monotonic clock ───────────────────────────────────────────────────────
    // _clock runs only while the timer is active (not paused, not stopped).
    // _sessionElapsedBase is the accumulated elapsed from all prior run intervals.
    private readonly Stopwatch _clock = new();
    private TimeSpan _sessionElapsedBase;

    // SessionElapsed at the moment the current section started (or was restarted).
    private TimeSpan _sessionElapsedAtSectionStart;

    // ── Plan ──────────────────────────────────────────────────────────────────
    private SessionPlan? _plan;
    private TimeSpan _totalPlannedDuration;

    // ── Section state ─────────────────────────────────────────────────────────
    // -1 until a plan is loaded (see CurrentSectionIndex contract in ISessionTimerService).
    private int _currentSectionIndex = -1;
    private TimeSpan _currentSectionExtension;

    // ── Flags ─────────────────────────────────────────────────────────────────
    private bool _isRunning;
    private bool _isPaused;
    private bool _isSessionComplete;

    // ── Per-section accumulators (for summary) ────────────────────────────────
    private SectionAccumulator[]? _accumulators;

    // ── Internal tick timer ───────────────────────────────────────────────────
    private readonly System.Timers.Timer _ticker;

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<TimerTickEventArgs>? Tick;
    public event EventHandler<SectionChangedEventArgs>? SectionChanged;
    public event EventHandler? StateChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="autoAdvanceSections">
    /// Initial value for <see cref="AutoAdvanceSections"/> (PRD §7.11). Default: <c>false</c>.
    /// </param>
    public SessionTimerService(bool autoAdvanceSections = false)
    {
        AutoAdvanceSections = autoAdvanceSections;
        _ticker = new System.Timers.Timer(250) { AutoReset = true };
        _ticker.Elapsed += OnTick;
    }

    // ── ISessionTimerService — Properties ─────────────────────────────────────

    public SessionPlan? Plan { get { lock (_sync) return _plan; } }
    public bool IsRunning { get { lock (_sync) return _isRunning; } }
    public bool IsPaused { get { lock (_sync) return _isPaused; } }
    public bool IsSessionComplete { get { lock (_sync) return _isSessionComplete; } }
    public int CurrentSectionIndex { get { lock (_sync) return _currentSectionIndex; } }
    public bool AutoAdvanceSections { get; set; }

    public SessionSection? CurrentSection
    {
        get
        {
            lock (_sync)
            {
                if (_plan is null || _isSessionComplete
                    || _currentSectionIndex < 0 || _currentSectionIndex >= _plan.Sections.Count)
                    return null;
                return _plan.Sections[_currentSectionIndex];
            }
        }
    }

    public TimeSpan TotalPlannedDuration { get { lock (_sync) return _totalPlannedDuration; } }

    public TimeSpan SessionElapsed
    {
        get { lock (_sync) return ComputeSessionElapsed(); }
    }

    public TimeSpan SessionRemaining
    {
        get
        {
            lock (_sync)
            {
                var r = _totalPlannedDuration - ComputeSessionElapsed();
                return r > TimeSpan.Zero ? r : TimeSpan.Zero;
            }
        }
    }

    public TimeSpan SessionOvertime
    {
        get
        {
            lock (_sync)
            {
                var o = ComputeSessionElapsed() - _totalPlannedDuration;
                return o > TimeSpan.Zero ? o : TimeSpan.Zero;
            }
        }
    }

    public TimeSpan CurrentSectionElapsed
    {
        get { lock (_sync) return ComputeSectionElapsed(); }
    }

    public TimeSpan CurrentSectionRemaining
    {
        get
        {
            lock (_sync)
            {
                if (_plan is null || _isSessionComplete) return TimeSpan.Zero;
                var effective = _plan.Sections[_currentSectionIndex].Duration + _currentSectionExtension;
                var r = effective - ComputeSectionElapsed();
                return r > TimeSpan.Zero ? r : TimeSpan.Zero;
            }
        }
    }

    public TimeSpan CurrentSectionOvertime
    {
        get
        {
            lock (_sync)
            {
                if (_plan is null || _isSessionComplete) return TimeSpan.Zero;
                var effective = _plan.Sections[_currentSectionIndex].Duration + _currentSectionExtension;
                var o = ComputeSectionElapsed() - effective;
                return o > TimeSpan.Zero ? o : TimeSpan.Zero;
            }
        }
    }

    public TimeSpan BehindSchedule
    {
        get { lock (_sync) return ComputeBehindSchedule(); }
    }

    // ── ISessionTimerService — Controls ───────────────────────────────────────

    public void LoadPlan(SessionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (_sync)
        {
            SnapshotAndStopClock();
            _plan = plan;
            _totalPlannedDuration = plan.Sections.Aggregate(TimeSpan.Zero, (s, sec) => s + sec.Duration);
            ResetState();
            InitAccumulators();
        }
        _ticker.Stop();
        RaiseStateChanged();
    }

    public void Start()
    {
        bool started;
        lock (_sync)
        {
            if (_plan is null || _plan.Sections.Count == 0 || _isRunning)
            {
                started = false;
            }
            else
            {
                _isRunning = true;
                _isPaused = false;
                _isSessionComplete = false;
                _sessionElapsedBase = TimeSpan.Zero;
                _sessionElapsedAtSectionStart = TimeSpan.Zero;
                _currentSectionExtension = TimeSpan.Zero;
                _clock.Restart();
                MarkSectionEntered(_currentSectionIndex);
                started = true;
            }
        }
        if (started)
        {
            _ticker.Start();
            RaiseStateChanged();
        }
    }

    public void Pause()
    {
        bool paused;
        lock (_sync)
        {
            if (!_isRunning || _isPaused)
            {
                paused = false;
            }
            else
            {
                _sessionElapsedBase += _clock.Elapsed;
                _clock.Stop();
                _isPaused = true;
                paused = true;
            }
        }
        if (paused)
        {
            _ticker.Stop();
            RaiseStateChanged();
        }
    }

    public void Resume()
    {
        bool resumed;
        lock (_sync)
        {
            if (!_isRunning || !_isPaused)
            {
                resumed = false;
            }
            else
            {
                _isPaused = false;
                _clock.Restart();
                resumed = true;
            }
        }
        if (resumed)
        {
            _ticker.Start();
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Stops the current session and resets all timing state to initial values.
    /// Unlike <see cref="Pause"/>, this clears all progress but preserves the loaded plan.
    /// After Stop, the next <see cref="Start"/> will begin from section 0 with elapsed = 0.
    /// 
    /// Must be called only when a plan is loaded (otherwise does nothing).
    /// </summary>
    public void Stop()
    {
        bool stopped;
        lock (_sync)
        {
            // Only stop if a session is active (running, paused, or complete)
            if (_plan is null || (!_isRunning && !_isPaused && !_isSessionComplete))
            {
                stopped = false;
            }
            else
            {
                SnapshotAndStopClock();
                ResetState();
                if (_plan is not null) InitAccumulators();
                stopped = true;
            }
        }
        if (stopped)
        {
            _ticker.Stop();
            RaiseStateChanged();
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            SnapshotAndStopClock();
            ResetState();
            if (_plan is not null) InitAccumulators();
        }
        _ticker.Stop();
        RaiseStateChanged();
        RaiseSectionChanged(-1, 0, SectionChangeReason.Reset);
    }

    public void NextSection()
    {
        int prevIndex, newIndex;
        bool complete;

        lock (_sync)
        {
            if (_plan is null || !_isRunning || _isSessionComplete) return;

            prevIndex = _currentSectionIndex;
            FinalizeSection(prevIndex, earlyExit: true);

            if (_currentSectionIndex >= _plan.Sections.Count - 1)
            {
                _isSessionComplete = true;
                SnapshotAndStopClock();
                complete = true;
                newIndex = _currentSectionIndex;
            }
            else
            {
                _currentSectionIndex++;
                _sessionElapsedAtSectionStart = ComputeSessionElapsed();
                _currentSectionExtension = TimeSpan.Zero;
                newIndex = _currentSectionIndex;
                MarkSectionEntered(newIndex);
                complete = false;
            }
        }

        if (complete)
        {
            _ticker.Stop();
            RaiseStateChanged();
        }
        else
        {
            RaiseSectionChanged(prevIndex, newIndex, SectionChangeReason.ManualNext);
        }
    }

    public void PreviousSection()
    {
        int prevIndex, newIndex;

        lock (_sync)
        {
            if (_plan is null || !_isRunning || _isSessionComplete || _currentSectionIndex <= 0) return;

            prevIndex = _currentSectionIndex;
            FinalizeSection(prevIndex, earlyExit: true);

            _currentSectionIndex--;
            _sessionElapsedAtSectionStart = ComputeSessionElapsed();
            _currentSectionExtension = TimeSpan.Zero;
            newIndex = _currentSectionIndex;
            MarkSectionEntered(newIndex);
        }

        RaiseSectionChanged(prevIndex, newIndex, SectionChangeReason.ManualPrevious);
    }

    public void RestartCurrentSection()
    {
        int idx;

        lock (_sync)
        {
            if (_plan is null || !_isRunning || _isSessionComplete) return;

            idx = _currentSectionIndex;
            FinalizeSection(idx, earlyExit: false);
            _accumulators![idx].RestartCount++;
            _sessionElapsedAtSectionStart = ComputeSessionElapsed();
            _currentSectionExtension = TimeSpan.Zero;
            MarkSectionEntered(idx);
        }

        RaiseSectionChanged(idx, idx, SectionChangeReason.ManualRestart);
    }

    public void ExtendCurrentSection(TimeSpan extension)
    {
        lock (_sync)
        {
            if (_plan is null || !_isRunning || _isSessionComplete || extension <= TimeSpan.Zero) return;
            _currentSectionExtension += extension;
            _accumulators![_currentSectionIndex].TotalExtensions += extension;
        }
    }

    // ── ISessionTimerService — Summary ────────────────────────────────────────

    public SessionResult GetResult()
    {
        lock (_sync)
        {
            if (_plan is null)
            {
                return new SessionResult
                {
                    SessionTitle = string.Empty,
                    PlannedDuration = TimeSpan.Zero,
                    ActualDuration = TimeSpan.Zero,
                    TotalExtensions = TimeSpan.Zero,
                    Sections = []
                };
            }

            var sessionElapsed = ComputeSessionElapsed();
            var sections = new List<SectionResult>(_plan.Sections.Count);

            for (int i = 0; i < _plan.Sections.Count; i++)
            {
                var planSection = _plan.Sections[i];
                var acc = _accumulators![i];

                // For the currently active section, add live elapsed on top of any prior finalized time.
                var actual = acc.FinalizedElapsed;
                if (_isRunning && !_isSessionComplete && i == _currentSectionIndex)
                    actual += ComputeSectionElapsed();

                sections.Add(new SectionResult
                {
                    Index = i,
                    Title = planSection.Title,
                    PlannedDuration = planSection.Duration,
                    ActualDuration = actual,
                    TotalExtensions = acc.TotalExtensions,
                    RestartCount = acc.RestartCount,
                    WasVisited = acc.WasVisited || (_isRunning && !_isSessionComplete && i == _currentSectionIndex),
                    WasSkipped = acc.WasSkipped
                });
            }

            var totalExtensions = _accumulators!.Aggregate(TimeSpan.Zero, (s, a) => s + a.TotalExtensions);

            return new SessionResult
            {
                SessionTitle = _plan.Title,
                PlannedDuration = _totalPlannedDuration,
                ActualDuration = sessionElapsed,
                TotalExtensions = totalExtensions,
                Sections = sections
            };
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _ticker.Stop();
        _ticker.Elapsed -= OnTick;
        _ticker.Dispose();
        _clock.Stop();
    }

    // ── Private — monotonic helpers ───────────────────────────────────────────

    /// <summary>
    /// Computes the current session elapsed time from the monotonic source.
    /// Must be called under <c>_sync</c>.
    /// </summary>
    private TimeSpan ComputeSessionElapsed()
    {
        if (!_isRunning || _isPaused || _isSessionComplete)
            return _sessionElapsedBase;
        return _sessionElapsedBase + _clock.Elapsed;
    }

    /// <summary>
    /// Computes elapsed time within the current section.
    /// Must be called under <c>_sync</c>.
    /// </summary>
    private TimeSpan ComputeSectionElapsed()
    {
        if (!_isRunning || _isSessionComplete) return TimeSpan.Zero;
        return ComputeSessionElapsed() - _sessionElapsedAtSectionStart;
    }

    /// <summary>
    /// Computes how far behind the original plan the session is (PRD §7.10).
    /// Must be called under <c>_sync</c>.
    /// <para>Snapshots the monotonic elapsed exactly once to avoid phantom drift from
    /// double-reading <see cref="_clock"/> (bug fix: Lambert, Phase 8).</para>
    /// </summary>
    private TimeSpan ComputeBehindSchedule()
    {
        if (_plan is null || !_isRunning) return TimeSpan.Zero;

        // Single snapshot prevents two separate reads of _clock.Elapsed from diverging.
        var sessionElapsed = ComputeSessionElapsed();
        // Derive section elapsed from the same snapshot — no second clock read.
        var sectionElapsed = _isSessionComplete
            ? TimeSpan.Zero
            : sessionElapsed - _sessionElapsedAtSectionStart;

        // Planned time for all completed sections.
        var plannedPrevious = TimeSpan.Zero;
        for (int i = 0; i < _currentSectionIndex && i < _plan.Sections.Count; i++)
            plannedPrevious += _plan.Sections[i].Duration;

        // Add the minimum of actual and planned for the current section
        // (extensions don't count — they represent deliberate extra time).
        var currentPlanned = _isSessionComplete
            ? _totalPlannedDuration - plannedPrevious
            : _plan.Sections[_currentSectionIndex].Duration;

        var currentCapped = sectionElapsed < currentPlanned ? sectionElapsed : currentPlanned;

        var behind = sessionElapsed - (plannedPrevious + currentCapped);
        return behind > TimeSpan.Zero ? behind : TimeSpan.Zero;
    }

    // ── Private — state helpers ───────────────────────────────────────────────

    /// <summary>
    /// Snapshots remaining clock time into <see cref="_sessionElapsedBase"/> and stops the clock.
    /// Must be called under <c>_sync</c>.
    /// </summary>
    private void SnapshotAndStopClock()
    {
        if (_clock.IsRunning)
        {
            _sessionElapsedBase += _clock.Elapsed;
            _clock.Stop();
        }
    }

    private void ResetState()
    {
        _sessionElapsedBase = TimeSpan.Zero;
        _sessionElapsedAtSectionStart = TimeSpan.Zero;
        _currentSectionExtension = TimeSpan.Zero;
        // -1 when no plan is present; 0 when resetting to the start of a loaded plan.
        _currentSectionIndex = _plan is not null ? 0 : -1;
        _isRunning = false;
        _isPaused = false;
        _isSessionComplete = false;
    }

    private void InitAccumulators()
    {
        var count = _plan!.Sections.Count;
        _accumulators = new SectionAccumulator[count];
        for (int i = 0; i < count; i++)
            _accumulators[i] = new SectionAccumulator();
    }

    private void MarkSectionEntered(int index)
    {
        if (_accumulators is not null && index < _accumulators.Length)
            _accumulators[index].WasVisited = true;
    }

    /// <summary>
    /// Records the elapsed time for <paramref name="index"/> into its accumulator,
    /// then determines whether the section should be flagged as skipped (left early).
    /// Must be called under <c>_sync</c> before changing <see cref="_currentSectionIndex"/>.
    /// </summary>
    private void FinalizeSection(int index, bool earlyExit)
    {
        if (_accumulators is null || _plan is null || index >= _accumulators.Length) return;

        var elapsed = ComputeSectionElapsed();
        _accumulators[index].FinalizedElapsed += elapsed;

        if (earlyExit)
        {
            var effective = _plan.Sections[index].Duration + _currentSectionExtension;
            if (elapsed < effective)
                _accumulators[index].WasSkipped = true;
        }
    }

    // ── Private — tick handler ────────────────────────────────────────────────

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        TimerTickEventArgs? tickArgs;
        bool triggerAutoAdvance;
        int sectionIndexSnapshot;

        lock (_sync)
        {
            if (!_isRunning || _isPaused || _plan is null || _isSessionComplete) return;

            var sessionElapsed = ComputeSessionElapsed();
            var sectionElapsed = ComputeSectionElapsed();
            var effective = _plan.Sections[_currentSectionIndex].Duration + _currentSectionExtension;

            var sessionRemaining = _totalPlannedDuration - sessionElapsed;
            var sessionOvertime = sessionElapsed - _totalPlannedDuration;
            var sectionRemaining = effective - sectionElapsed;
            var sectionOvertime = sectionElapsed - effective;

            tickArgs = new TimerTickEventArgs
            {
                CurrentSectionIndex = _currentSectionIndex,
                TotalPlannedDuration = _totalPlannedDuration,
                SessionElapsed = sessionElapsed,
                SessionRemaining = sessionRemaining > TimeSpan.Zero ? sessionRemaining : TimeSpan.Zero,
                SessionOvertime = sessionOvertime > TimeSpan.Zero ? sessionOvertime : TimeSpan.Zero,
                IsSessionOvertime = sessionOvertime > TimeSpan.Zero,
                CurrentSectionElapsed = sectionElapsed,
                CurrentSectionRemaining = sectionRemaining > TimeSpan.Zero ? sectionRemaining : TimeSpan.Zero,
                CurrentSectionOvertime = sectionOvertime > TimeSpan.Zero ? sectionOvertime : TimeSpan.Zero,
                IsSectionOvertime = sectionOvertime > TimeSpan.Zero,
                BehindSchedule = ComputeBehindSchedule()
            };

            triggerAutoAdvance = AutoAdvanceSections && sectionElapsed >= effective;
            sectionIndexSnapshot = _currentSectionIndex;
        }

        // Raise Tick outside the lock so subscribers don't deadlock.
        Tick?.Invoke(this, tickArgs);

        if (!triggerAutoAdvance) return;

        // Auto-advance: re-acquire lock and verify we are still on the same section.
        int prevIndex, newIndex;
        bool complete;

        lock (_sync)
        {
            // Guard against a concurrent manual advance that already changed the section.
            if (_isSessionComplete || _isPaused || _currentSectionIndex != sectionIndexSnapshot) return;

            // Recheck threshold (extensions may have been applied between lock releases).
            var effective = _plan!.Sections[_currentSectionIndex].Duration + _currentSectionExtension;
            if (ComputeSectionElapsed() < effective) return;

            prevIndex = _currentSectionIndex;
            FinalizeSection(prevIndex, earlyExit: false);

            if (_currentSectionIndex >= _plan.Sections.Count - 1)
            {
                _isSessionComplete = true;
                SnapshotAndStopClock();
                complete = true;
                newIndex = _currentSectionIndex;
            }
            else
            {
                _currentSectionIndex++;
                _sessionElapsedAtSectionStart = ComputeSessionElapsed();
                _currentSectionExtension = TimeSpan.Zero;
                newIndex = _currentSectionIndex;
                MarkSectionEntered(newIndex);
                complete = false;
            }
        }

        if (complete)
        {
            _ticker.Stop();
            RaiseStateChanged();
        }
        else
        {
            RaiseSectionChanged(prevIndex, newIndex, SectionChangeReason.AutoAdvance);
        }
    }

    // ── Private — event raisers ───────────────────────────────────────────────

    private void RaiseStateChanged() =>
        StateChanged?.Invoke(this, EventArgs.Empty);

    private void RaiseSectionChanged(int prev, int curr, SectionChangeReason reason) =>
        SectionChanged?.Invoke(this, new SectionChangedEventArgs
        {
            PreviousSectionIndex = prev,
            CurrentSectionIndex = curr,
            Reason = reason
        });

    // ── Private — inner types ─────────────────────────────────────────────────

    /// <summary>
    /// Mutable per-section state accumulated throughout the session.
    /// Lives entirely inside <see cref="SessionTimerService"/>; not exposed publicly.
    /// </summary>
    private sealed class SectionAccumulator
    {
        /// <summary>
        /// Sum of all elapsed snapshots taken when the section was finalized
        /// (i.e., when the user left it or it was restarted). Does NOT include
        /// the currently live elapsed for the active section — that is added at
        /// read time in <see cref="GetResult"/>.
        /// </summary>
        public TimeSpan FinalizedElapsed { get; set; }

        /// <summary>Total time added via ExtendCurrentSection while in this section.</summary>
        public TimeSpan TotalExtensions { get; set; }

        /// <summary>True if this section was ever the active section.</summary>
        public bool WasVisited { get; set; }

        /// <summary>True if the section was left before its effective duration completed.</summary>
        public bool WasSkipped { get; set; }

        /// <summary>Number of explicit RestartCurrentSection calls while in this section.</summary>
        public int RestartCount { get; set; }
    }
}
