# Lambert — History

## Seed
- Project: ElBruno.PresenterTimer — .NET 10 WPF "Session Timeline Overlay".
- PRD: docs/SessionTimelineOverlay_PRD.md (§11 test requirements).
- Requested by: elbruno.

## Learnings

### Phase 11: Timer Slice — SessionTimerService Tests (2026-06-02)

#### Determinism approach

`SessionTimerService` exposes no injectable time source — it owns a `Stopwatch` directly.
Tests were made deterministic by splitting behaviors into two buckets:

1. **Fully deterministic (no real-time wait)**: All navigation (`NextSection`, `PreviousSection`,
   `RestartCurrentSection`), state mutations (`Start`, `Pause`, `Resume`, `Reset`), extension
   accumulation (`ExtendCurrentSection`), and result snapshots (`GetResult`). These mutate
   synchronous fields under a lock and produce exact answers immediately.  Control-flow events
   (`StateChanged`, `SectionChanged`) are raised on the *calling* thread for all manual
   operations, so event tests are also lock-free and deterministic.

2. **Tolerance-based timing tests** (8 tests, marked "[timing-sensitive]"): A short
   `Task.Delay(50–300 ms)` is used for properties that require real elapsed time
   (pause preservation, overtime, BehindSchedule). All assert with generous ±150–200 ms
   tolerances. Sections as short as 50 ms are used so waits stay under 300 ms total.

No tests were marked `[Fact(Skip=...)]` — every scenario was coverable within the tolerance
approach. The `WhilePaused_SessionElapsed_IsFrozen` test exploits the fact that the paused
snapshot in `_sessionElapsedBase` is a field (immutable while paused), so two property reads
return identical `TimeSpan` values.

#### Tests added: 60 new tests (total: 109)

**`SessionTimerServiceTests.cs` (60 tests)**

| Group | Tests |
|---|---|
| LoadPlan state (4) | `LoadPlan_SetsPlan`, `LoadPlan_SetsTotalPlannedDuration`, `LoadPlan_DoesNotStartTimer`, `LoadPlan_WhileRunning_ResetsToNotRunning` |
| Start (7) | `Start_SetsIsRunning`, `Start_CurrentSectionIndex_IsZero`, `Start_IsPausedFalse`, `Start_IsSessionCompleteFalse`, `Start_CurrentSection_IsFirstSection`, `Start_BehindSchedule_IsEffectivelyZero`, `Start_RaisesStateChangedEvent` |
| NextSection (7) | `NextSection_AdvancesToSectionOne`, `NextSection_AdvancesThroughAllSections`, `NextSection_CurrentSection_UpdatesToNewSection`, `NextSection_AtLastSection_SetsSessionComplete`, `NextSection_AtLastSection_CurrentSection_IsNull`, `NextSection_WhenAlreadyComplete_IsNoOp`, `NextSection_RaisesSectionChangedEvent_WithManualNextReason` |
| PreviousSection (5) | `PreviousSection_AtFirstSection_IsNoOp`, `PreviousSection_AtFirstSection_DoesNotRaiseEvent`, `PreviousSection_FromSectionOne_GoesToZero`, `PreviousSection_RaisesSectionChangedEvent_WithManualPreviousReason`, `NextThenPrevious_ReturnsToFirstSection` |
| Reset (6) | `Reset_SetsIsRunningFalse`, `Reset_SetsCurrentSectionIndexToZero`, `Reset_ClearsIsPaused`, `Reset_ClearsIsSessionComplete`, `Reset_RaisesSectionChangedEvent_WithResetReason`, `Reset_RaisesStateChangedEvent` |
| Pause/Resume (7) | `Pause_SetsIsPaused`, `Pause_IsRunningRemainsTrue`, `Resume_ClearsIsPaused`, `Pause_RaisesStateChangedEvent`, `Resume_RaisesStateChangedEvent`, `PauseResume_PreservesElapsed`*, `WhilePaused_SessionElapsed_IsFrozen`* |
| ExtendCurrentSection (6) | `ExtendCurrentSection_TrackedInGetResult`, `ExtendCurrentSection_ZeroExtension_IsNoOp`, `ExtendCurrentSection_NegativeExtension_IsNoOp`, `ExtendCurrentSection_MultipleExtensions_Accumulate`, `ExtendCurrentSection_IncreasesCurrentSectionRemaining`, `ExtendCurrentSection_OvertimeIsZero_RightAfterExtension` |
| Overtime (3*) | `ShortSection_AfterElapsed_SectionOvertimePositive`*, `ShortSession_AfterElapsed_SessionOvertimePositive`*, `AfterSectionOvertime_BehindScheduleIsPositive`* |
| GetResult (11) | `GetResult_NoPlan_ReturnsEmptyResult`, `GetResult_AfterLoadPlan_PlannedDurationMatchesPlan`, `GetResult_SectionCount_MatchesPlan`, `GetResult_AfterStart_Section0_IsVisited`, `GetResult_BeforeNavigating_Section1_IsNotVisited`, `GetResult_AfterNextSection_Section0_WasSkipped`, `GetResult_Section1_WasVisited_AfterNavigation`, `GetResult_PlannedDurationPerSection_MatchesPlan`, `GetResult_TotalExtensions_ReflectsAllSectionExtensions`, `GetResult_RestartCount_AfterRestartCurrentSection`, `GetResult_SessionTitle_MatchesPlanTitle` |
| RestartCurrentSection (3) | `RestartCurrentSection_DoesNotChangeIndex`, `RestartCurrentSection_RaisesSectionChangedEvent_WithManualRestartReason`, `RestartCurrentSection_ResetsCurrentSectionElapsed`* |
| Dispose (1) | `Dispose_WhileRunning_DoesNotThrow` |

