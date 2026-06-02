namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// Lightweight display model for a single row in the Session Preview section list.
/// No change-notification needed — rows are replaced wholesale when the plan changes.
/// </summary>
public sealed class SectionRowViewModel
{
    public int     Index           { get; init; }
    public string  Title          { get; init; } = string.Empty;
    public string  DurationDisplay { get; init; } = string.Empty;
    public string? Notes           { get; init; }
}
