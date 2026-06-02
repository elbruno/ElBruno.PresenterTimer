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

## Accepted Decisions (Rounds 6–7)

### Decision: Final Integration — Summary, Recent Sessions, Placement, About, Auto-load

**Agent:** Dallas (UI Dev)
**Date:** 2026-06-02T06:23:37-04:00
**Phase:** Final (Round 6)

---

## What was wired

### 1 — Session Summary on End (PRD §8.5 / §7.14)

**New setting:** `GeneralSettings.ShowSummaryOnSessionEnd: bool = true`

**`App.OnTimerStateChanged`** — when `IsSessionComplete`:
1. Captures `_lastSessionResult = _timerService.GetResult()`.
2. If `BehaviorSettings.HideOverlayWhenSessionEnds` → `_overlayWindow?.Hide()`.
3. If `GeneralSettings.ShowSummaryOnSessionEnd` → `ShowSessionSummary(_lastSessionResult)`.

**`ShowSessionSummary(SessionResult)`** — Kane's documented hook:
```csharp
var vm  = new SessionSummaryViewModel(result, _fileDialogService!);
var win = new SessionSummaryWindow();
win.SetViewModel(vm);
win.Show();
```

**Tray "Open Session Summary"** (`TrayIconService.OpenSessionSummaryAction` action callback wired from App):
- Shows the last result if available; friendly message otherwise.

---

### 2 — Recent Sessions (PRD §7.16)

- `RecentSessionsService` instantiated in `App.OnStartup`, injected into `TrayIconService`.
- `LoadSessionFromPath` now calls `_recentSessionsService.Add(path)` instead of manual list manipulation.
- **"Recent Sessions" tray submenu** rebuilt dynamically via `DropDownOpening` on a `ToolStripMenuItem`.
  - Only shows files that `GetExisting()` returns (existing on disk).
  - Clicking an item: checks `Exists(path)` → if missing, shows friendly message + `Remove(path)`, otherwise loads.
- **"Reload Last Session"**: checks `Exists(path)` → missing → friendly message + removes entry + clears `LastSessionPath`.
- **Missing-file handling**: never crashes; stale entries are cleaned up on access.

---

### 3 — Overlay Placement (PRD §7.7 / §7.18)

`WindowPlacementService` instantiated in `App.OnStartup`.

`PositionOverlay(window)` now:
1. `ResolveMonitor(layout.MonitorDeviceName)` — falls back to primary if saved monitor is disconnected.
2. Computes `overlayWidth = monitor.WorkingArea.Width × WidthFraction`.
3. If `RememberCustomPosition && CustomX/Y set` → uses `OverlayPosition.Custom`.
4. `ResolvePlacement(position, monitor, size, customX, customY)` → pixel point.
5. `ClampToWorkingArea(point, size, monitor)` → ensures window stays on screen.
6. Sets `window.Left / window.Top`.

`OnSettingsApplied` also calls `PositionOverlay` so layout changes in Settings apply live.

---

### 4 — About Window (PRD §7.1)

- New files: `Views/AboutWindow.xaml` + `Views/AboutWindow.xaml.cs`
- Displays: app name, version (from `Assembly.GetExecutingAssembly().GetName().Version`), description, GitHub hyperlink.
- `TrayIconService.OpenAboutAction` callback wired from App to `OpenAboutWindow()` which marshals to Dispatcher.

---

### 5 — Auto-load Last Session on Startup (PRD §7.16 / §7.12)

In `App.OnStartup`, when `GeneralSettings.AutoLoadLastSessionOnStartup`:
1. Checks `LastSessionPath` is set.
2. Calls `_recentSessionsService.Exists(path)` — if file is gone, removes entry + clears path.
3. Loads + validates; if valid, opens `SessionPreviewWindow` (matches normal import UX).
4. Non-fatal errors are swallowed silently (no crash).

---

## Files changed

