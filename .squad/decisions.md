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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
