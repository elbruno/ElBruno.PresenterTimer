using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ElBruno.PresenterTimer.ViewModels;

namespace ElBruno.PresenterTimer.Views;

/// <summary>
/// Mini overlay window showing current section and time information in a compact format.
/// All display logic lives in <see cref="MiniOverlayViewModel"/>; this code-behind handles:
/// <list type="bullet">
///   <item>Updating the progress bar width based on session progress.</item>
///   <item>Drag-to-move via <c>DragMove()</c>.</item>
///   <item>Persisting the window position and size via the ViewModel callback.</item>
///   <item>Running the pulse flash <see cref="Storyboard"/> when <see cref="MiniOverlayViewModel.PulseRequested"/> fires.</item>
/// </list>
/// </summary>
public partial class MiniOverlayWindow : Window
{
    private Storyboard? _pulseStoryboard;
    private bool _allowClose;

    public MiniOverlayWindow()
    {
        InitializeComponent();
        LocationChanged += OnLocationChanged;
        SizeChanged += OnSizeChanged;
        DataContextChanged += OnDataContextChanged;
        Closing += OnClosing;

        Loaded += (_, _) =>
        {
            _pulseStoryboard = (Storyboard?)TryFindResource("PulseStoryboard");
            UpdateProgressBar();
        };
    }

    // ── VM wiring ─────────────────────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MiniOverlayViewModel oldVm)
            oldVm.PulseRequested -= OnPulseRequested;

        if (e.NewValue is MiniOverlayViewModel newVm)
            newVm.PulseRequested += OnPulseRequested;
    }

    private void OnPulseRequested(object? sender, EventArgs e)
    {
        // Clone-then-begin so rapid sequential alerts each get a fresh animation run.
        _pulseStoryboard?.Begin(this, HandoffBehavior.SnapshotAndReplace);
    }

    // ── Progress bar width update ──────────────────────────────────────────────

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateProgressBar();
        UpdateResponsiveFonts();
    }

    private void UpdateResponsiveFonts()
    {
        // Scale fonts based on window width
        // Default width is 320; scale proportionally
        double scale = ActualWidth / 320.0;
        
        if (FindName("SectionTitleBlock") is TextBlock sectionTitle)
        {
            double fontSize = 20 * scale;
            sectionTitle.FontSize = Math.Max(12, Math.Min(28, fontSize));
        }
        
        if (FindName("SectionLabelBlock") is TextBlock sectionLabel)
        {
            double fontSize = 11 * scale;
            sectionLabel.FontSize = Math.Max(7, Math.Min(16, fontSize));
        }
        
        if (FindName("SectionTimeBlock") is TextBlock sectionTime)
        {
            double fontSize = 18 * scale;
            sectionTime.FontSize = Math.Max(12, Math.Min(24, fontSize));
        }
        
        if (FindName("SessionLabelBlock") is TextBlock sessionLabel)
        {
            double fontSize = 10 * scale;
            sessionLabel.FontSize = Math.Max(7, Math.Min(14, fontSize));
        }
        
        if (FindName("SessionTimeBlock") is TextBlock sessionTime)
        {
            double fontSize = 14 * scale;
            sessionTime.FontSize = Math.Max(10, Math.Min(18, fontSize));
        }
    }

    private void UpdateProgressBar()
    {
        if (DataContext is not MiniOverlayViewModel vm) return;
        if (FindName("ProgressFill") is not System.Windows.Shapes.Rectangle progressFill) return;

        // Calculate available width for progress bar (border width minus padding and border)
        // The progress bar border is in a grid with padding=12, borderThickness=1
        // Available width = ActualWidth - 2*padding - 2*borderThickness
        double availableWidth = ActualWidth - 24 - 2;
        if (availableWidth <= 0) availableWidth = 1;

        progressFill.Width = Math.Max(0, availableWidth * vm.SessionProgressFraction);
    }

    // Subscribe to progress changes
    private void OnDataContextChanged()
    {
        if (DataContext is MiniOverlayViewModel vm)
        {
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MiniOverlayViewModel.SessionProgressFraction))
                {
                    UpdateProgressBar();
                }
            };
        }
    }

    // ── Drag-to-move ──────────────────────────────────────────────────────────

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnPauseResumeButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MiniOverlayViewModel vm)
        {
            if (vm.IsPaused)
                vm.ResumeSession();
            else
                vm.PauseSession();
        }
    }

    private void OnRestartButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MiniOverlayViewModel vm)
            vm.RestartCurrentSection();
    }

    // ── Position persistence ──────────────────────────────────────────────────

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (DataContext is MiniOverlayViewModel vm)
            vm.SavePosition(Left, Top);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;
        Hide();
    }

    public void ClosePermanently()
    {
        _allowClose = true;
        Close();
    }
}
