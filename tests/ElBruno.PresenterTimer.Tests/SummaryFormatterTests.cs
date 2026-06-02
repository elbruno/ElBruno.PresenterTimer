using System.Text.Json;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Models.Converters;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// Tests for <see cref="SummaryFormatter"/> — PRD §7.14.
///
/// <para>The PRD §7.14 example session ("AI Agents Recording", planned 27:00, actual 31:20,
/// difference +04:20 with four sections) is used as the primary fixture.
/// Edge cases (empty sections, under-time, unvisited sections) are also covered.</para>
///
/// <para><b>SettingsService injectable-path status (Parker):</b><br/>
/// Inspected <c>Services/SettingsService.cs</c> — the constructor still uses
/// <c>private static readonly</c> fields hardcoded to <c>%AppData%\ElBruno.PresenterTimer\settings.json</c>.
/// No injectable path/dir constructor exists.  New settings-path tests are therefore
/// deferred; the existing backup/restore suite in <c>SettingsServiceTests.cs</c> remains
/// the authoritative settings coverage.</para>
/// </summary>
public sealed class SummaryFormatterTests
{
    // ── Shared test fixture (PRD §7.14 example) ───────────────────────────────

    /// <summary>
    /// Builds the exact session result from PRD §7.14:
    ///   Planned 27:00, Actual 31:20, Difference +04:20.
    ///   Four visited sections with per-section planned/actual data.
    /// </summary>
    private static SessionResult BuildPrdExampleResult()
    {
        return new SessionResult
        {
            SessionTitle    = "AI Agents Recording",
            PlannedDuration = TimeSpan.FromMinutes(27),
            ActualDuration  = new TimeSpan(0, 31, 20),
            TotalExtensions = TimeSpan.Zero,
            Sections =
            [
                new SectionResult
                {
                    Index           = 0,
                    Title           = "Intro",
                    PlannedDuration = TimeSpan.FromMinutes(3),
                    ActualDuration  = new TimeSpan(0, 3, 20),
                    WasVisited      = true
                },
                new SectionResult
                {
                    Index           = 1,
                    Title           = "Problem Statement",
                    PlannedDuration = TimeSpan.FromMinutes(5),
                    ActualDuration  = new TimeSpan(0, 4, 50),
                    WasVisited      = true
                },
                new SectionResult
                {
                    Index           = 2,
                    Title           = "Demo",
                    PlannedDuration = TimeSpan.FromMinutes(15),
                    ActualDuration  = new TimeSpan(0, 18, 35),
                    WasVisited      = true
                },
                new SectionResult
                {
                    Index           = 3,
                    Title           = "Wrap-up",
                    PlannedDuration = TimeSpan.FromMinutes(4),
                    ActualDuration  = new TimeSpan(0, 4, 35),
                    WasVisited      = true
                }
            ]
        };
    }

    /// <summary>Builds a minimal result with no sections (edge case).</summary>
    private static SessionResult BuildEmptyResult() =>
        new()
        {
            SessionTitle    = "Empty Session",
            PlannedDuration = TimeSpan.FromMinutes(10),
            ActualDuration  = TimeSpan.FromMinutes(9),
            TotalExtensions = TimeSpan.Zero,
            Sections        = []
        };

    /// <summary>Builds a result where the session ran under time (negative difference).</summary>
    private static SessionResult BuildUnderTimeResult() =>
        new()
        {
            SessionTitle    = "Short Talk",
            PlannedDuration = TimeSpan.FromMinutes(20),
            ActualDuration  = TimeSpan.FromMinutes(18),
            TotalExtensions = TimeSpan.Zero,
            Sections =
            [
                new SectionResult
                {
                    Index           = 0,
                    Title           = "Intro",
                    PlannedDuration = TimeSpan.FromMinutes(5),
                    ActualDuration  = TimeSpan.FromMinutes(4),
                    WasVisited      = true
                },
                new SectionResult
                {
                    Index           = 1,
                    Title           = "Main",
                    PlannedDuration = TimeSpan.FromMinutes(15),
                    ActualDuration  = TimeSpan.FromMinutes(14),
                    WasVisited      = true
                }
            ]
        };

