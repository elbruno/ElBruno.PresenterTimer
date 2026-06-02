# Parker — History

## Seed
- Project: ElBruno.PresenterTimer — .NET 10 WPF "Session Timeline Overlay".
- PRD: docs/SessionTimelineOverlay_PRD.md.
- Requested by: elbruno.

## Learnings

### Phase 2 — Session Models + JSON Import + Validation (2026-06-02)

#### Public API Surface

**Models (namespace `ElBruno.PresenterTimer.Models`)**

| Type | Key members |
|---|---|
| `SessionPlan` | `string Title`, `string? Description`, `SessionMetadata? Metadata`, `List<SessionSection> Sections` |
| `SessionSection` | `string Title`, `TimeSpan Duration`, `string? Notes`, `string? Color`, `TimeSpan? WarningAt` |
| `SessionMetadata` | `string? Author`, `string? Version`, `string? CreatedAt` |
| `ValidationResult` | `bool IsValid`, `IReadOnlyList<string> Errors`, `static ValidationResult Success`, `static ValidationResult Failure(IReadOnlyList<string> errors)` |

**Converters (namespace `ElBruno.PresenterTimer.Models.Converters`)**

| Type | Notes |
|---|---|
| `TimeSpanJsonConverter` | `JsonConverter<TimeSpan>` — reads/writes `"hh\:mm\:ss"` (e.g. `"00:15:00"`) |
| `NullableTimeSpanJsonConverter` | `JsonConverter<TimeSpan?>` — same format + JSON null support |

**Abstractions (namespace `ElBruno.PresenterTimer.Abstractions`)**

```csharp
// ISessionLoaderService
SessionPlan Load(string path);          // throws SessionLoadException on file/parse errors
SessionPlan TryParse(string json);      // throws SessionLoadException on empty/bad JSON
string      ExportJson(SessionPlan);    // indented normalized JSON
string      ExportSampleJson();         // returns a fully-populated sample plan as JSON
TimeSpan    GetTotalDuration(SessionPlan); // sum of all section durations

// ISessionValidationService
ValidationResult Validate(SessionPlan); // never throws; returns structured result
```

**Services (namespace `ElBruno.PresenterTimer.Services`)**

| Type | Notes |
|---|---|
| `SessionLoaderService` | Implements `ISessionLoaderService`. All I/O and parse errors surface as `SessionLoadException`. |
| `SessionValidationService` | Implements `ISessionValidationService`. Enforces all PRD §7.4 rules. |
| `SessionLoadException` | `Exception` subclass with two ctors: `(string message)` and `(string message, Exception inner)`. |

#### TimeSpan converter format

- Round-trip format string: `@"hh\:mm\:ss"` (always two digits each, e.g. `"01:30:00"`)
- `TimeSpanJsonConverter` used on `SessionSection.Duration` via `[JsonConverter]` attribute
- `NullableTimeSpanJsonConverter` used on `SessionSection.WarningAt` via `[JsonConverter]` attribute
- Both are registered on the shared `JsonSerializerOptions` in `SessionLoaderService` as well (case-insensitive, indented)

#### Validation result shape

```csharp
public sealed class ValidationResult
{
    bool IsValid { get; }
    IReadOnlyList<string> Errors { get; }
    static ValidationResult Success { get; }   // pre-built empty-errors instance
    static ValidationResult Failure(IReadOnlyList<string> errors);
}
```

Error messages match PRD §7.4 style:
- `Section 3, "Demo", has a warning time of 00:06:00 but the section duration is only 00:05:00.`

#### Key file paths

```
src/ElBruno.PresenterTimer/
  Models/
    SessionPlan.cs
    SessionSection.cs
    SessionMetadata.cs
    ValidationResult.cs
    Converters/
      TimeSpanJsonConverter.cs
      NullableTimeSpanJsonConverter.cs
  Services/
    SessionLoadException.cs
    SessionLoaderService.cs
    SessionValidationService.cs
  Abstractions/
    ISessionLoaderService.cs
    ISessionValidationService.cs
```

#### Build note

