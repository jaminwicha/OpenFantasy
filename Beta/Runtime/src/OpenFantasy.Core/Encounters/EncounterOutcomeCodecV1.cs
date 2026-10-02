using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// Canonical bounded outcome owner: tag <c>encounter.outcome.v1</c>, format version 1, rules pin, finalized count, the
/// acknowledgment rows frame and an optional finalization-record frame (empty when absent). Decode re-encodes.
/// </summary>
public static class EncounterOutcomeCodecV1
{
    private const int AcknowledgmentByteLength = 256;
    private const int MaximumAcknowledgmentsByteLength = 64 + EncounterOutcomeState.MaximumAcknowledgments * AcknowledgmentByteLength;
    private const int MaximumRecordByteLength = 256 + EncounterOutcomeState.MaximumAcknowledgments * EntityId.ByteLength;
    public const int MaximumEncodedByteLength = 256 + MaximumAcknowledgmentsByteLength + MaximumRecordByteLength;

    public static byte[] Encode(EncounterOutcomeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] rows = CanonicalEncodingV1.Frame("encounter.outcome-acknowledgments.v1", state.Acknowledgments.Select(row =>
            (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("encounter.outcome-acknowledgment.v1", row.ActorId.ToByteArray(),
                row.CommandId.ToByteArray(), row.EncounterId.ToByteArray(), row.ActionHash.ToByteArray(),
                CanonicalEncodingV1.Unsigned64(row.WorldSequence))).ToArray());
        byte[] last = state.Last is { } record ? CanonicalEncodingV1.Frame("encounter.outcome-record.v1",
            record.EncounterId.ToByteArray(), record.ActionHash.ToByteArray(), CanonicalEncodingV1.Unsigned64((ulong)record.Result),
            CanonicalEncodingV1.Unsigned64((ulong)record.StandingSide), CanonicalEncodingV1.Unsigned64(record.WorldSequence),
            record.AcknowledgedActorIds.SelectMany(identity => identity.ToByteArray()).ToArray()) : [];
        byte[] bytes = CanonicalEncodingV1.Frame("encounter.outcome.v1", CanonicalEncodingV1.Unsigned64(1),
            state.RulesPin.ToByteArray(), CanonicalEncodingV1.Unsigned64(state.FinalizedCount), rows, last);
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The outcome owner exceeds its retained bound.", nameof(state));
        }
        return bytes;
    }

    public static EncounterOutcomeState Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 6);
            reader.Tag("encounter.outcome.v1"u8);
            reader.Version();
            var pin = Hash(reader.Exact(32));
            ulong finalized = reader.Number();
            ReadOnlySpan<byte> rowBytes = reader.Field(MaximumAcknowledgmentsByteLength);
            ReadOnlySpan<byte> lastBytes = reader.Field(MaximumRecordByteLength);
            reader.Complete();
            List<EncounterOutcomeAcknowledgment> rows = DecodeAcknowledgments(rowBytes);
            EncounterOutcomeRecord? last = lastBytes.IsEmpty ? null : DecodeRecord(lastBytes);
            var state = new EncounterOutcomeState(pin, finalized, rows, last);
            if (!Encode(state).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The outcome owner is not canonical.");
            }
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidCastException or OverflowException)
        {
            throw new FormatException("The outcome owner violates its invariants.", error);
        }
    }

    private static List<EncounterOutcomeAcknowledgment> DecodeAcknowledgments(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumAcknowledgmentsByteLength, EncounterOutcomeState.MaximumAcknowledgments + 1);
        reader.Tag("encounter.outcome-acknowledgments.v1"u8);
        List<EncounterOutcomeAcknowledgment> rows = [];
        while (reader.Remaining != 0)
        {
            var row = new WorldFrameReader(reader.Field(AcknowledgmentByteLength), AcknowledgmentByteLength, 6);
            row.Tag("encounter.outcome-acknowledgment.v1"u8);
            EntityId actor = row.Identity(), command = row.Identity(), encounter = row.Identity();
            ContentHash hash = Hash(row.Exact(32));
            ulong sequence = row.Number();
            row.Complete();
            rows.Add(new EncounterOutcomeAcknowledgment(actor, command, encounter, hash, sequence));
        }
        reader.Complete();
        return rows;
    }

    private static EncounterOutcomeRecord DecodeRecord(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumRecordByteLength, 7);
        reader.Tag("encounter.outcome-record.v1"u8);
        EntityId encounter = reader.Identity();
        ContentHash hash = Hash(reader.Exact(32));
        ulong result = reader.Number();
        ulong side = reader.Number();
        ulong sequence = reader.Number();
        ReadOnlySpan<byte> readers = reader.Field(EncounterOutcomeState.MaximumAcknowledgments * EntityId.ByteLength);
        reader.Complete();
        if (result > int.MaxValue || side > byte.MaxValue || readers.Length % EntityId.ByteLength != 0)
        {
            throw new FormatException("Unknown outcome result, standing side or reader row.");
        }
        List<EntityId> acknowledged = [];
        for (int offset = 0; offset < readers.Length; offset += EntityId.ByteLength)
        {
            acknowledged.Add(EntityId.FromBytes(readers.Slice(offset, EntityId.ByteLength)));
        }
        return new EncounterOutcomeRecord(encounter, hash, (EncounterOutcomeResult)(int)result, (int)side, sequence, acknowledged);
    }

    private static ContentHash Hash(ReadOnlySpan<byte> bytes) => new(Convert.ToHexString(bytes).ToLowerInvariant());
}
