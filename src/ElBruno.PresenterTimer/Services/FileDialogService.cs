using System.Windows.Forms;
using ElBruno.PresenterTimer.Abstractions;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Implements <see cref="IFileDialogService"/> using WinForms <see cref="OpenFileDialog"/>
/// and <see cref="SaveFileDialog"/>. Safe to call on the STA/WPF UI thread because
/// <c>UseWindowsForms=true</c> is set in the project (PRD §8.2).
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    private const string JsonFilter     = "JSON files (*.json)|*.json|All files (*.*)|*.*";
    private const string MarkdownFilter = "Markdown files (*.md)|*.md|All files (*.*)|*.*";

    /// <inheritdoc/>
    public string? ShowOpenJsonDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Title           = "Import Session JSON",
            Filter          = JsonFilter,
            CheckFileExists = true,
            Multiselect     = false,
        };

        return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
    }

    /// <inheritdoc/>
    public string? ShowSaveJsonDialog(string? defaultFileName = null)
    {
        using var dlg = new SaveFileDialog
        {
            Title       = "Export JSON",
            Filter      = JsonFilter,
            DefaultExt  = "json",
            FileName    = defaultFileName ?? "session.json",
        };

        return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
    }

    /// <inheritdoc/>
    public string? ShowSaveMarkdownDialog(string? defaultFileName = null)
    {
        using var dlg = new SaveFileDialog
        {
            Title      = "Save Summary as Markdown",
            Filter     = MarkdownFilter,
            DefaultExt = "md",
            FileName   = defaultFileName ?? "session-summary.md",
        };

        return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
    }
}
