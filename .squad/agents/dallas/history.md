# Dallas — History

## Seed
- Project: ElBruno.PresenterTimer — .NET 10 WPF "Session Timeline Overlay".
- PRD: docs/SessionTimelineOverlay_PRD.md.
- Requested by: elbruno.

## Learnings

### Phase 1 — App Shell + Systray + Settings (2026-06-02)

#### Tray approach: UseWindowsForms + NotifyIcon
- Added `<UseWindowsForms>true</UseWindowsForms>` alongside `<UseWPF>true</UseWPF>` in the csproj.
- `TrayIconService` uses `System.Windows.Forms.NotifyIcon` + `ContextMenuStrip` (no native WPF systray API).
- WinForms interop initialised in `App.OnStartup` via `System.Windows.Forms.Application.EnableVisualStyles()`.
- `ShutdownMode = ShutdownMode.OnExplicitShutdown` so no main window auto-shuts the app.
- WPF `Application` disambiguated with `using Application = System.Windows.Application;` in App.xaml.cs because both WPF and WinForms export an `Application` class.

#### Icon state generation (no external assets)
- Each `TrayState` value maps to a `Color` in `TrayIconService.StateColors`.
- `CreateColoredIcon(Color)` draws a filled 16×16 circle via `System.Drawing.Graphics.FillEllipse` onto a `Bitmap`, then calls `bmp.GetHicon()` to get a `System.Drawing.Icon`.
- Colors: Gray=NoSession, RoyalBlue=Loaded, SeaGreen=Running, Goldenrod=Warning, Crimson=Overtime, SlateBlue=Paused.
- Old `Icon` object is disposed on each state change to avoid GDI handle leaks.

#### AppSettings shape + defaults file path
- File: `%AppData%\ElBruno.PresenterTimer\settings.json`
- Root class: `AppSettings` (Models\AppSettings.cs), composed of:
  - `GeneralSettings` — launch minimized, remember session, auto-load, preview after import, confirm on reset/exit
  - `BehaviorSettings` — overlay auto-show, hide on end, auto-advance, overtime counting, hotkeys, click-through, lock-pause
  - `OverlayStyleSettings` — theme, accent/warning/overtime colors, opacity levels, font, border radius, show/hide flags
  - `OverlayLayoutSettings` — overlay mode, position, monitor, width fraction, height, drag/snap, custom X/Y
  - `AlertSettings` — warning thresholds (hh:mm:ss strings), alert enable flags, sound/notifications, message duration
  - `HotkeySettings` — enabled flag + suggested key strings (all disabled by default)
- `SettingsService.Load()` creates the file with defaults on first run (fallback to defaults on corrupt JSON per PRD §10.3).

#### Tray menu handler method names (ready for wiring)
All handlers are in `TrayIconService.cs` as private methods. Stubs reference the phase that owns them:
- `OnStartSession` → TODO Phase 2 — `ISessionTimerService.Start()`
- `OnPauseSession` → TODO Phase 2 — `ISessionTimerService.PauseOrResume()`
- `OnResetSession` → TODO Phase 2 — `ISessionTimerService.Reset()`
- `OnNextSection` → TODO Phase 2 — `ISessionTimerService.NextSection()`
- `OnPreviousSection` → TODO Phase 2 — `ISessionTimerService.PreviousSection()`
- `OnRestartCurrentSection` → TODO Phase 2 — `ISessionTimerService.RestartCurrentSection()`
- `OnExtendOneMinute` / `OnExtendFiveMinutes` → TODO Phase 2 — `ISessionTimerService.ExtendCurrentSection(TimeSpan)`
- `OnImportSessionJson` → TODO Phase 3 — `IFileDialogService.OpenJsonFile()` + `ISessionLoader.Load()`
- `OnReloadLastSession` → TODO Phase 3 — reload from settings last-path
- `OnRecentSessions` → TODO Phase 3 — build sub-menu from settings
- `OnExportSampleJson` → TODO Phase 3 — write sample JSON via `IFileDialogService.SaveJsonFile()`
- `OnShowTimeline` / `OnHideTimeline` / `OnShowHideOverlay` → TODO Phase 4 — overlay window show/hide
- `OnOpenSessionPreview` → TODO Phase 5
- `OnOpenSessionSummary` → TODO Phase 6
- `OnOpenSettings` → TODO Phase 8 — `SettingsWindow.Show()`
- `OnAbout` → TODO Phase 9

