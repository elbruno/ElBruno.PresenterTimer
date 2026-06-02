using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Loads and parses session plans from JSON files or JSON strings,
/// and exports session plans back to normalized JSON.
/// </summary>
public interface ISessionLoaderService
{
    /// <summary>
    /// Reads a JSON file at <paramref name="path"/> and parses it into a <see cref="SessionPlan"/>.
    /// </summary>
    /// <exception cref="Services.SessionLoadException">
    /// Thrown if the file cannot be read or the JSON is malformed.
    /// </exception>
    SessionPlan Load(string path);

    /// <summary>
    /// Parses a raw JSON string into a <see cref="SessionPlan"/>.
    /// </summary>
    /// <exception cref="Services.SessionLoadException">
    /// Thrown if the JSON is empty, null, or malformed.
    /// </exception>
    SessionPlan TryParse(string json);

    /// <summary>
    /// Serializes a <see cref="SessionPlan"/> to indented, normalized JSON.
    /// Used by "Export Normalized JSON" in the Session Preview window.
    /// </summary>
    string ExportJson(SessionPlan plan);

    /// <summary>
    /// Returns a fully populated sample <see cref="SessionPlan"/> serialized to JSON.
    /// Used by "Export Sample JSON" in the tray menu.
    /// </summary>
    string ExportSampleJson();

    /// <summary>
    /// Sums the <see cref="SessionSection.Duration"/> of all sections in <paramref name="plan"/>.
    /// </summary>
    TimeSpan GetTotalDuration(SessionPlan plan);
}
