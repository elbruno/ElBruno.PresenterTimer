using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace ElBruno.PresenterTimer.Views;

/// <summary>
/// About window showing app name, version, description, and project link (PRD §7.1).
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly()
            .GetName()
            .Version;

        VersionText.Text = version is not null
            ? $"Version {version.Major}.{version.Minor}.{version.Build}"
            : "Version 1.0.0";
    }

    private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // Non-critical; swallow silently
        }
        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();
}
