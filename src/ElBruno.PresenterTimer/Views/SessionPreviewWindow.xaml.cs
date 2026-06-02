using System.Windows;

namespace ElBruno.PresenterTimer.Views;

/// <summary>
/// Session Preview window (PRD §7.5).
/// All display logic lives in <see cref="ViewModels.SessionPreviewViewModel"/>.
/// This code-behind is intentionally empty — it only calls <c>InitializeComponent()</c>.
/// </summary>
public partial class SessionPreviewWindow : Window
{
    public SessionPreviewWindow()
    {
        InitializeComponent();
    }
}
