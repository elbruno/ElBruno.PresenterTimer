using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElBruno.PresenterTimer.Models.Converters;

/// <summary>
/// Serializes/deserializes <see cref="TimeSpan"/> as an "HH:mm:ss" string (e.g. "00:15:00").
/// </summary>
public sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    internal const string Format = @"hh\:mm\:ss";

    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null || !TimeSpan.TryParseExact(value, Format, null, out var result))
            throw new JsonException($"Cannot parse '{value}' as a TimeSpan. Expected format: HH:mm:ss.");
        return result;
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format));
}
