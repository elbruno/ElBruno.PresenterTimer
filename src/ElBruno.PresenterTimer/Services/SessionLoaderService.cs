using System.IO;
using System.Text.Json;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Models.Converters;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Loads <see cref="SessionPlan"/> objects from JSON files or strings and exports them back to JSON.
/// All I/O and parse errors are surfaced as <see cref="SessionLoadException"/>; no raw exceptions
/// escape to callers.
/// </summary>
public sealed class SessionLoaderService : ISessionLoaderService
{
    private static readonly JsonSerializerOptions SerializerOptions = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };
        options.Converters.Add(new TimeSpanJsonConverter());
        options.Converters.Add(new NullableTimeSpanJsonConverter());
        return options;
    }

    /// <inheritdoc/>
    public SessionPlan Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SessionLoadException($"Could not read session file '{path}': {ex.Message}", ex);
        }

        return TryParse(json);
    }

    /// <inheritdoc/>
    public SessionPlan TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new SessionLoadException("Session JSON is empty or whitespace.");

        SessionPlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<SessionPlan>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new SessionLoadException($"Invalid session JSON: {ex.Message}", ex);
        }

        if (plan is null)
            throw new SessionLoadException("Session JSON deserialized to null. Ensure the root element is a JSON object.");

        return plan;
    }

    /// <inheritdoc/>
    public string ExportJson(SessionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return JsonSerializer.Serialize(plan, SerializerOptions);
    }

    /// <inheritdoc/>
    public string ExportSampleJson()
    {
        var sample = new SessionPlan
        {
            Title = "Sample Session",
            Description = "A sample session plan for demonstration purposes.",
            Metadata = new SessionMetadata
            {
                Author = "Your Name",
                Version = "1.0",
                CreatedAt = DateTime.Today.ToString("yyyy-MM-dd"),
            },
            Sections =
            [
                new SessionSection
                {
                    Title = "Intro",
                    Duration = TimeSpan.FromMinutes(3),
                    Notes = "Welcome and context",
                    Color = "#4CAF50",
                    WarningAt = TimeSpan.FromMinutes(1),
                },
                new SessionSection
                {
                    Title = "Demo",
                    Duration = TimeSpan.FromMinutes(15),
                    Notes = "Main technical demo",
                    Color = "#2196F3",
                    WarningAt = TimeSpan.FromMinutes(2),
                },
                new SessionSection
                {
                    Title = "Wrap-up",
                    Duration = TimeSpan.FromMinutes(4),
                    Notes = "Summary and call to action",
                    Color = "#FF9800",
                    WarningAt = TimeSpan.FromMinutes(1),
                },
            ],
        };
        return ExportJson(sample);
    }

    /// <inheritdoc/>
    public TimeSpan GetTotalDuration(SessionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Sections.Aggregate(TimeSpan.Zero, (acc, s) => acc + s.Duration);
    }
}