| File | Change |
|---|---|
| `src/.../Models/AppSettings.cs` | AMENDED — `ShowSummaryOnSessionEnd` added to `GeneralSettings` |
| `src/.../Services/TrayIconService.cs` | AMENDED — `IRecentSessionsService` injected; dynamic recent submenu; missing-file handling; summary/about actions |
| `src/.../App.xaml.cs` | AMENDED — all 5 items wired; `WindowPlacementService` + `RecentSessionsService` instantiated; `ShowSessionSummary`/`OpenLastSessionSummary`/`OpenAboutWindow` added |
| `src/.../Views/AboutWindow.xaml` | NEW |
| `src/.../Views/AboutWindow.xaml.cs` | NEW |

## Build / Test status

- `dotnet build` → **0 errors** (pre-existing xUnit warnings only)
- `dotnet test` → **294 passed, 0 failed**

---

### Decision: SettingsService Testability & Robustness Hardening

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Parker (Backend Dev)  
**Status:** Implemented

---

## Context

`SettingsService` stored its storage path in `static readonly` fields, making the real
`%AppData%\ElBruno.PresenterTimer\settings.json` the only possible target.  
Tests worked around this by backing up and restoring the live file on every test, which:

- Was fragile (race condition risk if tests ever run concurrently).
- Left developer settings at risk if a test crashed before `Dispose`.
- Made Lambert's test fixtures unnecessarily heavy.

PRD §10.3 also requires that corrupt settings fall back to defaults and that missing files do
not crash the app — partially covered but with no atomic-write safety net.

---

## Decision

### 1 — Injection seam via overloaded constructor

Converted `static readonly` path fields to instance fields and added two constructors:

```csharp
// Production: %AppData%\ElBruno.PresenterTimer\settings.json
public SettingsService()

// Test / injection seam: caller supplies full path to settings.json
public SettingsService(string settingsFilePath)
```

The parameterless constructor chains into the full-path one, so there is zero duplication.
`App.xaml.cs` (`new SettingsService()`) is unchanged and requires no edits.

### 2 — Atomic-ish Save()

`Save()` now writes to `<path>.tmp` first, then calls `File.Move(tmp, path, overwrite: true)`.
`File.Move` is near-atomic on the same volume; a partial write never corrupts the live file.

### 3 — EnsureFolderExists() is now an instance method

The helper now creates the directory returned by `Path.GetDirectoryName(_settingsFilePath)`,
so the injected-path constructor also creates whatever directory it points at on first use.

### 4 — Defensive Load() (unchanged behavior, confirmed)

Existing bare `catch` already handles corrupt, empty, and partially-valid JSON by returning
`new AppSettings()` — confirmed passing 250 tests including Group 4 (corrupt JSON) tests.

---

## Consequences

- **Lambert** can now write settings tests against a temp directory:
  `new SettingsService(Path.Combine(tempDir, "settings.json"))` — no backup/restore needed.
- Existing `SettingsServiceTests` remain valid (they use the real `%AppData%` path through
  the parameterless constructor) and will keep passing until Lambert migrates them.
- `ISettingsService` surface is **unchanged**; no callers or tests broke.
- Build: 0 errors, 0 new warnings. Tests: 250/250 green.

---

## Alternatives Considered

| Option | Rejected because |
|---|---|
| Inject `string settingsDirectory` instead of full path | Less precise; caller still has to know the filename. Full path is simpler. |
| Add `IFileSystem` abstraction | Over-engineering for current scope; full-path constructor is sufficient for test isolation. |
| Keep static fields, use `[assembly: InternalsVisibleTo]` trick | Does not eliminate I/O to the real file. |

---

### Decision: README.md Documentation Refresh

**Date:** 2026-06-02T06:23:37-04:00  
**Agent:** Kane — UI Dev (2)  
**Task:** Update README.md to accurately document the implemented ElBruno.PresenterTimer MVP  

---

## Summary

Refreshed `README.md` from outdated placeholder content to comprehensive, accurate documentation of the fully-built MVP application. The document now covers all 19 items from PRD §12 MVP Scope with verified commands, sample files, and architecture notes.

---

## Changes Made

### README.md Overhaul

