using System.Diagnostics;
using System.Windows.Input;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// ViewModel for <c>Views\SettingsWindow.xaml</c> (PRD §7.13).
/// Holds editable copies of all <see cref="AppSettings"/> groups so changes can be
/// discarded on Cancel without mutating live settings.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService    _settingsService;
    private readonly IFileDialogService  _fileDialogService;
    private readonly IWindowPlacementService _windowPlacementService;

    // ── RequestClose ──────────────────────────────────────────────────────────
    /// <summary>Raised to tell the view to close itself.</summary>
    public event Action? RequestClose;

    // ══════════════════════════════════════════════════════════════════════════
    // General tab
    // ══════════════════════════════════════════════════════════════════════════

    private bool _launchMinimizedToTray;
    public bool LaunchMinimizedToTray
    {
        get => _launchMinimizedToTray;
        set => SetProperty(ref _launchMinimizedToTray, value);
    }

    private bool _rememberLastSession;
    public bool RememberLastSession
    {
        get => _rememberLastSession;
        set => SetProperty(ref _rememberLastSession, value);
    }

    private bool _autoLoadLastSessionOnStartup;
    public bool AutoLoadLastSessionOnStartup
    {
        get => _autoLoadLastSessionOnStartup;
        set => SetProperty(ref _autoLoadLastSessionOnStartup, value);
    }

    private bool _showSessionPreviewAfterImport;
    public bool ShowSessionPreviewAfterImport
    {
        get => _showSessionPreviewAfterImport;
        set => SetProperty(ref _showSessionPreviewAfterImport, value);
    }

    private bool _confirmBeforeReset;
    public bool ConfirmBeforeReset
    {
        get => _confirmBeforeReset;
        set => SetProperty(ref _confirmBeforeReset, value);
    }

    private bool _confirmBeforeExitWhileRunning;
    public bool ConfirmBeforeExitWhileRunning
    {
        get => _confirmBeforeExitWhileRunning;
        set => SetProperty(ref _confirmBeforeExitWhileRunning, value);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Behavior tab
    // ══════════════════════════════════════════════════════════════════════════

    private bool _showOverlayWhenSessionStarts;
    public bool ShowOverlayWhenSessionStarts
    {
        get => _showOverlayWhenSessionStarts;
        set => SetProperty(ref _showOverlayWhenSessionStarts, value);
    }

    private bool _hideOverlayWhenSessionEnds;
    public bool HideOverlayWhenSessionEnds
    {
        get => _hideOverlayWhenSessionEnds;
        set => SetProperty(ref _hideOverlayWhenSessionEnds, value);
    }

    private bool _autoAdvanceSections;
    public bool AutoAdvanceSections
    {
        get => _autoAdvanceSections;
        set => SetProperty(ref _autoAdvanceSections, value);
    }

    private bool _keepCountingOvertimeAfterSectionEnd;
    public bool KeepCountingOvertimeAfterSectionEnd
    {
        get => _keepCountingOvertimeAfterSectionEnd;
        set => SetProperty(ref _keepCountingOvertimeAfterSectionEnd, value);
    }

    private bool _keepCountingOvertimeAfterSessionEnd;
    public bool KeepCountingOvertimeAfterSessionEnd
    {
        get => _keepCountingOvertimeAfterSessionEnd;
        set => SetProperty(ref _keepCountingOvertimeAfterSessionEnd, value);
    }

    private bool _enableGlobalHotkeys;
    public bool EnableGlobalHotkeys
    {
        get => _enableGlobalHotkeys;
        set => SetProperty(ref _enableGlobalHotkeys, value);
    }

    private bool _enableOverlayClickThrough;
    public bool EnableOverlayClickThrough
    {
        get => _enableOverlayClickThrough;
        set => SetProperty(ref _enableOverlayClickThrough, value);
    }

    private bool _pauseTimerWhenComputerLocks;
    public bool PauseTimerWhenComputerLocks
    {
        get => _pauseTimerWhenComputerLocks;
        set => SetProperty(ref _pauseTimerWhenComputerLocks, value);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Overlay Style tab
    // ══════════════════════════════════════════════════════════════════════════

    private string _theme = "System";
    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    private string _accentColor = "#0078D4";
    public string AccentColor
    {
        get => _accentColor;
        set => SetProperty(ref _accentColor, value);
    }

    private string _warningColor = "#FFC107";
    public string WarningColor
    {
        get => _warningColor;
        set => SetProperty(ref _warningColor, value);
    }

    private string _overtimeColor = "#E53935";
    public string OvertimeColor
    {
        get => _overtimeColor;
        set => SetProperty(ref _overtimeColor, value);
    }

    private int _completedSectionOpacity = 45;
    public int CompletedSectionOpacity
    {
        get => _completedSectionOpacity;
        set => SetProperty(ref _completedSectionOpacity, Math.Clamp(value, 0, 100));
    }

    private int _upcomingSectionOpacity = 55;
    public int UpcomingSectionOpacity
    {
        get => _upcomingSectionOpacity;
        set => SetProperty(ref _upcomingSectionOpacity, Math.Clamp(value, 0, 100));
    }

    private int _currentSectionOpacity = 100;
    public int CurrentSectionOpacity
    {
        get => _currentSectionOpacity;
        set => SetProperty(ref _currentSectionOpacity, Math.Clamp(value, 0, 100));
    }

    private int _progressFillOpacity = 20;
    public int ProgressFillOpacity
    {
        get => _progressFillOpacity;
        set => SetProperty(ref _progressFillOpacity, Math.Clamp(value, 0, 100));
    }

    private int _overlayOpacity = 85;
    public int OverlayOpacity
    {
        get => _overlayOpacity;
        set => SetProperty(ref _overlayOpacity, Math.Clamp(value, 10, 100));
    }

    private string _fontFamily = "Segoe UI";
    public string FontFamily
    {
        get => _fontFamily;
        set => SetProperty(ref _fontFamily, value);
    }

    private string _fontSize = "Medium";
    public string FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    private string _borderRadius = "Medium";
    public string BorderRadius
    {
        get => _borderRadius;
        set => SetProperty(ref _borderRadius, value);
    }

    private bool _showSectionLabels = true;
    public bool ShowSectionLabels
    {
        get => _showSectionLabels;
        set => SetProperty(ref _showSectionLabels, value);
    }

    private bool _showSessionTitle = true;
    public bool ShowSessionTitle
    {
        get => _showSessionTitle;
        set => SetProperty(ref _showSessionTitle, value);
    }

    private bool _showCurrentSectionTitle = true;
    public bool ShowCurrentSectionTitle
    {
        get => _showCurrentSectionTitle;
        set => SetProperty(ref _showCurrentSectionTitle, value);
    }

    private bool _showNextSectionTitle = true;
    public bool ShowNextSectionTitle
    {
        get => _showNextSectionTitle;
        set => SetProperty(ref _showNextSectionTitle, value);
    }

    private bool _showTimeRemaining = true;
    public bool ShowTimeRemaining
    {
        get => _showTimeRemaining;
        set => SetProperty(ref _showTimeRemaining, value);
    }

    private bool _showElapsedTime = true;
    public bool ShowElapsedTime
    {
        get => _showElapsedTime;
        set => SetProperty(ref _showElapsedTime, value);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Overlay Layout tab
    // ══════════════════════════════════════════════════════════════════════════

    private string _overlayMode = "FullTimeline";
    public string OverlayMode
    {
        get => _overlayMode;
        set => SetProperty(ref _overlayMode, value);
    }

    private string _position = "TopCenter";
    public string Position
    {
        get => _position;
        set => SetProperty(ref _position, value);
    }

    private int _monitor;
    public int Monitor
    {
        get => _monitor;
        set
        {
            var clamped = Math.Max(0, value);
            if (!SetProperty(ref _monitor, clamped))
                return;

            if (clamped < AvailableMonitors.Count)
                SelectedMonitorDeviceName = AvailableMonitors[clamped].DeviceName;
        }
    }

    public sealed class MonitorSelectionOption
    {
        public required string DeviceName { get; init; }
        public required string DisplayName { get; init; }
    }

    private IReadOnlyList<MonitorSelectionOption> _availableMonitors = [];
    public IReadOnlyList<MonitorSelectionOption> AvailableMonitors
    {
        get => _availableMonitors;
        private set => SetProperty(ref _availableMonitors, value);
    }

    private string? _selectedMonitorDeviceName;
    public string? SelectedMonitorDeviceName
    {
        get => _selectedMonitorDeviceName;
        set
        {
            if (!SetProperty(ref _selectedMonitorDeviceName, value))
                return;

            var selectedIndex = AvailableMonitors
                .Select((m, index) => new { m.DeviceName, index })
                .FirstOrDefault(x => string.Equals(x.DeviceName, value, StringComparison.OrdinalIgnoreCase))
                ?.index;

            if (selectedIndex.HasValue)
                Monitor = selectedIndex.Value;
        }
    }

    private double _widthFraction = 0.80;
    public double WidthFraction
    {
        get => _widthFraction;
        set => SetProperty(ref _widthFraction, Math.Clamp(value, 0.2, 1.0));
    }

    private string _overlayHeight = "Compact";
    public string OverlayHeight
    {
        get => _overlayHeight;
        set => SetProperty(ref _overlayHeight, value);
    }

    private bool _rememberCustomPosition = true;
    public bool RememberCustomPosition
    {
        get => _rememberCustomPosition;
        set => SetProperty(ref _rememberCustomPosition, value);
    }

    private bool _enableDragToMove = true;
    public bool EnableDragToMove
    {
        get => _enableDragToMove;
        set => SetProperty(ref _enableDragToMove, value);
    }

    private bool _snapToScreenEdges = true;
    public bool SnapToScreenEdges
    {
        get => _snapToScreenEdges;
        set => SetProperty(ref _snapToScreenEdges, value);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Alerts tab
    // ══════════════════════════════════════════════════════════════════════════

    private bool _enableSectionWarningAlerts = true;
    public bool EnableSectionWarningAlerts
    {
        get => _enableSectionWarningAlerts;
        set => SetProperty(ref _enableSectionWarningAlerts, value);
    }

    private string _sectionWarningThreshold = "00:01:00";
    public string SectionWarningThreshold
    {
        get => _sectionWarningThreshold;
        set => SetProperty(ref _sectionWarningThreshold, value);
    }

    private bool _enableSessionWarningAlerts = true;
    public bool EnableSessionWarningAlerts
    {
        get => _enableSessionWarningAlerts;
        set => SetProperty(ref _enableSessionWarningAlerts, value);
    }

    private string _sessionWarningThreshold = "00:03:00";
    public string SessionWarningThreshold
    {
        get => _sessionWarningThreshold;
        set => SetProperty(ref _sessionWarningThreshold, value);
    }

    private bool _enableSectionEndAlerts = true;
    public bool EnableSectionEndAlerts
    {
        get => _enableSectionEndAlerts;
        set => SetProperty(ref _enableSectionEndAlerts, value);
    }

    private bool _enableSessionEndAlerts = true;
    public bool EnableSessionEndAlerts
    {
        get => _enableSessionEndAlerts;
        set => SetProperty(ref _enableSessionEndAlerts, value);
    }

    private bool _enableOvertimeAlerts = true;
    public bool EnableOvertimeAlerts
    {
        get => _enableOvertimeAlerts;
        set => SetProperty(ref _enableOvertimeAlerts, value);
    }

    private bool _enableOverlayPulse = true;
    public bool EnableOverlayPulse
    {
        get => _enableOverlayPulse;
        set => SetProperty(ref _enableOverlayPulse, value);
    }

    private bool _enableSoundAlerts;
    public bool EnableSoundAlerts
    {
        get => _enableSoundAlerts;
        set => SetProperty(ref _enableSoundAlerts, value);
    }

    private bool _enableWindowsNotifications;
    public bool EnableWindowsNotifications
    {
        get => _enableWindowsNotifications;
        set => SetProperty(ref _enableWindowsNotifications, value);
    }

    private int _alertMessageDurationSeconds = 5;
    public int AlertMessageDurationSeconds
    {
        get => _alertMessageDurationSeconds;
        set => SetProperty(ref _alertMessageDurationSeconds, Math.Max(1, value));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Hotkeys tab
    // ══════════════════════════════════════════════════════════════════════════

    private bool _hotkeysEnabled;
    public bool HotkeysEnabled
    {
        get => _hotkeysEnabled;
        set => SetProperty(ref _hotkeysEnabled, value);
    }

    private string _pauseResumeHotkey = "Ctrl+Alt+Space";
    public string PauseResumeHotkey
    {
        get => _pauseResumeHotkey;
        set => SetProperty(ref _pauseResumeHotkey, value);
    }

    private string _nextSectionHotkey = "Ctrl+Alt+Right";
    public string NextSectionHotkey
    {
        get => _nextSectionHotkey;
        set => SetProperty(ref _nextSectionHotkey, value);
    }

    private string _previousSectionHotkey = "Ctrl+Alt+Left";
    public string PreviousSectionHotkey
    {
        get => _previousSectionHotkey;
        set => SetProperty(ref _previousSectionHotkey, value);
    }

    private string _resetSessionHotkey = "Ctrl+Alt+R";
    public string ResetSessionHotkey
    {
        get => _resetSessionHotkey;
        set => SetProperty(ref _resetSessionHotkey, value);
    }

    private string _showHideOverlayHotkey = "Ctrl+Alt+H";
    public string ShowHideOverlayHotkey
    {
        get => _showHideOverlayHotkey;
        set => SetProperty(ref _showHideOverlayHotkey, value);
    }

    private string _extendSectionOneMinuteHotkey = "Ctrl+Alt+Up";
    public string ExtendSectionOneMinuteHotkey
    {
        get => _extendSectionOneMinuteHotkey;
        set => SetProperty(ref _extendSectionOneMinuteHotkey, value);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Static option lists for combo boxes
    // ══════════════════════════════════════════════════════════════════════════

    public static IReadOnlyList<string> ThemeOptions        { get; } = ["System", "Light", "Dark"];
    public static IReadOnlyList<string> FontSizeOptions     { get; } = ["Small", "Medium", "Large"];
    public static IReadOnlyList<string> BorderRadiusOptions { get; } = ["None", "Small", "Medium", "Large"];
    public static IReadOnlyList<string> OverlayModeOptions  { get; } = ["FullTimeline", "Compact"];
    public static IReadOnlyList<string> PositionOptions     { get; } = ["TopCenter", "TopLeft", "TopRight", "BottomCenter", "BottomLeft", "BottomRight"];
    public static IReadOnlyList<string> HeightOptions       { get; } = ["Compact", "Expanded"];
    public static IReadOnlyList<string> FontFamilyOptions   { get; } = ["Segoe UI", "Consolas", "Calibri", "Arial", "Tahoma"];

    // ══════════════════════════════════════════════════════════════════════════
    // Commands
    // ══════════════════════════════════════════════════════════════════════════

    public ICommand SaveCommand            { get; }
    public ICommand CancelCommand          { get; }
    public ICommand ApplyCommand           { get; }
    public ICommand ResetToDefaultsCommand { get; }
    public ICommand OpenSettingsFolderCommand { get; }
    public ICommand ExportSettingsCommand  { get; }
    public ICommand ImportSettingsCommand  { get; }

    /// <summary>
    /// Plays the test sound immediately (ignores <c>EnableSoundAlerts</c>) so the user
    /// can confirm audio is working from the Alerts tab (PRD §7.8).
    /// </summary>
    public ICommand TestSoundCommand       { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsViewModel(ISettingsService settingsService, IFileDialogService fileDialogService, IWindowPlacementService windowPlacementService,
        Action? playTestSound = null)
    {
        _settingsService        = settingsService;
        _fileDialogService      = fileDialogService;
        _windowPlacementService = windowPlacementService;

        LoadFromSettings(_settingsService.Settings);

        SaveCommand            = new RelayCommand(ExecuteSave);
        CancelCommand          = new RelayCommand(ExecuteCancel);
        ApplyCommand           = new RelayCommand(ExecuteApply);
        ResetToDefaultsCommand = new RelayCommand(ExecuteResetToDefaults);
        OpenSettingsFolderCommand = new RelayCommand(ExecuteOpenSettingsFolder);
        ExportSettingsCommand  = new RelayCommand(ExecuteExportSettings);
        ImportSettingsCommand  = new RelayCommand(ExecuteImportSettings);
        TestSoundCommand       = new RelayCommand(() => playTestSound?.Invoke());
    }

    // ── Command implementations ───────────────────────────────────────────────

    private void ExecuteApply()
    {
        ApplyToSettings(_settingsService.Settings);
        _settingsService.Save();
        _settingsService.RaiseSettingsApplied();
    }

    private void ExecuteSave()
    {
        ExecuteApply();
        RequestClose?.Invoke();
    }

    private void ExecuteCancel()
        => RequestClose?.Invoke();

    private void ExecuteResetToDefaults()
        => LoadFromSettings(new AppSettings());

    private static void ExecuteOpenSettingsFolder()
    {
        var folder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ElBruno.PresenterTimer");

        System.IO.Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private void ExecuteExportSettings()
    {
        // Persist current form state first so the exported file reflects what's shown
        ApplyToSettings(_settingsService.Settings);
        _settingsService.Save();

        var savePath = _fileDialogService.ShowSaveJsonDialog("settings.json");
        if (savePath is null) return;

        var sourceFile = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ElBruno.PresenterTimer", "settings.json");

        try
        {
            System.IO.File.Copy(sourceFile, savePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Could not export settings:\n\n{ex.Message}",
                "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ExecuteImportSettings()
    {
        var openPath = _fileDialogService.ShowOpenJsonDialog();
        if (openPath is null) return;

        var destFile = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ElBruno.PresenterTimer", "settings.json");

        try
        {
            System.IO.File.Copy(openPath, destFile, overwrite: true);
            _settingsService.Load();
            LoadFromSettings(_settingsService.Settings);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Could not import settings:\n\n{ex.Message}",
                "Import Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    // ── Load / Apply helpers ─────────────────────────────────────────────────

    /// <summary>Populates all VM properties from the given <see cref="AppSettings"/> snapshot.</summary>
    private void LoadFromSettings(AppSettings s)
    {
        // General
        LaunchMinimizedToTray         = s.General.LaunchMinimizedToTray;
        RememberLastSession           = s.General.RememberLastSession;
        AutoLoadLastSessionOnStartup  = s.General.AutoLoadLastSessionOnStartup;
        ShowSessionPreviewAfterImport = s.General.ShowSessionPreviewAfterImport;
        ConfirmBeforeReset            = s.General.ConfirmBeforeReset;
        ConfirmBeforeExitWhileRunning = s.General.ConfirmBeforeExitWhileRunning;

        // Behavior
        ShowOverlayWhenSessionStarts         = s.Behavior.ShowOverlayWhenSessionStarts;
        HideOverlayWhenSessionEnds           = s.Behavior.HideOverlayWhenSessionEnds;
        AutoAdvanceSections                  = s.Behavior.AutoAdvanceSections;
        KeepCountingOvertimeAfterSectionEnd  = s.Behavior.KeepCountingOvertimeAfterSectionEnd;
        KeepCountingOvertimeAfterSessionEnd  = s.Behavior.KeepCountingOvertimeAfterSessionEnd;
        EnableGlobalHotkeys                  = s.Behavior.EnableGlobalHotkeys;
        EnableOverlayClickThrough            = s.Behavior.EnableOverlayClickThrough;
        PauseTimerWhenComputerLocks          = s.Behavior.PauseTimerWhenComputerLocks;

        // Overlay Style
        Theme                    = s.OverlayStyle.Theme;
        AccentColor              = s.OverlayStyle.AccentColor;
        WarningColor             = s.OverlayStyle.WarningColor;
        OvertimeColor            = s.OverlayStyle.OvertimeColor;
        CompletedSectionOpacity  = s.OverlayStyle.CompletedSectionOpacity;
        UpcomingSectionOpacity   = s.OverlayStyle.UpcomingSectionOpacity;
        CurrentSectionOpacity    = s.OverlayStyle.CurrentSectionOpacity;
        ProgressFillOpacity      = s.OverlayStyle.ProgressFillOpacity;
        OverlayOpacity           = s.OverlayStyle.OverlayOpacity;
        FontFamily               = s.OverlayStyle.FontFamily;
        FontSize                 = s.OverlayStyle.FontSize;
        BorderRadius             = s.OverlayStyle.BorderRadius;
        ShowSectionLabels        = s.OverlayStyle.ShowSectionLabels;
        ShowSessionTitle         = s.OverlayStyle.ShowSessionTitle;
        ShowCurrentSectionTitle  = s.OverlayStyle.ShowCurrentSectionTitle;
        ShowNextSectionTitle     = s.OverlayStyle.ShowNextSectionTitle;
        ShowTimeRemaining        = s.OverlayStyle.ShowTimeRemaining;
        ShowElapsedTime          = s.OverlayStyle.ShowElapsedTime;

        // Overlay Layout
        OverlayMode           = s.OverlayLayout.OverlayMode;
        Position              = s.OverlayLayout.Position;
        Monitor               = s.OverlayLayout.Monitor;
        LoadMonitorSelection(s.OverlayLayout.MonitorDeviceName, s.OverlayLayout.Monitor);
        WidthFraction         = s.OverlayLayout.WidthFraction;
        OverlayHeight         = s.OverlayLayout.Height;
        RememberCustomPosition = s.OverlayLayout.RememberCustomPosition;
        EnableDragToMove      = s.OverlayLayout.EnableDragToMove;
        SnapToScreenEdges     = s.OverlayLayout.SnapToScreenEdges;

        // Alerts
        EnableSectionWarningAlerts  = s.Alerts.EnableSectionWarningAlerts;
        SectionWarningThreshold     = s.Alerts.SectionWarningThreshold;
        EnableSessionWarningAlerts  = s.Alerts.EnableSessionWarningAlerts;
        SessionWarningThreshold     = s.Alerts.SessionWarningThreshold;
        EnableSectionEndAlerts      = s.Alerts.EnableSectionEndAlerts;
        EnableSessionEndAlerts      = s.Alerts.EnableSessionEndAlerts;
        EnableOvertimeAlerts        = s.Alerts.EnableOvertimeAlerts;
        EnableOverlayPulse          = s.Alerts.EnableOverlayPulse;
        EnableSoundAlerts           = s.Alerts.EnableSoundAlerts;
        EnableWindowsNotifications  = s.Alerts.EnableWindowsNotifications;
        AlertMessageDurationSeconds = s.Alerts.AlertMessageDurationSeconds;

        // Hotkeys
        HotkeysEnabled              = s.Hotkeys.Enabled;
        PauseResumeHotkey           = s.Hotkeys.PauseResume;
        NextSectionHotkey           = s.Hotkeys.NextSection;
        PreviousSectionHotkey       = s.Hotkeys.PreviousSection;
        ResetSessionHotkey          = s.Hotkeys.ResetSession;
        ShowHideOverlayHotkey       = s.Hotkeys.ShowHideOverlay;
        ExtendSectionOneMinuteHotkey = s.Hotkeys.ExtendSectionOneMinute;
    }

    /// <summary>Writes all VM properties back into the given <see cref="AppSettings"/> object.</summary>
    private void ApplyToSettings(AppSettings s)
    {
        // General
        s.General.LaunchMinimizedToTray         = LaunchMinimizedToTray;
        s.General.RememberLastSession           = RememberLastSession;
        s.General.AutoLoadLastSessionOnStartup  = AutoLoadLastSessionOnStartup;
        s.General.ShowSessionPreviewAfterImport = ShowSessionPreviewAfterImport;
        s.General.ConfirmBeforeReset            = ConfirmBeforeReset;
        s.General.ConfirmBeforeExitWhileRunning = ConfirmBeforeExitWhileRunning;

        // Behavior
        s.Behavior.ShowOverlayWhenSessionStarts        = ShowOverlayWhenSessionStarts;
        s.Behavior.HideOverlayWhenSessionEnds          = HideOverlayWhenSessionEnds;
        s.Behavior.AutoAdvanceSections                 = AutoAdvanceSections;
        s.Behavior.KeepCountingOvertimeAfterSectionEnd = KeepCountingOvertimeAfterSectionEnd;
        s.Behavior.KeepCountingOvertimeAfterSessionEnd = KeepCountingOvertimeAfterSessionEnd;
        s.Behavior.EnableGlobalHotkeys                 = EnableGlobalHotkeys;
        s.Behavior.EnableOverlayClickThrough           = EnableOverlayClickThrough;
        s.Behavior.PauseTimerWhenComputerLocks         = PauseTimerWhenComputerLocks;

        // Overlay Style
        s.OverlayStyle.Theme                   = Theme;
        s.OverlayStyle.AccentColor             = AccentColor;
        s.OverlayStyle.WarningColor            = WarningColor;
        s.OverlayStyle.OvertimeColor           = OvertimeColor;
        s.OverlayStyle.CompletedSectionOpacity = CompletedSectionOpacity;
        s.OverlayStyle.UpcomingSectionOpacity  = UpcomingSectionOpacity;
        s.OverlayStyle.CurrentSectionOpacity   = CurrentSectionOpacity;
        s.OverlayStyle.ProgressFillOpacity     = ProgressFillOpacity;
        s.OverlayStyle.OverlayOpacity          = OverlayOpacity;
        s.OverlayStyle.FontFamily              = FontFamily;
        s.OverlayStyle.FontSize                = FontSize;
        s.OverlayStyle.BorderRadius            = BorderRadius;
        s.OverlayStyle.ShowSectionLabels       = ShowSectionLabels;
        s.OverlayStyle.ShowSessionTitle        = ShowSessionTitle;
        s.OverlayStyle.ShowCurrentSectionTitle = ShowCurrentSectionTitle;
        s.OverlayStyle.ShowNextSectionTitle    = ShowNextSectionTitle;
        s.OverlayStyle.ShowTimeRemaining       = ShowTimeRemaining;
        s.OverlayStyle.ShowElapsedTime         = ShowElapsedTime;

        // Overlay Layout
        s.OverlayLayout.OverlayMode            = OverlayMode;
        s.OverlayLayout.Position               = Position;
        s.OverlayLayout.Monitor                = Monitor;
        s.OverlayLayout.MonitorDeviceName      = ResolveMonitorDeviceNameForSave();
        s.OverlayLayout.WidthFraction          = WidthFraction;
        s.OverlayLayout.Height                 = OverlayHeight;
        s.OverlayLayout.RememberCustomPosition = RememberCustomPosition;
        s.OverlayLayout.EnableDragToMove       = EnableDragToMove;
        s.OverlayLayout.SnapToScreenEdges      = SnapToScreenEdges;

        // Alerts
        s.Alerts.EnableSectionWarningAlerts  = EnableSectionWarningAlerts;
        s.Alerts.SectionWarningThreshold     = SectionWarningThreshold;
        s.Alerts.EnableSessionWarningAlerts  = EnableSessionWarningAlerts;
        s.Alerts.SessionWarningThreshold     = SessionWarningThreshold;
        s.Alerts.EnableSectionEndAlerts      = EnableSectionEndAlerts;
        s.Alerts.EnableSessionEndAlerts      = EnableSessionEndAlerts;
        s.Alerts.EnableOvertimeAlerts        = EnableOvertimeAlerts;
        s.Alerts.EnableOverlayPulse          = EnableOverlayPulse;
        s.Alerts.EnableSoundAlerts           = EnableSoundAlerts;
        s.Alerts.EnableWindowsNotifications  = EnableWindowsNotifications;
        s.Alerts.AlertMessageDurationSeconds = AlertMessageDurationSeconds;

        // Hotkeys
        s.Hotkeys.Enabled                 = HotkeysEnabled;
        s.Hotkeys.PauseResume             = PauseResumeHotkey;
        s.Hotkeys.NextSection             = NextSectionHotkey;
        s.Hotkeys.PreviousSection         = PreviousSectionHotkey;
        s.Hotkeys.ResetSession            = ResetSessionHotkey;
        s.Hotkeys.ShowHideOverlay         = ShowHideOverlayHotkey;
        s.Hotkeys.ExtendSectionOneMinute  = ExtendSectionOneMinuteHotkey;
    }

    private void LoadMonitorSelection(string? savedDeviceName, int legacyMonitorIndex)
    {
        var monitorSnapshot = _windowPlacementService.GetAvailableMonitors();
        AvailableMonitors = monitorSnapshot
            .Select((monitor, index) => new MonitorSelectionOption
            {
                DeviceName = monitor.DeviceName,
                DisplayName = monitor.IsPrimary
                    ? $"{index}: {monitor.DeviceName} (Primary)"
                    : $"{index}: {monitor.DeviceName}"
            })
            .ToList();

        string? resolvedDeviceName = null;

        if (!string.IsNullOrWhiteSpace(savedDeviceName))
        {
            resolvedDeviceName = AvailableMonitors
                .FirstOrDefault(m => string.Equals(m.DeviceName, savedDeviceName, StringComparison.OrdinalIgnoreCase))
                ?.DeviceName;
        }

        if (resolvedDeviceName is null && legacyMonitorIndex >= 0 && legacyMonitorIndex < AvailableMonitors.Count)
            resolvedDeviceName = AvailableMonitors[legacyMonitorIndex].DeviceName;

        if (resolvedDeviceName is null)
        {
            resolvedDeviceName = monitorSnapshot.FirstOrDefault(m => m.IsPrimary)?.DeviceName
                               ?? AvailableMonitors.FirstOrDefault()?.DeviceName;
        }

        SelectedMonitorDeviceName = resolvedDeviceName;
    }

    private string? ResolveMonitorDeviceNameForSave()
    {
        if (!string.IsNullOrWhiteSpace(SelectedMonitorDeviceName))
            return SelectedMonitorDeviceName;

        if (Monitor >= 0 && Monitor < AvailableMonitors.Count)
            return AvailableMonitors[Monitor].DeviceName;

        return null;
    }
}
