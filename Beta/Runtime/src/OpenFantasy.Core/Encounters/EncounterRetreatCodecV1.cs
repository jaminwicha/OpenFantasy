using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// Canonical bounded retreat owner: tag <c>encounter.retreat.v1</c>, format version 1, rules pin, accepted count,
/// optional continuation frame and optional terminal-record frame (empty fields when absent). Decode re-encodes.
/// </summary>
public static class EncounterRetreatCodecV1
{
    public const int MaximumEncodedByteLength = 4 * 1024;
    private const int MaximumContinuationByteLength = EncounterRetreatState.MaximumCommandByteLength + 256;
    private const int MaximumOutcomeByteLength = 512 + EncounterRetreatOutcome.MaximumCancelledDecisions * EntityId.ByteLength;

    public static byte[] Encode(EncounterRetreatState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] active = state.Active is { } continuation ? CanonicalEncodingV1.Frame("encounter.retreat-continuation.v1",
            WorldActionCodec.Encode(continuation.Command), Position(continuation.Origin),
            CanonicalEncodingV1.Unsigned64((ulong)continuation.SamplesTaken),
            CanonicalEncodingV1.Unsigned64(continuation.ReadinessCharged)) : [];
        byte[] last = state.Last is { } outcome ? CanonicalEncodingV1.Frame("encounter.retreat-outcome.v1",
            outcome.ActorId.ToByteArray(), outcome.CommandId.ToByteArray(), outcome.EncounterId.ToByteArray(),
            Position(outcome.Origin), Position(outcome.Final), CanonicalEncodingV1.Unsigned64((ulong)outcome.SamplesTaken),
            CanonicalEncodingV1.Unsigned64((ulong)outcome.RouteSamples), CanonicalEncodingV1.Unsigned64(outcome.ReadinessCharged),
            CanonicalEncodingV1.Unsigned64((ulong)outcome.Status), CanonicalEncodingV1.Unsigned64((ulong)outcome.Reason),
            CanonicalEncodingV1.Unsigned64(outcome.WorldSequence),
            outcome.CancelledDecisionIds.SelectMany(identity => identity.ToByteArray()).ToArray()) : [];
        byte[] bytes = CanonicalEncodingV1.Frame("encounter.retreat.v1", CanonicalEncodingV1.Unsigned64(1),
            state.RulesPin.ToByteArray(), CanonicalEncodingV1.Unsigned64(state.AcceptedCount), active, last);
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The retreat owner exceeds its retained bound.", nameof(state));
        }
        return bytes;
    }

    public static EncounterRetreatState Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 6);
            reader.Tag("encounter.retreat.v1"u8);
            reader.Version();
            var pin = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
            ulong accepted = reader.Number();
            ReadOnlySpan<byte> activeBytes = reader.Field(MaximumContinuationByteLength);
            ReadOnlySpan<byte> lastBytes = reader.Field(MaximumOutcomeByteLength);
            reader.Complete();
            EncounterRetreatContinuation? active = activeBytes.IsEmpty ? null : DecodeContinuation(activeBytes);
            EncounterRetreatOutcome? last = lastBytes.IsEmpty ? null : DecodeOutcome(lastBytes);
            var state = new EncounterRetreatState(pin, accepted, active, last);
            if (!Encode(state).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The retreat owner is not canonical.");
            }
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidCastException or OverflowException)
        {
            throw new FormatException("The retreat owner violates its invariants.", error);
        }
    }

    private static EncounterRetreatContinuation DecodeContinuation(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumContinuationByteLength, 5);
        reader.Tag("encounter.retreat-continuation.v1"u8);
        WorldActionCommand command = WorldActionCodec.Decode(reader.Field(EncounterRetreatState.MaximumCommandByteLength));
        SubunitPosition origin = ReadPosition(reader.Field(PositionByteLength));
        int samples = checked((int)reader.Number());
        ulong charged = reader.Number();
        reader.Complete();
        return new EncounterRetreatContinuation(command, origin, samples, charged);
    }

    private static EncounterRetreatOutcome DecodeOutcome(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumOutcomeByteLength, 13);
        reader.Tag("encounter.retreat-outcome.v1"u8);
        EntityId actor = reader.Identity(), command = reader.Identity(), encounter = reader.Identity();
        SubunitPosition origin = ReadPosition(reader.Field(PositionByteLength));
        SubunitPosition final = ReadPosition(reader.Field(PositionByteLength));
        int samples = checked((int)reader.Number());
        int route = checked((int)reader.Number());
        ulong charged = reader.Number();
        ulong status = reader.Number();
        ulong reason = reader.Number();
        ulong sequence = reader.Number();
        ReadOnlySpan<byte> cancelledBytes = reader.Field(EncounterRetreatOutcome.MaximumCancelledDecisions * EntityId.ByteLength);
        reader.Complete();
        if (status > int.MaxValue || reason > int.MaxValue || cancelledBytes.Length % EntityId.ByteLength != 0)
        {
            throw new FormatException("Unknown retreat record status, reason or cancelled identity row.");
        }
        List<EntityId> cancelled = [];
        for (int offset = 0; offset < cancelledBytes.Length; offset += EntityId.ByteLength)
        {
            EntityId identity = EntityId.FromBytes(cancelledBytes.Slice(offset, EntityId.ByteLength));
            if (cancelled.Count != 0 && identity.CompareTo(cancelled[^1]) <= 0)
            {
                throw new FormatException("Cancelled retreat choices are not in strict identity order.");
            }
            cancelled.Add(identity);
        }
        return new EncounterRetreatOutcome(actor, command, encounter, origin, final, samples, route, charged,
            (EncounterRetreatStatus)(int)status, (EncounterRetreatReason)(int)reason, sequence, cancelled);
    }

    internal static byte[] Position(SubunitPosition position) => CanonicalEncodingV1.Frame("spatial.subunit-position.v1",
        Signed(position.X), Signed(position.Y), Signed(position.Z));

    /// <summary>Tagged frame of three signed coordinates: 79 bytes.</summary>
    internal const int PositionByteLength = 96;

    internal static SubunitPosition ReadPosition(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, PositionByteLength, 4);
        reader.Tag("spatial.subunit-position.v1"u8);
        var position = new SubunitPosition(reader.Signed(), reader.Signed(), reader.Signed());
        reader.Complete();
        return position;
    }

    private static byte[] Signed(long value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(result, value);
        return result;
    }
}
