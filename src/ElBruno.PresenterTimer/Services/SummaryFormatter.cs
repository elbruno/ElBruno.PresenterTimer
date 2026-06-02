using System.Text;
using System.Text.Json;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Models.Converters;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// UI-agnostic formatter that converts a <see cref="SessionResult"/> into
/// plain text, Markdown, or JSON (PRD §7.14).
/// All methods are pure functions — no WPF/WinForms dependencies.
/// Lambert can cover <see cref="FormatPlainText"/> and <see cref="FormatMarkdown"/> with unit tests.
/// </summary>
public static class SummaryFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        opts.Converters.Add(new TimeSpanJsonConverter());
        opts.Converters.Add(new NullableTimeSpanJsonConverter());
        return opts;
    }

    // ── Time helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Formats an absolute <see cref="TimeSpan"/> as <c>MM:ss</c> (under 1 hour)
    /// or <c>HH:MM:ss</c>.  Negative values are treated as zero.
    /// </summary>
    public static string FormatTime(TimeSpan ts)
    {
        var abs = ts < TimeSpan.Zero ? TimeSpan.Zero : ts;
        if (abs.TotalHours >= 1)
            return $"{(int)abs.TotalHours:D2}:{abs.Minutes:D2}:{abs.Seconds:D2}";
        return $"{(int)abs.TotalMinutes:D2}:{abs.Seconds:D2}";
    }

    /// <summary>
    /// Formats a signed difference <see cref="TimeSpan"/> with a leading <c>+</c> or <c>-</c>.
    /// Examples: <c>+04:20</c>, <c>-00:10</c>, <c>+00:00</c>.
    /// </summary>
    public static string FormatDifference(TimeSpan diff)
    {
        if (diff < TimeSpan.Zero)
            return "-" + FormatTime(-diff);
        return "+" + FormatTime(diff);
    }

    // ── Plain text (PRD §7.14 example format) ────────────────────────────────

    /// <summary>
    /// Produces the plain-text summary shown in PRD §7.14.
    /// </summary>
    public static string FormatPlainText(SessionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        sb.AppendLine($"Session completed: {result.SessionTitle}");
        sb.AppendLine();
        sb.AppendLine($"Planned:    {FormatTime(result.PlannedDuration)}");
        sb.AppendLine($"Actual:     {FormatTime(result.ActualDuration)}");
        sb.AppendLine($"Difference: {FormatDifference(result.Difference)}");

        if (result.TotalExtensions > TimeSpan.Zero)
            sb.AppendLine($"Extensions: {FormatTime(result.TotalExtensions)}");

        sb.AppendLine();

        foreach (var s in result.Sections)
        {
            if (!s.WasVisited) continue;

            var extras = new List<string>();
            if (s.WasOvertime)                        extras.Add("OVERTIME");
            if (s.WasSkipped)                         extras.Add("skipped");
            if (s.TotalExtensions > TimeSpan.Zero)    extras.Add($"ext {FormatTime(s.TotalExtensions)}");
            if (s.RestartCount > 0)                   extras.Add($"restarts {s.RestartCount}");

            var extrasStr = extras.Count > 0 ? $"  [{string.Join(", ", extras)}]" : string.Empty;
            sb.AppendLine(
                $"{s.Title}: planned {FormatTime(s.PlannedDuration)}, " +
                $"actual {FormatTime(s.ActualDuration)}, " +
                $"{FormatDifference(s.Difference)}" +
                extrasStr);
        }

        // Include unvisited sections so the reader knows they were not reached
        var unvisited = result.Sections.Where(s => !s.WasVisited).ToList();
        if (unvisited.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Not reached:");
            foreach (var s in unvisited)
                sb.AppendLine($"  {s.Title}: planned {FormatTime(s.PlannedDuration)}");
        }

        return sb.ToString().TrimEnd();
    }

    // ── Markdown ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Produces a Markdown-formatted summary with a header table and section table.
    /// </summary>
    public static string FormatMarkdown(SessionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        sb.AppendLine($"# Session Summary: {result.SessionTitle}");
        sb.AppendLine();
        sb.AppendLine("| | |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| **Planned** | `{FormatTime(result.PlannedDuration)}` |");
        sb.AppendLine($"| **Actual** | `{FormatTime(result.ActualDuration)}` |");
        sb.AppendLine($"| **Difference** | `{FormatDifference(result.Difference)}` |");
        if (result.TotalExtensions > TimeSpan.Zero)
            sb.AppendLine($"| **Total Extensions** | `{FormatTime(result.TotalExtensions)}` |");

        sb.AppendLine();
        sb.AppendLine("## Sections");
        sb.AppendLine();
        sb.AppendLine("| # | Section | Planned | Actual | Diff | Overtime | Extensions | Skipped |");
        sb.AppendLine("|---|---------|---------|--------|------|:--------:|:----------:|:-------:|");

        foreach (var s in result.Sections)
        {
            var num        = (s.Index + 1).ToString();
            var title      = s.WasVisited ? s.Title : $"_{s.Title}_ *(not reached)*";
            var actual     = s.WasVisited ? $"`{FormatTime(s.ActualDuration)}`" : "—";
            var diff       = s.WasVisited ? $"`{FormatDifference(s.Difference)}`" : "—";
            var overtime   = s.WasOvertime ? "✓" : "";
            var extensions = s.TotalExtensions > TimeSpan.Zero
                ? $"`{FormatTime(s.TotalExtensions)}`" : "";
            var skipped    = s.WasSkipped ? "✓" : "";

            sb.AppendLine(
                $"| {num} | {title} | `{FormatTime(s.PlannedDuration)}` | {actual} | {diff} | {overtime} | {extensions} | {skipped} |");
        }

        return sb.ToString().TrimEnd();
    }

    // ── JSON ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serialises the <see cref="SessionResult"/> (including all sections) to indented JSON.
    /// <see cref="TimeSpan"/> values are written as <c>"HH:mm:ss"</c> strings.
    /// </summary>
    public static string FormatJson(SessionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
