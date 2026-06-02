using System.IO;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// Tests for <see cref="SessionLoaderService"/>: JSON parsing, round-trip, and duration sums.
/// Sample JSON files are copied from repo samples\ into TestData\ via the .csproj Content items
/// and resolved at runtime via AppContext.BaseDirectory. This avoids brittle path-climbing and
/// keeps tests independent of the working directory.
/// </summary>
public class LoaderParsingTests
{
    private static readonly SessionLoaderService Loader = new();

    // ── helper ──────────────────────────────────────────────────────────────

    private static string SamplePath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    private static SessionPlan LoadSample(string fileName)
        => Loader.Load(SamplePath(fileName));

    // ── valid file parsing ───────────────────────────────────────────────────

    [Fact]
    public void ShortDemo_Parse_TitleIs_QuickDemo()
    {
        var plan = LoadSample("short-demo.json");

        Assert.Equal("Quick Demo - 10 Minutes", plan.Title);
    }

    [Fact]
    public void ShortDemo_Parse_HasThreeSections()
    {
        var plan = LoadSample("short-demo.json");

        Assert.Equal(3, plan.Sections.Count);
    }

    [Fact]
    public void ShortDemo_Parse_DurationsParseCorrectly()
    {
        var plan = LoadSample("short-demo.json");

        Assert.Equal(TimeSpan.FromMinutes(2), plan.Sections[0].Duration); // Intro
        Assert.Equal(TimeSpan.FromMinutes(6), plan.Sections[1].Duration); // Demo
        Assert.Equal(TimeSpan.FromMinutes(2), plan.Sections[2].Duration); // Wrap-up
    }

    [Fact]
    public void Podcast_Parse_TitleAndSectionCount()
    {
        var plan = LoadSample("podcast.json");

        Assert.Equal("The Future of AI - Episode 5", plan.Title);
        Assert.Equal(6, plan.Sections.Count);
    }

    [Fact]
    public void Podcast_Parse_HasMetadata()
    {
        var plan = LoadSample("podcast.json");

        Assert.NotNull(plan.Metadata);
        Assert.Equal("Tech Podcast Team", plan.Metadata!.Author);
    }

    [Fact]
    public void Podcast_Parse_WarningAtParsesAsNullableTimeSpan()
    {
        var plan = LoadSample("podcast.json");

        // Guest Intro (section 1, 0-indexed) has warningAt "00:00:45"
        Assert.Equal(TimeSpan.FromSeconds(45), plan.Sections[1].WarningAt);
    }

    [Fact]
    public void ConferenceTalk_Parse_TitleAndSectionCount()
    {
        var plan = LoadSample("conference-talk.json");

        Assert.Equal("Cloud-Native Architecture Patterns", plan.Title);
        Assert.Equal(7, plan.Sections.Count);
    }

    [Fact]
    public void ConferenceTalk_Parse_LiveDemoIs15Minutes()
    {
        var plan = LoadSample("conference-talk.json");

        // "Live Demo" is section index 3 with "00:15:00"
        var liveDemo = plan.Sections.Single(s => s.Title == "Live Demo");
        Assert.Equal(TimeSpan.FromMinutes(15), liveDemo.Duration);
    }

    [Fact]
    public void Workshop_Parse_TitleAndSectionCount()
    {
        var plan = LoadSample("workshop.json");

        Assert.Equal("Hands-On: Building Your First Microservice", plan.Title);
        Assert.Equal(7, plan.Sections.Count);
    }

    [Fact]
    public void AiAgentsDemo_Parse_Title()
    {
        var plan = LoadSample("ai-agents-demo.json");

        Assert.Equal("Build Recording - AI Agents Demo", plan.Title);
    }

    [Fact]
    public void AiAgentsDemo_Parse_HasFourSections()
    {
        var plan = LoadSample("ai-agents-demo.json");

        Assert.Equal(4, plan.Sections.Count);
    }

    [Fact]
    public void AiAgentsDemo_Parse_SectionDurationsCorrect()
    {
        var plan = LoadSample("ai-agents-demo.json");

        Assert.Equal(TimeSpan.FromMinutes(3),  plan.Sections[0].Duration); // Intro
        Assert.Equal(TimeSpan.FromMinutes(5),  plan.Sections[1].Duration); // Context
        Assert.Equal(TimeSpan.FromMinutes(15), plan.Sections[2].Duration); // Demo
        Assert.Equal(TimeSpan.FromMinutes(4),  plan.Sections[3].Duration); // Wrap-up
    }

    [Fact]
    public void AiAgentsDemo_Parse_ColorsPresent()
    {
        var plan = LoadSample("ai-agents-demo.json");

        Assert.Equal("#4CAF50", plan.Sections[0].Color);
        Assert.Equal("#9C27B0", plan.Sections[2].Color);
    }

