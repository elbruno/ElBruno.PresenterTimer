namespace ElBruno.PresenterTimer.Models;

/// <summary>
/// Captures the full outcome of a completed (or in-progress) session.
/// Used for the Session Summary (PRD §7.14).
/// Retrieve via <see cref="Abstractions.ISessionTimerService.GetResult"/> at any time.
/// </summary>
public sealed class SessionResult
{
    /// <summary>Title of the session plan.</summary>
    public string SessionTitle { get; init; } = string.Empty;

    /// <summary>
    /// Sum of all section planned durations from the original plan (no extensions).
    /// </summary>
    public TimeSpan PlannedDuration { get; init; }

    /// <summary>Actual total elapsed time when the result was captured.</summary>
    public TimeSpan ActualDuration { get; init; }

    /// <summary>Total extensions applied across all sections during the session.</summary>
    public TimeSpan TotalExtensions { get; init; }

    /// <summary>
    /// Difference between actual and planned total duration
    /// (positive = over plan, negative = under).
    /// </summary>
    public TimeSpan Difference => ActualDuration - PlannedDuration;

    /// <summary>
    /// Per-section results in the same order as <see cref="SessionPlan.Sections"/>.
    /// All sections are present; unvisited sections have <see cref="SectionResult.WasVisited"/> = false.
    /// </summary>
    public IReadOnlyList<SectionResult> Sections { get; init; } = [];
}