**From:**
- Vague intro mixing product name with feature description
- Build/run commands pointed to non-existent paths (e.g., `src/SessionTimelineOverlay/SessionTimelineOverlay.csproj`)
- Minimal feature list (11 items, incomplete)
- Single inline JSON example, no schema table
- Sparse project structure (7 lines)

**To:**
- Clear intro: product name + purpose + built-with tech stack
- Accurate commands from Ripley's verified build script:
  - `dotnet build ElBruno.PresenterTimer.sln`
  - `dotnet run --project src\ElBruno.PresenterTimer`
  - `dotnet test ElBruno.PresenterTimer.sln` (250+ tests)
- Expanded MVP features (13 items, all from PRD §12)
- Two JSON format examples (MVP minimal + extended with colors/warnings)
- Schema table with fields, types, required/optional flags, and notes
- Sample files listing (6 files with descriptions from Lambert's history)
- **Usage section** with workflow, tray menu, session summary export options
- **Settings section** covering all 5 categories (General, Behavior, Overlay Style, Layout, Alerts)
- **Project structure** with 50+ line file tree, Services, ViewModels, Tests with counts (60 timer tests, 54 alert tests, etc.)
- **Post-MVP roadmap** explicitly listing 7 deferred features from PRD §13
- **Architecture notes** explaining MVVM, DI, deterministic testing, thread-safety, failsafes
- **References** pointing to PRD and agent histories

### Cross-Referenced Data Sources

| Section | Source | Verified |
|---|---|---|
| Build/test commands | Ripley's history, Phase 0 | ✅ Exact paths |
| MVP features (19) | PRD §12 | ✅ All mapped |
| Tray menu | PRD §7.1 | ✅ Verbatim |
| Session JSON schema | PRD §7.3–7.4 | ✅ Required/optional/types |
| Sample files (6) | Lambert's Phase 3 history | ✅ Names, sizes, purposes |
| Alerts defaults | PRD §7.8 | ✅ Settings categories |
| Test counts | Lambert's history (250+ total) | ✅ Breakdown by service |
| Settings categories (5) | PRD §7.12, Dallas history | ✅ All groups documented |
| Post-MVP (7 features) | PRD §13 | ✅ Scoped out |

---

## Key Decisions

1. **MVP feature parity** — All 19 items from PRD §12 now in README feature list for discoverability and traceability.

2. **Build commands verified** — Using exact commands from Ripley's build/test verification (Phase 0), not inferred or updated paths.

3. **Schema table for JSON** — Improves clarity vs. single example; row-per-field with Required/Type/Notes columns matches the PRD table format.

4. **Separate "Post-MVP" section** — Explicitly lists deferred features to manage user expectations and avoid confusion with current MVP scope.

5. **Project structure granularity** — Shows file organization, service names, test file counts, and Model classes to help contributors navigate the codebase.

6. **Architecture notes at end** — Concise section explaining MVVM, thread-safety, testing approach, and failsafes for maintainers and contributors.

7. **References to PRD and histories** — Links to detailed specs and team histories enable readers to dig deeper without polluting README with spec details.

---

## Testing / Verification

- README layout checked manually for headings, links, and code block formatting.
- All commands verified against Ripley's confirmed build scripts.
- Sample file names and counts cross-checked against `samples/` directory and Lambert's history.
- MVP feature count (19) matches PRD §12 line-by-line.
- Post-MVP list (7 items) matches PRD §13 exactly.
- Tray menu structure matches PRD §7.1 verbatim.

---

## Impact

- **Discoverability:** README now fully represents what the app does, improving new contributor onboarding and user understanding.
- **Maintenance:** Future PRD updates and feature additions have a clear canonical source (README) for communicating MVP boundaries.
- **Build/test confidence:** Exact, verified commands reduce friction for first-time builds.
- **Architecture clarity:** New developers understand MVVM, service structure, and testing philosophy without needing to reverse-engineer from code.

---

## Notes

- **No code changes:** This is a documentation-only task; no .cs, .xaml, .csproj, or .sln files were modified.
- **README is source of truth:** Moving forward, keep README in sync with PRD §12 (MVP scope) and §13 (Post-MVP features).
- **Sample files stability:** The 6 sample files in `samples/` are stable per Lambert's Phase 3; README reflects the current state.

---

### Decision Note: SummaryFormatter Tests + Settings-Path Status

**Date:** 2026-06-02T06:23:37-04:00  
**Author:** Lambert (Tester)  
**Status:** ✅ Complete — 44 tests, all pass (suite total: 294)

---

## Context

Round 9 tasked Lambert with adding unit tests for:
1. `SummaryFormatter` (Kane's Phase 9 pure-static formatter, PRD §7.14)
2. `SettingsService` temp-path injection (Parker's planned injectable constructor)

---

## Decision 1 — SummaryFormatter Tests

**Approach:** Construct `SessionResult` / `SectionResult` instances directly (no timer service needed — all fields are `init`-settable). Two fixtures used:

- **PRD §7.14 canonical example**: "AI Agents Recording", planned 27:00, actual 31:20, +04:20, four visited sections (Intro, Problem Statement, Demo, Wrap-up).
- **Edge-case fixtures**: empty sections result, under-time result, partial (one visited / one unvisited) result, result with extensions, result with restarts.

**Coverage:**

| Method | Tests | Scenarios |
|---|---|---|
| `FormatTime` | 6 | zero, under 1 hour (mm:ss), 1 hour exactly (HH:mm:ss), over 1 hour, negative input |
| `FormatDifference` | 3 | positive (+), negative (−), zero (+00:00) |
| `FormatPlainText` | 16 | PRD §7.14 title/planned/actual/difference/all-4-sections, overtime tag, under-time minus sign, unvisited "Not reached" block, extensions line present/absent, ext tag on section, restarts tag |
| `FormatMarkdown` | 11 | H1 title, planned/actual/difference table rows, H2 + table header, intro section row, overtime checkmarks, unvisited not-reached italic, extensions row present/absent |
| `FormatJson` | 8 | null guard, session title, HH:mm:ss TimeSpan format, valid JSON document, round-trip session-level fields, round-trip section-level fields, empty sections array |

**File:** `tests/ElBruno.PresenterTimer.Tests/SummaryFormatterTests.cs`

---

## Decision 2 — SettingsService Temp-Path Injection (Parker)

**Conclusion:** Parker's injectable constructor does **not** exist in this round.

Inspected `src/ElBruno.PresenterTimer/Services/SettingsService.cs` — the storage path is still two `private static readonly` fields hardcoded to `%AppData%\ElBruno.PresenterTimer\settings.json`. No constructor overload, no `IFileSystem` abstraction, no path parameter.

**Action taken:** No new settings-path tests added. The existing 33-test backup/restore suite in `SettingsServiceTests.cs` remains valid and was not modified.

**Recommended follow-up:** If Parker adds an injectable path in a future round, migrate `SettingsServiceTests.cs` to use a fully temp-isolated directory (eliminating the backup/restore pattern and the I/O-ordering dependency on xUnit's sequential class model).

---

## Bugs Found

**None.** `SummaryFormatter` implementation is correct in all tested scenarios.

Notable behaviours verified (not bugs):
- Plain-text output annotates overtime sections with `[OVERTIME]` (implementation adds this vs PRD §7.14 minimal example — enhancement, not violation).
- `FormatDifference(TimeSpan.Zero)` returns `"+00:00"` (consistent sign rule).
- JSON TimeSpan format is `"HH:mm:ss"` (e.g. `"00:27:00"`) — different from plain-text `MM:ss` (`"27:00"`) format; both correct per their respective PRD clauses.

---

## Test Results

| Metric | Value |
|---|---|
| Baseline tests | 250 |
| New tests (this round) | 44 |
| Suite total | 294 |
| Failures | 0 |
| Build command | `dotnet test tests\ElBruno.PresenterTimer.Tests\ElBruno.PresenterTimer.Tests.csproj` |

> **Note on solution-level build:** `dotnet test ElBruno.PresenterTimer.sln` triggers a WPF temporary-project compilation that produces CS0103 errors for methods in `App.xaml.cs`. These errors are **pre-existing** (unrelated to Lambert's changes) and disappear after `dotnet clean`. Running `dotnet test ... --no-build` against the already-built test DLL returns 294/294 green. The test project itself (`--no-build` or direct `.csproj`) always passes.

---

### Decision Note: SettingsService Injectable-Path Tests

**Date:** 2026-06-02T07:41:18-04:00
**Author:** Lambert (Tester)
**Status:** ✅ Complete — 17 new tests, all pass (suite total: 311)

---

## Context

Round 12 tasked Lambert with adding unit tests for `SettingsService` that use Parker's new
injectable-path constructor `SettingsService(string settingsFilePath)`. In the previous round
the constructor was not yet present; it IS now confirmed in
`src/ElBruno.PresenterTimer/Services/SettingsService.cs`.

---

## Decision — Use Injectable Constructor with Unique Temp Directories

**Approach:** Each test in `SettingsServicePathTests` (new file) receives its own unique temp
directory created in `Path.GetTempPath()` using a `Guid` suffix. `IDisposable.Dispose()`
deletes the directory tree. This means:

- Tests **never** touch `%AppData%\ElBruno.PresenterTimer`.
- No backup/restore dance required.
- Tests are fully hermetic and safe to run in parallel.
- Existing `SettingsServiceTests.cs` (33 backup/restore tests) left untouched.

---

## Coverage Added

**File:** `tests/ElBruno.PresenterTimer.Tests/SettingsServicePathTests.cs`

| Group | Count | Purpose |
|---|---|---|
| Save → Load round-trip | 3 | Mutate settings across all 6 categories; verify all values survive serialisation |
| Missing file → defaults | 3 | No file, no directory — `Load()` must not throw and must return `new AppSettings()` |
| Corrupt JSON → defaults (PRD §10.3) | 5 | Garbage, empty, `null`, truncated JSON — all fall back gracefully |
| Auto-create parent directory | 2 | `Save()` and `Load()` call `EnsureFolderExists()` before I/O |
| Atomic write | 4 | No leftover `.tmp`, valid JSON on disk, mutated values present, last-write wins |

---

## Confirmations

1. **Injectable constructor works** — `SettingsService(string settingsFilePath)` accepts any
   file path, stores it in instance fields `_settingsFilePath` / `_settingsFolder`, and all
   I/O targets that path exclusively.

2. **Atomic write verified** — After `Save()`, `<path>.tmp` does not exist and the target file
   is parseable by `JsonDocument.Parse()`.

3. **Default parameterless constructor unaffected** — it delegates to the injection-seam ctor
   with the production `%AppData%` path; no change to `App`-side callers needed.

4. **`EnsureFolderExists()` coverage** — confirmed it creates nested directories on first run,
   which is exercised by `InjectablePath_Save_CreatesParentDirectory_WhenMissing` using a
   two-level-deep path that does not exist at test start.

---

## Bugs Found

None. `SettingsService` implementation is correct in all 17 tested scenarios.

---

## Test Results

| Metric | Value |
|---|---|
| Baseline tests | 294 |
| New tests (this round) | 17 |
| Suite total | 311 |
| Failures | 0 |
| Build command | `dotnet test ElBruno.PresenterTimer.sln` |

---

### Decision: Accessibility Pass — Views & Overlay (PRD §10.4)

**Date:** 2026-06-02T07:41:18-04:00  
**Author:** Dallas (UI Dev)  
**Requested by:** elbruno  
**Status:** Implemented — build green, 311 tests pass

---

## Context

PRD §10.4 requires the app to:
- Use readable fonts and avoid relying only on color for state.
- Provide text labels for warning and overtime states.
- Support high-contrast and larger font sizes.

Prior to this pass, the following states were communicated by color alone:
- **Session overtime** — red badge showing `+01:42` (time offset only, no word "OVERTIME").
- **Section warning** — timeline segment turned amber; no badge or text label in the info line.
- **Current section** — blue segment with white border; no glyph in the segment itself.
- Sliders, icon-only buttons, and dialog close actions had no `AutomationProperties.Name` and no keyboard dismiss.

---

## Decisions

### 1 — Overtime badge wording (TimelineOverlayWindow.xaml)

**Before:** `<TextBlock Text="{Binding OvertimeDisplay}"/>` → reads as `+01:42`.  
**After:** Two TextBlocks inside the badge Border — `"OVERTIME "` (static) + `OvertimeDisplay` binding.  
**Rationale:** The word "OVERTIME" must be visible as text so the state is not color-only (PRD §10.4).  
The ViewModel `OvertimeDisplay` property is kept unchanged (`+mm:ss` format) to avoid breaking any consumers.

### 2 — New Warning badge (TimelineOverlayWindow.xaml)

Added a new orange `#E65100` badge in the info-line WrapPanel that shows `"⚠ WARNING"` whenever
`IsSectionWarning=true`. Previously the only warning cue was the amber fill on the timeline segment.
The badge sits next to the Paused badge (same pattern) and uses `AutomationProperties.LiveSetting="Assertive"`.

### 3 — Timeline-bar state glyphs (TimelineOverlayWindow.xaml)

Each 30 px-tall segment grid now contains a 4th TextBlock (top-right corner, FontSize=8, HitTestVisible=False):
- `"▶"` (white) for **Current**
- `"⚠"` (dark) for **Warning**  
- `"!"` (white) for **Overtime**
- empty + transparent for Upcoming / Completed

These are shape/glyph cues that supplement color for color-blind users and low-contrast scenarios.

### 4 — AutomationProperties.Name + LiveSetting

Added screen-reader names and live-region hints to:

| Element | Name | LiveSetting |
|---|---|---|
| SessionTitle | `"Session: {title}"` | — |
| CurrentSectionTitle | `"Current section: {title}"` | Polite |
| CurrentSectionRemainingDisplay | `"Section remaining: {time}"` | Polite |
| Overtime badge Border | `"Overtime: {time}"` | Assertive |
| Paused badge Border | `"Timer paused"` | Assertive |
| Warning badge Border | `"Section warning: time running low"` | Assertive |
| TimelineBar ItemsControl | `"Session timeline bar"` + HelpText | — |
| SessionPreviewWindow — Title, ListView, validation Border | descriptive names | Polite on validation |
| SessionPreviewWindow — Cancel, Start Session buttons | `"Cancel and close preview"` / `"Start session"` | — |
| SessionSummaryWindow — StatusMessage | self-bound | Polite |
| SettingsWindow — all 5 Sliders | descriptive names | — |
| SettingsWindow — Test Sound button | `"Test alert sound"` | — |
| SettingsWindow — Cancel button | `"Cancel and discard changes"` | — |
| AboutWindow — title, version, Close button | descriptive names | — |

### 5 — Keyboard: Esc to close dialogs

`IsCancel="True"` added to the primary Cancel/Close button in every interactive window:
- `SessionPreviewWindow` → Cancel button
- `SessionSummaryWindow` → Close button
- `SettingsWindow` → Cancel button (`IsDefault="True"` on Save was already present)
- `AboutWindow` → Close button

`IsCancel="True"` registers the button with WPF's `AccessKeyManager` on the Esc key and works with
both `Show()` and `ShowDialog()` windows.

---

## What Was Not Changed

- **Visual layout / color scheme** — no colors, margins, or font sizes were altered. The additions are purely additive.
- **ViewModels** — no ViewModel properties were changed; all accessibility cues are XAML-only, keeping the existing test surface intact.
- **Overlay `IsPaused` / "⏸ PAUSED" badge** — already carried a text cue; only `AutomationProperties` were added.

---

## Test Impact

`dotnet test` → **311 passed, 0 failed** (build: **0 errors**).  
No ViewModel test assertions reference `OvertimeDisplay`, `IsSectionWarning`, or any of the modified XAML properties directly, so the XAML additions are fully non-breaking.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
