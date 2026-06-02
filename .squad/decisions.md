# Squad Decisions

## Accepted Decisions (Round 1)

### Decision: Project Published to GitHub
**Date:** 2026-06-02
**Status:** APPROVED
**Summary:** Project published to private GitHub repo [elbruno/ElBruno.PresenterTimer](https://github.com/elbruno/ElBruno.PresenterTimer) under MIT license.
**Details:** Repository initialized with MIT license, comprehensive README.md, and expanded .gitignore. All 119 project files committed and pushed to origin/main.
### Decision: Phase 0 — Solution & Project Scaffolding

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Ripley (Lead/Architect)  
**Status:** Accepted

**Context:** Phase 0 establishes the foundation on which all subsequent phases build. The decisions here set the solution layout, target frameworks, MVVM infrastructure, and test harness for the entire project.

**Decisions:**
- Solution layout: `ElBruno.PresenterTimer.sln` with `src/ElBruno.PresenterTimer/` (WPF app) and `tests/ElBruno.PresenterTimer.Tests/` (xUnit tests)
- Classic `.sln` format (not `.slnx`) for maximum IDE and CI compatibility
- Target frameworks: `net10.0-windows` for both app and tests (tests require WPF for `RelayCommand` / `CommandManager`)
- Test framework: xUnit 2.9.x with five sanity tests for `ViewModelBase` and `RelayCommand`
- MVVM base: `ViewModelBase` (abstract, `INotifyPropertyChanged`) and `RelayCommand` (sealed, `ICommand`)
- Six service abstractions defined in `Abstractions/` (to be fleshed out in their respective phases)
- Root namespace: `ElBruno.PresenterTimer`
- App startup: `App.xaml` retains `StartupUri="MainWindow.xaml"` in Phase 0; Phase 1 will switch to tray-only

**Outcome:** Build succeeded, tests passed (5/5).

---

### Decision: Session Models, Validation Contract, and Loader Error Handling

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Parker (Backend Dev)  
**Status:** Accepted

**Context:** Phase 2 implements data models and services to import, parse, and validate session timeline plans from JSON.

**Decisions:**
- Model shapes: `SessionPlan`, `SessionSection`, `SessionMetadata`, `ValidationResult`
- TimeSpan serialization: `"HH:mm:ss"` format via `TimeSpanJsonConverter` and `NullableTimeSpanJsonConverter`
- Validation never throws; returns `ValidationResult` with zero or more error messages
- Validation rules enforce: non-empty title, ≥1 section, section titles, duration > 0, valid hex colors, warningAt < duration, total duration > 0
- Loader error handling: all errors surface as `SessionLoadException` (dedicated exception subclass)
- UI layers catch `SessionLoadException` and display `ex.Message` to user

**Outcome:** Build succeeded, 0 errors, 0 warnings.

---

### Decision: App Shell, Systray & Settings (Phase 1)

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Dallas (UI Dev)  
**Status:** Accepted

**Context:** Phase 1 delivers app shell, system tray integration, and settings persistence.

**Decisions:**
- Tray technology: `System.Windows.Forms.NotifyIcon` + `ContextMenuStrip` via `UseWindowsForms` (zero additional NuGet deps)
- Settings persistence: `%AppData%\ElBruno.PresenterTimer\settings.json`, System.Text.Json, indented
- Startup: `App.OnStartup` sets `ShutdownMode=OnExplicitShutdown`, calls `SettingsService.Load()`, then `TrayIconService.Initialize()`
- Shutdown: User clicks **Exit** → `TrayIconService.OnExit()` → icon hidden → `Dispose()` → `Application.Current.Shutdown()`
- Icon states: 16×16 `Bitmap` with programmatically rendered circles (Gray, RoyalBlue, SeaGreen, Goldenrod, Crimson, SlateBlue)
- Menu stubs: All handlers present but empty, each with `// TODO Phase N —` comment for later wiring

---

### Decision: Sample Test Data for Session Timeline Overlay

**Date:** 2026-06-02T06:23:37-04:00  
**Agent:** Lambert (Tester)  
**Status:** Completed

**Context:** Phase 3 delivers six sample session JSON files to support testing.

**Decisions:**
- Five valid files: short-demo.json, podcast.json, conference-talk.json, workshop.json, ai-agents-demo.json
- One invalid file: invalid-warning-exceeds-duration.json (warningAt > duration)
- Files cover MVP-only format, extended format with metadata/colors/warnings
- ai-agents-demo.json is exact reproduction of PRD §17 example
- Sample documentation in `samples/README.md`

**Outcome:** All 6 files created with valid JSON syntax; covers MVP, extended, and error scenarios.

## Accepted Decisions (Round 2)

### Decision: Timer Engine — API Contract, Monotonic Approach, Summary-Data Model (Phase 5)

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Parker (backend)  
**Status:** Implemented ✅

**Context:** Phase 5 delivers the backend timer engine that drives a `SessionPlan` through time.

**Key Decisions:**
- Monotonic timing via `System.Diagnostics.Stopwatch` with accumulating base
- Full `ISessionTimerService` API with controls, events, and properties
- `SessionResult`/`SectionResult` models for phase 9
- Thread safety via single lock; events raised outside lock
- Auto-advance (settable, default false)

**Files:** `Models/SectionResult.cs`, `Models/SessionResult.cs`, `Abstractions/ISessionTimerService.cs`, `Abstractions/TimerTickEventArgs.cs`, `Abstractions/SectionChangedEventArgs.cs`, `Services/SessionTimerService.cs`

**Build Status:** ✅ 0 errors, 0 warnings; 49 tests pass

---

### Decision: Session Preview Window + Import JSON Flow (Phase 4)

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Dallas (UI Dev agent)  
**Status:** Implemented

**Context:** Phase 4 wires the import → validate → preview pipeline end-to-end.

**Key Decisions:**
- MVVM binding surface with `SessionPreviewViewModel` and `SectionRowViewModel`
- "Import Different JSON" updates preview in-place (no close/reopen)
- `FileDialogService` uses `System.Windows.Forms` dialogs safely on STA thread
- "Start Session" hook via `onStartSession` delegate (Phase 5 connection point)
- Invalid files never open preview (PRD §8.2 compliance)
- Recent sessions persistence: `LastSessionPath` + `RecentSessionPaths` (max 10)

**Files:** `ViewModels/SessionPreviewViewModel.cs`, `ViewModels/SectionRowViewModel.cs`, `Views/SessionPreviewWindow.xaml`, `Views/SessionPreviewWindow.xaml.cs`, `Services/FileDialogService.cs`

**Build Status:** ✅ 0 errors, 0 warnings; all tests pass

---

### Decision: Overlay Integration (Phase 6)

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Dallas (UI Dev)  
**Status:** Implemented

**Context:** Phase 6 is the key visible milestone: running session produces live timeline overlay.

**Key Decisions:**
- Proportional timeline segment widths via `PixelWidth` binding (code-behind calls `UpdateSectionWidths`)
- `ProgressWidth` computed property avoids MultiValueConverter
- Timer event → tray state via change-only updates with `Interlocked`
- Overlay callbacks on `TrayIconService` to avoid Services → Views dependency
- `StartLoadedSession` as static App method (full lifecycle owner)
- Overlay position persistence via settings

**Files:** `ViewModels/OverlaySectionViewModel.cs`, `ViewModels/TimelineOverlayViewModel.cs`, `Views/TimelineOverlayWindow.xaml`, `Views/TimelineOverlayWindow.xaml.cs`, `App.xaml.cs`

**Build Status:** ✅ 0 errors, 0 warnings; 109 tests pass

---

### Decision: AlertService Design — Phase 7 Backend

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Parker (Backend Dev)  
**Status:** Implemented

**Context:** Phase 7 requires alert detection and deduplication engine (PRD §7.8).

**Key Decisions:**
- Attach/Detach pattern (not constructor injection) for timer attachment
- `ProcessState(TimerTickEventArgs, SessionPlan)` as testable core
- Dedup via `Dictionary<int, HashSet<AlertType>>` (section-level + session-level buckets)
- Per-section `WarningAt` overrides global threshold
- Sound/notification as flags only (default off)
- `AlertType` and `AlertEventArgs` placed in `Abstractions\`
- Thread safety via `_lock`

**Files:** `Abstractions/AlertType.cs`, `Abstractions/AlertEventArgs.cs`, `Abstractions/IAlertService.cs`, `Services/AlertService.cs`, `AssemblyInfo.cs`

**Build Status:** ✅ 0 errors, 0 warnings; 49 tests pass

---

### Decision Note: Phase 11 Loader + Validation Tests

**Date:** 2026-06-02T06:23:37-04:00  
**Agent:** Lambert (Tester)  
**Status:** ✅ Complete — 44 tests, all pass

**Coverage:**
- JSON parsing (5 valid files + malformed/empty/null cases)
- TimeSpan converter round-trip
- `GetTotalDuration` across all samples
- Validation rules (PRD §7.4): title, sections, duration, colors, warnings

**Test Data Strategy:** Content items copied to output directory (no path-climbing).

**Bugs Found:** None. Production code correct.

**Build Status:** ✅ Total: 49 tests, all green

---

### Decision: Phase 11 Timer Tests — Approach and Findings

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Lambert (Tester)  
**Status:** Implemented — all tests green

**Approach:** Two-tier determinism:
- **Tier 1** (52 tests): Fully deterministic (navigation, state, events, accumulators)
- **Tier 2** (8 tests): Tolerance-based timing (section/session overtime, pause preservation, BehindSchedule) with ±150–200 ms tolerance

No tests skipped with `[Fact(Skip=...)]`.

**Bugs Discovered (not fixed):**
1. `CurrentSectionIndex` documented as `-1` pre-plan; actual is `0`
2. `ComputeBehindSchedule` reads `_clock.Elapsed` twice, creating phantom lag (~100–300 ns)

**Files:** `tests/ElBruno.PresenterTimer.Tests/SessionTimerServiceTests.cs` (60 tests)

**Build Status:** ✅ Total: 109 tests, all pass

---

## Accepted Decisions (Round 4–5)

### Decision: Phase 7 UI — Alert Engine Integration

**Agent:** Dallas (UI Dev)  
**Date:** 2026-06-02T06:23:37-04:00  
**Status:** Implemented

**Context:** Phase 7 UI wires Parker's alert services into the live application overlay for PRD §7.8 visual alert integration.

**Key Decisions:**
- AlertRaised handler on timer thread; no explicit Dispatcher.Invoke in App (overlay VM marshals internally)
- Tray color driven exclusively by OnTimerTick (avoids redundant state changes on alert)
- Pulse via event + storyboard in code-behind; PulseFlash rectangle overlaid on root Grid with IsHitTestVisible=false
- Alert message injected into existing StackPanel (not separate window); respects overlay SizeToContent
- SoundAlertService created at startup + per-session with fresh AlertSettings
- SettingsViewModel.TestSoundCommand accepts optional Action delegate for sound testing
- Focus protection: pulse storyboard non-focus-stealing; balloon tips inherently non-focus-stealing

**Build Status:** ✅ 250 tests pass

---

### Decision: Settings UI — Phase 8

**Author:** Dallas (UI Dev)  
**Date:** 2026-06-02T06:23:37-04:00  
**Status:** Implemented

**Context:** Phase 8 tabbed Settings window with Save/Apply/Cancel/Reset/Export/Import buttons, live overlay opacity updates.

**Key Decisions:**
- Working-copy pattern: SettingsViewModel owns editable backing fields; ApplyToSettings writes on Apply/Save only; Cancel closes without touching service
- SettingsApplied event (not every Save) to avoid noisy updates on positional auto-save
- Live overlay update via ApplyStyleSettings method; OverlayOpacity setter made public
- Single-instance settings window via App field + Action callback (TrayIconService.OpenSettingsAction)
- Export/Import via file copy (not serializer round-trip) to guarantee identical format
- ComboBox option lists as static properties with x:Static in XAML

**Files Changed:** ISettingsService, SettingsService, TimelineOverlayViewModel, SettingsViewModel (NEW), SettingsWindow (NEW), TrayIconService, App.xaml.cs

**Build Status:** ✅ 164 tests pass

---

### Decision: Phase 9 — Session Summary Window + App Hook Instructions

**Agent:** Kane (UI Dev 2)  
**Date:** 2026-06-02T06:23:37-04:00  
**Phase:** 9  
**Status:** Implemented

**Context:** Session Summary window (PRD §7.14) launched at session end with plain-text / Markdown / JSON export commands.

**Components Built:**
- Views/SessionSummaryWindow.xaml(.cs) — WPF summary window
- ViewModels/SessionSummaryViewModel.cs — MVVM VM with Copy/MD/JSON export commands
- Services/SummaryFormatter.cs — UI-agnostic plain-text / Markdown / JSON formatter
- ViewModels/SectionSummaryRowViewModel.cs — lightweight section row display model
- IFileDialogService.ShowSaveMarkdownDialog(string?) added + implemented

**App Wiring (for Dallas next round):**
```csharp
private void ShowSessionSummary()
{
    var result = _timerService.GetResult();
    _dispatcher.BeginInvoke(() =>
    {
        var vm  = new SessionSummaryViewModel(result, _fileDialogService);
        var win = new SessionSummaryWindow();
        win.SetViewModel(vm);
        win.Show();
    });
}
```

**Files Created:** SessionSummaryWindow, SessionSummaryViewModel, SectionSummaryRowViewModel, SummaryFormatter

---

### Decision: Phase 11 Alert Slice — AlertService Tests

**Author:** Lambert (Tester)  
**Date:** 2026-06-02T06:23:37-04:00  
**Status:** Implemented — 54 tests, all pass

**Context:** Comprehensive test suite for AlertService (Phase 7 backend) with dedup, settings gating, and event validation.

**Design Decisions:**
- ProcessState as sole seam for deterministic alert evaluation (clock-free snapshots)
- StubTimerService only for SectionChanged-driven dedup tests (4 bucket-clear + 7 ManualSectionChange tests)
- Each of 5 alert toggles tested independently with disabled setting
- No tests for Attach/Detach/Dispose internals (validated implicitly by stub-backed tests)
- No bugs found in AlertService

**Outcome:**
- File: AlertServiceTests.cs (54 tests)
- Suite total: 217 tests (250 after Parker/Dallas/Kane additions)
- All tests green

---

### Decision: Phase 8 — Timer Fixes + Output Alert Services

**Author:** Parker (Backend Dev)  
**Date:** 2026-06-02T06:23:37-04:00  
**Phase:** 8  
**Status:** Implemented, all tests green

**Context:** Two bugs in SessionTimerService (Lambert findings) + two new output services for alert sounds/notifications.

**Bug Fixes:**
1. **CurrentSectionIndex Contract:** Fixed to return -1 (pre-plan) not 0, matching documented interface contract. Implementation: field init to -1; ResetState uses conditional assignment.
2. **ComputeBehindSchedule Drift:** Snapshot ComputeSessionElapsed once; derive sectionElapsed inline (not via second call). Eliminates 100–300 ns phantom lag.

**New Services:**
- **SoundAlertService:** Uses System.Media.SystemSounds (non-blocking, maps to Windows system sounds). PlayTestSound always works regardless of IsEnabled flag. IsEnabled is live read of AlertSettings.
- **SystemNotificationService:** Uses System.Windows.Forms.NotifyIcon.ShowBalloonTip (non-focus-stealing, PRD §10.2 compliant). Constructor: `(AlertSettings, NotifyIcon?)` with fallback self-managed icon if null.

**Build Status:** ✅ Total 250 tests pass

---

### Decision: Phase 10 — RecentSessionsService + WindowPlacementService

**Author:** Parker (Backend Dev)  
**Date:** 2026-06-02T06:23:37-04:00  
**Status:** Implemented — 21+28 tests, all pass

**Context:** PRD §7.16 recent sessions (max 10, deduped) + PRD §7.18 multi-monitor overlay placement with fallback.

**Key Decisions:**
- RecentSessionsService accepts `Func<string, bool>? fileExists = null` (injectable for testing; default File.Exists)
- WindowPlacementService accepts `Func<IReadOnlyList<MonitorInfo>>? monitorProvider = null` (injectable; default System.Windows.Forms.Screen adapter)
- MonitorInfo record + OverlayPosition enum for typed domain objects
- MonitorDeviceName added to OverlayLayoutSettings (Windows device name e.g. \\.\DISPLAY1) alongside legacy int Monitor
- Silent degradation everywhere: GetExisting catches exceptions → empty; ResolveMonitor always valid; Custom placement falls back to TopCenter

**Files Created:**
- Abstractions/OverlayPosition.cs
- Abstractions/MonitorInfo.cs
- Abstractions/IRecentSessionsService.cs
- Abstractions/IWindowPlacementService.cs
- Services/RecentSessionsService.cs
- Services/WindowPlacementService.cs
- Tests/RecentSessionsServiceTests.cs (21 tests)
- Tests/WindowPlacementServiceTests.cs (28 tests)

**Build Status:** ✅ 250 tests pass

---

### Decision: Phase 9 Polish Tests — SettingsService Persistence

**Author:** Lambert (Tester)  
**Date:** 2026-06-02T06:23:37-04:00  
**Status:** Implemented — 33 SettingsService tests, all pass

**Context:** Test SettingsService persistence (PRD §11), RecentSessionsService, WindowPlacementService, SummaryFormatter.

**Design Decisions:**
- **SettingsService:** Use real %AppData% path with backup/restore pattern (SettingsService is sealed; reflection unreliable on .NET 10)
- **New services:** Gracefully skip tests (RecentSessionsService, WindowPlacementService, SummaryFormatter) as APIs incomplete during this round; note for follow-up

**Deferred Test Work:**
| Service | Owner | Test scenarios |
|---|---|---|
| RecentSessionsService | Parker | dedupe, ordering, cap at 10, missing-file filtering, Remove/Clear |
| WindowPlacementService | Parker | position-enum → rectangle math, fallback to primary |
| SummaryFormatter | Kane | SessionResult → text/markdown format per PRD §7.14 |

**Test Results:**
- Before: 164 tests
- After: 197 tests (+33 new)
- Failures: 0

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
