namespace ElBruno.PresenterTimer.Models;

/// <summary>
/// Per-section data captured during a session run.
/// Used to build the Session Summary (PRD §7.14). Populated by
/// <see cref="Abstractions.ISessionTimerService.GetResult"/>.
/// </summary>
public sealed class SectionResult
{
    /// <summary>Zero-based index in the original <see cref="SessionPlan.Sections"/> list.</summary>
    public int Index { get; init; }

    /// <summary>Section title from the plan.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Planned duration from the original plan (no extensions applied).</summary>
    public TimeSpan PlannedDuration { get; init; }

    /// <summary>
    /// Actual time spent in this section (sum across all visits,
    /// including any time added via <c>ExtendCurrentSection</c>).
    /// </summary>
    public TimeSpan ActualDuration { get; init; }

    /// <summary>Total time added to this section via <c>ExtendCurrentSection</c> calls.</summary>
    public TimeSpan TotalExtensions { get; init; }

    /// <summary>Number of explicit <c>RestartCurrentSection</c> calls while this section was active.</summary>
    public int RestartCount { get; init; }

    /// <summary>
    /// True when the user advanced past this section before its effective duration completed
    /// (i.e., ActualDuration &lt; PlannedDuration + TotalExtensions).
    /// </summary>
    public bool WasSkipped { get; init; }

    /// <summary>True if this section was the current section at least once during the session.</summary>
    public bool WasVisited { get; init; }

    /// <summary>
    /// Difference between actual and planned duration (positive = over plan, negative = under).
    /// Extensions are not factored out here; use <see cref="TotalExtensions"/> to separate them.
    /// </summary>
    public TimeSpan Difference => ActualDuration - PlannedDuration;

    /// <summary>True when actual duration exceeded the original planned duration (before extensions).</summary>
    public bool WasOvertime => ActualDuration > PlannedDuration;
}