    // ══════════════════════════════════════════════════════════════════════════
    // Group 1 — FormatTime helper
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatTime_Zero_Returns_0000()
    {
        Assert.Equal("00:00", SummaryFormatter.FormatTime(TimeSpan.Zero));
    }

    [Fact]
    public void FormatTime_Under1Hour_ReturnsMmSs()
    {
        // 27 minutes 0 seconds → "27:00"
        Assert.Equal("27:00", SummaryFormatter.FormatTime(TimeSpan.FromMinutes(27)));
    }

    [Fact]
    public void FormatTime_Seconds_ReturnsMmSs()
    {
        // 4 minutes 20 seconds → "04:20"
        Assert.Equal("04:20", SummaryFormatter.FormatTime(new TimeSpan(0, 4, 20)));
    }

    [Fact]
    public void FormatTime_Exactly60Minutes_ReturnsHhMmSs()
    {
        // 1 hour exactly → "01:00:00"
        Assert.Equal("01:00:00", SummaryFormatter.FormatTime(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void FormatTime_Over1Hour_ReturnsHhMmSs()
    {
        // 1h 30m 15s → "01:30:15"
        Assert.Equal("01:30:15", SummaryFormatter.FormatTime(new TimeSpan(1, 30, 15)));
    }

    [Fact]
    public void FormatTime_Negative_TreatsAsZero()
    {
        Assert.Equal("00:00", SummaryFormatter.FormatTime(TimeSpan.FromMinutes(-5)));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 2 — FormatDifference helper
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatDifference_Positive_HasPlusSign()
    {
        // +04:20
        Assert.Equal("+04:20", SummaryFormatter.FormatDifference(new TimeSpan(0, 4, 20)));
    }

    [Fact]
    public void FormatDifference_Negative_HasMinusSign()
    {
        // -00:10
        Assert.Equal("-00:10", SummaryFormatter.FormatDifference(TimeSpan.FromSeconds(-10)));
    }

    [Fact]
    public void FormatDifference_Zero_HasPlusSign()
    {
        // Zero difference shows +00:00
        Assert.Equal("+00:00", SummaryFormatter.FormatDifference(TimeSpan.Zero));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 3 — FormatPlainText: PRD §7.14 example
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatPlainText_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SummaryFormatter.FormatPlainText(null!));
    }

    [Fact]
    public void FormatPlainText_PrdExample_ContainsSessionTitle()
    {
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());
        Assert.Contains("Session completed: AI Agents Recording", text);
    }

    [Fact]
    public void FormatPlainText_PrdExample_ContainsPlannedTime()
    {
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());
        Assert.Contains("Planned:", text);
        Assert.Contains("27:00", text);
    }

    [Fact]
    public void FormatPlainText_PrdExample_ContainsActualTime()
    {
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());
        Assert.Contains("Actual:", text);
        Assert.Contains("31:20", text);
    }

