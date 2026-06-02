namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// Lightweight display model for one row in the Session Summary section table.
/// No change-notification needed — the collection is built once from <see cref="SessionSummaryViewModel"/>.
/// </summary>
public sealed class SectionSummaryRowViewModel
{
    /// <summary>1-based display number.</summary>
    public int Number { get; init; }

    public string Title { get; init; } = string.Empty;

    public string PlannedDisplay { get; init; } = string.Empty;

    /// <summary>Formatted actual duration, or "—" if the section was not reached.</summary>
    public string ActualDisplay { get; init; } = string.Empty;

    /// <summary>Formatted signed difference (e.g. "+00:20"), or "—" if not reached.</summary>
    public string DifferenceDisplay { get; init; } = string.Empty;

    /// <summary>"OVERTIME" when the section ran over its planned duration; empty otherwise.</summary>
    public string OvertimeText { get; init; } = string.Empty;

    /// <summary>Formatted total extensions for this section, or empty if none were applied.</summary>
    public string ExtensionsDisplay { get; init; } = string.Empty;

    /// <summary>"Skipped" when the user advanced before the section completed; empty otherwise.</summary>
    public string SkippedText { get; init; } = string.Empty;

    /// <summary>Used by XAML DataTriggers to dim rows that were never reached.</summary>
    public bool WasVisited { get; init; }

    /// <summary>Drives red foreground for overtime rows.</summary>
    public bool WasOvertime { get; init; }

    /// <summary>Drives positive-difference green foreground.</summary>
    public bool DifferenceIsNegative { get; init; }
}
