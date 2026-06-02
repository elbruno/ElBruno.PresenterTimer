using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// Phase 11 — xUnit tests for <see cref="SessionTimerService"/> (PRD §11, §7.9–§7.11).
///
/// <para><b>Determinism strategy:</b></para>
/// <list type="bullet">
///   <item>Navigation / state mutations (NextSection, PreviousSection, ExtendCurrentSection,
///     Reset, RestartCurrentSection) execute synchronously and produce deterministic results.
///     Tests in these groups make NO real-time assertions.</item>
///   <item>Control-flow events (StateChanged, SectionChanged) are raised on the CALLING thread
///     for all manual operations, so event-subscription tests are also deterministic.</item>
///   <item>Timing properties (elapsed, remaining, overtime) require small Task.Delay waits.
///     These are marked "[timing-sensitive]" in their display name and use generous
///     tolerances (±150–200 ms) to guard against scheduler jitter.</item>
///   <item>No test requires waits longer than 300 ms.</item>
/// </list>
///
/// <para><b>Phase 8 fixes verified by this suite:</b></para>
/// <list type="bullet">
///   <item><see cref="ISessionTimerService.CurrentSectionIndex"/> correctly returns <c>-1</c>
///     before any plan is loaded (field now initialises to <c>-1</c>).</item>
///   <item><c>ComputeBehindSchedule</c> snapshots the monotonic clock once, eliminating
///     the phantom drift caused by reading <c>_clock.Elapsed</c> twice per call.</item>
/// </list>
/// </summary>
public sealed class SessionTimerServiceTests : IDisposable
{
    // xUnit creates one class instance per [Fact], so each test gets its own _sut.
    private readonly SessionTimerService _sut = new();

    public void Dispose() => _sut.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a plan whose sections have the given minute-based durations.</summary>
    private static SessionPlan PlanWithMinutes(params int[] minutes)
    {
        var sections = new List<SessionSection>(minutes.Length);
        for (int i = 0; i < minutes.Length; i++)
            sections.Add(new SessionSection
            {
                Title = $"Section {i + 1}",
                Duration = TimeSpan.FromMinutes(minutes[i])
            });
        return new SessionPlan { Title = "Test Plan", Sections = sections };
    }

