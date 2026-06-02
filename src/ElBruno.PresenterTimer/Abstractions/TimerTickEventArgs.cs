namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Payload for <see cref="ISessionTimerService.Tick"/> events (raised ~every 250 ms while running).
/// All timing values are recomputed from the monotonic source on each tick — never accumulated
/// from tick deltas (PRD §10.1).
/// </summary>
public sealed class TimerTickEventArgs : EventArgs
{
    /// <summary>Zero-based index of the currently active section.</summary>
    public int CurrentSectionIndex { get; init; }

    /// <summary>Sum of all planned section durations (no extensions).</summary>
    public TimeSpan TotalPlannedDuration { get; init; }

    // ── Session ───────────────────────────────────────────────────────────────

    /// <summary>Monotonic elapsed time for the whole session.</summary>
    public TimeSpan SessionElapsed { get; init; }

    /// <summary>Time remaining before the planned session total is reached. Zero when overtime.</summary>
    public TimeSpan SessionRemaining { get; init; }

    /// <summary>How far the session has exceeded its planned total. Zero when not overtime.</summary>
    public TimeSpan SessionOvertime { get; init; }

    /// <summary>True when <see cref="SessionOvertime"/> is positive.</summary>
    public bool IsSessionOvertime { get; init; }

    // ── Current section ───────────────────────────────────────────────────────

    /// <summary>Elapsed time in the current section since it started (or was last restarted).</summary>
    public TimeSpan CurrentSectionElapsed { get; init; }

    /// <summary>Time remaining in the current section's effective duration. Zero when overtime.</summary>
    public TimeSpan CurrentSectionRemaining { get; init; }

    /// <summary>How far the current section has exceeded its effective duration. Zero when not overtime.</summary>
    public TimeSpan CurrentSectionOvertime { get; init; }

    /// <summary>True when <see cref="CurrentSectionOvertime"/> is positive (auto-advance OFF).</summary>
    public bool IsSectionOvertime { get; init; }

    // ── Behind-schedule ───────────────────────────────────────────────────────

    /// <summary>
    /// How far behind the original plan the session currently is.
    /// Computed as <c>SessionElapsed − plannedTimeForThisPoint</c>, where
    /// <c>plannedTimeForThisPoint</c> = sum of previous sections' planned durations
    /// + min(CurrentSectionElapsed, currentSection.PlannedDuration).
    /// Zero when on schedule or ahead.
    /// </summary>
    public TimeSpan BehindSchedule { get; init; }
}
