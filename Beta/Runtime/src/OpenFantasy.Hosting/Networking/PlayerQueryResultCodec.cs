using System.Text.Json;
using OpenFantasy.Hosting.Networking.ObservationViews;

namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// Version 1 canonical UTF-8 JSON containing Version and Result. Complete explicit fields, including nulls,
/// use the observation value converters; compression, world roots and alternate JSON spellings are refused.
/// </summary>
public static class PlayerQueryResultCodec
{
    public const int Version = 1;
    public const int MaximumBodyBytes = 16 * 1024;
    private static readonly JsonSerializerOptions Options = PlayerViewCodec.CreateOptions();

    public static byte[] Encode(PlayerQueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        PlayerQueryResultValidation.Validate(result);
        var buffer = new PlayerQueryJsonBuffer();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            JsonSerializer.Serialize(writer, new ResultBody(Version, result), Options);
            writer.Flush();
        }
        PlayerViewCodec.Preflight(buffer.WrittenSpan);
        return buffer.WrittenSpan.ToArray();
    }

    public static PlayerQueryResult Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.IsEmpty || bytes.Length > MaximumBodyBytes)
                throw new FormatException("Query result exceeds its nonempty byte bound.");
            PlayerViewCodec.Preflight(bytes);
            ResultBody body = JsonSerializer.Deserialize<ResultBody>(bytes, Options)
                ?? throw new FormatException("Missing query result.");
            if (body.Version != Version || body.Result is null)
                throw new FormatException("Unsupported or incomplete query result.");
            if (!bytes.SequenceEqual(Encode(body.Result)))
                throw new FormatException("Query results require their complete canonical value encoding.");
            return body.Result;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException
            or OverflowException or NullReferenceException or NotSupportedException or System.Reflection.TargetInvocationException)
        {
            throw new FormatException("Invalid query result values.", exception);
        }
    }

    private sealed record ResultBody(int Version, PlayerQueryResult Result);
}
