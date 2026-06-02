using System.Text.Json.Serialization;

namespace ElBruno.PresenterTimer.Models;

/// <summary>
/// Optional extended metadata for a <see cref="SessionPlan"/>, corresponding to the
/// <c>metadata</c> object in the extended JSON format (PRD §7.3).
/// </summary>
public sealed class SessionMetadata
{
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }
}