#### Clean exit
- `OnExit` in `TrayIconService`: sets `_notifyIcon.Visible = false` (removes icon immediately), calls `Dispose()`, then `System.Windows.Application.Current.Shutdown()`.
- `App.OnExit` also calls `_trayIconService?.Dispose()` as a safety net.

#### Gotcha: missing System.IO in XAML temp project
- When `UseWindowsForms=true` is combined with `UseWPF=true` in `Microsoft.NET.Sdk`, the auto-generated `GlobalUsings.g.cs` for the XAML compilation temp project does not include `System.IO`.
- Fix: added `GlobalUsings.cs` at project root with `global using System.IO;` and `global using System.Net.Http;`. This file is included in both the main and temp compilation passes.

---

## Learnings

### Phase 4 — Session Preview Window + Import→Validate→Preview Flow (2026-06-02)

#### Preview ViewModel binding surface

- `SessionPreviewViewModel` derives from `ViewModelBase`; all display properties use `SetProperty`.
- `SectionRowViewModel` is a plain `sealed class` (no INPC) because `Sections` is an
  `ObservableCollection<SectionRowViewModel>` that is cleared and re-populated wholesale when
  "Import Different JSON" completes — individual row mutation is not needed.
- Properties: `Title`, `Description`, `TotalDurationDisplay`, `SectionCount`, `Sections`,
  `ValidationSummary`, `HasValidationIssues`.
- Commands: `StartSessionCommand`, `CancelCommand`, `ImportDifferentJsonCommand`,
  `ExportNormalizedJsonCommand`.
- Window close is signalled via `event Action? RequestClose` (the window code-behind has zero logic).

#### FileDialogService API

- `FileDialogService : IFileDialogService` in `Services\FileDialogService.cs`.
- `ShowOpenJsonDialog()` → `OpenFileDialog` (single JSON file, must exist).
- `ShowSaveJsonDialog(string? defaultFileName)` → `SaveFileDialog` (JSON, optional suggested name).
- Both run safely on the WPF STA thread thanks to `<UseWindowsForms>true</UseWindowsForms>`.
- `IFileDialogService` interface extended with `ShowSaveJsonDialog` to support the Export flows.

#### Import → Validate → Preview flow (TrayIconService.LoadSessionFromPath)

1. `IFileDialogService.ShowOpenJsonDialog()` → user picks a file (or cancels).
2. `ISessionLoaderService.Load(path)` → `SessionPlan`; any `SessionLoadException` → friendly `MessageBox.Show`.
3. `ISessionValidationService.Validate(plan)` → if `!IsValid`, show error dialog and **stop** (preview never opens).
4. Persist `LastSessionPath` + `RecentSessionPaths` (capped at 10, deduped) via `ISettingsService.Save()`.
5. `SetState(TrayState.Loaded)` → tray icon turns blue.
6. `Dispatcher.BeginInvoke(() => new SessionPreviewWindow { DataContext = vm }.Show())` — marshals to WPF thread.

#### "StartLoadedSession" hook for the timer

- `TrayIconService.OnSessionStartRequested(SessionPlan plan)` — private method, clearly marked
  `// TODO Phase 5`.  Passed as `onStartSession` delegate to `SessionPreviewViewModel`.
- `App.StartLoadedSession(SessionPlan plan)` — public static method on `App`; also marked
  `// TODO Phase 5`. Parker connects `ISessionTimerService.Start(plan)` at either point in the
  next sprint without hunting for the wiring site.

#### Recent/last-session persistence

- `GeneralSettings.LastSessionPath: string?` — overwritten on every successful import.
- `GeneralSettings.RecentSessionPaths: List<string>` — newest-first, max 10.  Same path is
  removed before inserting so re-imports just bubble the entry to index 0.
- Both fields are JSON-serialised transparently by `SettingsService` (no migration needed because
  missing fields default to `null` / empty list on first deserialisation).

#### ISettingsService extended

- Added `AppSettings Settings { get; }` to `ISettingsService` so services that need settings
  (TrayIconService, SessionPreviewViewModel) depend only on the abstraction.  `SettingsService`
  already exposed a public get; satisfying the interface required no implementation change.

---

