using System.Windows;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// ViewModel for <c>Views\SessionSummaryWindow.xaml</c> (PRD §7.14).
/// Takes a <see cref="SessionResult"/> from <see cref="ISessionTimerService.GetResult"/>
/// and exposes it in bindable form together with clipboard/file export commands.
/// No WPF window references live here; close is signalled via <see cref="RequestClose"/>.
/// </summary>
public sealed class SessionSummaryViewModel : ViewModelBase
{
    private readonly SessionResult      _result;
    private readonly IFileDialogService _fileDialogService;

    // ── Backing fields ────────────────────────────────────────────────────────

    private string _statusMessage = string.Empty;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the window should close (Close button or post-export close).</summary>
    public event Action? RequestClose;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Main constructor used by the application at session end.
    /// </summary>
    /// <param name="result">The session result from <see cref="ISessionTimerService.GetResult"/>.</param>
    /// <param name="fileDialogService">Provides save dialogs for Markdown and JSON export.</param>
    public SessionSummaryViewModel(SessionResult result, IFileDialogService fileDialogService)
    {
        _result            = result ?? throw new ArgumentNullException(nameof(result));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));

        // Populate display-only header values (never change after construction)
        SessionTitle              = result.SessionTitle;
        PlannedDisplay            = SummaryFormatter.FormatTime(result.PlannedDuration);
        ActualDisplay             = SummaryFormatter.FormatTime(result.ActualDuration);
        DifferenceDisplay         = SummaryFormatter.FormatDifference(result.Difference);
        DifferenceIsPositive      = result.Difference > TimeSpan.Zero;
        DifferenceIsNegative      = result.Difference < TimeSpan.Zero;
        HasTotalExtensions        = result.TotalExtensions > TimeSpan.Zero;
        TotalExtensionsDisplay    = SummaryFormatter.FormatTime(result.TotalExtensions);

        SectionRows = BuildSectionRows(result);

        CopyToClipboardCommand  = new RelayCommand(ExecuteCopyToClipboard);
        SaveAsMarkdownCommand   = new RelayCommand(ExecuteSaveAsMarkdown);
        SaveAsJsonCommand       = new RelayCommand(ExecuteSaveAsJson);
        CloseCommand            = new RelayCommand(() => RequestClose?.Invoke());
    }

    // ── Design-time / test constructor ────────────────────────────────────────

    /// <summary>
    /// Parameterless constructor for XAML design-time preview and unit tests.
    /// Populates the VM with a built-in sample result.
    /// </summary>
    public SessionSummaryViewModel()
        : this(BuildDesignTimeResult(), new FileDialogService())
    {
    }

    // ── Bound properties (header) ─────────────────────────────────────────────

    public string SessionTitle           { get; }
    public string PlannedDisplay         { get; }
    public string ActualDisplay          { get; }
    public string DifferenceDisplay      { get; }

    /// <summary><see langword="true"/> when actual exceeded plan — drives red difference text.</summary>
    public bool DifferenceIsPositive     { get; }

    /// <summary><see langword="true"/> when actual was under plan — drives green difference text.</summary>
    public bool DifferenceIsNegative     { get; }

    /// <summary>Whether any section extensions were applied during the session.</summary>
    public bool HasTotalExtensions       { get; }
    public string TotalExtensionsDisplay { get; }

    // ── Bound properties (section table) ─────────────────────────────────────

    public IReadOnlyList<SectionSummaryRowViewModel> SectionRows { get; }

    // ── Status / feedback ─────────────────────────────────────────────────────

    /// <summary>Transient status message shown after a successful export action.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand CopyToClipboardCommand { get; }
    public RelayCommand SaveAsMarkdownCommand  { get; }
    public RelayCommand SaveAsJsonCommand      { get; }
    public RelayCommand CloseCommand           { get; }

    // ── Command implementations ───────────────────────────────────────────────

    private void ExecuteCopyToClipboard()
    {
        try
        {
            System.Windows.Clipboard.SetText(SummaryFormatter.FormatPlainText(_result));
            StatusMessage = "✓ Summary copied to clipboard.";
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Could not write to clipboard:\n\n{ex.Message}",
                "Clipboard Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    private void ExecuteSaveAsMarkdown()
    {
        var suggested = SanitizeFileName(_result.SessionTitle) + "-summary.md";
        var path      = _fileDialogService.ShowSaveMarkdownDialog(suggested);
        if (path is null) return;

        TrySaveFile(path, SummaryFormatter.FormatMarkdown(_result),
            onSuccess: () => StatusMessage = $"✓ Markdown saved to {Path.GetFileName(path)}.");
    }

    private void ExecuteSaveAsJson()
    {
        var suggested = SanitizeFileName(_result.SessionTitle) + "-summary.json";
        var path      = _fileDialogService.ShowSaveJsonDialog(suggested);
        if (path is null) return;

        TrySaveFile(path, SummaryFormatter.FormatJson(_result),
            onSuccess: () => StatusMessage = $"✓ JSON saved to {Path.GetFileName(path)}.");
    }

    private void TrySaveFile(string path, string content, Action onSuccess)
    {
        try
        {
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
            onSuccess();
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Could not save file:\n\n{ex.Message}",
                "Export Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IReadOnlyList<SectionSummaryRowViewModel> BuildSectionRows(SessionResult result)
    {
        var rows = new List<SectionSummaryRowViewModel>(result.Sections.Count);
        foreach (var s in result.Sections)
        {
            rows.Add(new SectionSummaryRowViewModel
            {
                Number           = s.Index + 1,
                Title            = s.Title,
                PlannedDisplay   = SummaryFormatter.FormatTime(s.PlannedDuration),
                ActualDisplay    = s.WasVisited
                    ? SummaryFormatter.FormatTime(s.ActualDuration) : "—",
                DifferenceDisplay = s.WasVisited
                    ? SummaryFormatter.FormatDifference(s.Difference) : "—",
                OvertimeText     = s.WasOvertime ? "OVERTIME" : string.Empty,
                ExtensionsDisplay = s.TotalExtensions > TimeSpan.Zero
                    ? SummaryFormatter.FormatTime(s.TotalExtensions) : string.Empty,
                SkippedText      = s.WasSkipped ? "Skipped" : string.Empty,
                WasVisited       = s.WasVisited,
                WasOvertime      = s.WasOvertime,
                DifferenceIsNegative = s.WasVisited && s.Difference < TimeSpan.Zero,
            });
        }
        return rows.AsReadOnly();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }

    private static SessionResult BuildDesignTimeResult()
    {
        return new SessionResult
        {
            SessionTitle     = "AI Agents Recording",
            PlannedDuration  = TimeSpan.FromMinutes(27),
            ActualDuration   = new TimeSpan(0, 31, 20),
            TotalExtensions  = TimeSpan.FromMinutes(2),
            Sections =
            [
                new SectionResult { Index = 0, Title = "Intro",            PlannedDuration = TimeSpan.FromMinutes(3),  ActualDuration = new TimeSpan(0, 3, 20),  WasVisited = true },
                new SectionResult { Index = 1, Title = "Problem Statement", PlannedDuration = TimeSpan.FromMinutes(5),  ActualDuration = new TimeSpan(0, 4, 50),  WasVisited = true },
                new SectionResult { Index = 2, Title = "Demo",             PlannedDuration = TimeSpan.FromMinutes(15), ActualDuration = new TimeSpan(0, 18, 35), WasVisited = true, TotalExtensions = TimeSpan.FromMinutes(2) },
                new SectionResult { Index = 3, Title = "Wrap-up",          PlannedDuration = TimeSpan.FromMinutes(4),  ActualDuration = new TimeSpan(0, 4, 35),  WasVisited = true },
            ],
        };
    }
}
