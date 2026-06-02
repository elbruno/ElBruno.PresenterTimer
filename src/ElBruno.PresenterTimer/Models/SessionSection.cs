using System.Text.Json.Serialization;
using ElBruno.PresenterTimer.Models.Converters;

namespace ElBruno.PresenterTimer.Models;

/// <summary>
/// Represents one timed segment within a <see cref="SessionPlan"/> (PRD §9.2).
/// <c>Duration</c> and <c>WarningAt</c> are serialized as "HH:mm:ss" strings.
/// </summary>
public sealed class SessionSection
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    [JsonConverter(typeof(TimeSpanJsonConverter))]
    public TimeSpan Duration { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("warningAt")]
    [JsonConverter(typeof(NullableTimeSpanJsonConverter))]
    public TimeSpan? WarningAt { get; set; }
}