## Learnings

### Phase 6 — Full Timeline Overlay + Live Session End-to-End (2026-06-02)

#### Overlay VM binding surface

- `TimelineOverlayViewModel` derives from `ViewModelBase`; all display strings are `string` properties
  (`CurrentSectionElapsedDisplay`, `CurrentSectionRemainingDisplay`, `SessionElapsedDisplay`,
  `SessionRemainingDisplay`, `OvertimeDisplay`, etc.) updated on each ~250 ms `Tick`.
- `IsPaused`, `IsRunning`, `IsSessionOvertime`, `IsSectionWarning` are `bool` properties
  driving `DataTrigger`s in XAML (badges, borders, colors).
- `OverlayOpacity` (double 0–1) is read from `AppSettings.OverlayStyle.OverlayOpacity / 100.0`
  and bound to `Window.Opacity`.
- `Sections: ObservableCollection<OverlaySectionViewModel>` — populated once in the ctor,
  mutated in-place on every tick (no clear/re-add).

#### Proportional layout technique

- Each `OverlaySectionViewModel` carries `DurationFraction` (double, 0–1 of total session).
- The overlay code-behind calls `vm.UpdateSectionWidths(TimelineBar.ActualWidth)` on `Loaded`
  and `SizeChanged`; the VM writes `PixelWidth = availableWidth × DurationFraction` on each section.
- XAML `ItemTemplate` binds each segment `Grid.Width="{Binding PixelWidth}"` directly — no converter,
  no custom panel.
- `ProgressWidth = PixelWidth × ProgressFraction` is also a computed property on
  `OverlaySectionViewModel` (re-fired via `RefreshProgressWidth()` whenever either input changes),
  bound to `Rectangle.Width` for the elapsed-fill marker.

#### Timer events → visual states + tray colors

| Condition | Section `State` | Tray |
|---|---|---|
| `i < CurrentSectionIndex` | `Completed` (dimmed, no fill) | — |
| `i > CurrentSectionIndex` | `Upcoming` (low opacity) | — |
| current, `IsSectionOvertime` | `Overtime` (red border+bg) | Crimson |
| current, `remaining ≤ warningAt/threshold` | `Warning` (amber) | Goldenrod |
| current, normal | `Current` (blue, white border) | SeaGreen |
| `IsPaused` (StateChanged) | overlay shows ⏸ badge | SlateBlue |

- Timer events fire on a thread-pool thread; all XAML updates use `_dispatcher.BeginInvoke`.
- Tray updates use `Interlocked.Exchange` on `_lastTrayStateOrdinal` to suppress redundant
  `SetState` calls on every 250 ms tick — only transitions dispatch a `BeginInvoke`.

#### StartLoadedSession integration

- `App.StartLoadedSession(SessionPlan)` is the single entry-point for a new session:
  1. Dispose old `SessionTimerService` + close overlay.
  2. `new SessionTimerService()` → `LoadPlan(plan)` → subscribe `Tick`/`StateChanged`.
  3. `_trayIconService.SetTimerService(_timerService)` — wires all tray menu handlers.
  4. Create `TimelineOverlayViewModel` + `TimelineOverlayWindow`, call `PositionOverlay`.
  5. Set `ShowOverlayAction`, `HideOverlayAction`, `ToggleOverlayAction` callbacks on the tray service.
  6. Conditionally `_overlayWindow.Show()` if `BehaviorSettings.ShowOverlayWhenSessionStarts`.
  7. `_timerService.Start()` → `SetState(TrayState.Running)`.
- `TrayIconService.OnSessionStartRequested` now calls `App.StartLoadedSession(plan)` —
  the TODO hook is fully resolved.

#### Overlay window callbacks pattern (Services → Views decoupling)

- `TrayIconService` exposes `Action? ShowOverlayAction`, `HideOverlayAction`, `ToggleOverlayAction`.
- `App.StartSessionInternal` sets these to lambdas that capture `_overlayWindow`.
- This avoids a Services → Views namespace dependency while keeping `TrayIconService` testable.

#### Overlay position persistence

- `TimelineOverlayWindow.OnLocationChanged` calls `vm.SavePosition(Left, Top)`.
- `TimelineOverlayViewModel.SavePosition` fires an `onPositionChanged: Action<double, double>`
  delegate injected via constructor.