`dotnet build ElBruno.PresenterTimer.sln` → **0 errors, 0 warnings** (verified).
`System.IO` must be an explicit using in `SessionLoaderService.cs` — the WPF XAML temp assembly does not inherit all implicit global usings.

---

## Learnings

### Phase 5 — Timer Engine (2026-06-02)

#### Public API Surface

**New models (namespace `ElBruno.PresenterTimer.Models`)**

| Type | Key members |
|---|---|
| `SectionResult` | `int Index`, `string Title`, `TimeSpan PlannedDuration`, `TimeSpan ActualDuration`, `TimeSpan TotalExtensions`, `int RestartCount`, `bool WasSkipped`, `bool WasVisited`, `TimeSpan Difference`, `bool WasOvertime` |
| `SessionResult` | `string SessionTitle`, `TimeSpan PlannedDuration`, `TimeSpan ActualDuration`, `TimeSpan TotalExtensions`, `TimeSpan Difference`, `IReadOnlyList<SectionResult> Sections` |

**New abstractions (namespace `ElBruno.PresenterTimer.Abstractions`)**

| Type | Notes |
|---|---|
| `TimerTickEventArgs` | Payload for `ISessionTimerService.Tick`; carries `SessionElapsed`, `SessionRemaining`, `SessionOvertime`, `IsSessionOvertime`, `CurrentSectionElapsed`, `CurrentSectionRemaining`, `CurrentSectionOvertime`, `IsSectionOvertime`, `BehindSchedule`, `TotalPlannedDuration`, `CurrentSectionIndex` |
| `SectionChangedEventArgs` | `PreviousSectionIndex`, `CurrentSectionIndex`, `SectionChangeReason` |
| `SectionChangeReason` | Enum: `ManualNext`, `ManualPrevious`, `ManualRestart`, `AutoAdvance`, `Reset` |

**Refined interface `ISessionTimerService : IDisposable`**

```csharp
// Plan management
void LoadPlan(SessionPlan plan);
SessionPlan? Plan { get; }

// State
bool IsRunning { get; }
bool IsPaused { get; }
bool IsSessionComplete { get; }
int CurrentSectionIndex { get; }       // -1 when no plan
SessionSection? CurrentSection { get; } // null when complete or no plan

// Timing (all monotonic)
TimeSpan TotalPlannedDuration { get; }
TimeSpan SessionElapsed { get; }
TimeSpan SessionRemaining { get; }      // 0 when overtime
TimeSpan SessionOvertime { get; }       // 0 when not overtime
TimeSpan CurrentSectionElapsed { get; }
TimeSpan CurrentSectionRemaining { get; }
TimeSpan CurrentSectionOvertime { get; }
TimeSpan BehindSchedule { get; }        // 0 when on-schedule/ahead

// Settings
bool AutoAdvanceSections { get; set; }  // default false (PRD §7.11)

// Controls (PRD §7.9)
void Start();
void Pause();
void Resume();
void Reset();
void NextSection();
void PreviousSection();
void RestartCurrentSection();
void ExtendCurrentSection(TimeSpan extension);  // +1/+5 min

// Events (thread-pool thread; UI must marshal)
event EventHandler<TimerTickEventArgs>? Tick;           // ~250ms cadence
event EventHandler<SectionChangedEventArgs>? SectionChanged;
event EventHandler? StateChanged;

// Summary
SessionResult GetResult();  // safe to call mid-session
```

#### Overtime and Behind-Schedule semantics

- **Section overtime** (`CurrentSectionOvertime > 0`): section elapsed > effective duration AND auto-advance is OFF. Section stays active; `IsSectionOvertime = true`.
- **Session overtime** (`SessionOvertime > 0`): total session elapsed > `TotalPlannedDuration`.
- **BehindSchedule**: `max(0, SessionElapsed − (sum of previous sections' planned + min(currentSectionElapsed, currentSection.PlannedDuration)))`. Extensions are excluded from the baseline — deliberate extra time does not inflate behind-schedule.

#### Monotonic timing (PRD §10.1)

