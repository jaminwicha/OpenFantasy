using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Control;

/// <summary>Bounded canonical administration messages. Existing commands, witnesses and receipts keep their bytes.</summary>
public static class PlayerControlRequestCodec
{
    public const int MaximumBodyBytes = PlayerQueryResultCodec.MaximumBodyBytes;

    public static byte[] Encode(PlayerControlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool valid = request.Kind switch
        {
            PlayerControlRequestKind.ReadPermissions => request.Command is null && request.Acquisition is null && request.Witness is null,
            PlayerControlRequestKind.SubmitPermission => request.Command is { Intent: ControlPermissionIntent, ControlWitness: null }
                && request.Acquisition is null && request.Witness is null,
            PlayerControlRequestKind.Acquire => request.Command is null && request.Acquisition is not null && request.Witness is null,
            PlayerControlRequestKind.Release => request.Command is null && request.Acquisition is null && request.Witness is not null,
            _ => false
        };
        if (!valid) throw new FormatException("Control administration requires its exact operation shape.");
        return ControlJson.Encode(new RequestBody(1, request.Kind,
            request.Command is { } command ? WorldActionCodec.Encode(command) : null, request.Acquisition,
            request.Witness is { } witness ? WorldControlWitnessCodecV1.Encode(witness) : null));
    }

    public static PlayerControlRequest Decode(ReadOnlySpan<byte> bytes) => ControlJson.Decode(bytes, data =>
    {
        RequestBody body = JsonSerializer.Deserialize<RequestBody>(data, ControlJson.Options)
            ?? throw new FormatException("Missing control request.");
        if (body.Version != 1) throw new FormatException("Unsupported control request version.");
        return new PlayerControlRequest(body.Kind, body.Command is { } command ? WorldActionCodec.Decode(command) : null,
            body.Acquisition, body.Witness is { } witness ? WorldControlWitnessCodecV1.Decode(witness) : null);
    }, Encode);

    private sealed record RequestBody(int Version, PlayerControlRequestKind Kind, byte[]? Command,
        LocalControlAcquisition? Acquisition, byte[]? Witness);
}

public static class PlayerControlResponseCodec
{
    public static byte[] Encode(PlayerControlResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        bool valid = response.Kind switch
        {
            PlayerControlRequestKind.ReadPermissions => response.Board is { } board && response.Receipt is null && response.Witness is null
                && board.WorldId == response.WorldId && board.BranchId == response.BranchId && board.HomeActorId == response.HomeActorId,
            PlayerControlRequestKind.SubmitPermission => response.Board is null && response.Receipt is { } receipt && response.Witness is null
                && receipt.WorldId == response.WorldId && receipt.BranchId == response.BranchId,
            PlayerControlRequestKind.Acquire => response.Board is null && response.Receipt is null
                && response.Witness is { } witness && witness.GranteeActorId == response.HomeActorId,
            PlayerControlRequestKind.Release => response.Board is null && response.Receipt is null && response.Witness is null,
            _ => false
        };
        if (!valid) throw new FormatException("Control response does not match its operation and participant scope.");
        return ControlJson.Encode(new ResponseBody(1, response.WorldId, response.BranchId, response.HomeActorId,
            response.Kind, response.Board, response.Receipt is { } result ? WorldReceiptCodecV1.Encode(result) : null,
            response.Witness is { } held ? WorldControlWitnessCodecV1.Encode(held) : null));
    }

    public static PlayerControlResponse Decode(ReadOnlySpan<byte> bytes) => ControlJson.Decode(bytes, data =>
    {
        ResponseBody body = JsonSerializer.Deserialize<ResponseBody>(data, ControlJson.Options)
            ?? throw new FormatException("Missing control response.");
        if (body.Version != 1) throw new FormatException("Unsupported control response version.");
        return new PlayerControlResponse(body.WorldId, body.BranchId, body.HomeActorId, body.Kind, body.Board,
            body.Receipt is { } receipt ? WorldReceiptCodecV1.Decode(receipt) : null,
            body.Witness is { } witness ? WorldControlWitnessCodecV1.Decode(witness) : null);
    }, Encode);

    private sealed record ResponseBody(int Version, EntityId WorldId, EntityId BranchId, EntityId HomeActorId,
        PlayerControlRequestKind Kind, LocalControlPermissionBoard? Board, byte[]? Receipt, byte[]? Witness);
}

internal static class ControlJson
{
    internal static readonly JsonSerializerOptions Options = PlayerViewCodec.CreateOptions();

    internal static byte[] Encode<T>(T body)
    {
        var buffer = new PlayerQueryJsonBuffer();
        using var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, body, Options);
        writer.Flush();
        PlayerViewCodec.Preflight(buffer.WrittenSpan);
        return buffer.WrittenSpan.ToArray();
    }

    internal static T Decode<T>(ReadOnlySpan<byte> bytes, Func<byte[], T> decode, Func<T, byte[]> encode)
    {
        try
        {
            if (bytes.IsEmpty || bytes.Length > PlayerControlRequestCodec.MaximumBodyBytes)
                throw new FormatException("Control administration exceeds its nonempty byte bound.");
            PlayerViewCodec.Preflight(bytes);
            T value = decode(bytes.ToArray());
            if (!bytes.SequenceEqual(encode(value))) throw new FormatException("Control administration requires complete canonical values.");
            return value;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException
            or OverflowException or NullReferenceException or NotSupportedException or System.Reflection.TargetInvocationException)
        {
            throw new FormatException("Invalid control administration values.", error);
        }
    }
}