- `App.OnOverlayPositionChanged` writes `OverlayLayoutSettings.CustomX/Y` and calls
  `ISettingsService.Save()`.
- `App.PositionOverlay` checks `RememberCustomPosition && CustomX.HasValue` on session start
  to restore the saved position; otherwise falls back to the named position ("TopCenter" etc.)
  computed from the selected monitor's bounds and `WidthFraction`.

---

## Learnings

### Phase 8 — Settings UI (2026-06-02)

#### SettingsWindow / ViewModel structure

- `SettingsViewModel` derives from `ViewModelBase`; all editable fields use `SetProperty`.
- The VM holds a **working copy** of all settings: `LoadFromSettings(AppSettings)` populates it
  at construction; `ApplyToSettings(AppSettings)` writes it back on Save/Apply.
  Cancel discards the working copy without touching `ISettingsService.Settings`.
- Static `IReadOnlyList<string>` option lists (`ThemeOptions`, `FontSizeOptions`, etc.) are exposed
  as static properties so XAML can bind them via `{x:Static vm:SettingsViewModel.XOptions}`.
- Commands: `SaveCommand` (apply + close), `CancelCommand` (close only), `ApplyCommand`
  (apply without close), `ResetToDefaultsCommand` (reload defaults into form),
  `OpenSettingsFolderCommand` (Explorer.exe %AppData%\ElBruno.PresenterTimer),
  `ExportSettingsCommand` (copy settings.json to user path via `IFileDialogService.ShowSaveJsonDialog`),
  `ImportSettingsCommand` (copy from user path + reload via `ISettingsService.Load`).
- `event Action? RequestClose` — the code-behind subscribes and calls `window.Close()` so the VM
  never directly references the `Window` type.

#### Settings-changed / live-apply mechanism

- `ISettingsService` gains `event EventHandler? SettingsApplied` and `void RaiseSettingsApplied()`.
  `RaiseSettingsApplied()` is **not** called in every `Save()` — positional auto-saves (drag) do not
  trigger it. Only `SettingsViewModel.ExecuteApply()` calls it, keeping overlay re-application
  scoped to deliberate user actions.
- `SettingsService` implements the event/method with a trivial `EventHandler?.Invoke`.
- `App.OnStartup` subscribes `_settingsService.SettingsApplied += OnSettingsApplied`.
- `App.OnSettingsApplied` checks `_overlayWindow?.DataContext is TimelineOverlayViewModel` and
  calls `vm.ApplyStyleSettings(settings.OverlayStyle)`.
- `TimelineOverlayViewModel.ApplyStyleSettings` marshals to the WPF dispatcher and updates
  `OverlayOpacity` (changed `private set` → `set`).  Additional style properties (colors, font)
  can be wired the same way in later phases.

#### Single-instance settings window

- `App` owns `_settingsWindow: SettingsWindow?` (field, null when closed).
- `OpenSettingsWindow()` (called via `TrayIconService.OpenSettingsAction`) checks `IsLoaded`; if
  already open it calls `Activate()`, else creates a fresh `SettingsWindow` + `SettingsViewModel`.
- `_settingsWindow.Closed` handler resets the field to `null` so the next open call creates a new
  instance (avoids stale-window issues).
- `TrayIconService.OpenSettingsAction: Action?` follows the same Action-callback pattern as
  `ShowOverlayAction/HideOverlayAction` — tray service stays free of Views dependencies.

#### Export / Import approach

- Export: `ApplyToSettings` + `Save()` first (so file reflects form state), then
  `File.Copy(settingsFilePath, userChosenPath, overwrite: true)`.
- Import: `File.Copy(userChosenPath, settingsFilePath, overwrite: true)`, then
  `ISettingsService.Load()` and `LoadFromSettings(_settingsService.Settings)` to refresh the form.
- Both use `IFileDialogService.ShowSaveJsonDialog` / `ShowOpenJsonDialog` — no new service needed.

#### AppSettings groups now editable in Settings UI

