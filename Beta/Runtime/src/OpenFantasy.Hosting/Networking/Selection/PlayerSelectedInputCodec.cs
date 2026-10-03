using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Selection;

public static class PlayerSelectedInputCodec
{
    public const int MaximumBodyBytes = 4 + PlayerSelectionCodec.MaximumBodyBytes + WorldActionCodec.MaximumCommandByteLength;
    public static byte[] Encode(PlayerSelectedInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        byte[] selection = PlayerSelectionCodec.Encode(input.Selection);
        byte[] payload;
        switch (input.Kind)
        {
            case PlayerSelectedInputKind.SelectActor:
                if (input.TargetActorId is null || input.TargetActorId == default(EntityId))
                    throw new FormatException("Selection requires a named actor.");
                payload = input.TargetActorId!.Value.ToByteArray();
                break;
            case PlayerSelectedInputKind.Pause: payload = []; break;
            case PlayerSelectedInputKind.Resume:
                if (input.StreamId is null || input.StreamId == default(EntityId) || input.Revision is null or 0)
                    throw new FormatException("Resume requires a delivered observation identity.");
                payload = new byte[24]; input.StreamId.Value.WriteBytes(payload);
                BinaryPrimitives.WriteUInt64BigEndian(payload.AsSpan(16), input.Revision.Value);
                break;
            case PlayerSelectedInputKind.Movement:
                ValidateMovement(input.DeltaX, input.DeltaY);
                payload = new byte[8]; BinaryPrimitives.WriteInt32BigEndian(payload, input.DeltaX);
                BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(4), input.DeltaY); break;
            case PlayerSelectedInputKind.Command:
                if (input.Action is not { } action || action.ActorId != input.Selection.ActorId
                    || action.ControlWitness != input.Selection.ControlWitness)
                    throw new FormatException("Command does not match its actor selection.");
                payload = WorldActionCodec.Encode(action);
                if (payload.Length > WorldActionCodec.MaximumCommandByteLength) throw new FormatException("Oversized selected command.");
                break;
            case PlayerSelectedInputKind.Query:
                var operation = input.Operation ?? throw new FormatException("Missing selected query operation.");
                if (operation == LocalSelectedQueryOperation.ReadPrivateHistory)
                {
                    if (input.Inquiry is not null || input.SubjectId is not null || input.DirectoryRequest is null)
                        throw new FormatException("Directory input requires only its page request.");
                    byte[] directory = PlayerPrivateHistoryRequestCodec.Encode(input.DirectoryRequest);
                    payload = new byte[1 + directory.Length]; payload[0] = (byte)operation; directory.CopyTo(payload, 1);
                    break;
                }
                if (input.DirectoryRequest is not null) throw new FormatException("Unexpected directory request.");
                var query = LegacyQuery(operation, input.Inquiry, input.SubjectId);
                byte[] encoded = PlayerQueryCodec.Encode(query);
                payload = new byte[1 + encoded.Length]; payload[0] = (byte)operation; encoded.CopyTo(payload, 1); break;
            default: throw new FormatException("Unknown selected input kind.");
        }
        byte[] bytes = new byte[4 + selection.Length + payload.Length];
        bytes[0] = input.Operation == LocalSelectedQueryOperation.ReadPrivateHistory ? (byte)2 : (byte)1;
        bytes[1] = (byte)input.Kind;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), checked((ushort)selection.Length));
        selection.CopyTo(bytes, 4); payload.CopyTo(bytes, 4 + selection.Length); return bytes;
    }

    public static PlayerSelectedInput Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.Length < 4 || bytes.Length > MaximumBodyBytes || bytes[0] is not (1 or 2)) throw new FormatException("Invalid selected input header.");
            int length = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            if (length > bytes.Length - 4) throw new FormatException("Truncated actor selection.");
            var selection = PlayerSelectionCodec.Decode(bytes.Slice(4, length));
            var payload = bytes[(4 + length)..];
            var kind = (PlayerSelectedInputKind)bytes[1];
            PlayerSelectedInput result = kind switch
            {
                PlayerSelectedInputKind.SelectActor when payload.Length == 16 => PlayerSelectedInput.SelectActor(selection, EntityId.FromBytes(payload)),
                PlayerSelectedInputKind.Pause when payload.IsEmpty => PlayerSelectedInput.Pause(selection),
                PlayerSelectedInputKind.Resume when payload.Length == 24 => PlayerSelectedInput.Resume(selection, EntityId.FromBytes(payload[..16]), BinaryPrimitives.ReadUInt64BigEndian(payload[16..])),
                PlayerSelectedInputKind.Movement when payload.Length == 8 => PlayerSelectedInput.Movement(selection, BinaryPrimitives.ReadInt32BigEndian(payload), BinaryPrimitives.ReadInt32BigEndian(payload[4..])),
                PlayerSelectedInputKind.Command when payload.Length <= WorldActionCodec.MaximumCommandByteLength => PlayerSelectedInput.Command(selection, WorldActionCodec.Decode(payload)),
                PlayerSelectedInputKind.Query when payload.Length > 1 && bytes[0] == 1 => ReadQuery(selection, payload),
                PlayerSelectedInputKind.Query when payload.Length > 1 && bytes[0] == 2
                    && payload[0] == (byte)LocalSelectedQueryOperation.ReadPrivateHistory =>
                    PlayerSelectedInput.ReadPrivateHistory(selection, PlayerPrivateHistoryRequestCodec.Decode(payload[1..])),
                _ => throw new FormatException("Unknown selected input or contradictory payload length.")
            };
            if (!bytes.SequenceEqual(Encode(result))) throw new FormatException("Noncanonical selected input.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { throw new FormatException("Invalid selected input values.", error); }
    }

    private static PlayerSelectedInput ReadQuery(LocalActorSelection selection, ReadOnlySpan<byte> bytes)
    {
        var operation = (LocalSelectedQueryOperation)bytes[0];
        var legacy = PlayerQueryCodec.Decode(bytes[1..]);
        var expected = LegacyQuery(operation, legacy.Inquiry, legacy.SubjectId);
        if (legacy.Kind != expected.Kind) throw new FormatException("Selected query operation and payload disagree.");
        return PlayerSelectedInput.Query(selection, operation, legacy.Inquiry, legacy.SubjectId);
    }

    internal static PlayerQuery LegacyQuery(LocalSelectedQueryOperation operation, LocalTradeInquiry? inquiry, EntityId? subject)
    {
        PlayerQueryKind kind = operation switch
        {
            LocalSelectedQueryOperation.SubmitTradeInquiry => PlayerQueryKind.SubmitTradeInquiry,
            LocalSelectedQueryOperation.CancelTradeInquiry => PlayerQueryKind.CancelTradeInquiry,
            LocalSelectedQueryOperation.ReadTradeInquiry => PlayerQueryKind.ReadTradeInquiry,
            LocalSelectedQueryOperation.InspectOpenPaidQuote or LocalSelectedQueryOperation.InspectPaidQuoteHistory => PlayerQueryKind.InspectPaidQuote,
            LocalSelectedQueryOperation.InspectOpenMaterialProposal or LocalSelectedQueryOperation.InspectMaterialProposalHistory => PlayerQueryKind.InspectMaterialProposal,
            _ => throw new FormatException("Unknown selected query operation.")
        };
        return new(kind, inquiry, subject);
    }
    private static void ValidateMovement(int x, int y)
    {
        if (!(x == 0 && y == 0 || x == 0 && y is -32 or 32 || y == 0 && x is -32 or 32))
            throw new FormatException("Selected movement requires one fixed cardinal sample or stop.");
    }
}
