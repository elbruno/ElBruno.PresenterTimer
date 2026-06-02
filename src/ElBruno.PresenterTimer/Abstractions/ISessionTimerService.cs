using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Drives a <see cref="SessionPlan"/> through time, tracking per-section and whole-session
/// elapsed/remaining using a monotonic time source (PRD §10.1, §7.9–§7.11).
/// Framework-agnostic: no WPF dependency. Subscribe to events for UI-layer updates.
/// </summary>
public interface ISessionTimerService : IDisposable
{
    // ── Plan ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a <see cref="SessionPlan"/> and resets all timer state.
    /// Must be called before <see cref="Start"/>.
    /// </summary>
    void LoadPlan(SessionPlan plan);

    /// <summary>The currently loaded plan, or <c>null</c> if none has been loaded.</summary>
    SessionPlan? Plan { get; }

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>True while the timer is running (including when paused).</summary>
    bool IsRunning { get; }

    /// <summary>True when the timer has been paused via <see cref="Pause"/>.</summary>
    bool IsPaused { get; }

    /// <summary>
    /// True after the final section has ended (auto-advance) or the user has
    /// manually advanced past the last section.
    /// </summary>
    bool IsSessionComplete { get; }

    /// <summary>Zero-based index of the currently active section. -1 when no plan is loaded.</summary>
    int CurrentSectionIndex { get; }

    /// <summary>
    /// The currently active <see cref="SessionSection"/>, or <c>null</c> when the session
    /// is complete or no plan is loaded.
    /// </summary>
    SessionSection? CurrentSection { get; }

    // ── Timing (all values computed from monotonic source) ────────────────────

    /// <summary>Sum of all section planned durations from the original plan (no extensions).</summary>
    TimeSpan TotalPlannedDuration { get; }

    /// <summary>Monotonic elapsed time for the whole session.</summary>
    TimeSpan SessionElapsed { get; }

    /// <summary>
    /// Time remaining before the planned session total is reached.
    /// Returns <see cref="TimeSpan.Zero"/> when the session is in overtime.
    /// </summary>
    TimeSpan SessionRemaining { get; }

    /// <summary>
    /// How far the session has exceeded its planned total duration.
    /// Returns <see cref="TimeSpan.Zero"/> when not in overtime (PRD §7.10).
    /// </summary>
    TimeSpan SessionOvertime { get; }

    /// <summary>Elapsed time since the current section started (or was last restarted).</summary>
    TimeSpan CurrentSectionElapsed { get; }

    /// <summary>
    /// Time remaining in the current section's effective duration (planned + extensions).
    /// Returns <see cref="TimeSpan.Zero"/> when in section overtime and auto-advance is OFF.
    /// </summary>
    TimeSpan CurrentSectionRemaining { get; }

    /// <summary>
    /// How far the current section has exceeded its effective duration.
    /// Returns <see cref="TimeSpan.Zero"/> when not overtime, or when auto-advance is ON
    /// (section advances before overtime can accumulate).
    /// </summary>
    TimeSpan CurrentSectionOvertime { get; }

    /// <summary>
    /// How far behind the original plan the session currently is (PRD §7.10).
    /// Compares <see cref="SessionElapsed"/> against the planned time expected at this point
    /// (sum of previous planned durations + min(currentElapsed, currentPlanned)).
    /// Returns <see cref="TimeSpan.Zero"/> when on schedule or ahead.
    /// </summary>
    TimeSpan BehindSchedule { get; }

    // ── Settings ──────────────────────────────────────────────────────────────

    /// <summary>
    /// When <c>true</c>, the timer automatically advances to the next section when the
    /// current section reaches its planned duration (PRD §7.11). Default: <c>false</c>.
    /// </summary>
    bool AutoAdvanceSections { get; set; }

    // ── Controls (PRD §7.9) ───────────────────────────────────────────────────

    /// <summary>Starts the timer from the beginning of the first section.</summary>
    void Start();

    /// <summary>Pauses a running timer, preserving elapsed time.</summary>
    void Pause();

    /// <summary>Resumes a paused timer from the exact point it was paused.</summary>
    void Resume();

    /// <summary>Stops the timer and resets all state to the beginning of the plan.</summary>
    void Reset();

    /// <summary>Advances to the next section. Records elapsed time for the current section.</summary>
    void NextSection();

    /// <summary>Returns to the previous section. The current section's elapsed is recorded.</summary>
    void PreviousSection();

    /// <summary>Restarts the current section from zero without changing the section index.</summary>
    void RestartCurrentSection();

    /// <summary>
    /// Adds <paramref name="extension"/> to the current section's effective duration
    /// (e.g., pass <c>TimeSpan.FromMinutes(1)</c> for a +1 min extension).
    /// </summary>
    void ExtendCurrentSection(TimeSpan extension);

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised approximately every 250 ms while the timer is running.
    /// All display values in the payload are recomputed from the monotonic source — never
    /// accumulated from tick deltas (PRD §10.1). Raised on a thread-pool thread;
    /// UI consumers must marshal to their dispatcher.
    /// </summary>
    event EventHandler<TimerTickEventArgs>? Tick;

    /// <summary>
    /// Raised whenever the active section changes (manual navigation, auto-advance, or reset).
    /// </summary>
    event EventHandler<SectionChangedEventArgs>? SectionChanged;

    /// <summary>
    /// Raised when <see cref="IsRunning"/>, <see cref="IsPaused"/>, or
    /// <see cref="IsSessionComplete"/> changes. Raised on a thread-pool thread.
    /// </summary>
    event EventHandler? StateChanged;

    // ── Summary data (PRD §7.14) ──────────────────────────────────────────────

    /// <summary>
    /// Returns a snapshot of session results at the moment of the call:
    /// planned vs actual per section, extensions applied, skip flags.
    /// Safe to call at any time, including mid-session.
    /// Phase 9 uses this to build the Session Summary view.
    /// </summary>
    SessionResult GetResult();
}
