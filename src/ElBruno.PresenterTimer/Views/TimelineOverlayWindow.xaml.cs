using System.Windows;
using System.Windows.Media.Animation;
using ElBruno.PresenterTimer.ViewModels;

namespace ElBruno.PresenterTimer.Views;

/// <summary>
/// Borderless, always-on-top, transparent overlay window showing the session timeline (PRD §7.6, §7.7).
/// All display logic lives in <see cref="TimelineOverlayViewModel"/>; this code-behind only handles:
/// <list type="bullet">
///   <item>Distributing proportional pixel widths to section segments on resize.</item>
///   <item>Drag-to-move via <c>DragMove()</c>.</item>
///   <item>Persisting the last window position via the ViewModel callback.</item>
///   <item>Running the pulse flash <see cref="Storyboard"/> when <see cref="TimelineOverlayViewModel.PulseRequested"/> fires.</item>
/// </list>
/// </summary>
public partial class TimelineOverlayWindow : Window
{
    private Storyboard? _pulseStoryboard;

    public TimelineOverlayWindow()
    {
        InitializeComponent();
        LocationChanged += OnLocationChanged;
        DataContextChanged += OnDataContextChanged;

        Loaded += (_, _) =>
        {
            _pulseStoryboard = (Storyboard?)TryFindResource("PulseStoryboard");
        };
    }

    // ── VM wiring ─────────────────────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TimelineOverlayViewModel oldVm)
            oldVm.PulseRequested -= OnPulseRequested;

        if (e.NewValue is TimelineOverlayViewModel newVm)
            newVm.PulseRequested += OnPulseRequested;
    }

    private void OnPulseRequested(object? sender, EventArgs e)
    {
        // Clone-then-begin so rapid sequential alerts each get a fresh animation run.
        _pulseStoryboard?.Begin(this, HandoffBehavior.SnapshotAndReplace);
    }

    // ── Proportional timeline widths ──────────────────────────────────────────

    private void OnTimelineBarLoaded(object sender, RoutedEventArgs e)
        => NotifyWidthChanged();

    private void OnTimelineBarSizeChanged(object sender, SizeChangedEventArgs e)
        => NotifyWidthChanged();

    private void NotifyWidthChanged()
    {
        if (DataContext is TimelineOverlayViewModel vm)
            vm.UpdateSectionWidths(TimelineBar.ActualWidth);
    }

    // ── Drag-to-move ──────────────────────────────────────────────────────────

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    // ── Position persistence (PRD §7.7 "remember last position") ─────────────

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (DataContext is TimelineOverlayViewModel vm)
            vm.SavePosition(Left, Top);
    }
}
