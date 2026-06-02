namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>Describes why the active section changed.</summary>
public enum SectionChangeReason
{
    /// <summary>User explicitly called <c>NextSection</c>.</summary>
    ManualNext,

    /// <summary>User explicitly called <c>PreviousSection</c>.</summary>
    ManualPrevious,

    /// <summary>User explicitly called <c>RestartCurrentSection</c>.</summary>
    ManualRestart,

    /// <summary>Auto-advance fired when the section reached its planned duration (PRD §7.11).</summary>
    AutoAdvance,

    /// <summary>Timer was reset to the beginning.</summary>
    Reset
}

/// <summary>
/// Payload for <see cref="ISessionTimerService.SectionChanged"/> events.
/// </summary>
public sealed class SectionChangedEventArgs : EventArgs
{
    /// <summary>Zero-based index of the section that was active before the change.</summary>
    public int PreviousSectionIndex { get; init; }

    /// <summary>Zero-based index of the section that is now active.</summary>
    public int CurrentSectionIndex { get; init; }

    /// <summary>What caused the section change.</summary>
    public SectionChangeReason Reason { get; init; }
}