    // ── invalid / malformed JSON ─────────────────────────────────────────────

    [Fact]
    public void MalformedJson_TryParse_ThrowsSessionLoadException_NotRawJsonException()
    {
        const string badJson = "{ this is not valid json !!!";

        var ex = Assert.Throws<SessionLoadException>(() => Loader.TryParse(badJson));

        // The inner exception should be a JsonException, but the surface type must be SessionLoadException
        Assert.IsType<SessionLoadException>(ex);
        Assert.NotNull(ex.InnerException); // wraps the original JsonException
    }

    [Fact]
    public void EmptyString_TryParse_ThrowsSessionLoadException()
    {
        Assert.Throws<SessionLoadException>(() => Loader.TryParse(""));
    }

    [Fact]
    public void WhitespaceOnly_TryParse_ThrowsSessionLoadException()
    {
        Assert.Throws<SessionLoadException>(() => Loader.TryParse("   \t\n  "));
    }

    [Fact]
    public void NullLiteralJson_TryParse_ThrowsSessionLoadException()
    {
        // JSON null is syntactically valid but deserializes to null → should throw
        Assert.Throws<SessionLoadException>(() => Loader.TryParse("null"));
    }

    // ── TimeSpan converter round-trip ────────────────────────────────────────

    [Fact]
    public void ExportJson_ThenTryParse_RoundTripsEqualDurations()
    {
        var plan = LoadSample("ai-agents-demo.json");
        var json  = Loader.ExportJson(plan);
        var reparsed = Loader.TryParse(json);

        for (int i = 0; i < plan.Sections.Count; i++)
            Assert.Equal(plan.Sections[i].Duration, reparsed.Sections[i].Duration);
    }

    [Fact]
    public void ExportJson_TimeSpanFormattedAs_HhMmSs()
    {
        var plan = new SessionPlan
        {
            Title    = "Test",
            Sections = [new SessionSection { Title = "S1", Duration = TimeSpan.FromMinutes(90) }],
        };
        var json = Loader.ExportJson(plan);

        // "90 minutes" must appear as "01:30:00" not "90:00" or an ISO variant
        Assert.Contains("\"01:30:00\"", json);
    }

    [Fact]
    public void ExportJson_NullableWarningAt_FormattedAs_HhMmSs()
    {
        var plan = new SessionPlan
        {
            Title = "Test",
            Sections =
            [
                new SessionSection
                {
                    Title    = "S1",
                    Duration = TimeSpan.FromMinutes(10),
                    WarningAt = TimeSpan.FromMinutes(2),
                },
            ],
        };
        var json = Loader.ExportJson(plan);

        Assert.Contains("\"00:02:00\"", json);
    }

    // ── GetTotalDuration ────────────────────────────────────────────────────

    [Fact]
    public void GetTotalDuration_AiAgentsDemo_Is27Minutes()
    {
        // Intro 3 + Context 5 + Demo 15 + Wrap-up 4 = 27 minutes
        var plan = LoadSample("ai-agents-demo.json");

        Assert.Equal(TimeSpan.FromMinutes(27), Loader.GetTotalDuration(plan));
    }

    [Fact]
    public void GetTotalDuration_ShortDemo_Is10Minutes()
    {
        // Intro 2 + Demo 6 + Wrap-up 2 = 10 minutes
        var plan = LoadSample("short-demo.json");

        Assert.Equal(TimeSpan.FromMinutes(10), Loader.GetTotalDuration(plan));
    }

    [Fact]
    public void GetTotalDuration_Podcast_Is30Minutes()
    {
        // 3+2+8+9+5+3 = 30 minutes
        var plan = LoadSample("podcast.json");

        Assert.Equal(TimeSpan.FromMinutes(30), Loader.GetTotalDuration(plan));
    }

    [Fact]
    public void GetTotalDuration_ConferenceTalk_Is45Minutes()
    {
        // 3+7+8+15+7+4+1 = 45 minutes
        var plan = LoadSample("conference-talk.json");

        Assert.Equal(TimeSpan.FromMinutes(45), Loader.GetTotalDuration(plan));
    }

    [Fact]
    public void GetTotalDuration_Workshop_Is60Minutes()
    {
        // 5+10+12+5+12+10+6 = 60 minutes
        var plan = LoadSample("workshop.json");

        Assert.Equal(TimeSpan.FromMinutes(60), Loader.GetTotalDuration(plan));
    }

    // ── ExportSampleJson ─────────────────────────────────────────────────────

    [Fact]
    public void ExportSampleJson_ProducesParseableOutput()
    {
        var json = Loader.ExportSampleJson();
        var plan = Loader.TryParse(json);

        Assert.NotNull(plan);
        Assert.False(string.IsNullOrWhiteSpace(plan.Title));
        Assert.True(plan.Sections.Count > 0);
    }
}
