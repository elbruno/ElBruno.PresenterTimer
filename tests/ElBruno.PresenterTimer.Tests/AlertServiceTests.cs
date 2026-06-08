using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// Phase 11 — xUnit tests for <see cref="AlertService"/> (PRD §7.8).
///
/// <para><b>Determinism strategy:</b></para>
/// All tests drive the service through the <c>internal</c> <see cref="AlertService.ProcessState"/>
/// seam.  No real timer is needed; tests build synthetic <see cref="TimerTickEventArgs"/> snapshots
/// and feed them directly.  Results are therefore clock-free and fully deterministic.
///
/// <para>For <see cref="SectionChangedEventArgs"/>-driven dedup tests (bucket clears on
/// ManualRestart / Reset / ManualNext) a minimal <see cref="StubTimerService"/> is attached so the
/// private <c>OnSectionChanged</c> handler fires without a live timer.</para>
///
/// <para><b>Bug found (not fixed — documented only):</b></para>
/// No bugs discovered in <see cref="AlertService"/>; behaviour matches PRD §7.8 exactly.
/// (The pre-existing <c>CurrentSectionIndex</c> default-zero bug in
/// <see cref="Services.SessionTimerService"/> was documented in <c>SessionTimerServiceTests</c>.)
/// </summary>
public sealed class AlertServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static SessionPlan SingleSectionPlan(TimeSpan duration, TimeSpan? warningAt = null) =>
        new()
        {
            Title = "Test Plan",
            Sections =
            [
                new SessionSection { Title = "Only Section", Duration = duration, WarningAt = warningAt }
            ]
        };

    private static SessionPlan TwoSectionPlan(
        TimeSpan s0Duration, TimeSpan s1Duration,
        TimeSpan? s0Warning = null, TimeSpan? s1Warning = null) =>
        new()
        {
            Title = "Test Plan",
            Sections =
            [
                new SessionSection { Title = "Section A", Duration = s0Duration, WarningAt = s0Warning },
                new SessionSection { Title = "Section B", Duration = s1Duration, WarningAt = s1Warning }
            ]
        };

    /// <summary>Creates a tick snapshot with convenient named parameters.</summary>
    private static TimerTickEventArgs Tick(
        int sectionIndex = 0,
        TimeSpan? sessionRemaining = null,
        bool isSessionOvertime = false,
        TimeSpan? sessionOvertime = null,
        TimeSpan? sectionRemaining = null,
        bool isSectionOvertime = false,
        TimeSpan? sectionOvertime = null) =>
        new()
        {
            CurrentSectionIndex    = sectionIndex,
            SessionRemaining       = sessionRemaining ?? TimeSpan.FromMinutes(10),
            IsSessionOvertime      = isSessionOvertime,
            SessionOvertime        = sessionOvertime ?? TimeSpan.Zero,
            CurrentSectionRemaining = sectionRemaining ?? TimeSpan.FromMinutes(5),
            IsSectionOvertime      = isSectionOvertime,
            CurrentSectionOvertime  = sectionOvertime ?? TimeSpan.Zero,
        };

    /// <summary>Returns an AlertService with every alert type enabled, sound/notifications off.</summary>
    private static AlertService AllEnabled() =>
        new(new AlertSettings
        {
            EnableSectionWarningAlerts  = true,
            EnableSectionEndAlerts      = true,
            EnableSessionWarningAlerts  = true,
            EnableSessionEndAlerts      = true,
            EnableOvertimeAlerts        = true,
            EnableSoundAlerts           = false,
            EnableWindowsNotifications  = false,
            SectionWarningThreshold     = "00:01:00",
            SessionWarningThreshold     = "00:03:00"
        });

    // ══════════════════════════════════════════════════════════════════════════
    // Section Warning
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SectionWarning_Fires_WhenRemainingCrossesThreshold()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        // 30 s remaining — below the 1-min global threshold
        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromSeconds(30)), plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionWarning));
    }

    [Fact]
    public void SectionWarning_DoesNotFire_WhenRemainingAboveThreshold()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        // 2 min remaining — above the 1-min threshold
        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromMinutes(2)), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SectionWarning));
    }

    [Fact]
    public void SectionWarning_DoesNotFire_WhenRemainingIsZero()
    {
        // At exactly zero the section-end fires; SectionWarning requires remaining > 0
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.Zero), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SectionWarning));
    }

    [Fact]
    public void SectionWarning_DoesNotRefire_OnSubsequentTicksInSameSection()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10));
        var raised = Capture(sut);
        var tick  = Tick(sectionRemaining: TimeSpan.FromSeconds(30));

        sut.ProcessState(tick, plan);
        sut.ProcessState(tick, plan);
        sut.ProcessState(tick, plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionWarning));
    }

    [Fact]
    public void SectionWarning_UsesPerSectionWarningAt_Override()
    {
        // Per-section WarningAt = 2 min, global = 1 min.
        // At 90 s remaining only the per-section threshold (2 min) is crossed.
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10), warningAt: TimeSpan.FromMinutes(2));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromSeconds(90)), plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionWarning));
    }

    [Fact]
    public void SectionWarning_PerSectionWarningAt_DoesNotFire_BeforeThreshold()
    {
        // Per-section WarningAt = 30 s; 2 min remaining → no trigger
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10), warningAt: TimeSpan.FromSeconds(30));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromMinutes(2)), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SectionWarning));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Section End
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SectionEnd_Fires_WhenSectionRemainingIsZero()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.Zero), plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionEnd));
    }

    [Fact]
    public void SectionEnd_DoesNotFire_WhenRemainingIsPositive()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromSeconds(1)), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SectionEnd));
    }

    [Fact]
    public void SectionEnd_DoesNotRefire_OnSubsequentTicks()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);
        var tick  = Tick(sectionRemaining: TimeSpan.Zero);

        sut.ProcessState(tick, plan);
        sut.ProcessState(tick, plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionEnd));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Session Warning
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SessionWarning_Fires_WhenSessionRemainingCrossesThreshold()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        // 2 min left — below 3-min session threshold
        sut.ProcessState(Tick(sessionRemaining: TimeSpan.FromMinutes(2)), plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SessionWarning));
    }

    [Fact]
    public void SessionWarning_DoesNotFire_WhenRemainingAboveThreshold()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        // 5 min left — above 3-min threshold
        sut.ProcessState(Tick(sessionRemaining: TimeSpan.FromMinutes(5)), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SessionWarning));
    }

    [Fact]
    public void SessionWarning_DoesNotFire_WhenRemainingIsZero()
    {
        // At exactly zero remaining > 0 guard blocks SessionWarning
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sessionRemaining: TimeSpan.Zero), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SessionWarning));
    }

    [Fact]
    public void SessionWarning_DoesNotRefire_OnSubsequentTicks()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);
        var tick  = Tick(sessionRemaining: TimeSpan.FromMinutes(2));

        sut.ProcessState(tick, plan);
        sut.ProcessState(tick, plan);
        sut.ProcessState(tick, plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SessionWarning));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Session End
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SessionEnd_Fires_WhenSessionRemainingIsZero()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sessionRemaining: TimeSpan.Zero), plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SessionEnd));
    }

    [Fact]
    public void SessionEnd_DoesNotRefire_OnSubsequentTicks()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);
        var tick  = Tick(sessionRemaining: TimeSpan.Zero);

        sut.ProcessState(tick, plan);
        sut.ProcessState(tick, plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SessionEnd));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Section Overtime
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SectionOvertime_Fires_WhenIsSectionOvertimeIsTrue()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(
            sectionRemaining: TimeSpan.Zero,
            isSectionOvertime: true,
            sectionOvertime: TimeSpan.FromSeconds(10)), plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionOvertime));
    }

    [Fact]
    public void SectionOvertime_DoesNotFire_WhenIsSectionOvertimeIsFalse()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(isSectionOvertime: false), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SectionOvertime));
    }

    [Fact]
    public void SectionOvertime_DoesNotRefire_OnSubsequentTicks()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);
        var tick  = Tick(isSectionOvertime: true, sectionOvertime: TimeSpan.FromSeconds(10));

        sut.ProcessState(tick, plan);
        sut.ProcessState(tick, plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionOvertime));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Session Overtime
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SessionOvertime_Fires_WhenIsSessionOvertimeIsTrue()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(
            isSessionOvertime: true,
            sessionOvertime: TimeSpan.FromSeconds(30)), plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SessionOvertime));
    }

    [Fact]
    public void SessionOvertime_DoesNotFire_WhenIsSessionOvertimeIsFalse()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(isSessionOvertime: false), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SessionOvertime));
    }

    [Fact]
    public void SessionOvertime_DoesNotRefire_OnSubsequentTicks()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);
        var tick  = Tick(isSessionOvertime: true, sessionOvertime: TimeSpan.FromSeconds(30));

        sut.ProcessState(tick, plan);
        sut.ProcessState(tick, plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SessionOvertime));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Dedup — Reset() clears all state
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Dedup_SectionWarning_RefiresAfterReset()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10));
        var raised = Capture(sut);
        var tick  = Tick(sectionRemaining: TimeSpan.FromSeconds(30));

        sut.ProcessState(tick, plan);    // fires once
        sut.Reset();
        sut.ProcessState(tick, plan);    // should fire again

        Assert.Equal(2, raised.Count(a => a.AlertType == AlertType.SectionWarning));
    }

    [Fact]
    public void Dedup_SectionEnd_RefiresAfterReset()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);
        var tick  = Tick(sectionRemaining: TimeSpan.Zero);

        sut.ProcessState(tick, plan);
        sut.Reset();
        sut.ProcessState(tick, plan);

        Assert.Equal(2, raised.Count(a => a.AlertType == AlertType.SectionEnd));
    }

    [Fact]
    public void Dedup_SessionWarning_RefiresAfterReset()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);
        var tick  = Tick(sessionRemaining: TimeSpan.FromMinutes(2));

        sut.ProcessState(tick, plan);
        sut.Reset();
        sut.ProcessState(tick, plan);

        Assert.Equal(2, raised.Count(a => a.AlertType == AlertType.SessionWarning));
    }

    [Fact]
    public void Dedup_SessionEnd_RefiresAfterReset()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);
        var tick  = Tick(sessionRemaining: TimeSpan.Zero);

        sut.ProcessState(tick, plan);
        sut.Reset();
        sut.ProcessState(tick, plan);

        Assert.Equal(2, raised.Count(a => a.AlertType == AlertType.SessionEnd));
    }

    [Fact]
    public void Dedup_SectionOvertime_RefiresAfterReset()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);
        var tick  = Tick(isSectionOvertime: true, sectionOvertime: TimeSpan.FromSeconds(10));

        sut.ProcessState(tick, plan);
        sut.Reset();
        sut.ProcessState(tick, plan);

        Assert.Equal(2, raised.Count(a => a.AlertType == AlertType.SectionOvertime));
    }

    [Fact]
    public void Dedup_SessionOvertime_RefiresAfterReset()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);
        var tick  = Tick(isSessionOvertime: true, sessionOvertime: TimeSpan.FromSeconds(30));

        sut.ProcessState(tick, plan);
        sut.Reset();
        sut.ProcessState(tick, plan);

        Assert.Equal(2, raised.Count(a => a.AlertType == AlertType.SessionOvertime));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Dedup — Section bucket cleared on SectionChanged events
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Dedup_SectionWarning_RefiresAfterManualRestartSectionChanged()
    {
        var stub = new StubTimerService();
        var sut  = new AlertService(new AlertSettings
        {
            EnableSectionWarningAlerts = true,
            SectionWarningThreshold    = "00:01:00"
        });
        var plan = SingleSectionPlan(TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);

        var raised = Capture(sut);
        var tick  = Tick(sectionRemaining: TimeSpan.FromSeconds(30));

        sut.ProcessState(tick, plan);    // fires
        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionWarning));

        // ManualRestart clears the section-0 dedup bucket
        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 0,
            Reason               = SectionChangeReason.ManualRestart
        });

        sut.ProcessState(tick, plan);    // should fire again

        Assert.Equal(2, raised.Count(a => a.AlertType == AlertType.SectionWarning));
    }

    [Fact]
    public void Dedup_SectionWarning_RefiresAfterNavigatingBackToSection()
    {
        var stub = new StubTimerService();
        var sut  = new AlertService(new AlertSettings
        {
            EnableSectionWarningAlerts = true,
            SectionWarningThreshold    = "00:01:00"
        });
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);

        var raised = Capture(sut);
        var tick  = Tick(sectionIndex: 1, sectionRemaining: TimeSpan.FromSeconds(30));

        sut.ProcessState(tick, plan);    // fires for section 1
        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionWarning));

        // ManualNext clears the target section's bucket
        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 1,
            Reason               = SectionChangeReason.ManualNext
        });

        sut.ProcessState(tick, plan);    // should fire again after bucket clear

        Assert.Equal(2, raised.Count(a => a.AlertType == AlertType.SectionWarning));
    }

    [Fact]
    public void Dedup_AllAlerts_ClearedAfterSectionChangeReasonReset()
    {
        var stub = new StubTimerService();
        var sut  = AllEnabled();
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);

        var raised = Capture(sut);
        var tick  = Tick(sessionRemaining: TimeSpan.FromMinutes(2));

        sut.ProcessState(tick, plan);
        int beforeCount = raised.Count;

        // SectionChangeReason.Reset calls _fired.Clear() — equivalent to full Reset()
        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 0,
            Reason               = SectionChangeReason.Reset
        });

        sut.ProcessState(tick, plan);

        Assert.True(raised.Count > beforeCount,
            "Alerts should refire after SectionChanged with Reason=Reset clears all dedup state.");
    }

    [Fact]
    public void Dedup_SectionWarning_RefiresAfterAutoAdvanceToSection()
    {
        var stub = new StubTimerService();
        var sut  = new AlertService(new AlertSettings
        {
            EnableSectionWarningAlerts = true,
            SectionWarningThreshold    = "00:01:00"
        });
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);

        var raised = Capture(sut);
        var tick  = Tick(sectionIndex: 1, sectionRemaining: TimeSpan.FromSeconds(30));

        // Pre-populate section 1's bucket (simulate previous visit)
        sut.ProcessState(tick, plan);
        Assert.Single(raised.Where(a => a.AlertType == AlertType.SectionWarning));

        // AutoAdvance also clears the new section's bucket
        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 1,
            Reason               = SectionChangeReason.AutoAdvance
        });

        sut.ProcessState(tick, plan);

        Assert.Equal(2, raised.Count(a => a.AlertType == AlertType.SectionWarning));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Settings Toggles
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Toggle_SectionWarning_DisabledDoesNotFire()
    {
        var sut   = new AlertService(new AlertSettings { EnableSectionWarningAlerts = false });
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromSeconds(30)), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SectionWarning));
    }

    [Fact]
    public void Toggle_SectionEnd_DisabledDoesNotFire()
    {
        var sut   = new AlertService(new AlertSettings { EnableSectionEndAlerts = false });
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.Zero), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SectionEnd));
    }

    [Fact]
    public void Toggle_SessionWarning_DisabledDoesNotFire()
    {
        var sut   = new AlertService(new AlertSettings { EnableSessionWarningAlerts = false });
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sessionRemaining: TimeSpan.FromMinutes(2)), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SessionWarning));
    }

    [Fact]
    public void Toggle_SessionEnd_DisabledDoesNotFire()
    {
        var sut   = new AlertService(new AlertSettings { EnableSessionEndAlerts = false });
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sessionRemaining: TimeSpan.Zero), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SessionEnd));
    }

    [Fact]
    public void Toggle_OvertimeAlerts_DisabledDoesNotFireSectionOvertime()
    {
        var sut   = new AlertService(new AlertSettings { EnableOvertimeAlerts = false });
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(isSectionOvertime: true, sectionOvertime: TimeSpan.FromSeconds(10)), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SectionOvertime));
    }

    [Fact]
    public void Toggle_OvertimeAlerts_DisabledDoesNotFireSessionOvertime()
    {
        var sut   = new AlertService(new AlertSettings { EnableOvertimeAlerts = false });
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(isSessionOvertime: true, sessionOvertime: TimeSpan.FromSeconds(30)), plan);

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.SessionOvertime));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ShouldPlaySound / ShouldShowNotification flags
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Flags_DefaultSettings_SoundAndNotificationAreFalse()
    {
        // Default AlertSettings: EnableSoundAlerts = false, EnableWindowsNotifications = false
        var sut   = new AlertService(new AlertSettings());
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromSeconds(30)), plan);

        var alert = raised.First(a => a.AlertType == AlertType.SectionWarning);
        Assert.False(alert.ShouldPlaySound);
        Assert.False(alert.ShouldShowNotification);
    }

    [Fact]
    public void Flags_SoundEnabled_ShouldPlaySoundIsTrue()
    {
        var sut = new AlertService(new AlertSettings
        {
            EnableSectionWarningAlerts = true,
            EnableSoundAlerts          = true,
            EnableWindowsNotifications = false,
            SectionWarningThreshold    = "00:01:00"
        });
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromSeconds(30)), plan);

        var alert = raised.First(a => a.AlertType == AlertType.SectionWarning);
        Assert.True(alert.ShouldPlaySound);
        Assert.False(alert.ShouldShowNotification);
    }

    [Fact]
    public void Flags_NotificationsEnabled_ShouldShowNotificationIsTrue()
    {
        var sut = new AlertService(new AlertSettings
        {
            EnableSectionWarningAlerts = true,
            EnableSoundAlerts          = false,
            EnableWindowsNotifications = true,
            SectionWarningThreshold    = "00:01:00"
        });
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromSeconds(30)), plan);

        var alert = raised.First(a => a.AlertType == AlertType.SectionWarning);
        Assert.False(alert.ShouldPlaySound);
        Assert.True(alert.ShouldShowNotification);
    }

    [Fact]
    public void Flags_BothEnabled_BothAreTrueForSectionEnd()
    {
        var sut = new AlertService(new AlertSettings
        {
            EnableSectionEndAlerts     = true,
            EnableSoundAlerts          = true,
            EnableWindowsNotifications = true
        });
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.Zero), plan);

        var alert = raised.First(a => a.AlertType == AlertType.SectionEnd);
        Assert.True(alert.ShouldPlaySound);
        Assert.True(alert.ShouldShowNotification);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ManualSectionChange alert
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ManualSectionChange_Fires_OnManualNext()
    {
        var stub = new StubTimerService();
        var sut  = AllEnabled();
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);
        var raised = Capture(sut);

        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 1,
            Reason               = SectionChangeReason.ManualNext
        });

        Assert.Single(raised.Where(a => a.AlertType == AlertType.ManualSectionChange));
    }

    [Fact]
    public void ManualSectionChange_Fires_OnManualPrevious()
    {
        var stub = new StubTimerService();
        var sut  = AllEnabled();
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);
        var raised = Capture(sut);

        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 1,
            CurrentSectionIndex  = 0,
            Reason               = SectionChangeReason.ManualPrevious
        });

        Assert.Single(raised.Where(a => a.AlertType == AlertType.ManualSectionChange));
    }

    [Fact]
    public void ManualSectionChange_DoesNotFire_OnAutoAdvance()
    {
        var stub = new StubTimerService();
        var sut  = AllEnabled();
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);
        var raised = Capture(sut);

        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 1,
            Reason               = SectionChangeReason.AutoAdvance
        });

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.ManualSectionChange));
    }

    [Fact]
    public void ManualSectionChange_DoesNotFire_OnManualRestart()
    {
        var stub = new StubTimerService();
        var sut  = AllEnabled();
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);
        var raised = Capture(sut);

        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 0,
            Reason               = SectionChangeReason.ManualRestart
        });

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.ManualSectionChange));
    }

    [Fact]
    public void ManualSectionChange_DoesNotFire_OnReset()
    {
        var stub = new StubTimerService();
        var sut  = AllEnabled();
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);
        var raised = Capture(sut);

        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 0,
            Reason               = SectionChangeReason.Reset
        });

        Assert.Empty(raised.Where(a => a.AlertType == AlertType.ManualSectionChange));
    }

    [Fact]
    public void ManualSectionChange_ShouldPlaySound_IsAlwaysFalse_EvenWhenSoundEnabled()
    {
        var stub = new StubTimerService();
        var sut  = new AlertService(new AlertSettings
        {
            EnableSoundAlerts          = true,
            EnableWindowsNotifications = true
        });
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);
        var raised = Capture(sut);

        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 1,
            Reason               = SectionChangeReason.ManualNext
        });

        var alert = raised.First(a => a.AlertType == AlertType.ManualSectionChange);
        Assert.False(alert.ShouldPlaySound);
        Assert.False(alert.ShouldShowNotification);
    }

    [Fact]
    public void ManualSectionChange_Message_ContainsSectionTitle()
    {
        var stub = new StubTimerService();
        var sut  = AllEnabled();
        var plan = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        stub.Plan = plan;
        sut.Attach(stub);
        var raised = Capture(sut);

        stub.FireSectionChanged(new SectionChangedEventArgs
        {
            PreviousSectionIndex = 0,
            CurrentSectionIndex  = 1,
            Reason               = SectionChangeReason.ManualNext
        });

        var alert = raised.First(a => a.AlertType == AlertType.ManualSectionChange);
        Assert.Contains("Section B", alert.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Alert payload correctness
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SectionWarning_SectionIndex_MatchesTick()
    {
        var sut   = AllEnabled();
        var plan  = TwoSectionPlan(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionIndex: 1, sectionRemaining: TimeSpan.FromSeconds(30)), plan);

        var alert = raised.First(a => a.AlertType == AlertType.SectionWarning);
        Assert.Equal(1, alert.SectionIndex);
    }

    [Fact]
    public void SectionWarning_Message_ContainsSectionTitle()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(10));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.FromSeconds(30)), plan);

        var alert = raised.First(a => a.AlertType == AlertType.SectionWarning);
        Assert.Contains("Only Section", alert.Message);
    }

    [Fact]
    public void SectionEnd_Message_ContainsSectionTitle()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionRemaining: TimeSpan.Zero), plan);

        var alert = raised.First(a => a.AlertType == AlertType.SectionEnd);
        Assert.Contains("Only Section", alert.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Constructor / edge cases
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSettingsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AlertService(null!));
    }

    [Fact]
    public void ProcessState_WithOutOfRangeSectionIndex_DoesNotThrow()
    {
        // Section-level alerts skipped; session-level still work — no exception
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);
        var tick  = Tick(sectionIndex: 99, sessionRemaining: TimeSpan.FromMinutes(2));

        var ex = Record.Exception(() => sut.ProcessState(tick, plan));

        Assert.Null(ex);
        // Session warning should still fire despite invalid section index
        Assert.Single(raised.Where(a => a.AlertType == AlertType.SessionWarning));
    }

    [Fact]
    public void ProcessState_WithNegativeSectionIndex_SessionWarningStillFires()
    {
        var sut   = AllEnabled();
        var plan  = SingleSectionPlan(TimeSpan.FromMinutes(5));
        var raised = Capture(sut);

        sut.ProcessState(Tick(sectionIndex: -1, sessionRemaining: TimeSpan.FromMinutes(2)), plan);

        Assert.Single(raised.Where(a => a.AlertType == AlertType.SessionWarning));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Shared capture helper
    // ══════════════════════════════════════════════════════════════════════════

    private static List<AlertEventArgs> Capture(AlertService sut)
    {
        var list = new List<AlertEventArgs>();
        sut.AlertRaised += (_, e) => list.Add(e);
        return list;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Stub ISessionTimerService — allows tests to fire events without a live timer
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Minimal stub of <see cref="ISessionTimerService"/> that lets test code fire
/// <see cref="SectionChanged"/> and <see cref="Tick"/> events on demand.
/// Only <see cref="Plan"/> is used by <see cref="AlertService.Attach"/>'s
/// <c>OnTimerTick</c> path; all other members are no-ops.
/// </summary>
internal sealed class StubTimerService : ISessionTimerService
{
    public SessionPlan? Plan { get; set; }

    public bool IsRunning        => false;
    public bool IsPaused         => false;
    public bool IsSessionComplete => false;
    public int  CurrentSectionIndex => 0;
    public SessionSection? CurrentSection => null;

    public TimeSpan TotalPlannedDuration     => TimeSpan.Zero;
    public TimeSpan SessionElapsed           => TimeSpan.Zero;
    public TimeSpan SessionRemaining         => TimeSpan.Zero;
    public TimeSpan SessionOvertime          => TimeSpan.Zero;
    public TimeSpan CurrentSectionElapsed    => TimeSpan.Zero;
    public TimeSpan CurrentSectionRemaining  => TimeSpan.Zero;
    public TimeSpan CurrentSectionOvertime   => TimeSpan.Zero;
    public TimeSpan BehindSchedule           => TimeSpan.Zero;

    public bool AutoAdvanceSections { get; set; }

    public event EventHandler<TimerTickEventArgs>?     Tick;
    public event EventHandler<SectionChangedEventArgs>? SectionChanged;
    public event EventHandler?                          StateChanged;

    public void LoadPlan(SessionPlan plan) { Plan = plan; }
    public void Start()  { }
    public void Pause()  { }
    public void Resume() { }
    public void Stop()   { }
    public void Reset()  { }
    public void NextSection()            { }
    public void PreviousSection()        { }
    public void RestartCurrentSection()  { }
    public void ExtendCurrentSection(TimeSpan extension) { }
    public SessionResult GetResult() => new();
    public void Dispose() { }

    /// <summary>Fires <see cref="SectionChanged"/> synchronously on the calling thread.</summary>
    public void FireSectionChanged(SectionChangedEventArgs args)
        => SectionChanged?.Invoke(this, args);

    /// <summary>Fires <see cref="Tick"/> synchronously on the calling thread.</summary>
    public void FireTick(TimerTickEventArgs args)
        => Tick?.Invoke(this, args);
}
