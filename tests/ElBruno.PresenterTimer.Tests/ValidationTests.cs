using System.IO;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// Tests for <see cref="SessionValidationService"/> against all PRD §7.4 rules.
/// Covers: missing title, missing/empty sections, zero duration, invalid warningAt,
/// and confirmation that a fully-valid plan returns Success.
/// </summary>
public class ValidationTests
{
    private static readonly SessionLoaderService       Loader     = new();
    private static readonly SessionValidationService   Validator  = new();

    private static string SamplePath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    // ── missing title ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyTitle_IsInvalid()
    {
        var plan = new SessionPlan
        {
            Title    = "",
            Sections = [new SessionSection { Title = "S1", Duration = TimeSpan.FromMinutes(5) }],
        };

        var result = Validator.Validate(plan);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyTitle_ErrorMentionsTitle()
    {
        var plan = new SessionPlan
        {
            Title    = "   ",
            Sections = [new SessionSection { Title = "S1", Duration = TimeSpan.FromMinutes(5) }],
        };

        var result = Validator.Validate(plan);

        Assert.Contains(result.Errors, e => e.Contains("title", StringComparison.OrdinalIgnoreCase));
    }

    // ── missing / empty sections ──────────────────────────────────────────────

    [Fact]
    public void Validate_NullSections_IsInvalid()
    {
        var plan = new SessionPlan { Title = "Test" }; // Sections initialised to empty list by default

        // Force null to test the null branch explicitly
        plan.Sections = null!;

        var result = Validator.Validate(plan);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_EmptySectionsArray_IsInvalid()
    {
        var plan = new SessionPlan { Title = "Test", Sections = [] };

        var result = Validator.Validate(plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("section", StringComparison.OrdinalIgnoreCase));
    }

    // ── zero duration ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_SectionWithZeroDuration_IsInvalid()
    {
        var plan = new SessionPlan
        {
            Title = "Test",
            Sections =
            [
                new SessionSection { Title = "S1", Duration = TimeSpan.Zero },
            ],
        };

        var result = Validator.Validate(plan);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_SectionWithZeroDuration_ErrorMentionsDuration()
    {
        var plan = new SessionPlan
        {
            Title = "Test",
            Sections =
            [
                new SessionSection { Title = "Zero Section", Duration = TimeSpan.Zero },
            ],
        };

        var result = Validator.Validate(plan);

        Assert.Contains(result.Errors, e =>
            e.Contains("duration", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("zero", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("greater", StringComparison.OrdinalIgnoreCase));
    }

    // ── warningAt >= duration ────────────────────────────────────────────────

    [Fact]
    public void Validate_InvalidWarningExceedsDuration_FromSampleFile_IsInvalid()
    {
        var plan = Loader.Load(SamplePath("invalid-warning-exceeds-duration.json"));

        var result = Validator.Validate(plan);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidWarningExceedsDuration_ErrorMentionsOffendingSection()
    {
        var plan = Loader.Load(SamplePath("invalid-warning-exceeds-duration.json"));

        var result = Validator.Validate(plan);

        // PRD §7.4 example: "Section 2, "Demo", has a warning time of 00:06:00..."
        Assert.Contains(result.Errors, e =>
            e.Contains("Demo", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("warning", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WarningEqualsToDuration_IsInvalid()
    {
        // WarningAt == Duration is also invalid (must be strictly less than)
        var plan = new SessionPlan
        {
            Title = "Test",
            Sections =
            [
                new SessionSection
                {
                    Title     = "Equal Section",
                    Duration  = TimeSpan.FromMinutes(5),
                    WarningAt = TimeSpan.FromMinutes(5), // equal → invalid
                },
            ],
        };

        var result = Validator.Validate(plan);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WarningLessThanDuration_IsValid()
    {
        var plan = new SessionPlan
        {
            Title = "Test",
            Sections =
            [
                new SessionSection
                {
                    Title     = "OK Section",
                    Duration  = TimeSpan.FromMinutes(5),
                    WarningAt = TimeSpan.FromMinutes(1), // strictly less → valid
                },
            ],
        };

        var result = Validator.Validate(plan);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ── fully-valid samples return Success ────────────────────────────────────

    [Fact]
    public void Validate_AiAgentsDemo_ReturnsSuccess()
    {
        var plan = Loader.Load(SamplePath("ai-agents-demo.json"));

        var result = Validator.Validate(plan);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ShortDemo_ReturnsSuccess()
    {
        var plan = Loader.Load(SamplePath("short-demo.json"));

        var result = Validator.Validate(plan);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_Podcast_ReturnsSuccess()
    {
        var plan = Loader.Load(SamplePath("podcast.json"));

        var result = Validator.Validate(plan);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ConferenceTalk_ReturnsSuccess()
    {
        var plan = Loader.Load(SamplePath("conference-talk.json"));

        var result = Validator.Validate(plan);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_Workshop_ReturnsSuccess()
    {
        var plan = Loader.Load(SamplePath("workshop.json"));

        var result = Validator.Validate(plan);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ── ValidationResult shape ────────────────────────────────────────────────

    [Fact]
    public void ValidationResult_Success_IsValidTrueAndEmptyErrors()
    {
        var result = ValidationResult.Success;

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationResult_Failure_IsValidFalseAndHasErrors()
    {
        var result = ValidationResult.Failure(["Error A", "Error B"]);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    // ── section with missing title ────────────────────────────────────────────

    [Fact]
    public void Validate_SectionWithMissingTitle_IsInvalid()
    {
        var plan = new SessionPlan
        {
            Title = "Test Plan",
            Sections =
            [
                new SessionSection { Title = "", Duration = TimeSpan.FromMinutes(5) },
            ],
        };

        var result = Validator.Validate(plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("title", StringComparison.OrdinalIgnoreCase));
    }
}