- A single `System.Diagnostics.Stopwatch` (`_clock`) runs only while the timer is active.
- `_sessionElapsedBase` accumulates across pause/resume cycles (`_sessionElapsedBase += _clock.Elapsed` on pause).
- `SessionElapsed = _sessionElapsedBase + _clock.Elapsed` (when running); `_sessionElapsedBase` (when paused/stopped/complete).
- `CurrentSectionElapsed = SessionElapsed − _sessionElapsedAtSectionStart` (set on each section start/restart).
- A `System.Timers.Timer` ticks every 250ms; the tick handler ONLY reads the clock and raises events — it never accumulates time itself.

#### Auto-advance (PRD §7.11)

- `AutoAdvanceSections` is a settable property (default `false`).
- On each tick, if `AutoAdvanceSections && CurrentSectionElapsed >= effectiveDuration`, the handler re-acquires the lock, guards against concurrent manual advances (by comparing the section index snapshot), and advances. Fires `SectionChanged` with reason `AutoAdvance`.

#### SessionResult / SectionResult shape (for Phase 9)

```csharp
SessionResult {
    string SessionTitle;
    TimeSpan PlannedDuration;   // no extensions
    TimeSpan ActualDuration;    // real elapsed
    TimeSpan TotalExtensions;   // sum across all sections
    TimeSpan Difference;        // ActualDuration - PlannedDuration
    IReadOnlyList<SectionResult> Sections;
}

SectionResult {
    int Index;
    string Title;
    TimeSpan PlannedDuration;   // original plan
    TimeSpan ActualDuration;    // sum of all visits
    TimeSpan TotalExtensions;   // added during this section
    int RestartCount;           // explicit RestartCurrentSection calls
    bool WasSkipped;            // left before effective duration completed
    bool WasVisited;            // was ever the current section
    TimeSpan Difference;        // ActualDuration - PlannedDuration
    bool WasOvertime;           // ActualDuration > PlannedDuration
}
```

#### Key file paths

```
src/ElBruno.PresenterTimer/
  Models/
    SectionResult.cs        (NEW — Phase 9 summary data)
    SessionResult.cs        (NEW — Phase 9 summary data)
  Abstractions/
    ISessionTimerService.cs (REFINED — full interface)
    TimerTickEventArgs.cs   (NEW)
    SectionChangedEventArgs.cs (NEW — includes SectionChangeReason enum)
  Services/
    SessionTimerService.cs  (NEW — full implementation)
```

#### Build note

`dotnet build ElBruno.PresenterTimer.sln` → **0 errors, 0 warnings** (verified).
All existing tests continue to pass (49 total including prior phases).

---

## Learnings

### Phase 7 — AlertService (2026-06-02)

#### Public API Surface

**New types (namespace `ElBruno.PresenterTimer.Abstractions`)**

| Type | Notes |
|---|---|
| `AlertType` | Enum: `SectionWarning`, `SectionEnd`, `SessionWarning`, `SessionEnd`, `SectionOvertime`, `SessionOvertime`, `ManualSectionChange` |
| `AlertEventArgs` | `AlertType AlertType`, `string Message`, `int SectionIndex`, `bool ShouldPlaySound`, `bool ShouldShowNotification` |

**Replaced interface `IAlertService` (namespace `ElBruno.PresenterTimer.Abstractions`)**

```csharp
event EventHandler<AlertEventArgs>? AlertRaised;

void Attach(ISessionTimerService timer);  // subscribe to Tick + SectionChanged
void Detach();                            // unsubscribe; safe if nothing attached
void Reset();                             // clear all dedup state
```

**New service `AlertService` (namespace `ElBruno.PresenterTimer.Services`)**

```csharp
// Constructor
public AlertService(AlertSettings settings);

// Implements IAlertService + IDisposable (Dispose() calls Detach())

// Testable core — internal, exposed via InternalsVisibleTo("ElBruno.PresenterTimer.Tests")
internal void ProcessState(TimerTickEventArgs tick, SessionPlan plan);

// Session-level dedup bucket sentinel (also internal/visible to tests)
internal const int SessionBucket = -1;
```

#### Dedup Data Structure

