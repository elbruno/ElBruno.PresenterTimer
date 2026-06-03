using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// ViewModel for <c>Views\SessionPlanEditorWindow.xaml</c>.
/// Supports creating, editing, validating, loading and saving SessionPlan JSON.
/// </summary>
public sealed class SessionPlanEditorViewModel : ViewModelBase
{
    private readonly ISessionLoaderService _loaderService;
    private readonly ISessionValidationService _validationService;
    private readonly IFileDialogService _fileDialogService;
    private readonly Action<string, string> _writeAllText;

    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _metadataAuthor = string.Empty;
    private string _metadataVersion = string.Empty;
    private string _metadataCreatedAt = string.Empty;
    private string _validationSummary = string.Empty;
    private bool _hasValidationIssues;
    private string _statusMessage = string.Empty;
    private string? _currentFilePath;
    private SessionPlanEditorSectionViewModel? _selectedSection;
    private bool _suppressValidationRefresh;

    public event Action? RequestClose;

    public SessionPlanEditorViewModel(
        ISessionLoaderService loaderService,
        ISessionValidationService validationService,
        IFileDialogService fileDialogService,
        Action<string, string>? writeAllText = null)
    {
        _loaderService = loaderService ?? throw new ArgumentNullException(nameof(loaderService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _writeAllText = writeAllText ?? File.WriteAllText;

        Sections = [];
        Sections.CollectionChanged += OnSectionsCollectionChanged;

        NewPlanCommand = new RelayCommand(ExecuteNewPlan);
        OpenJsonCommand = new RelayCommand(ExecuteOpenJson);
        SaveJsonCommand = new RelayCommand(ExecuteSaveJson);
        AddSectionCommand = new RelayCommand(ExecuteAddSection);
        RemoveSelectedSectionCommand = new RelayCommand(ExecuteRemoveSelectedSection, () => SelectedSection is not null);
        MoveSectionUpCommand = new RelayCommand(ExecuteMoveSectionUp, CanMoveSectionUp);
        MoveSectionDownCommand = new RelayCommand(ExecuteMoveSectionDown, CanMoveSectionDown);
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke());

        LoadFromPlan(CreateDefaultPlan());
    }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
                RefreshValidationFeedback();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
                RefreshValidationFeedback();
        }
    }

    public string MetadataAuthor
    {
        get => _metadataAuthor;
        set
        {
            if (SetProperty(ref _metadataAuthor, value))
                RefreshValidationFeedback();
        }
    }

    public string MetadataVersion
    {
        get => _metadataVersion;
        set
        {
            if (SetProperty(ref _metadataVersion, value))
                RefreshValidationFeedback();
        }
    }

    public string MetadataCreatedAt
    {
        get => _metadataCreatedAt;
        set
        {
            if (SetProperty(ref _metadataCreatedAt, value))
                RefreshValidationFeedback();
        }
    }

    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    public bool HasValidationIssues
    {
        get => _hasValidationIssues;
        private set => SetProperty(ref _hasValidationIssues, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set => SetProperty(ref _currentFilePath, value);
    }

    public ObservableCollection<SessionPlanEditorSectionViewModel> Sections { get; }

    public SessionPlanEditorSectionViewModel? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
                RelayCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand NewPlanCommand { get; }
    public RelayCommand OpenJsonCommand { get; }
    public RelayCommand SaveJsonCommand { get; }
    public RelayCommand AddSectionCommand { get; }
    public RelayCommand RemoveSelectedSectionCommand { get; }
    public RelayCommand MoveSectionUpCommand { get; }
    public RelayCommand MoveSectionDownCommand { get; }
    public RelayCommand CloseCommand { get; }

    private void ExecuteNewPlan()
    {
        LoadFromPlan(CreateDefaultPlan());
        CurrentFilePath = null;
        StatusMessage = "Started a new session plan.";
    }

    private void ExecuteOpenJson()
    {
        var path = _fileDialogService.ShowOpenJsonDialog();
        if (path is null) return;

        SessionPlan plan;
        try
        {
            plan = _loaderService.Load(path);
        }
        catch (SessionLoadException ex)
        {
            HasValidationIssues = true;
            ValidationSummary = $"⚠ Could not load session file: {ex.Message}";
            StatusMessage = "Could not open session plan.";
            return;
        }

        LoadFromPlan(plan);
        CurrentFilePath = path;
        StatusMessage = $"✓ Loaded {Path.GetFileName(path)}.";
    }

    private void ExecuteSaveJson()
    {
        if (!TryBuildPlan(out var plan, out var buildErrors))
        {
            ApplyValidationResult(ValidationResult.Failure(buildErrors));
            StatusMessage = "Fix editor errors before saving.";
            return;
        }

        var validation = _validationService.Validate(plan);
        ApplyValidationResult(validation);
        if (!validation.IsValid)
        {
            StatusMessage = "Fix validation issues before saving.";
            return;
        }

        var suggestedName = CurrentFilePath is not null
            ? Path.GetFileName(CurrentFilePath)
            : $"{SanitizeFileName(plan.Title)}.json";
        var savePath = _fileDialogService.ShowSaveJsonDialog(suggestedName);
        if (savePath is null) return;

        try
        {
            var json = _loaderService.ExportJson(plan);
            _writeAllText(savePath, json);
            CurrentFilePath = savePath;
            StatusMessage = $"✓ Saved {Path.GetFileName(savePath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save session plan: {ex.Message}";
        }
    }

    private void ExecuteAddSection()
    {
        var section = new SessionPlanEditorSectionViewModel
        {
            Title = "New Section",
            DurationText = "00:05:00",
        };
        Sections.Add(section);
        SelectedSection = section;
        RefreshValidationFeedback();
    }

    private void ExecuteRemoveSelectedSection()
    {
        if (SelectedSection is null) return;

        var idx = Sections.IndexOf(SelectedSection);
        if (idx < 0) return;

        Sections.RemoveAt(idx);
        SelectedSection = Sections.Count == 0
            ? null
            : Sections[Math.Min(idx, Sections.Count - 1)];
        RefreshValidationFeedback();
    }

    private void ExecuteMoveSectionUp()
    {
        if (SelectedSection is null) return;

        var idx = Sections.IndexOf(SelectedSection);
        if (idx <= 0) return;

        Sections.Move(idx, idx - 1);
        RefreshValidationFeedback();
    }

    private void ExecuteMoveSectionDown()
    {
        if (SelectedSection is null) return;

        var idx = Sections.IndexOf(SelectedSection);
        if (idx < 0 || idx >= Sections.Count - 1) return;

        Sections.Move(idx, idx + 1);
        RefreshValidationFeedback();
    }

    private bool CanMoveSectionUp()
        => SelectedSection is not null && Sections.IndexOf(SelectedSection) > 0;

    private bool CanMoveSectionDown()
        => SelectedSection is not null
           && Sections.IndexOf(SelectedSection) >= 0
           && Sections.IndexOf(SelectedSection) < Sections.Count - 1;

    private void LoadFromPlan(SessionPlan plan)
    {
        _suppressValidationRefresh = true;
        try
        {
            Title = plan.Title;
            Description = plan.Description ?? string.Empty;
            MetadataAuthor = plan.Metadata?.Author ?? string.Empty;
            MetadataVersion = plan.Metadata?.Version ?? string.Empty;
            MetadataCreatedAt = plan.Metadata?.CreatedAt ?? string.Empty;

            Sections.Clear();
            foreach (var section in plan.Sections)
                Sections.Add(SessionPlanEditorSectionViewModel.FromSection(section));

            SelectedSection = Sections.FirstOrDefault();
        }
        finally
        {
            _suppressValidationRefresh = false;
        }

        RefreshValidationFeedback();
    }

    private void OnSectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var removed in e.OldItems.OfType<SessionPlanEditorSectionViewModel>())
                removed.PropertyChanged -= OnSectionPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var added in e.NewItems.OfType<SessionPlanEditorSectionViewModel>())
                added.PropertyChanged += OnSectionPropertyChanged;
        }

        RelayCommand.RaiseCanExecuteChanged();
    }

    private void OnSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => RefreshValidationFeedback();

    private void RefreshValidationFeedback()
    {
        if (_suppressValidationRefresh) return;

        if (!TryBuildPlan(out var plan, out var buildErrors))
        {
            ApplyValidationResult(ValidationResult.Failure(buildErrors));
            return;
        }

        ApplyValidationResult(_validationService.Validate(plan));
    }

    private bool TryBuildPlan(out SessionPlan plan, out List<string> errors)
    {
        plan = new SessionPlan();
        errors = [];

        plan.Title = Title.Trim();
        plan.Description = NormalizeNull(Description);
        plan.Metadata = BuildMetadataOrNull();
        plan.Sections = [];

        if (Sections.Count == 0)
            errors.Add("Session must contain at least one section.");

        for (int i = 0; i < Sections.Count; i++)
        {
            if (!Sections[i].TryBuildSection(i + 1, out var section, out var error))
            {
                errors.Add(error!);
                continue;
            }

            plan.Sections.Add(section);
        }

        return errors.Count == 0;
    }

    private void ApplyValidationResult(ValidationResult result)
    {
        if (result.IsValid)
        {
            HasValidationIssues = false;
            ValidationSummary = "✓ Session validated successfully — no issues found.";
            return;
        }

        HasValidationIssues = true;
        ValidationSummary = "⚠ " + string.Join("\n⚠ ", result.Errors);
    }

    private SessionMetadata? BuildMetadataOrNull()
    {
        var author = NormalizeNull(MetadataAuthor);
        var version = NormalizeNull(MetadataVersion);
        var createdAt = NormalizeNull(MetadataCreatedAt);

        if (author is null && version is null && createdAt is null)
            return null;

        return new SessionMetadata
        {
            Author = author,
            Version = version,
            CreatedAt = createdAt,
        };
    }

    private static SessionPlan CreateDefaultPlan()
    {
        return new SessionPlan
        {
            Title = "New Session Plan",
            Sections =
            [
                new SessionSection
                {
                    Title = "New Section",
                    Duration = TimeSpan.FromMinutes(5),
                },
            ],
        };
    }

    private static string? NormalizeNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }
}
