using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Selection;

/// <summary>A full selected observation on one authenticated connection's ordered presentation stream.</summary>
public sealed record PlayerSelectedObservation(EntityId StreamId, ulong Revision, LocalSelectedPlayerObservation Observation);

/// <summary>The full home and scoped actor share one aggregate byte/expansion budget and one capture identity.</summary>
public static class PlayerSelectedObservationCodec
{
    public const int Version = 1;
    public const int MaximumBodyBytes = PlayerViewCodec.MaximumBodyBytes;
    public const int MaximumExpandedJsonBytes = PlayerViewCodec.MaximumExpandedJsonBytes;
    private static readonly JsonSerializerOptions Options = SelectedObservationOptions.Create();
    public static byte[] Encode(PlayerSelectedObservation observation)
    {
        using var json = EncodeJson(observation);
        return json.WrittenSpan.Length <= MaximumBodyBytes ? json.WrittenSpan.ToArray() : ObservationCompression.Encode(json.WrittenSpan);
    }
    public static PlayerSelectedObservation Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.IsEmpty || bytes.Length > MaximumBodyBytes) throw new FormatException("Invalid selected observation size.");
            byte[]? expanded = ObservationCompression.IsCompressed(bytes) ? ObservationCompression.Decode(bytes) : null;
            ReadOnlySpan<byte> json = expanded is null ? bytes : expanded;
            PlayerViewCodec.Preflight(json);
            var body = JsonSerializer.Deserialize<Body>(json, Options) ?? throw new FormatException("Missing selected observation.");
            if (body.Version != Version || body.Value is null) throw new FormatException("Unsupported selected observation version.");
            using var canonical = EncodeJson(body.Value);
            if (!json.SequenceEqual(canonical.WrittenSpan)) throw new FormatException("Noncanonical selected observation.");
            return body.Value;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException
            or OverflowException or NullReferenceException or NotSupportedException or System.Reflection.TargetInvocationException)
        { throw new FormatException("Invalid selected observation values.", error); }
    }
    private static BoundedObservationJsonBuffer EncodeJson(PlayerSelectedObservation value)
    {
        SelectedObservationValidation.Validate(value);
        var buffer = new BoundedObservationJsonBuffer();
        try
        {
            using var writer = new Utf8JsonWriter(buffer);
            JsonSerializer.Serialize(writer, new Body(Version, value), Options); writer.Flush();
            PlayerViewCodec.Preflight(buffer.WrittenSpan);
            return buffer;
        }
        catch { buffer.Dispose(); throw; }
    }
    private sealed record Body(int Version, PlayerSelectedObservation Value);
}
