namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>Visual display state for a single timeline section (PRD §7.6).</summary>
public enum SectionVisualState
{
    Upcoming,
    Current,
    Completed,
    Warning,
    Overtime
}

/// <summary>
/// ViewModel for one proportional segment inside the <c>TimelineOverlayWindow</c> timeline bar.
/// Width is computed from <see cref="DurationFraction"/> × available bar width and stored in
/// <see cref="PixelWidth"/> so the XAML ItemsControl can bind directly.
/// </summary>
public sealed class OverlaySectionViewModel : ViewModelBase
{
    private string             _title           = string.Empty;
    private SectionVisualState _state           = SectionVisualState.Upcoming;
    private double             _durationFraction;
    private double             _pixelWidth      = 80.0;
    private double             _progressFraction;
    private double             _progressWidth;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>Visual state used by DataTriggers in XAML to drive colors/borders.</summary>
    public SectionVisualState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    /// <summary>Fraction of the total session duration this section occupies (0–1).</summary>
    public double DurationFraction
    {
        get => _durationFraction;
        set => SetProperty(ref _durationFraction, value);
    }

    /// <summary>Computed pixel width = <see cref="DurationFraction"/> × available bar width.</summary>
    public double PixelWidth
    {
        get => _pixelWidth;
        set
        {
            if (SetProperty(ref _pixelWidth, value))
                RefreshProgressWidth();
        }
    }

    /// <summary>Elapsed-within-section fraction (0–1). Drives the moving progress marker.</summary>
    public double ProgressFraction
    {
        get => _progressFraction;
        set
        {
            if (SetProperty(ref _progressFraction, value))
                RefreshProgressWidth();
        }
    }

    /// <summary>
    /// Pixel width of the elapsed-progress fill inside the segment
    /// (<c>PixelWidth × ProgressFraction</c>). Bound directly from XAML; avoids a converter.
    /// </summary>
    public double ProgressWidth
    {
        get => _progressWidth;
        private set => SetProperty(ref _progressWidth, value);
    }

    private void RefreshProgressWidth()
        => ProgressWidth = _pixelWidth * _progressFraction;
}
