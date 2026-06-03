using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// Editable section row used by <see cref="SessionPlanEditorViewModel"/>.
/// </summary>
public sealed class SessionPlanEditorSectionViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _durationText = "00:05:00";
    private string _notes = string.Empty;
    private string _color = string.Empty;
    private string _warningAtText = string.Empty;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string DurationText
    {
        get => _durationText;
        set => SetProperty(ref _durationText, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public string WarningAtText
    {
        get => _warningAtText;
        set => SetProperty(ref _warningAtText, value);
    }

    public bool TryBuildSection(int sectionNumber, out SessionSection section, out string? error)
    {
        section = new SessionSection();
        error = null;

        if (!TimeSpan.TryParseExact(DurationText.Trim(), @"hh\:mm\:ss", null, out var duration))
        {
            error = $"Section {sectionNumber} has an invalid duration \"{DurationText}\". Use HH:mm:ss format.";
            return false;
        }

        TimeSpan? warningAt = null;
        if (!string.IsNullOrWhiteSpace(WarningAtText))
        {
            if (!TimeSpan.TryParseExact(WarningAtText.Trim(), @"hh\:mm\:ss", null, out var warning))
            {
                error = $"Section {sectionNumber} has an invalid warning time \"{WarningAtText}\". Use HH:mm:ss format.";
                return false;
            }

            warningAt = warning;
        }

        section = new SessionSection
        {
            Title = Title.Trim(),
            Duration = duration,
            Notes = NormalizeNull(Notes),
            Color = NormalizeNull(Color),
            WarningAt = warningAt,
        };
        return true;
    }

    public static SessionPlanEditorSectionViewModel FromSection(SessionSection section)
    {
        return new SessionPlanEditorSectionViewModel
        {
            Title = section.Title,
            DurationText = section.Duration.ToString(@"hh\:mm\:ss"),
            Notes = section.Notes ?? string.Empty,
            Color = section.Color ?? string.Empty,
            WarningAtText = section.WarningAt?.ToString(@"hh\:mm\:ss") ?? string.Empty,
        };
    }

    private static string? NormalizeNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