All six groups have full round-trip coverage in `SettingsWindow`:
- **General** — startup/session/confirmation flags (tab: General)
- **Behavior** — overlay auto-show, timer flags, click-through, hotkeys toggle (tab: Behavior)
- **OverlayStyleSettings** — theme, hex colors, opacity sliders ×4, font combo, visibility flags (tab: Overlay Style)
- **OverlayLayoutSettings** — mode/position/monitor combos, width fraction slider, height, drag/snap (tab: Overlay Layout)
- **AlertSettings** — warning thresholds, enable flags, sound/notifications, message duration (tab: Alerts)
- **HotkeySettings** — enabled flag + six key-string textboxes (tab: Hotkeys)
- An **Advanced** tab surfaces click-through, overtime edge-cases, and position-memory flags for quick access.

---

## Learnings

### Phase 7 UI — Alert Engine Integration (2026-06-02)

#### App wiring (App.xaml.cs)

- Three new fields: `AlertService? _alertService`, `SoundAlertService? _soundAlertService`,
  `SystemNotificationService? _notificationService`.
- A `SoundAlertService` is also created at `OnStartup` (app-lifetime) for the Settings test-sound
  button, which must work before any session is started.
- In `StartSessionInternal`:
  1. Call `TearDownAlertServices()` before creating new ones (handles re-starts cleanly).
  2. `new AlertService(settings.Alerts)` → `Attach(_timerService)` → subscribe `AlertRaised`.
  3. `new SoundAlertService(settings.Alerts)` — also overwrites the app-startup reference.
  4. `new SystemNotificationService(settings.Alerts, _trayIconService.NotifyIcon)` — reuses the
     existing tray `NotifyIcon` to avoid a second notification-area entry.
- `OnAlertRaised` (thread-pool thread):
  - Calls `overlayVm.TriggerAlert(e, durationSeconds, enablePulse)` — always; marshals internally.
  - Tray color is already kept up-to-date by `OnTimerTick`; no duplicate `SetState` call needed.
  - `ShouldPlaySound` → switch on `AlertType` to call the matching `SoundAlertService.Play*`.
  - `ShouldShowNotification` → `SystemNotificationService.Notify(alertType.ToString(), message)`.
- `TearDownAlertServices()`: unsubscribes `AlertRaised`, calls `Detach()` + `Dispose()` on
  `_alertService`; calls `Dispose()` on `_notificationService`; nulls `_soundAlertService` (no
  `IDisposable` on that service). Called on `StartSessionInternal` re-start AND `OnExit`.

#### TrayIconService — NotifyIcon exposure

- Added `public NotifyIcon? NotifyIcon => _notifyIcon;` so `App` can pass it to
  `SystemNotificationService` without creating a second tray entry.

#### Overlay VM — transient alert message + pulse event (TimelineOverlayViewModel)

- `string AlertMessage` / `bool IsAlertMessageVisible` — new INPC properties.
- `event EventHandler? PulseRequested` — fired on the WPF dispatcher when an alert arrives and
  `AlertSettings.EnableOverlayPulse` is `true`. Code-behind subscribes and starts the storyboard.
- `DispatcherTimer _alertMessageTimer` — initialized in constructor (DispatcherTimer captures
  the WPF dispatcher automatically when created on the UI thread); restarts on each call to
  `TriggerAlert`; clears `IsAlertMessageVisible` and `AlertMessage` on tick.
- `TriggerAlert(AlertEventArgs e, int durationSeconds, bool enablePulse)`:
  - `BeginInvoke`-marshals to dispatcher.
  - Sets message + visibility, fires `PulseRequested` if `enablePulse`, restarts timer.
- `Dispose()` now also calls `_alertMessageTimer.Stop()` and unhooks the Tick handler.

#### Overlay XAML — pulse flash + alert message (TimelineOverlayWindow.xaml / .xaml.cs)

- **Structural change**: root content changed from `<Border>` to `<Grid>` that stacks:
  1. `RootBorder` (existing draggable shell).
  2. `PulseFlash` — a `Rectangle` with `IsHitTestVisible="False"` and initial `Opacity="0"` that
     floats on top; its opacity is animated by the pulse storyboard.
- **Pulse storyboard** (`PulseStoryboard` in `Window.Resources`): 4-keyframe
  `DoubleAnimationUsingKeyFrames` on `PulseFlash.Opacity` — 0→0.38→0.08→0.28→0.00 over 500 ms
  (double flash, then fades).  The code-behind calls `_pulseStoryboard.Begin(this, HandoffBehavior.SnapshotAndReplace)` 
  so rapid successive alerts each get a fresh run.