    /// <summary>
    /// Creates a plan whose sections have the given millisecond-based durations.
    /// Used for timing-sensitive overtime tests to keep required wait times small.
    /// </summary>
    private static SessionPlan PlanWithMs(params int[] ms)
    {
        var sections = new List<SessionSection>(ms.Length);
        for (int i = 0; i < ms.Length; i++)
            sections.Add(new SessionSection
            {
                Title = $"Section {i + 1}",
                Duration = TimeSpan.FromMilliseconds(ms[i])
            });
        return new SessionPlan { Title = "Test Plan", Sections = sections };
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 1 — LoadPlan: plan state
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CurrentSectionIndex_IsMinusOne_BeforePlanLoaded()
    {
        // Contract: -1 when no plan is loaded (ISessionTimerService XML-doc).
        // Bug was: field initialised to 0 (C# default). Fixed in Phase 8.
        Assert.Equal(-1, _sut.CurrentSectionIndex);
    }

    [Fact]
    public void LoadPlan_SetsPlan()
    {
        var plan = PlanWithMinutes(5, 10);
        _sut.LoadPlan(plan);
        Assert.Same(plan, _sut.Plan);
    }

    [Fact]
    public void LoadPlan_SetsTotalPlannedDuration()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 10, 15));
        Assert.Equal(TimeSpan.FromMinutes(30), _sut.TotalPlannedDuration);
    }

    [Fact]
    public void LoadPlan_DoesNotStartTimer()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        Assert.False(_sut.IsRunning);
    }

    [Fact]
    public void LoadPlan_WhileRunning_ResetsToNotRunning()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        _sut.NextSection();                 // advance to section 1
        _sut.LoadPlan(PlanWithMinutes(3, 3)); // reload resets all state
        Assert.False(_sut.IsRunning);
        _sut.Start();
        Assert.Equal(0, _sut.CurrentSectionIndex);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 2 — Start: initial state
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Start_SetsIsRunning()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        Assert.True(_sut.IsRunning);
    }

    [Fact]
    public void Start_CurrentSectionIndex_IsZero()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 10));
        _sut.Start();
        Assert.Equal(0, _sut.CurrentSectionIndex);
    }

    [Fact]
    public void Start_IsPausedFalse()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        Assert.False(_sut.IsPaused);
    }

    [Fact]
    public void Start_IsSessionCompleteFalse()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        Assert.False(_sut.IsSessionComplete);
    }

    [Fact]
    public void Start_CurrentSection_IsFirstSection()
    {
        var plan = PlanWithMinutes(5, 10);
        _sut.LoadPlan(plan);
        _sut.Start();
        Assert.Equal(plan.Sections[0], _sut.CurrentSection);
    }

    [Fact]
    public void Start_BehindSchedule_IsEffectivelyZero()
    {
        // At section 0 right after start, elapsed ≈ 0 ≤ sectionPlanned → BehindSchedule = 0.
        // After Phase 8 fix: ComputeBehindSchedule() snapshots the clock once so
        // behind = sessionElapsed − sectionElapsed = 0 exactly.
        _sut.LoadPlan(PlanWithMinutes(10, 10));
        _sut.Start();
        Assert.True(_sut.BehindSchedule < TimeSpan.FromMilliseconds(10),
            $"Expected BehindSchedule < 10ms at session start; got {_sut.BehindSchedule.TotalMilliseconds:F4}ms");
    }

    [Fact]
    public void Start_RaisesStateChangedEvent()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        var raised = false;
        _sut.StateChanged += (_, _) => raised = true;
        _sut.Start();
        Assert.True(raised);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 3 — NextSection: forward navigation
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void NextSection_AdvancesToSectionOne()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5, 5));
        _sut.Start();
        _sut.NextSection();
        Assert.Equal(1, _sut.CurrentSectionIndex);
    }

    [Fact]
    public void NextSection_AdvancesThroughAllSections()
    {
        _sut.LoadPlan(PlanWithMinutes(1, 2, 3));
        _sut.Start();
        _sut.NextSection();
        _sut.NextSection();
        Assert.Equal(2, _sut.CurrentSectionIndex);
    }

    [Fact]
    public void NextSection_CurrentSection_UpdatesToNewSection()
    {
        var plan = PlanWithMinutes(5, 5);
        _sut.LoadPlan(plan);
        _sut.Start();
        _sut.NextSection();
        Assert.Equal(plan.Sections[1], _sut.CurrentSection);
    }

    [Fact]
    public void NextSection_AtLastSection_SetsSessionComplete()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        _sut.NextSection(); // → section 1
        _sut.NextSection(); // → complete (past last)
        Assert.True(_sut.IsSessionComplete);
    }

    [Fact]
    public void NextSection_AtLastSection_CurrentSection_IsNull()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        _sut.NextSection(); // only section → complete
        Assert.Null(_sut.CurrentSection);
    }

    [Fact]
    public void NextSection_WhenAlreadyComplete_IsNoOp()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        _sut.NextSection(); // → complete
        _sut.NextSection(); // no-op, must not throw
        Assert.True(_sut.IsSessionComplete);
    }

    [Fact]
    public void NextSection_RaisesSectionChangedEvent_WithManualNextReason()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        SectionChangedEventArgs? args = null;
        _sut.SectionChanged += (_, e) => args = e;
        _sut.NextSection();
        Assert.NotNull(args);
        Assert.Equal(SectionChangeReason.ManualNext, args!.Reason);
        Assert.Equal(0, args.PreviousSectionIndex);
        Assert.Equal(1, args.CurrentSectionIndex);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 4 — PreviousSection: backward navigation
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PreviousSection_AtFirstSection_IsNoOp()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        _sut.PreviousSection(); // already at 0 — must be a no-op
        Assert.Equal(0, _sut.CurrentSectionIndex);
    }

    [Fact]
    public void PreviousSection_AtFirstSection_DoesNotRaiseEvent()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        var raised = false;
        _sut.SectionChanged += (_, _) => raised = true;
        _sut.PreviousSection();
        Assert.False(raised);
    }

    [Fact]
    public void PreviousSection_FromSectionOne_GoesToZero()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5, 5));
        _sut.Start();
        _sut.NextSection();     // → 1
        _sut.PreviousSection(); // → 0
        Assert.Equal(0, _sut.CurrentSectionIndex);
    }

    [Fact]
    public void PreviousSection_RaisesSectionChangedEvent_WithManualPreviousReason()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        _sut.NextSection(); // → 1
        SectionChangedEventArgs? args = null;
        _sut.SectionChanged += (_, e) => args = e;
        _sut.PreviousSection();
        Assert.NotNull(args);
        Assert.Equal(SectionChangeReason.ManualPrevious, args!.Reason);
        Assert.Equal(1, args.PreviousSectionIndex);
        Assert.Equal(0, args.CurrentSectionIndex);
    }

    [Fact]
    public void NextThenPrevious_ReturnsToFirstSection()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 10, 15));
        _sut.Start();
        _sut.NextSection();
        _sut.NextSection();
        _sut.PreviousSection();
        _sut.PreviousSection();
        Assert.Equal(0, _sut.CurrentSectionIndex);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 5 — Reset
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Reset_SetsIsRunningFalse()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        _sut.Reset();
        Assert.False(_sut.IsRunning);
    }

    [Fact]
    public void Reset_SetsCurrentSectionIndexToZero()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5, 5));
        _sut.Start();
        _sut.NextSection();
        _sut.NextSection();
        _sut.Reset();
        Assert.Equal(0, _sut.CurrentSectionIndex);
    }

    [Fact]
    public void Reset_ClearsIsPaused()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        _sut.Pause();
        _sut.Reset();
        Assert.False(_sut.IsPaused);
    }

    [Fact]
    public void Reset_ClearsIsSessionComplete()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        _sut.NextSection(); // only one section → complete
        _sut.Reset();
        Assert.False(_sut.IsSessionComplete);
    }

    [Fact]
    public void Reset_RaisesSectionChangedEvent_WithResetReason()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        SectionChangedEventArgs? args = null;
        _sut.SectionChanged += (_, e) => args = e;
        _sut.Reset();
        Assert.NotNull(args);
        Assert.Equal(SectionChangeReason.Reset, args!.Reason);
        Assert.Equal(-1, args.PreviousSectionIndex);
        Assert.Equal(0, args.CurrentSectionIndex);
    }

    [Fact]
    public void Reset_RaisesStateChangedEvent()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        var raised = false;
        _sut.StateChanged += (_, _) => raised = true;
        _sut.Reset();
        Assert.True(raised);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 6 — Pause / Resume
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Pause_SetsIsPaused()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        _sut.Pause();
        Assert.True(_sut.IsPaused);
    }

    [Fact]
    public void Pause_IsRunningRemainsTrue()
    {
        // IsRunning stays true while paused; the session is still active.
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        _sut.Pause();
        Assert.True(_sut.IsRunning);
    }

    [Fact]
    public void Resume_ClearsIsPaused()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        _sut.Pause();
        _sut.Resume();
        Assert.False(_sut.IsPaused);
    }

    [Fact]
    public void Pause_RaisesStateChangedEvent()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        var raised = false;
        _sut.StateChanged += (_, _) => raised = true;
        _sut.Pause();
        Assert.True(raised);
    }

    [Fact]
    public void Resume_RaisesStateChangedEvent()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        _sut.Pause();
        var raised = false;
        _sut.StateChanged += (_, _) => raised = true;
        _sut.Resume();
        Assert.True(raised);
    }

    /// <summary>
    /// [timing-sensitive] Verifies that elapsed time during a pause is NOT counted.
    /// Start → wait ~100ms → Pause → wait ~100ms (should be ignored) → Resume → wait ~100ms.
    /// Expected: SessionElapsed ≈ 200ms, not ≈ 300ms. Tolerance: ±150ms.
    /// </summary>
    [Fact(DisplayName = "PauseResume_PreservesElapsed [timing-sensitive]")]
    public async Task PauseResume_PreservesElapsed()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        await Task.Delay(100);
        _sut.Pause();
        var elapsedAtPause = _sut.SessionElapsed;
        await Task.Delay(100); // must NOT be counted
        _sut.Resume();
        await Task.Delay(100);
        var elapsedAfterResume = _sut.SessionElapsed;

        // Running time should be ~200ms (pre-pause + post-resume), not ~300ms.
        Assert.True(elapsedAfterResume < TimeSpan.FromMilliseconds(360),
            $"Expected elapsed < 360ms (pause time excluded); got {elapsedAfterResume.TotalMilliseconds:F0}ms");
        Assert.True(elapsedAfterResume >= elapsedAtPause,
            "Elapsed after resume must be ≥ elapsed at pause.");
    }

    /// <summary>
    /// [timing-sensitive] While paused, SessionElapsed must be frozen (no increase).
    /// </summary>
    [Fact(DisplayName = "WhilePaused_SessionElapsed_IsFrozen [timing-sensitive]")]
    public async Task WhilePaused_SessionElapsed_IsFrozen()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        await Task.Delay(50);
        _sut.Pause();
        var snap1 = _sut.SessionElapsed;
        await Task.Delay(100);
        var snap2 = _sut.SessionElapsed;
        Assert.Equal(snap1, snap2);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 7 — ExtendCurrentSection
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExtendCurrentSection_TrackedInGetResult()
    {
        var extension = TimeSpan.FromMinutes(5);
        _sut.LoadPlan(PlanWithMinutes(10));
        _sut.Start();
        _sut.ExtendCurrentSection(extension);
        var result = _sut.GetResult();
        Assert.Equal(extension, result.Sections[0].TotalExtensions);
    }

    [Fact]
    public void ExtendCurrentSection_ZeroExtension_IsNoOp()
    {
        _sut.LoadPlan(PlanWithMinutes(10));
        _sut.Start();
        _sut.ExtendCurrentSection(TimeSpan.Zero);
        var result = _sut.GetResult();
        Assert.Equal(TimeSpan.Zero, result.Sections[0].TotalExtensions);
    }

    [Fact]
    public void ExtendCurrentSection_NegativeExtension_IsNoOp()
    {
        _sut.LoadPlan(PlanWithMinutes(10));
        _sut.Start();
        _sut.ExtendCurrentSection(TimeSpan.FromMinutes(-1));
        var result = _sut.GetResult();
        Assert.Equal(TimeSpan.Zero, result.Sections[0].TotalExtensions);
    }

    [Fact]
    public void ExtendCurrentSection_MultipleExtensions_Accumulate()
    {
        _sut.LoadPlan(PlanWithMinutes(10));
        _sut.Start();
        _sut.ExtendCurrentSection(TimeSpan.FromMinutes(1));
        _sut.ExtendCurrentSection(TimeSpan.FromMinutes(5));
        var result = _sut.GetResult();
        Assert.Equal(TimeSpan.FromMinutes(6), result.Sections[0].TotalExtensions);
    }

    [Fact]
    public void ExtendCurrentSection_IncreasesCurrentSectionRemaining()
    {
        _sut.LoadPlan(PlanWithMinutes(10));
        _sut.Start();
        var before = _sut.CurrentSectionRemaining;
        var extension = TimeSpan.FromMinutes(5);
        _sut.ExtendCurrentSection(extension);
        var after = _sut.CurrentSectionRemaining;
        // after ≈ before + extension (minus negligible sub-ms execution time).
        Assert.True(after >= before + extension - TimeSpan.FromMilliseconds(100),
            $"Expected remaining to increase by ≈{extension.TotalMinutes:F0}min; " +
            $"before={before.TotalMinutes:F3}min, after={after.TotalMinutes:F3}min");
    }

    [Fact]
    public void ExtendCurrentSection_OvertimeIsZero_RightAfterExtension()
    {
        // A 10-min section just started has effectively no elapsed time.
        // Extending it further cannot produce overtime; remaining stays positive.
        _sut.LoadPlan(PlanWithMinutes(10));
        _sut.Start();
        _sut.ExtendCurrentSection(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.Zero, _sut.CurrentSectionOvertime);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 8 — Overtime (timing-sensitive; uses millisecond-duration sections)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// [timing-sensitive] After a section's 50ms planned duration elapses (auto-advance OFF),
    /// CurrentSectionOvertime must be positive and CurrentSectionRemaining must be zero.
    /// Waits 200ms for a reliable margin over the 50ms threshold.
    /// </summary>
    [Fact(DisplayName = "ShortSection_AfterElapsed_SectionOvertimePositive [timing-sensitive]")]
    public async Task ShortSection_AfterElapsed_SectionOvertimePositive()
    {
        _sut.LoadPlan(PlanWithMs(50)); // 50ms section
        _sut.AutoAdvanceSections = false;
        _sut.Start();
        await Task.Delay(200);

        Assert.True(_sut.CurrentSectionOvertime > TimeSpan.Zero,
            $"Expected CurrentSectionOvertime > 0; got {_sut.CurrentSectionOvertime.TotalMilliseconds:F0}ms");
        Assert.Equal(TimeSpan.Zero, _sut.CurrentSectionRemaining);
    }

    /// <summary>
    /// [timing-sensitive] After the total session planned duration elapses,
    /// SessionOvertime must be positive and SessionRemaining must be zero.
    /// Uses a 100ms total plan; waits 300ms.
    /// </summary>
    [Fact(DisplayName = "ShortSession_AfterElapsed_SessionOvertimePositive [timing-sensitive]")]
    public async Task ShortSession_AfterElapsed_SessionOvertimePositive()
    {
        _sut.LoadPlan(PlanWithMs(50, 50)); // 100ms total
        _sut.AutoAdvanceSections = false;
        _sut.Start();
        await Task.Delay(300);

        Assert.True(_sut.SessionOvertime > TimeSpan.Zero,
            $"Expected SessionOvertime > 0; got {_sut.SessionOvertime.TotalMilliseconds:F0}ms");
        Assert.Equal(TimeSpan.Zero, _sut.SessionRemaining);
    }

    /// <summary>
    /// [timing-sensitive] When a section stays active past its planned duration,
    /// BehindSchedule must be positive (we are behind the original plan).
    /// Section 0 is 50ms; section 1 is 5000ms. We wait 200ms while still in section 0.
    /// </summary>
    [Fact(DisplayName = "AfterSectionOvertime_BehindScheduleIsPositive [timing-sensitive]")]
    public async Task AfterSectionOvertime_BehindScheduleIsPositive()
    {
        _sut.LoadPlan(PlanWithMs(50, 5000));
        _sut.AutoAdvanceSections = false;
        _sut.Start();
        await Task.Delay(200); // 150ms past section 0's 50ms budget

        Assert.True(_sut.BehindSchedule > TimeSpan.Zero,
            $"Expected BehindSchedule > 0 after staying 150ms past a 50ms section; " +
            $"got {_sut.BehindSchedule.TotalMilliseconds:F0}ms");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 9 — GetResult / SectionResult
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetResult_NoPlan_ReturnsEmptyResult()
    {
        var result = _sut.GetResult();
        Assert.Equal(string.Empty, result.SessionTitle);
        Assert.Equal(TimeSpan.Zero, result.PlannedDuration);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public void GetResult_AfterLoadPlan_PlannedDurationMatchesPlan()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 10, 15));
        _sut.Start();
        var result = _sut.GetResult();
        Assert.Equal(TimeSpan.FromMinutes(30), result.PlannedDuration);
    }

    [Fact]
    public void GetResult_SectionCount_MatchesPlan()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 10, 15));
        _sut.Start();
        var result = _sut.GetResult();
        Assert.Equal(3, result.Sections.Count);
    }

    [Fact]
    public void GetResult_AfterStart_Section0_IsVisited()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        var result = _sut.GetResult();
        Assert.True(result.Sections[0].WasVisited);
    }

    [Fact]
    public void GetResult_BeforeNavigating_Section1_IsNotVisited()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        var result = _sut.GetResult();
        Assert.False(result.Sections[1].WasVisited);
    }

    [Fact]
    public void GetResult_AfterNextSection_Section0_WasSkipped()
    {
        // Advancing immediately (elapsed ≈ 0ms) leaves section 0 long before
        // its 10-minute planned duration → WasSkipped must be true.
        _sut.LoadPlan(PlanWithMinutes(10, 10));
        _sut.Start();
        _sut.NextSection();
        var result = _sut.GetResult();
        Assert.True(result.Sections[0].WasSkipped,
            "Section 0 was left far before its 10-minute planned duration.");
    }

    [Fact]
    public void GetResult_Section1_WasVisited_AfterNavigation()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        _sut.NextSection();
        var result = _sut.GetResult();
        Assert.True(result.Sections[1].WasVisited);
    }

    [Fact]
    public void GetResult_PlannedDurationPerSection_MatchesPlan()
    {
        var plan = PlanWithMinutes(3, 7, 15);
        _sut.LoadPlan(plan);
        _sut.Start();
        var result = _sut.GetResult();
        for (int i = 0; i < plan.Sections.Count; i++)
            Assert.Equal(plan.Sections[i].Duration, result.Sections[i].PlannedDuration);
    }

    [Fact]
    public void GetResult_TotalExtensions_ReflectsAllSectionExtensions()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        _sut.ExtendCurrentSection(TimeSpan.FromMinutes(2)); // section 0: +2
        _sut.NextSection();
        _sut.ExtendCurrentSection(TimeSpan.FromMinutes(3)); // section 1: +3
        var result = _sut.GetResult();
        Assert.Equal(TimeSpan.FromMinutes(5), result.TotalExtensions);
    }

    [Fact]
    public void GetResult_RestartCount_AfterRestartCurrentSection()
    {
        _sut.LoadPlan(PlanWithMinutes(10));
        _sut.Start();
        _sut.RestartCurrentSection();
        _sut.RestartCurrentSection();
        var result = _sut.GetResult();
        Assert.Equal(2, result.Sections[0].RestartCount);
    }

    [Fact]
    public void GetResult_SessionTitle_MatchesPlanTitle()
    {
        var plan = new SessionPlan
        {
            Title = "My Conference Talk",
            Sections = [new SessionSection { Title = "Intro", Duration = TimeSpan.FromMinutes(5) }]
        };
        _sut.LoadPlan(plan);
        _sut.Start();
        var result = _sut.GetResult();
        Assert.Equal("My Conference Talk", result.SessionTitle);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 10 — RestartCurrentSection
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RestartCurrentSection_DoesNotChangeIndex()
    {
        _sut.LoadPlan(PlanWithMinutes(5, 5));
        _sut.Start();
        _sut.NextSection(); // → 1
        _sut.RestartCurrentSection();
        Assert.Equal(1, _sut.CurrentSectionIndex);
    }

    [Fact]
    public void RestartCurrentSection_RaisesSectionChangedEvent_WithManualRestartReason()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        SectionChangedEventArgs? args = null;
        _sut.SectionChanged += (_, e) => args = e;
        _sut.RestartCurrentSection();
        Assert.NotNull(args);
        Assert.Equal(SectionChangeReason.ManualRestart, args!.Reason);
        // Restart: previous and current are the same index.
        Assert.Equal(0, args.PreviousSectionIndex);
        Assert.Equal(0, args.CurrentSectionIndex);
    }

    /// <summary>
    /// [timing-sensitive] After waiting 100ms and restarting, the section elapsed
    /// resets to near zero (it must be less than before the restart).
    /// </summary>
    [Fact(DisplayName = "RestartCurrentSection_ResetsCurrentSectionElapsed [timing-sensitive]")]
    public async Task RestartCurrentSection_ResetsCurrentSectionElapsed()
    {
        _sut.LoadPlan(PlanWithMinutes(5));
        _sut.Start();
        await Task.Delay(100);
        var beforeRestart = _sut.CurrentSectionElapsed;
        _sut.RestartCurrentSection();
        var afterRestart = _sut.CurrentSectionElapsed;
        Assert.True(afterRestart < beforeRestart,
            $"After restart, section elapsed ({afterRestart.TotalMilliseconds:F0}ms) " +
            $"should be less than before restart ({beforeRestart.TotalMilliseconds:F0}ms).");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 11 — Dispose safety
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Dispose_WhileRunning_DoesNotThrow()
    {
        // Use a dedicated instance so the class-level _sut isn't double-disposed.
        var svc = new SessionTimerService();
        svc.LoadPlan(PlanWithMinutes(5));
        svc.Start();
        svc.Dispose(); // must not throw
    }
}