```
_fired: Dictionary<int, HashSet<AlertType>>
  key  = 0-based section index  (section-level alerts)
  key  = SessionBucket (-1)      (SessionWarning, SessionEnd, SessionOvertime)
  value = set of AlertType values that have already fired in this section lifetime
```

- `TryMarkFired(bucketKey, type)`: thread-safe check-and-set; returns `true` only the first time.
- Section bucket cleared on: SectionChanged (any reason except Reset) → fresh entry.
- ManualRestart (`SectionChangeReason.ManualRestart`) clears the current section's bucket so alerts can refire.
- Full `Reset()` clears the entire dictionary.
- Pause/resume has no effect on dedup — the `_fired` set persists across pause/resume cycles.

#### Settings Gates

| Toggle | Guards |
|---|---|
| `EnableSectionWarningAlerts` | `SectionWarning` alert |
| `EnableSectionEndAlerts` | `SectionEnd` alert |
| `EnableSessionWarningAlerts` | `SessionWarning` alert |
| `EnableSessionEndAlerts` | `SessionEnd` alert |
| `EnableOvertimeAlerts` | `SectionOvertime` + `SessionOvertime` alerts |
| `EnableSoundAlerts` | Sets `AlertEventArgs.ShouldPlaySound` |
| `EnableWindowsNotifications` | Sets `AlertEventArgs.ShouldShowNotification` |

`ManualSectionChange` has no toggle; `ShouldPlaySound` and `ShouldShowNotification` are always `false`.

Per-section `SessionSection.WarningAt` overrides `AlertSettings.SectionWarningThreshold` for the `SectionWarning` threshold.

#### How to Test Without a Real Timer

```csharp
var settings = new AlertSettings();
var service = new AlertService(settings);
var plan = /* build a SessionPlan */;
var raised = new List<AlertEventArgs>();
service.AlertRaised += (_, e) => raised.Add(e);

// Simulate a tick where 30s remain in section 0 (threshold is 1 min)
service.ProcessState(new TimerTickEventArgs
{
    CurrentSectionIndex = 0,
    CurrentSectionRemaining = TimeSpan.FromSeconds(30),
    // ... other fields
}, plan);

// Assert SectionWarning fired once
Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionWarning));

// Call again — dedup suppresses refire
service.ProcessState(/* same state */, plan);
Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionWarning)); // still 1
```

To simulate restart: call `service.Reset()` (or fire `SectionChanged` with `ManualRestart`) then call `ProcessState` again — alert fires a second time.

#### Key File Paths

```
src/ElBruno.PresenterTimer/
  Abstractions/
    AlertType.cs         (NEW)
    AlertEventArgs.cs    (NEW)
    IAlertService.cs     (REPLACED — Phase 7 full interface)
  Services/
    AlertService.cs      (NEW)
  AssemblyInfo.cs        (AMENDED — InternalsVisibleTo test project)
.squad/decisions/inbox/
  parker-alert-service.md (NEW — decision note)
```

#### Build Note

`dotnet build ElBruno.PresenterTimer.sln` → **0 errors, 0 warnings** (verified).
All 49 existing tests pass.

---

## Learnings

### Phase 8 — Timer Fixes + Output Alert Services (2026-06-02)

#### Bug Fix 1 — `CurrentSectionIndex` Contract

**Final contract (ISessionTimerService & SessionTimerService):**
- `CurrentSectionIndex == -1` when no plan has been loaded (i.e., `LoadPlan` has never been called, or `Reset()` is called with no plan present).
- `CurrentSectionIndex == 0` once a plan is loaded (after `LoadPlan` or after `Reset()` with a plan loaded).
- `CurrentSectionIndex >= 0` while the session is running (0-based, matches section index in `Plan.Sections`).

