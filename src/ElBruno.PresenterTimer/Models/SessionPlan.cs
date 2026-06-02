using System.Text.Json.Serialization;

namespace ElBruno.PresenterTimer.Models;

/// <summary>
/// Root model for an imported session timeline (PRD §9.1).
/// Maps 1-to-1 with the JSON schema defined in PRD §7.3.
/// </summary>
public sealed class SessionPlan
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("metadata")]
    public SessionMetadata? Metadata { get; set; }

    [JsonPropertyName("sections")]
    public List<SessionSection> Sections { get; set; } = [];
}
