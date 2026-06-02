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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