**Implementation change:**
- Field init: `private int _currentSectionIndex = -1;` (was `0` via C# default).
- `ResetState()`: `_currentSectionIndex = _plan is not null ? 0 : -1;` (was always `0`).
- `CurrentSection` property: added `_currentSectionIndex < 0` guard before indexing `Plan.Sections`.
- New test: `CurrentSectionIndex_IsMinusOne_BeforePlanLoaded` verifies the fix.

#### Bug Fix 2 — `ComputeBehindSchedule` Phantom Drift

**Root cause:** The method called `ComputeSectionElapsed()` (which internally calls `ComputeSessionElapsed()` → reads `_clock.Elapsed`) and then called `ComputeSessionElapsed()` again — two separate reads of `_clock.Elapsed`, creating a sub-millisecond gap that produced spurious non-zero values at session start.

**Fix:** Snapshot `ComputeSessionElapsed()` once into a local variable at the top of `ComputeBehindSchedule()`, then derive `sectionElapsed` from that same snapshot:

```csharp
var sessionElapsed = ComputeSessionElapsed();  // single clock read
var sectionElapsed = _isSessionComplete ? TimeSpan.Zero : sessionElapsed - _sessionElapsedAtSectionStart;
// ... rest uses sessionElapsed and sectionElapsed only
```

`BehindSchedule` is now exactly `0` at session start (was ~100–300 ns). Existing test tolerance (`< 10ms`) still passes; comment updated to reflect the fix.

#### New Service — `SoundAlertService`

**Abstraction:** `Abstractions/ISoundAlertService.cs`

```csharp
public interface ISoundAlertService
{
    bool IsEnabled { get; set; }   // reads/writes AlertSettings.EnableSoundAlerts
    void PlaySectionWarning();     // no-op when IsEnabled == false
    void PlaySectionEnd();         // no-op when IsEnabled == false
    void PlaySessionEnd();         // no-op when IsEnabled == false
    void PlayTestSound();          // ALWAYS plays (Settings UI "Test sound" button)
}
```

**Implementation:** `Services/SoundAlertService.cs`
- Constructor: `SoundAlertService(AlertSettings settings)`
- Uses `System.Media.SystemSounds` — `Play()` dispatches to the OS audio system and returns immediately (non-blocking).
- Sound mapping: `PlaySectionWarning` → `Asterisk`; `PlaySectionEnd/PlaySessionEnd` → `Exclamation`; `PlayTestSound` → `Beep`.
- Default OFF: `AlertSettings.EnableSoundAlerts = false` by default (PRD §7.8).

**Dallas/Kane wiring note:** Subscribe to `IAlertService.AlertRaised`; when `e.ShouldPlaySound == true`, call the appropriate `Play*` method based on `e.AlertType`. Call `PlayTestSound()` from the Settings "Test Sound" button (no check needed — it ignores `IsEnabled`).

#### New Service — `SystemNotificationService`

**Interface:** `Abstractions/ISystemNotificationService.cs` (already existed)

```csharp
void Notify(string title, string message);
```

**Implementation:** `Services/SystemNotificationService.cs`

```csharp
// Constructor options:
public SystemNotificationService(AlertSettings settings)
public SystemNotificationService(AlertSettings settings, NotifyIcon? sharedIcon)
```

- Uses `System.Windows.Forms.NotifyIcon.ShowBalloonTip` — non-focus-stealing (PRD §10.2).
- **Preferred:** pass the app's existing `NotifyIcon` (from `TrayIconService`) via the `sharedIcon` constructor param — avoids a second tray entry. App layer injects it at startup.
- **Fallback:** if no shared icon, a self-managed `NotifyIcon` is lazily created and disposed with the service.
- Default OFF: `AlertSettings.EnableWindowsNotifications = false` by default (PRD §7.8).
- `IDisposable.Dispose()` only disposes the internally-owned icon; never disposes a shared icon.

**Dallas/Kane wiring note:** Subscribe to `IAlertService.AlertRaised`; when `e.ShouldShowNotification == true`, call `Notify(e.AlertType.ToString(), e.Message)`. Inject the tray `NotifyIcon` when constructing: `new SystemNotificationService(settings, trayIconService.Icon)`.

#### Key File Paths (Phase 8)

```
src/ElBruno.PresenterTimer/
  Services/
    SessionTimerService.cs  (FIXED — CurrentSectionIndex field init + ResetState + ComputeBehindSchedule)
    SoundAlertService.cs    (NEW)
    SystemNotificationService.cs (NEW)
  Abstractions/
    ISoundAlertService.cs   (NEW)
tests/ElBruno.PresenterTimer.Tests/
  SessionTimerServiceTests.cs (AMENDED — new test + comment update)
.squad/decisions/inbox/
  parker-fixes-output-services.md (NEW)
```

#### Build Note

`dotnet build ElBruno.PresenterTimer.sln` → **0 errors, 0 warnings** (verified).
All 164 tests pass (109 prior + 55 AlertService tests from Phase 7 suite + 1 new CurrentSectionIndex contract test).

---

## Learnings

### Phase 10 — RecentSessionsService + WindowPlacementService (2026-06-02)

#### Public API Surface

**New types (namespace `ElBruno.PresenterTimer.Abstractions`)**

| Type | Notes |
|---|---|
| `OverlayPosition` | Enum: `TopCenter`, `TopLeft`, `TopRight`, `BottomCenter`, `BottomLeft`, `BottomRight`, `Custom` |
| `MonitorInfo` | Sealed record: `string DeviceName`, `Rectangle Bounds`, `Rectangle WorkingArea`, `bool IsPrimary` |
| `IRecentSessionsService` | Interface (see below) |
| `IWindowPlacementService` | Interface (see below) |

**`IRecentSessionsService` (namespace `ElBruno.PresenterTimer.Abstractions`)**

```csharp
void Add(string path);                 // dedupe CI, most-recent-first, cap=10, updates LastSessionPath, persists
IReadOnlyList<string> GetAll();        // all stored paths, newest-first
IReadOnlyList<string> GetExisting();   // only paths whose files exist (never throws on missing files)
bool Exists(string path);              // file-existence check via injected predicate
void Remove(string path);              // case-insensitive remove + save
void Clear();                          // clear all + save
```

**`IWindowPlacementService` (namespace `ElBruno.PresenterTimer.Abstractions`)**

```csharp
IReadOnlyList<MonitorInfo> GetAvailableMonitors();
MonitorInfo GetPrimaryMonitor();
MonitorInfo? FindMonitorByDeviceName(string deviceName);         // null if disconnected
MonitorInfo ResolveMonitor(string? savedDeviceName);             // fallback to primary if null/disconnected (PRD §7.18)
Point ResolvePlacement(OverlayPosition, MonitorInfo, Size, double? customX, double? customY);
Point ClampToWorkingArea(Point, Size, MonitorInfo);
OverlayPosition ParsePosition(string?);                          // safe parse, returns TopCenter on unknown
```

**New services (namespace `ElBruno.PresenterTimer.Services`)**

| Type | Notes |
|---|---|
| `RecentSessionsService` | Implements `IRecentSessionsService`. Backed by `ISettingsService`. |
| `WindowPlacementService` | Implements `IWindowPlacementService`. Uses `System.Windows.Forms.Screen` for live data. |

**AppSettings change (namespace `ElBruno.PresenterTimer.Models`)**

- `OverlayLayoutSettings.MonitorDeviceName: string?` added — persists the preferred monitor's Windows device name (e.g. `\\.\DISPLAY1`) for `ResolveMonitor`. The existing `int Monitor` field is kept as a legacy/fallback.

#### Testability Seams

| Service | Injectable seam |
|---|---|
| `RecentSessionsService` | `Func<string, bool>? fileExists` constructor param — defaults to `File.Exists`. Tests inject a `HashSet`-based predicate. |
| `WindowPlacementService` | `Func<IReadOnlyList<MonitorInfo>>? monitorProvider` constructor param — defaults to `Screen.AllScreens` adapter. Tests inject a static list of synthetic monitors. |

#### Missing-File Handling (PRD §7.16, §10.3)

- `GetExisting()` filters the stored list via the injected predicate; never throws (any predicate exception is swallowed).
- `Exists(path)` provides a single-path check consistent with `GetExisting()` — the UI should call this before loading to show a friendly error and optionally call `Remove(path)` to clean the dead entry.
- `Add()`, `Remove()`, `Clear()` never throw regardless of file system state.

#### Monitor Fallback (PRD §7.18)

- `ResolveMonitor(savedDeviceName)`: if `savedDeviceName` is null or the named monitor is not in the live list (disconnected), returns the flagged primary monitor; if no primary flag exists, returns the first in the list; if the list is empty, returns a hard-coded 1920×1080 fallback (never returns null / never throws).
- `ResolvePlacement(Custom, ...)` with no `customX`/`customY` values falls back silently to `TopCenter` — handles the case where a saved custom position hasn't been set yet.

#### Key File Paths (Phase 10)

```
src/ElBruno.PresenterTimer/
  Abstractions/
    OverlayPosition.cs           (NEW)
    MonitorInfo.cs               (NEW)
    IRecentSessionsService.cs    (NEW)
    IWindowPlacementService.cs   (NEW)
  Services/
    RecentSessionsService.cs     (NEW)
    WindowPlacementService.cs    (NEW)
    FileDialogService.cs         (FIXED — added missing ShowSaveMarkdownDialog implementation)
  Models/
    AppSettings.cs               (AMENDED — MonitorDeviceName added to OverlayLayoutSettings)
tests/ElBruno.PresenterTimer.Tests/
  RecentSessionsServiceTests.cs  (NEW — 21 tests)
  WindowPlacementServiceTests.cs (NEW — 28 tests)
.squad/decisions/inbox/
  parker-recent-placement.md     (NEW)
```

#### Build Note

`dotnet build ElBruno.PresenterTimer.sln` → **0 errors, 0 warnings** (verified).
All 250 tests pass (164 prior + 21 RecentSessionsService + 28 WindowPlacementService + ... wait — 86 new total; see note).
_Test count rose from 164 → 250 because other agents (Lambert et al.) also added tests in the same session; Parker's Phase 10 contribution is the 49 tests in the two new test files._

#### Dallas Wiring Note

Dallas should wire the following in the next round:
- **Tray Recent Sessions sub-menu**: call `IRecentSessionsService.GetExisting()` to build items; on click call `IRecentSessionsService.Exists(path)` first — if false show friendly error and call `Remove(path)`.
- **Reload Last Session**: read `ISettingsService.Settings.General.LastSessionPath` + call `Exists()`.
- **Overlay placement**: call `IWindowPlacementService.ResolveMonitor(settings.OverlayLayout.MonitorDeviceName)`, then `ResolvePlacement(ParsePosition(settings.OverlayLayout.Position), monitor, overlaySize, customX, customY)`.
- **Settings UI monitor combo**: bind to `GetAvailableMonitors()`, save selected `DeviceName` to `OverlayLayoutSettings.MonitorDeviceName`.



---

## Learnings

### Phase 6b — SettingsService Testability + Robustness (2026-06-02)

#### Injection seam — new constructor overload

`csharp
// Production (unchanged) — %AppData%\ElBruno.PresenterTimer\settings.json
public SettingsService()

// Injection seam — full path to settings.json (for tests / DI)
public SettingsService(string settingsFilePath)
`

The parameterless constructor chains into the full-path one; zero duplication.
`App.xaml.cs` uses `new SettingsService()` and required **no edits**.

#### Robustness changes

| Area | Before | After |
|---|---|---|
| Path storage | `static readonly` string fields | Instance fields `_settingsFilePath` / `_settingsFolder` |
| `EnsureFolderExists()` | Static method; always used the static `SettingsFolder` | Instance method; creates `Path.GetDirectoryName(_settingsFilePath)` |
| `Save()` writes | `File.WriteAllText(path, json)` — partial write could corrupt | Write to `path + ".tmp"` then `File.Move(tmp, path, overwrite:true)` (atomic-ish on same volume) |
| Corrupt/missing JSON | `catch{}` → defaults (was already correct) | Unchanged; confirmed by 250-test suite |

#### Lambert note

Lambert can now point settings tests at a temp dir without any backup/restore ceremony:

`csharp
var svc = new SettingsService(Path.Combine(tempDir, "settings.json"));
`

The existing `SettingsServiceTests` remain valid; they still use `new SettingsService()`
against the real `%AppData%` path and continue to pass.

#### Build / test status

`dotnet test ElBruno.PresenterTimer.sln` → **0 errors, 250/250 green**.