    [Fact]
    public void FormatPlainText_PrdExample_ContainsDifference_WithPlusSign()
    {
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());
        Assert.Contains("Difference:", text);
        Assert.Contains("+04:20", text);
    }

    [Fact]
    public void FormatPlainText_PrdExample_Intro_SectionLineCorrect()
    {
        // Intro: planned 03:00, actual 03:20, +00:20
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());
        Assert.Contains("Intro:", text);
        Assert.Contains("planned 03:00", text);
        Assert.Contains("actual 03:20", text);
        Assert.Contains("+00:20", text);
    }

    [Fact]
    public void FormatPlainText_PrdExample_ProblemStatement_UnderTime_HasMinusSign()
    {
        // Problem Statement: planned 05:00, actual 04:50, -00:10
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());
        Assert.Contains("Problem Statement:", text);
        Assert.Contains("planned 05:00", text);
        Assert.Contains("actual 04:50", text);
        Assert.Contains("-00:10", text);
    }

    [Fact]
    public void FormatPlainText_PrdExample_Demo_SectionLineCorrect()
    {
        // Demo: planned 15:00, actual 18:35, +03:35
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());
        Assert.Contains("Demo:", text);
        Assert.Contains("planned 15:00", text);
        Assert.Contains("actual 18:35", text);
        Assert.Contains("+03:35", text);
    }

    [Fact]
    public void FormatPlainText_PrdExample_OvertimeSections_HaveOvertimeTag()
    {
        // Intro, Demo, Wrap-up are all over plan → should each include [OVERTIME].
        // Problem Statement is under plan → no OVERTIME tag on that line.
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var introLine = lines.FirstOrDefault(l => l.StartsWith("Intro:"));
        var problemLine = lines.FirstOrDefault(l => l.StartsWith("Problem Statement:"));
        var demoLine = lines.FirstOrDefault(l => l.StartsWith("Demo:"));

        Assert.NotNull(introLine);
        Assert.Contains("OVERTIME", introLine);

        Assert.NotNull(problemLine);
        Assert.DoesNotContain("OVERTIME", problemLine);

        Assert.NotNull(demoLine);
        Assert.Contains("OVERTIME", demoLine);
    }

    [Fact]
    public void FormatPlainText_PrdExample_WrapUp_SectionLineCorrect()
    {
        // Wrap-up: planned 04:00, actual 04:35, +00:35
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());
        Assert.Contains("Wrap-up:", text);
        Assert.Contains("planned 04:00", text);
        Assert.Contains("actual 04:35", text);
        Assert.Contains("+00:35", text);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 4 — FormatPlainText: edge cases
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatPlainText_EmptySections_ContainsHeaderAndNoSectionLines()
    {
        var text = SummaryFormatter.FormatPlainText(BuildEmptyResult());

        Assert.Contains("Session completed: Empty Session", text);
        Assert.Contains("Planned:", text);
        Assert.Contains("Actual:", text);
        // No "Not reached:" block because Sections is empty
        Assert.DoesNotContain("Not reached:", text);
    }

    [Fact]
    public void FormatPlainText_UnderTime_DifferenceHasMinusSign()
    {
        var text = SummaryFormatter.FormatPlainText(BuildUnderTimeResult());

        Assert.Contains("Difference:", text);
        Assert.Contains("-02:00", text);  // 18 − 20 = −2 min
    }

    [Fact]
    public void FormatPlainText_UnvisitedSection_AppearsInNotReachedBlock()
    {
        var result = new SessionResult
        {
            SessionTitle    = "Partial Session",
            PlannedDuration = TimeSpan.FromMinutes(10),
            ActualDuration  = TimeSpan.FromMinutes(5),
            TotalExtensions = TimeSpan.Zero,
            Sections =
            [
                new SectionResult
                {
                    Index           = 0,
                    Title           = "Visited Part",
                    PlannedDuration = TimeSpan.FromMinutes(5),
                    ActualDuration  = TimeSpan.FromMinutes(5),
                    WasVisited      = true
                },
                new SectionResult
                {
                    Index           = 1,
                    Title           = "Never Reached",
                    PlannedDuration = TimeSpan.FromMinutes(5),
                    ActualDuration  = TimeSpan.Zero,
                    WasVisited      = false
                }
            ]
        };

        var text = SummaryFormatter.FormatPlainText(result);

        Assert.Contains("Not reached:", text);
        Assert.Contains("Never Reached", text);
        // Unvisited section must NOT appear in the main section list
        var mainBlock = text.Split("Not reached:")[0];
        Assert.DoesNotContain("Never Reached", mainBlock);
    }

    [Fact]
    public void FormatPlainText_WithExtensions_ExtensionsLinePresent()
    {
        var result = new SessionResult
        {
            SessionTitle    = "Extended Session",
            PlannedDuration = TimeSpan.FromMinutes(10),
            ActualDuration  = TimeSpan.FromMinutes(12),
            TotalExtensions = TimeSpan.FromMinutes(2),
            Sections        = []
        };

        var text = SummaryFormatter.FormatPlainText(result);
        Assert.Contains("Extensions:", text);
        Assert.Contains("02:00", text);
    }

    [Fact]
    public void FormatPlainText_NoExtensions_ExtensionsLineAbsent()
    {
        var text = SummaryFormatter.FormatPlainText(BuildPrdExampleResult());
        // No TotalExtensions on the PRD example — line must not appear
        Assert.DoesNotContain("Extensions:", text);
    }

    [Fact]
    public void FormatPlainText_SectionWithExtensions_ShowsExtTag()
    {
        var result = new SessionResult
        {
            SessionTitle    = "With Ext",
            PlannedDuration = TimeSpan.FromMinutes(5),
            ActualDuration  = TimeSpan.FromMinutes(7),
            TotalExtensions = TimeSpan.FromMinutes(2),
            Sections =
            [
                new SectionResult
                {
                    Index           = 0,
                    Title           = "Main Part",
                    PlannedDuration = TimeSpan.FromMinutes(5),
                    ActualDuration  = TimeSpan.FromMinutes(7),
                    TotalExtensions = TimeSpan.FromMinutes(2),
                    WasVisited      = true
                }
            ]
        };

        var text = SummaryFormatter.FormatPlainText(result);
        var lines = text.Split('\n');
        var sectionLine = lines.FirstOrDefault(l => l.StartsWith("Main Part:"));
        Assert.NotNull(sectionLine);
        Assert.Contains("ext 02:00", sectionLine);
    }

    [Fact]
    public void FormatPlainText_SectionWithRestarts_ShowsRestartsCount()
    {
        var result = new SessionResult
        {
            SessionTitle    = "Restarted",
            PlannedDuration = TimeSpan.FromMinutes(5),
            ActualDuration  = TimeSpan.FromMinutes(5),
            TotalExtensions = TimeSpan.Zero,
            Sections =
            [
                new SectionResult
                {
                    Index           = 0,
                    Title           = "Demo Part",
                    PlannedDuration = TimeSpan.FromMinutes(5),
                    ActualDuration  = TimeSpan.FromMinutes(5),
                    RestartCount    = 2,
                    WasVisited      = true
                }
            ]
        };

        var text = SummaryFormatter.FormatPlainText(result);
        var lines = text.Split('\n');
        var sectionLine = lines.FirstOrDefault(l => l.StartsWith("Demo Part:"));
        Assert.NotNull(sectionLine);
        Assert.Contains("restarts 2", sectionLine);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 5 — FormatMarkdown
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatMarkdown_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SummaryFormatter.FormatMarkdown(null!));
    }

    [Fact]
    public void FormatMarkdown_PrdExample_H1ContainsSessionTitle()
    {
        var md = SummaryFormatter.FormatMarkdown(BuildPrdExampleResult());
        Assert.Contains("# Session Summary: AI Agents Recording", md);
    }

    [Fact]
    public void FormatMarkdown_PrdExample_PlannedRowPresent()
    {
        var md = SummaryFormatter.FormatMarkdown(BuildPrdExampleResult());
        Assert.Contains("**Planned**", md);
        Assert.Contains("`27:00`", md);
    }

    [Fact]
    public void FormatMarkdown_PrdExample_ActualRowPresent()
    {
        var md = SummaryFormatter.FormatMarkdown(BuildPrdExampleResult());
        Assert.Contains("**Actual**", md);
        Assert.Contains("`31:20`", md);
    }

    [Fact]
    public void FormatMarkdown_PrdExample_DifferenceRowPresent()
    {
        var md = SummaryFormatter.FormatMarkdown(BuildPrdExampleResult());
        Assert.Contains("**Difference**", md);
        Assert.Contains("`+04:20`", md);
    }

    [Fact]
    public void FormatMarkdown_PrdExample_H2SectionsPresentAboveSectionTable()
    {
        var md = SummaryFormatter.FormatMarkdown(BuildPrdExampleResult());
        Assert.Contains("## Sections", md);
        // Table header row
        Assert.Contains("| # | Section |", md);
    }

    [Fact]
    public void FormatMarkdown_PrdExample_IntroSectionTableRowPresent()
    {
        var md = SummaryFormatter.FormatMarkdown(BuildPrdExampleResult());
        // Intro is section #1
        Assert.Contains("| 1 | Intro |", md);
        Assert.Contains("`03:00`", md);  // planned
        Assert.Contains("`03:20`", md);  // actual
        Assert.Contains("`+00:20`", md); // diff
    }

    [Fact]
    public void FormatMarkdown_PrdExample_OvertimeSections_HaveCheckmark()
    {
        var md = SummaryFormatter.FormatMarkdown(BuildPrdExampleResult());
        // The Overtime column uses "✓" for overtime sections
        // Problem Statement is NOT overtime; the others are.
        var rows = md.Split('\n')
                     .Where(l => l.TrimStart().StartsWith('|'))
                     .ToList();

        var introRow = rows.FirstOrDefault(r => r.Contains("| Intro |"));
        var problemRow = rows.FirstOrDefault(r => r.Contains("| Problem Statement |"));
        var demoRow = rows.FirstOrDefault(r => r.Contains("| Demo |"));

        Assert.NotNull(introRow);
        Assert.Contains("✓", introRow);

        Assert.NotNull(problemRow);
        Assert.DoesNotContain("✓", problemRow);

        Assert.NotNull(demoRow);
        Assert.Contains("✓", demoRow);
    }

    [Fact]
    public void FormatMarkdown_UnvisitedSection_IsMarkedNotReached()
    {
        var result = new SessionResult
        {
            SessionTitle    = "Incomplete",
            PlannedDuration = TimeSpan.FromMinutes(10),
            ActualDuration  = TimeSpan.FromMinutes(5),
            TotalExtensions = TimeSpan.Zero,
            Sections =
            [
                new SectionResult
                {
                    Index           = 0,
                    Title           = "Done Part",
                    PlannedDuration = TimeSpan.FromMinutes(5),
                    ActualDuration  = TimeSpan.FromMinutes(5),
                    WasVisited      = true
                },
                new SectionResult
                {
                    Index           = 1,
                    Title           = "Skipped Part",
                    PlannedDuration = TimeSpan.FromMinutes(5),
                    ActualDuration  = TimeSpan.Zero,
                    WasVisited      = false
                }
            ]
        };

        var md = SummaryFormatter.FormatMarkdown(result);
        // Unvisited section gets italic + "(not reached)" suffix in markdown
        Assert.Contains("*(not reached)*", md);
        // Actual and Diff columns should be "—" for unvisited sections
        var skippedRow = md.Split('\n').FirstOrDefault(l => l.Contains("Skipped Part"));
        Assert.NotNull(skippedRow);
        Assert.Contains("—", skippedRow);
    }

    [Fact]
    public void FormatMarkdown_NoExtensions_TotalExtensionsRowAbsent()
    {
        var md = SummaryFormatter.FormatMarkdown(BuildPrdExampleResult());
        Assert.DoesNotContain("Total Extensions", md);
    }

    [Fact]
    public void FormatMarkdown_WithExtensions_TotalExtensionsRowPresent()
    {
        var result = new SessionResult
        {
            SessionTitle    = "Extended",
            PlannedDuration = TimeSpan.FromMinutes(10),
            ActualDuration  = TimeSpan.FromMinutes(12),
            TotalExtensions = TimeSpan.FromMinutes(2),
            Sections        = []
        };

        var md = SummaryFormatter.FormatMarkdown(result);
        Assert.Contains("Total Extensions", md);
        Assert.Contains("`02:00`", md);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Group 6 — FormatJson
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatJson_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SummaryFormatter.FormatJson(null!));
    }

    [Fact]
    public void FormatJson_PrdExample_ContainsSessionTitleInJson()
    {
        var json = SummaryFormatter.FormatJson(BuildPrdExampleResult());
        Assert.Contains("AI Agents Recording", json);
    }

    [Fact]
    public void FormatJson_TimeSpansFormattedAsHhMmSs()
    {
        // TimeSpanJsonConverter formats as "HH:mm:ss" — e.g. 27 min → "00:27:00"
        var json = SummaryFormatter.FormatJson(BuildPrdExampleResult());
        Assert.Contains("\"00:27:00\"", json);  // PlannedDuration
        Assert.Contains("\"00:31:20\"", json);  // ActualDuration
    }

    [Fact]
    public void FormatJson_IsValidJson()
    {
        var json = SummaryFormatter.FormatJson(BuildPrdExampleResult());
        // Must not throw
        using var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void FormatJson_RoundTrip_PreservesSessionTitleAndDurations()
    {
        var original = BuildPrdExampleResult();
        var json     = SummaryFormatter.FormatJson(original);

        // Rebuild matching JsonSerializerOptions to deserialize
        var opts = new JsonSerializerOptions
        {
            WriteIndented              = true,
            PropertyNameCaseInsensitive = true
        };
        opts.Converters.Add(new TimeSpanJsonConverter());
        opts.Converters.Add(new NullableTimeSpanJsonConverter());

        var restored = JsonSerializer.Deserialize<SessionResult>(json, opts);

        Assert.NotNull(restored);
        Assert.Equal(original.SessionTitle, restored.SessionTitle);
        Assert.Equal(original.PlannedDuration, restored.PlannedDuration);
        Assert.Equal(original.ActualDuration, restored.ActualDuration);
        Assert.Equal(original.TotalExtensions, restored.TotalExtensions);
        Assert.Equal(original.Sections.Count, restored.Sections.Count);
    }

    [Fact]
    public void FormatJson_RoundTrip_PreservesSectionData()
    {
        var original = BuildPrdExampleResult();
        var json     = SummaryFormatter.FormatJson(original);

        var opts = new JsonSerializerOptions
        {
            WriteIndented               = true,
            PropertyNameCaseInsensitive = true
        };
        opts.Converters.Add(new TimeSpanJsonConverter());
        opts.Converters.Add(new NullableTimeSpanJsonConverter());

        var restored = JsonSerializer.Deserialize<SessionResult>(json, opts);

        Assert.NotNull(restored);
        for (var i = 0; i < original.Sections.Count; i++)
        {
            Assert.Equal(original.Sections[i].Title,           restored.Sections[i].Title);
            Assert.Equal(original.Sections[i].PlannedDuration, restored.Sections[i].PlannedDuration);
            Assert.Equal(original.Sections[i].ActualDuration,  restored.Sections[i].ActualDuration);
            Assert.Equal(original.Sections[i].WasVisited,      restored.Sections[i].WasVisited);
        }
    }

    [Fact]
    public void FormatJson_EmptySections_ProducesEmptySectionsArray()
    {
        var json = SummaryFormatter.FormatJson(BuildEmptyResult());
        // The Sections array must be present and empty
        using var doc  = JsonDocument.Parse(json);
        var       root = doc.RootElement;
        Assert.True(root.TryGetProperty("Sections", out var sections));
        Assert.Equal(JsonValueKind.Array, sections.ValueKind);
        Assert.Equal(0, sections.GetArrayLength());
    }
}
