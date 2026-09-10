using System.Text.Encodings.Web;
using System.Text.Json;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Content;

/// <summary>The retained source packet's sorted-key, compact UTF-8 payload convention.</summary>
internal static class FarmCanonicalJson
{
    internal static ContentHash Hash(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(writer, value);
        }
        return ContentHash.Compute(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                JsonProperty[] properties = value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
                if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                {
                    throw new InvalidDataException("Canonical farm payloads cannot contain duplicate properties.");
                }
                writer.WriteStartObject();
                foreach (JsonProperty property in properties)
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement entry in value.EnumerateArray())
                {
                    Write(writer, entry);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number when value.TryGetInt64(out long integer):
                writer.WriteNumberValue(integer);
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(value.GetBoolean());
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidDataException("This farm payload profile requires exact signed integers and supported JSON values.");
        }
    }
}