- **Alert message row**: `<Border>` inserted between the info line and the timeline bar in
  `RootBorder`'s `StackPanel`. Visibility is driven by a `DataTrigger` on `IsAlertMessageVisible`.
  Collapsed by default; shows amber `#FFE082` `TextBlock` with the alert text.
- **Code-behind pattern**: `DataContextChanged` subscribes/unsubscribes `PulseRequested` cleanly
  when the VM is swapped (e.g. new session). Storyboard resolved from resources in the `Loaded`
  handler (resource must exist after `InitializeComponent()`).

#### Settings — Test Sound button (SettingsViewModel / SettingsWindow.xaml)

- `SettingsViewModel` constructor gains optional `Action? playTestSound = null` parameter.
  Backwards-compatible (default null = button is a no-op if no sound service is injected).
- `ICommand TestSoundCommand` — backed by `new RelayCommand(() => playTestSound?.Invoke())`.
- `App.OpenSettingsWindow` passes `soundService.PlayTestSound` as the delegate, using
  `_soundAlertService ?? new SoundAlertService(_settingsService.Settings.Alerts)` so the
  button is always wired even before the first session starts.
- `SettingsWindow.xaml` Alerts tab: "🔊 Test Sound" button bound to `TestSoundCommand`, below
  the Alert Message Duration section, with a tooltip explaining it ignores the enabled toggle.

#### Teardown correctness

| Event | Action |
|---|---|
| New session starts | `TearDownAlertServices()` → unsubscribe/detach/dispose old ones |
| App exits | `TearDownAlertServices()` then `_timerService.Dispose()` |
| Reset (via timer) | `AlertService.OnSectionChanged(reason: Reset)` auto-clears dedup internally |

---

## Learnings

### Phase Final — Final Integration (2026-06-02)

#### Summary-on-end wiring (PRD §8.5 / §7.14)

- Added ShowSummaryOnSessionEnd: bool = true to GeneralSettings (AppSettings.cs).
- App.OnTimerStateChanged now has an IsSessionComplete branch that:
  1. Captures _lastSessionResult = _timerService.GetResult().
  2. Calls _overlayWindow?.Hide() when BehaviorSettings.HideOverlayWhenSessionEnds.
  3. Calls ShowSessionSummary(_lastSessionResult) when GeneralSettings.ShowSummaryOnSessionEnd.
- ShowSessionSummary(SessionResult) uses Kane's documented hook: 
ew SessionSummaryViewModel(result, _fileDialogService) → 
ew SessionSummaryWindow() → win.SetViewModel(vm) → win.Show(), all marshalled via Dispatcher.BeginInvoke.
- _lastSessionResult stored as an App field; OpenLastSessionSummary() action wired to TrayIconService.OpenSessionSummaryAction shows the last result or a friendly message if none.

#### Recent sessions menu + missing-file handling (PRD §7.16)

- RecentSessionsService (Parker Phase 10) injected into TrayIconService as a new constructor parameter.
- TrayIconService.LoadSessionFromPath replaced manual AddToRecentSessions() helper with _recentSessionsService.Add(path).
- "Recent Sessions" tray item changed from a click-handler stub to a ToolStripMenuItem with DropDownOpening event; the handler calls _recentSessionsService.GetExisting() to rebuild the submenu dynamically — only shows files that exist.
- Clicking a recent item: LoadRecentSession(path) checks Exists(path) first → if missing, shows friendly message + Remove(path) → no crash.
- OnReloadLastSession: checks Exists(path) → if missing, shows message + removes entry + clears LastSessionPath.
- RecentSessionsService also instantiated in App.OnStartup (shared instance).

#### Overlay placement integration (PRD §7.7 / §7.18)

- WindowPlacementService (Parker Phase 10) instantiated in App.OnStartup.
- PositionOverlay(window) now calls ResolveMonitor(layout.MonitorDeviceName) (falls back to primary if saved device is disconnected), then ResolvePlacement(position, monitor, size, customX, customY), then ClampToWorkingArea — window is guaranteed to stay within the working area.
- If RememberCustomPosition && CustomX/Y are set, treats position as OverlayPosition.Custom.
- App.OnSettingsApplied now also calls PositionOverlay(_overlayWindow) so layout settings changes apply live.

#### About window (PRD §7.1)

