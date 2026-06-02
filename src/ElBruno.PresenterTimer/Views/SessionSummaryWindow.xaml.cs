using System.Windows;

namespace ElBruno.PresenterTimer.Views;

/// <summary>
/// Session Summary window (PRD §7.14).
/// All display and command logic lives in <see cref="ViewModels.SessionSummaryViewModel"/>.
/// This code-behind only initialises the component and wires the VM's close event.
/// </summary>
public partial class SessionSummaryWindow : Window
{
    public SessionSummaryWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Called by App (or a test host) after construction to bind the VM
    /// and subscribe to its <see cref="ViewModels.SessionSummaryViewModel.RequestClose"/> event.
    /// </summary>
    public void SetViewModel(ViewModels.SessionSummaryViewModel vm)
    {
        DataContext = vm;
        vm.RequestClose += Close;
    }
}
