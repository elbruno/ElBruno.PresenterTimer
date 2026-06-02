using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElBruno.PresenterTimer.Models.Converters;

/// <summary>
/// Serializes/deserializes <see cref="TimeSpan?"/> as an "HH:mm:ss" string or JSON null.
/// </summary>
public sealed class NullableTimeSpanJsonConverter : JsonConverter<TimeSpan?>
{
    public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var value = reader.GetString();
        if (value is null)
            return null;

        if (!TimeSpan.TryParseExact(value, TimeSpanJsonConverter.Format, null, out var result))
            throw new JsonException($"Cannot parse '{value}' as a TimeSpan. Expected format: HH:mm:ss.");

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString(TimeSpanJsonConverter.Format));
        else
            writer.WriteNullValue();
    }
}