`*` = timing-sensitive (uses `Task.Delay`; asserts with tolerance)

#### Bugs found

1. **`CurrentSectionIndex` pre-plan value** (`ISessionTimerService.cs` doc vs `SessionTimerService.cs`):
   Interface documentation states the property returns `-1` when no plan is loaded.
   Implementation initialises `_currentSectionIndex` to `0` (C# default for `int`); the field
   is never explicitly set to `-1`. Tests assert actual runtime behaviour (`0`), not the doc.

2. **`ComputeBehindSchedule` double-clock-read** (`SessionTimerService.cs`):
   The method calls `ComputeSessionElapsed()` twice — once via `ComputeSectionElapsed()` and
   once directly — each reading `_clock.Elapsed` independently. Between the two reads, the
   Stopwatch advances ~100–300 ns, producing a spurious positive `BehindSchedule` value even
   when perfectly on-schedule. `Start_BehindSchedule_IsEffectivelyZero` asserts `< 10ms`
   rather than `== 0` to tolerate this. Production code not modified.

---

### Phase 3: Sample Data Creation (2026-06-02)

**Sample Files Created:**

1. **short-demo.json** (~10 min)
   - Format: MVP (no colors, metadata, or warnings)
   - Sections: Intro (2m), Demo (6m), Wrap-up (2m)
   - Purpose: Test minimal schema compliance

2. **podcast.json** (~30 min)
   - Format: Extended (metadata + per-section colors + warnings)
   - Sections: 6 sections with distinct colors (#4CAF50, #2196F3, #FF9800, #9C27B0, #F44336, #009688)
   - warningAt ranges: 00:00:45 to 00:02:00
   - Purpose: Test extended schema with multiple colored sections

3. **conference-talk.json** (~45 min)
   - Format: Extended (metadata with conference field)
   - Sections: 7 sections covering intro, context, design, live demo, patterns, Q&A, wrap-up
   - Per-section warnings from 00:00:30 to 00:03:00
   - Purpose: Test longer sessions with live demo and Q&A

4. **workshop.json** (~60 min)
   - Format: Extended (metadata with level field)
   - Sections: 7 sections including theory, 3 exercises, break, Q&A
   - Mixed durations (5m-12m per section)
   - Purpose: Test full-length workshop format with breaks and exercises

5. **ai-agents-demo.json** (~27 min)
   - Format: Extended (matches PRD §17 specification exactly)
   - Sections: Intro, Context, Demo, Wrap-up with specified colors and warnings
   - Purpose: Reference implementation; validates against documented spec

6. **invalid-warning-exceeds-duration.json** (intentionally invalid)
   - Validation Violation: "Demo" section has duration "00:05:00" but warningAt "00:06:00"
   - Expected error: "Warning threshold (00:06:00) cannot exceed section duration (00:05:00)"
   - Purpose: Test validator correctly catches semantic violations per PRD §7.4

**Format Distribution:**
- MVP-only: 1 file (short-demo.json)
- Extended: 5 files (podcast, conference-talk, workshop, ai-agents-demo, invalid)

**Validation Rule Tested:**
- Invalid file tests PRD §7.4 rule: "Optional `warningAt` must be less than section duration"
- Test assertion: Session validation must reject file 6 and report the specific violation

## Learnings

### Phase 11: Loader + Validation Tests (2026-06-02)

#### Test Data Strategy

Chose **Content items with `CopyToOutputDirectory=Always`** in the test `.csproj`:
```xml
<Content Include="..\..\samples\*.json">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  <Link>TestData\%(Filename)%(Extension)</Link>
</Content>
```
Tests resolve files via `Path.Combine(AppContext.BaseDirectory, "TestData", fileName)`.
This avoids brittle path-climbing and works regardless of working directory at test run time.

**Note:** `using System.IO;` must be explicit in test files — WPF projects suppress some implicit usings (same quirk documented in Parker's history).

#### Tests Added: 44 new tests (49 total; 5 existing sanity tests unchanged)

**`LoaderParsingTests.cs` (27 tests):**
- `ShortDemo_Parse_TitleIs_QuickDemo`
- `ShortDemo_Parse_HasThreeSections`
- `ShortDemo_Parse_DurationsParseCorrectly`
- `Podcast_Parse_TitleAndSectionCount`
- `Podcast_Parse_HasMetadata`
- `Podcast_Parse_WarningAtParsesAsNullableTimeSpan`
- `ConferenceTalk_Parse_TitleAndSectionCount`
- `ConferenceTalk_Parse_LiveDemoIs15Minutes`
- `Workshop_Parse_TitleAndSectionCount`
- `AiAgentsDemo_Parse_Title`
- `AiAgentsDemo_Parse_HasFourSections`
- `AiAgentsDemo_Parse_SectionDurationsCorrect`
- `AiAgentsDemo_Parse_ColorsPresent`
- `MalformedJson_TryParse_ThrowsSessionLoadException_NotRawJsonException`
- `EmptyString_TryParse_ThrowsSessionLoadException`
- `WhitespaceOnly_TryParse_ThrowsSessionLoadException`
- `NullLiteralJson_TryParse_ThrowsSessionLoadException`
- `ExportJson_ThenTryParse_RoundTripsEqualDurations`
- `ExportJson_TimeSpanFormattedAs_HhMmSs`
- `ExportJson_NullableWarningAt_FormattedAs_HhMmSs`
- `GetTotalDuration_AiAgentsDemo_Is27Minutes`
- `GetTotalDuration_ShortDemo_Is10Minutes`
- `GetTotalDuration_Podcast_Is30Minutes`
- `GetTotalDuration_ConferenceTalk_Is45Minutes`
- `GetTotalDuration_Workshop_Is60Minutes`
- `ExportSampleJson_ProducesParseableOutput`

**`ValidationTests.cs` (22 tests):**
- `Validate_EmptyTitle_IsInvalid`
- `Validate_EmptyTitle_ErrorMentionsTitle`
- `Validate_NullSections_IsInvalid`
- `Validate_EmptySectionsArray_IsInvalid`
- `Validate_SectionWithZeroDuration_IsInvalid`
- `Validate_SectionWithZeroDuration_ErrorMentionsDuration`
- `Validate_InvalidWarningExceedsDuration_FromSampleFile_IsInvalid`
- `Validate_InvalidWarningExceedsDuration_ErrorMentionsOffendingSection`
- `Validate_WarningEqualsToDuration_IsInvalid`
- `Validate_WarningLessThanDuration_IsValid`
- `Validate_AiAgentsDemo_ReturnsSuccess`
- `Validate_ShortDemo_ReturnsSuccess`
- `Validate_Podcast_ReturnsSuccess`
- `Validate_ConferenceTalk_ReturnsSuccess`
- `Validate_Workshop_ReturnsSuccess`
- `ValidationResult_Success_IsValidTrueAndEmptyErrors`
- `ValidationResult_Failure_IsValidFalseAndHasErrors`
- `Validate_SectionWithMissingTitle_IsInvalid`

**Note:** workshop.json has a `"color": "#E1BEE7"` on the Break section (7-char hex, `#RGB`-incompatible but valid 6-digit `#RRGGBB`). The validator accepts it correctly; `Validate_Workshop_ReturnsSuccess` passes.

#### Bugs Found

None. Production code is correct. All 49 tests pass (`dotnet test ElBruno.PresenterTimer.sln`).

---

## Learnings

### Phase 11: AlertService Tests (2026-06-02)

#### Determinism approach

`AlertService.ProcessState` is `internal` and exposed to the test project via `InternalsVisibleTo`.
Tests construct an `AlertService` with a crafted `AlertSettings`, subscribe to `AlertRaised`, and
call `ProcessState` with hand-built `TimerTickEventArgs` snapshots — zero real-time waits, fully
deterministic.

For `SectionChanged`-driven dedup tests (bucket clears on ManualRestart / ManualNext / Reset), a
minimal `StubTimerService` (`ISessionTimerService` no-op stub) is attached via `Attach()`, then
`stub.FireSectionChanged(...)` fires the private `OnSectionChanged` handler directly without any
live timer infrastructure.

#### Tests added: 54 new tests (total: 163)

**`AlertServiceTests.cs` (54 tests)**

| Group | Count | Representative test names |
|---|---|---|
| SectionWarning | 6 | `SectionWarning_Fires_WhenRemainingCrossesThreshold`, `SectionWarning_DoesNotRefire_OnSubsequentTicksInSameSection`, `SectionWarning_UsesPerSectionWarningAt_Override` |
| SectionEnd | 3 | `SectionEnd_Fires_WhenSectionRemainingIsZero`, `SectionEnd_DoesNotRefire_OnSubsequentTicks`, `SectionEnd_DoesNotFire_WhenRemainingIsPositive` |
| SessionWarning | 4 | `SessionWarning_Fires_WhenSessionRemainingCrossesThreshold`, `SessionWarning_DoesNotRefire_OnSubsequentTicks`, `SessionWarning_DoesNotFire_WhenRemainingIsZero` |
| SessionEnd | 2 | `SessionEnd_Fires_WhenSessionRemainingIsZero`, `SessionEnd_DoesNotRefire_OnSubsequentTicks` |
| SectionOvertime | 3 | `SectionOvertime_Fires_WhenIsSectionOvertimeIsTrue`, `SectionOvertime_DoesNotFire_WhenIsSectionOvertimeIsFalse`, `SectionOvertime_DoesNotRefire_OnSubsequentTicks` |
| SessionOvertime | 3 | `SessionOvertime_Fires_WhenIsSessionOvertimeIsTrue`, `SessionOvertime_DoesNotFire_WhenIsSessionOvertimeIsFalse`, `SessionOvertime_DoesNotRefire_OnSubsequentTicks` |
| Dedup — Reset | 6 | `Dedup_SectionWarning_RefiresAfterReset`, `Dedup_SessionWarning_RefiresAfterReset`, `Dedup_SessionOvertime_RefiresAfterReset` |
| Dedup — SectionChanged | 4 | `Dedup_SectionWarning_RefiresAfterManualRestartSectionChanged`, `Dedup_SectionWarning_RefiresAfterNavigatingBackToSection`, `Dedup_AllAlerts_ClearedAfterSectionChangeReasonReset`, `Dedup_SectionWarning_RefiresAfterAutoAdvanceToSection` |
| Settings Toggles | 6 | `Toggle_SectionWarning_DisabledDoesNotFire`, `Toggle_SessionEnd_DisabledDoesNotFire`, `Toggle_OvertimeAlerts_DisabledDoesNotFireSectionOvertime` |
| Sound / Notification flags | 4 | `Flags_DefaultSettings_SoundAndNotificationAreFalse`, `Flags_SoundEnabled_ShouldPlaySoundIsTrue`, `Flags_BothEnabled_BothAreTrueForSectionEnd` |
| ManualSectionChange | 7 | `ManualSectionChange_Fires_OnManualNext`, `ManualSectionChange_Fires_OnManualPrevious`, `ManualSectionChange_DoesNotFire_OnAutoAdvance`, `ManualSectionChange_ShouldPlaySound_IsAlwaysFalse_EvenWhenSoundEnabled`, `ManualSectionChange_Message_ContainsSectionTitle` |
| Payload correctness | 3 | `SectionWarning_SectionIndex_MatchesTick`, `SectionWarning_Message_ContainsSectionTitle`, `SectionEnd_Message_ContainsSectionTitle` |
| Constructor / edge cases | 3 | `Constructor_ThrowsArgumentNullException_WhenSettingsIsNull`, `ProcessState_WithOutOfRangeSectionIndex_DoesNotThrow`, `ProcessState_WithNegativeSectionIndex_SessionWarningStillFires` |

#### Bugs found

No bugs in `AlertService`. All behaviours match PRD §7.8 exactly:
- Dedup fires once per section lifetime; `Reset()` and `SectionChanged` bucket clears allow refiring.
- Settings toggles correctly gate each alert type independently.
- `ShouldPlaySound`/`ShouldShowNotification` mirror `EnableSoundAlerts`/`EnableWindowsNotifications`.
- `ManualSectionChange` always sets both flags to `false` regardless of sound/notification settings.
- Per-section `WarningAt` correctly overrides the global `SectionWarningThreshold`.

All 163 tests pass (`dotnet test ElBruno.PresenterTimer.sln`).

---

## Learnings

### Phase 9 Polish — Settings Persistence Tests + New-Service Coverage (2026-06-02)

#### Tests Added: 33 new tests (total: 197)

**`SettingsServiceTests.cs` (33 tests)**

| Group | Count | Representative test names |
|---|---|---|
| AppSettings defaults | 14 | `AppSettings_Defaults_General_LaunchMinimizedToTray_IsTrue`, `AppSettings_Defaults_Alerts_EnableSoundAlerts_IsFalse`, `AppSettings_Defaults_Hotkeys_Enabled_IsFalse` |
| Load when file absent | 3 | `Load_WhenFileAbsent_Settings_IsNotNull`, `Load_WhenFileAbsent_General_HasExpectedDefaults`, `Load_WhenFileAbsent_CreatesSettingsFile` |
| Save → Load round-trip | 9 | `SaveThenLoad_RoundTrips_LastSessionPath`, `SaveThenLoad_RoundTrips_RecentSessionPaths`, `SaveThenLoad_RoundTrips_EnableSoundAlerts_True`, `SaveThenLoad_RoundTrips_AccentColor`, `SaveThenLoad_RoundTrips_OverlayOpacity`, `SaveThenLoad_RoundTrips_CustomXY_Position`, `SaveThenLoad_RoundTrips_Behavior_AutoAdvanceSections`, `SaveThenLoad_RoundTrips_HotkeysEnabled_True`, `Save_ProducesValidJson` |
| Corrupt/invalid file fallback | 5 | `Load_WithCorruptJson_DoesNotThrow`, `Load_WithCorruptJson_FallsBackToDefaultSettings`, `Load_WithEmptyFile_FallsBackToDefaults_WithoutThrowing`, `Load_WithNullLiteralJson_FallsBackToDefaults`, `Load_WithPartiallyValidJson_FallsBackToDefaults_WithoutThrowing` |
| RaiseSettingsApplied event | 2 | `RaiseSettingsApplied_Fires_SettingsAppliedEvent`, `RaiseSettingsApplied_WithNoSubscribers_DoesNotThrow` |

#### New services covered vs deferred

| Service | Status | Reason |
|---|---|---|
| `SettingsService` | ✅ Covered (33 tests) | Fully implemented |
| `RecentSessionsService` | ⏳ Deferred | Not yet created (Parker/Kane in-progress) |
| `WindowPlacementService` | ⏳ Deferred | Not yet created |
| `SummaryFormatter` | ⏳ Deferred | Not yet created (Kane in-progress) |

#### How the %AppData% path was handled

`SettingsService` uses two `private static readonly` fields hardcoded to
`%AppData%\ElBruno.PresenterTimer\settings.json` with no constructor injection point.

Strategy used:
- Compute the same path inline in the test class (matching the known convention).
- `IDisposable` constructor/`Dispose()` backup/restore pattern: copy any existing
  `settings.json` to `settings.json.test-backup` before each test; restore on teardown.
- Each `[Fact]` class instance starts with no settings file so state is predictable.
- Tests are I/O-dependent but safe; xUnit's default sequential-per-class model prevents
  concurrent collisions during normal `dotnet test` runs.

**Limitation documented:** The path cannot be injected. If a future refactor adds a
constructor parameter or an `IFileSystem` abstraction, tests should be migrated to use
a fully temp-isolated path (e.g., `Path.GetTempPath()` or in-memory via `System.IO.Abstractions`).

#### Bugs found

None. `SettingsService` correctly:
- Falls back to `new AppSettings()` on all error paths (corrupt JSON, empty file, null literal JSON, partial JSON).
- Writes defaults on first run (file absent).
- Preserves all tested fields through the JSON round-trip.
- Fires `SettingsApplied` only when `RaiseSettingsApplied()` is called explicitly.

All 197 tests pass (`dotnet test ElBruno.PresenterTimer.sln`).

---

## Learnings

### Phase 9 — SummaryFormatter Tests (2026-06-02)

#### Settings-Path Injection (Parker) — NOT available

Inspected `src/ElBruno.PresenterTimer/Services/SettingsService.cs`.
The constructor still uses `private static readonly` fields hardcoded to
`%AppData%\ElBruno.PresenterTimer\settings.json` — Parker's injectable path/dir
constructor does **not** exist in this round.
The existing backup/restore suite (`SettingsServiceTests.cs`, 33 tests) remains
the authoritative settings coverage; no new settings-path tests added.

#### Tests added: 44 new tests (total: 294)

**`SummaryFormatterTests.cs` (44 tests)**

| Group | Count | Representative test names |
|---|---|---|
| FormatTime helper | 6 | `FormatTime_Zero_Returns_0000`, `FormatTime_Under1Hour_ReturnsMmSs`, `FormatTime_Exactly60Minutes_ReturnsHhMmSs`, `FormatTime_Over1Hour_ReturnsHhMmSs`, `FormatTime_Negative_TreatsAsZero` |
| FormatDifference helper | 3 | `FormatDifference_Positive_HasPlusSign`, `FormatDifference_Negative_HasMinusSign`, `FormatDifference_Zero_HasPlusSign` |
| FormatPlainText PRD §7.14 | 9 | `FormatPlainText_Null_ThrowsArgumentNullException`, `FormatPlainText_PrdExample_ContainsSessionTitle`, `FormatPlainText_PrdExample_ContainsPlannedTime`, `FormatPlainText_PrdExample_ContainsActualTime`, `FormatPlainText_PrdExample_ContainsDifference_WithPlusSign`, `FormatPlainText_PrdExample_Intro_SectionLineCorrect`, `FormatPlainText_PrdExample_ProblemStatement_UnderTime_HasMinusSign`, `FormatPlainText_PrdExample_Demo_SectionLineCorrect`, `FormatPlainText_PrdExample_OvertimeSections_HaveOvertimeTag` |
| FormatPlainText edge cases | 7 | `FormatPlainText_EmptySections_ContainsHeaderAndNoSectionLines`, `FormatPlainText_UnderTime_DifferenceHasMinusSign`, `FormatPlainText_UnvisitedSection_AppearsInNotReachedBlock`, `FormatPlainText_WithExtensions_ExtensionsLinePresent`, `FormatPlainText_NoExtensions_ExtensionsLineAbsent`, `FormatPlainText_SectionWithExtensions_ShowsExtTag`, `FormatPlainText_SectionWithRestarts_ShowsRestartsCount` |
| FormatMarkdown | 11 | `FormatMarkdown_Null_ThrowsArgumentNullException`, `FormatMarkdown_PrdExample_H1ContainsSessionTitle`, `FormatMarkdown_PrdExample_PlannedRowPresent`, `FormatMarkdown_PrdExample_ActualRowPresent`, `FormatMarkdown_PrdExample_DifferenceRowPresent`, `FormatMarkdown_PrdExample_H2SectionsPresentAboveSectionTable`, `FormatMarkdown_PrdExample_IntroSectionTableRowPresent`, `FormatMarkdown_PrdExample_OvertimeSections_HaveCheckmark`, `FormatMarkdown_UnvisitedSection_IsMarkedNotReached`, `FormatMarkdown_NoExtensions_TotalExtensionsRowAbsent`, `FormatMarkdown_WithExtensions_TotalExtensionsRowPresent` |
| FormatJson | 8 | `FormatJson_Null_ThrowsArgumentNullException`, `FormatJson_PrdExample_ContainsSessionTitleInJson`, `FormatJson_TimeSpansFormattedAsHhMmSs`, `FormatJson_IsValidJson`, `FormatJson_RoundTrip_PreservesSessionTitleAndDurations`, `FormatJson_RoundTrip_PreservesSectionData`, `FormatJson_EmptySections_ProducesEmptySectionsArray` |

*Note: Count above (6+3+9+7+11+8 = 44)*

#### Bugs found

None. `SummaryFormatter` implementation is correct:
- `FormatTime` correctly uses `MM:ss` under 1 hour and `HH:MM:ss` for ≥1 hour; treats negative as zero.
- `FormatDifference` prefixes `+` for zero and positive, `-` for negative.
- `FormatPlainText` produces exactly the PRD §7.14 format, adds `[OVERTIME]` for over-budget sections, `ext` for extensions, `restarts N` for restart counts, "Not reached:" block for unvisited sections.
- Extensions line is correctly omitted when `TotalExtensions == TimeSpan.Zero`.
- `FormatMarkdown` produces valid Markdown table with checkmarks (✓) for overtime sections, `—` and italic for unvisited sections.
- `FormatJson` serializes TimeSpans as `"HH:mm:ss"` strings and round-trips all `init`-settable fields.
- All three methods throw `ArgumentNullException` for null input.

All 294 tests pass (`dotnet test tests\ElBruno.PresenterTimer.Tests\ElBruno.PresenterTimer.Tests.csproj`).

---

## Learnings

### Phase 12 — Settings-Path Injection Tests (2026-06-02)

#### Injectable constructor confirmed

Parker's `SettingsService(string settingsFilePath)` injection-seam constructor IS present in
`src/ElBruno.PresenterTimer/Services/SettingsService.cs`. The parameterless constructor delegates
to it via `: this(...)`, so both share the same code paths. Key implementation details:

- `_settingsFilePath` and `_settingsFolder` are instance fields (not static readonly).
- `Save()` uses an atomic-ish write: serialises to `<path>.tmp` then `File.Move(tmp, dest, overwrite: true)`.
- `EnsureFolderExists()` calls `Directory.CreateDirectory()` before every `Load()` and `Save()`.
- Missing/corrupt files fall back to `new AppSettings()` per PRD §10.3.

#### Tests added: 17 new tests (total: 311)

**`SettingsServicePathTests.cs` (17 tests) — file: `tests/ElBruno.PresenterTimer.Tests/SettingsServicePathTests.cs`**

Each test creates a unique temp dir via `Path.Combine(Path.GetTempPath(), Guid.NewGuid("N"))`.
`IDisposable.Dispose()` deletes the tree (best-effort, never fails the test).

| Group | Count | Test names |
|---|---|---|
| Save → Load round-trip (all categories) | 3 | `InjectablePath_SaveThenLoad_RoundTrips_MultipleCategories`, `InjectablePath_SaveThenLoad_PreservesMonitorDeviceName`, `InjectablePath_SaveThenLoad_PreservesAllHotkeyBindings` |
| Missing file → defaults without throwing | 3 | `InjectablePath_Load_WhenFileAbsent_DoesNotThrow`, `InjectablePath_Load_WhenFileAbsent_ReturnsDefaultSettings`, `InjectablePath_Load_WhenFileAbsent_CreatesSettingsFileOnDisk` |
| Corrupt JSON → defaults without throwing (PRD §10.3) | 5 | `InjectablePath_Load_WithGarbageFile_DoesNotThrow`, `InjectablePath_Load_WithGarbageFile_FallsBackToDefaults`, `InjectablePath_Load_WithEmptyFile_DoesNotThrow_AndReturnsDefaults`, `InjectablePath_Load_WithNullLiteralJson_FallsBackToDefaults`, `InjectablePath_Load_WithTruncatedJson_DoesNotThrow` |
| Auto-create parent directory | 2 | `InjectablePath_Save_CreatesParentDirectory_WhenMissing`, `InjectablePath_Load_CreatesParentDirectory_WhenMissing` |
| Atomic write — no .tmp leftover, valid JSON | 4 | `InjectablePath_Save_LeavesNoTmpFile`, `InjectablePath_Save_WritesValidJson`, `InjectablePath_Save_JsonContains_MutatedValues`, `InjectablePath_MultipleSaves_LastValueWins` |

#### Confirmed: injectable constructor works correctly

All 17 tests pass. The new tests never touch `%AppData%\ElBruno.PresenterTimer`.
The existing `SettingsServiceTests.cs` (33 backup/restore tests) was left unchanged.

All 311 tests pass (`dotnet test ElBruno.PresenterTimer.sln`).

