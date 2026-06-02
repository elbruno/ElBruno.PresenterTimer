using System.Collections.ObjectModel;
using System.Windows.Forms;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// ViewModel for <c>Views\SessionPreviewWindow.xaml</c>.
/// Exposes the loaded <see cref="SessionPlan"/> in a bindable form per PRD §7.5.
/// No logic lives in the code-behind; all commands and state live here.
/// </summary>
public sealed class SessionPreviewViewModel : ViewModelBase
{
    // ── Service dependencies ──────────────────────────────────────────────────
    private readonly ISessionLoaderService     _loaderService;
    private readonly ISessionValidationService _validationService;
    private readonly IFileDialogService        _fileDialogService;
    private readonly ISettingsService          _settingsService;
    private readonly Action<SessionPlan>?      _onStartSession;

    // ── Backing fields ────────────────────────────────────────────────────────
    private SessionPlan _plan;
    private string      _title                = string.Empty;
    private string      _description          = string.Empty;
    private string      _totalDurationDisplay = string.Empty;
    private int         _sectionCount;
    private string      _validationSummary    = string.Empty;
    private bool        _hasValidationIssues;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the window should close (Cancel or after Start Session).</summary>
    public event Action? RequestClose;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SessionPreviewViewModel(
        SessionPlan                plan,
        ISessionLoaderService      loaderService,
        ISessionValidationService  validationService,
        IFileDialogService         fileDialogService,
        ISettingsService           settingsService,
        Action<SessionPlan>?       onStartSession = null)
    {
        _loaderService     = loaderService;
        _validationService = validationService;
        _fileDialogService = fileDialogService;
        _settingsService   = settingsService;
        _onStartSession    = onStartSession;
        _plan              = plan;

        Sections = [];
        UpdateFromPlan(plan);

        StartSessionCommand       = new RelayCommand(ExecuteStartSession);
        CancelCommand             = new RelayCommand(() => RequestClose?.Invoke());
        ImportDifferentJsonCommand = new RelayCommand(ExecuteImportDifferentJson);
        ExportNormalizedJsonCommand = new RelayCommand(ExecuteExportNormalizedJson);
    }

    // ── Bound properties ──────────────────────────────────────────────────────

    /// <summary>Session title (PRD §7.5).</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>Session description (PRD §7.5).</summary>
    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    /// <summary>Human-readable total planned duration, e.g. "27:00" or "01:30:00".</summary>
    public string TotalDurationDisplay
    {
        get => _totalDurationDisplay;
        private set => SetProperty(ref _totalDurationDisplay, value);
    }

    /// <summary>Number of sections in the loaded plan.</summary>
    public int SectionCount
    {
        get => _sectionCount;
        private set => SetProperty(ref _sectionCount, value);
    }

    /// <summary>Section rows displayed in the ListView.</summary>
    public ObservableCollection<SectionRowViewModel> Sections { get; }

    /// <summary>Human-readable validation outcome shown below the section list.</summary>
    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    /// <summary><see langword="true"/> when validation found issues; drives warning-style binding.</summary>
    public bool HasValidationIssues
    {
        get => _hasValidationIssues;
        private set => SetProperty(ref _hasValidationIssues, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand StartSessionCommand        { get; }
    public RelayCommand CancelCommand              { get; }
    public RelayCommand ImportDifferentJsonCommand  { get; }
    public RelayCommand ExportNormalizedJsonCommand { get; }

    // ── Command implementations ───────────────────────────────────────────────

    private void ExecuteStartSession()
    {
        // TODO Phase 5 — Parker connects ISessionTimerService.Start(plan) via the onStartSession hook
        _onStartSession?.Invoke(_plan);
        RequestClose?.Invoke();
    }

    private void ExecuteImportDifferentJson()
    {
        var path = _fileDialogService.ShowOpenJsonDialog();
        if (path is null) return;

        SessionPlan newPlan;
        try
        {
            newPlan = _loaderService.Load(path);
        }
        catch (SessionLoadException ex)
        {
            MessageBox.Show(
                $"Could not load session file:\n\n{ex.Message}",
                "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var result = _validationService.Validate(newPlan);
        if (!result.IsValid)
        {
            var errors = string.Join("\n• ", result.Errors);
            MessageBox.Show(
                $"Invalid session file.\n\n• {errors}",
                "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Persist new last-session path
        _settingsService.Settings.General.LastSessionPath = path;
        AddToRecentSessions(path);
        _settingsService.Save();

        // Refresh the preview in place — window stays open
        _plan = newPlan;
        UpdateFromPlan(newPlan);
    }

    private void ExecuteExportNormalizedJson()
    {
        var json         = _loaderService.ExportJson(_plan);
        var suggestedName = SanitizeFileName(_plan.Title) + ".json";
        var savePath      = _fileDialogService.ShowSaveJsonDialog(suggestedName);
        if (savePath is null) return;

        try
        {
            File.WriteAllText(savePath, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not save file:\n\n{ex.Message}",
                "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void UpdateFromPlan(SessionPlan plan)
    {
        Title                = plan.Title;
        Description          = plan.Description ?? string.Empty;
        TotalDurationDisplay = FormatDuration(_loaderService.GetTotalDuration(plan));
        SectionCount         = plan.Sections.Count;

        Sections.Clear();
        for (int i = 0; i < plan.Sections.Count; i++)
        {
            var s = plan.Sections[i];
            Sections.Add(new SectionRowViewModel
            {
                Index           = i + 1,
                Title          = s.Title,
                DurationDisplay = FormatDuration(s.Duration),
                Notes           = s.Notes,
            });
        }

        var validation = _validationService.Validate(plan);
        if (validation.IsValid)
        {
            HasValidationIssues = false;
            ValidationSummary   = "✓ Session validated successfully — no issues found.";
        }
        else
        {
            HasValidationIssues = true;
            ValidationSummary   = "⚠ " + string.Join("\n⚠ ", validation.Errors);
        }
    }

    private void AddToRecentSessions(string path)
    {
        var recent = _settingsService.Settings.General.RecentSessionPaths;
        recent.Remove(path); // avoid duplicates
        recent.Insert(0, path);
        if (recent.Count > 10)
            recent.RemoveRange(10, recent.Count - 10);
    }

    /// <summary>Formats a <see cref="TimeSpan"/> as "MM:ss" or "H:MM:ss" for display.</summary>
    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }
}