- New files: Views/AboutWindow.xaml + Views/AboutWindow.xaml.cs.
- Displays: app name, version (from Assembly.GetExecutingAssembly().GetName().Version), one-line description, Hyperlink to the GitHub repo (opens via Process.Start + UseShellExecute).
- TrayIconService.OpenAboutAction: Action? callback wired from App.OnStartup to OpenAboutWindow() which marshals to WPF dispatcher and calls 
ew AboutWindow().Show().
- Follows the same Action? callback pattern already used for OpenSettingsAction.

#### Auto-load-on-startup (PRD §7.12 / §7.16)

- In App.OnStartup, when GeneralSettings.AutoLoadLastSessionOnStartup is 	rue:
  1. Reads LastSessionPath.
  2. Calls _recentSessionsService.Exists(path) — if file is gone, removes entry + clears path silently.
  3. If file exists: loads + validates; opens SessionPreviewWindow via Dispatcher.BeginInvoke (matches normal import UX).
  4. Any exception is swallowed (non-fatal auto-load failure must not crash the app).

#### Build / test status

- dotnet build → **0 errors** (pre-existing xUnit analyzer warnings only, unchanged).
- dotnet test → **294 passed, 0 failed**.

---

## Learnings

### Accessibility Pass — PRD §10.4 (2026-06-02)

#### No-color-only signaling (PRD §10.4 rule 1)

- **Overtime badge** (`TimelineOverlayWindow.xaml`): changed from a `+01:42` time-only label to
  `"OVERTIME " + OvertimeDisplay` (two TextBlocks inside the badge Border). The word "OVERTIME" is
  always visible alongside the time offset, so overtime is readable without relying on red background.
- **Warning badge** (new, `TimelineOverlayWindow.xaml`): added a second badge in the info-line
  WrapPanel that shows `"⚠ WARNING"` in orange (`#E65100`) when `IsSectionWarning=true`. Previously
  the warning state was only signalled by the amber timeline-bar segment color.
- **Timeline-bar state glyphs** (`TimelineOverlayWindow.xaml`): added a 4th TextBlock layer inside
  each segment's Grid (top-right corner, FontSize=8). Shows `"▶"` for Current, `"⚠"` for Warning,
  `"!"` for Overtime, invisible otherwise. These are shape/glyph cues that supplement the color change.

#### Text labels for states (PRD §10.4 rule 2)

- Paused badge already carried `"⏸ PAUSED"` text — no change needed.
- Overtime badge upgraded to `"OVERTIME +mm:ss"` (see above).
- Section warning gets its own `"⚠ WARNING"` badge (see above).

#### AutomationProperties / screen-reader names (PRD §10.4 rule 3)

Added `AutomationProperties.Name` (and `AutomationProperties.LiveSetting` where appropriate) to:
- **TimelineOverlayWindow.xaml**: SessionTitle, CurrentSectionTitle, CurrentSectionRemainingDisplay
  (all with `LiveSetting="Polite"`); Overtime badge (`LiveSetting="Assertive"`); Paused badge
  (`LiveSetting="Assertive"`); Warning badge (`LiveSetting="Assertive"`); TimelineBar ItemsControl.
- **SessionPreviewWindow.xaml**: Window title TextBlock, sections ListView, validation Border
  (`LiveSetting="Polite"`), Cancel and Start Session buttons.
- **SessionSummaryWindow.xaml**: StatusMessage TextBlock (`LiveSetting="Polite"`); Close button
  (already had AutomationProperties; added `IsCancel="True"`).
- **SettingsWindow.xaml**: all four opacity Sliders, width-fraction Slider, Cancel button, Test Sound
  button.
- **AboutWindow.xaml**: app title TextBlock, version TextBlock (self-referential binding), Close button.

#### Keyboard (PRD §10.4 rule 5)

- `IsCancel="True"` added to Cancel/Close buttons in: `SessionPreviewWindow`, `SessionSummaryWindow`,
  `SettingsWindow`, `AboutWindow`. Pressing Esc now activates the close/cancel action in all dialogs.
- `IsDefault="True"` on the Save button in `SettingsWindow` was already present — no change needed.

#### Build / test status

- `dotnet build` → **0 errors** (pre-existing xUnit analyzer warnings only, unchanged).
- `dotnet test` → **311 passed, 0 failed**.
