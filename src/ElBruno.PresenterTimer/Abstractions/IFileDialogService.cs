namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Provides OS-level file open/save dialogs for importing and exporting session JSON files.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Shows an open-file dialog filtered to JSON files.
    /// </summary>
    /// <returns>The selected file path, or <c>null</c> if the user cancelled.</returns>
    string? ShowOpenJsonDialog();

    /// <summary>
    /// Shows a save-file dialog filtered to JSON files.
    /// </summary>
    /// <param name="defaultFileName">Optional suggested file name (without path).</param>
    /// <returns>The chosen save path, or <c>null</c> if the user cancelled.</returns>
    string? ShowSaveJsonDialog(string? defaultFileName = null);

    /// <summary>
    /// Shows a save-file dialog filtered to Markdown files (.md).
    /// </summary>
    /// <param name="defaultFileName">Optional suggested file name (without path).</param>
    /// <returns>The chosen save path, or <c>null</c> if the user cancelled.</returns>
    string? ShowSaveMarkdownDialog(string? defaultFileName = null);
}
