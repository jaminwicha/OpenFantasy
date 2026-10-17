using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>Explicit collective interpretation, with unchanged legacy terminal evidence nested as its original bytes.</summary>
public static class EncounterRetreatCodecV2
{
    public const int MaximumEncodedByteLength = 512 * 1024;

    public static byte[] Encode(EncounterRetreatState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var withdrawal = state.Withdrawal ?? throw new ArgumentException("Collective retreat requires its explicit owner.");
        var legacy = new EncounterRetreatState(state.RulesPin, state.AcceptedCount, null, state.Last);
        byte[] parties = CanonicalEncodingV1.Frame("encounter.withdrawal-sides.v1", withdrawal.Parties
            .Select(party => (ReadOnlyMemory<byte>)EncounterWithdrawalCodecV1.Encode(party)).ToArray());
        byte[] bytes = CanonicalEncodingV1.Frame("encounter.retreat.v2", CanonicalEncodingV1.Unsigned64(2),
            EncounterWithdrawalState.RulesPin.ToByteArray(), EncounterRetreatCodecV1.Encode(legacy),
            CanonicalEncodingV1.Unsigned64(withdrawal.Revision), parties,
            withdrawal.Last is { } last ? EncodeResolution(last) : []);
        if (bytes.Length > MaximumEncodedByteLength) throw new ArgumentException("Collective retreat exceeds its byte bound.");
        return bytes;
    }

    public static EncounterRetreatState Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 7);
            reader.Tag("encounter.retreat.v2"u8);
            if (reader.Number() != 2 || !reader.Exact(32).SequenceEqual(EncounterWithdrawalState.RulesPin.ToByteArray()))
                throw new FormatException("Unknown collective retreat interpretation.");
            var legacy = EncounterRetreatCodecV1.Decode(reader.Field(EncounterRetreatCodecV1.MaximumEncodedByteLength));
            if (legacy.Active is not null) throw new FormatException("A legacy continuation cannot run under collective rules.");
            ulong revision = reader.Number();
            var sideReader = new WorldFrameReader(reader.Field(MaximumEncodedByteLength), MaximumEncodedByteLength, 3);
            sideReader.Tag("encounter.withdrawal-sides.v1"u8);
            List<EncounterWithdrawalParty> parties = [];
            while (sideReader.Remaining > 0)
                parties.Add(EncounterWithdrawalCodecV1.Decode(sideReader.Field(EncounterWithdrawalCodecV1.MaximumEncodedByteLength)));
            sideReader.Complete();
            var last = reader.Field(MaximumEncodedByteLength);
            reader.Complete();
            var state = legacy.WithWithdrawal(new(revision, parties, last.IsEmpty ? null : DecodeResolution(last)));
            if (!Encode(state).AsSpan().SequenceEqual(bytes)) throw new FormatException("Collective retreat is not canonical.");
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidCastException or OverflowException)
        {
            throw new FormatException("Collective retreat violates its invariants.", error);
        }
    }

    private static byte[] EncodeResolution(EncounterWithdrawalResolution value) => CanonicalEncodingV1.Frame(
        "encounter.withdrawal-resolution.v1", CanonicalEncodingV1.Unsigned64(value.WorldSequence),
        CanonicalEncodingV1.Frame("encounter.withdrawal-ended.v1", value.Ended.Select(end => (ReadOnlyMemory<byte>)
            CanonicalEncodingV1.Frame("encounter.withdrawal-end.v1", EncounterWithdrawalCodecV1.EncodeMember(end.Member),
                CanonicalEncodingV1.Unsigned64((ulong)end.Reason), CanonicalEncodingV1.Unsigned64(end.FailedSampleCost))).ToArray()),
        value.CancelledDecisions.SelectMany(identity => identity.ToByteArray()).ToArray());

    private static EncounterWithdrawalResolution DecodeResolution(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 4);
        reader.Tag("encounter.withdrawal-resolution.v1"u8);
        ulong sequence = reader.Number();
        var endedReader = new WorldFrameReader(reader.Field(MaximumEncodedByteLength), MaximumEncodedByteLength,
            EncounterTimingSnapshot.MaximumActors + 1);
        endedReader.Tag("encounter.withdrawal-ended.v1"u8);
        List<EncounterWithdrawalEnd> ended = [];
        while (endedReader.Remaining > 0)
        {
            var end = new WorldFrameReader(endedReader.Field(2048), 2048, 4);
            end.Tag("encounter.withdrawal-end.v1"u8);
            var member = EncounterWithdrawalCodecV1.DecodeMember(end.Field(EncounterWithdrawalCodecV1.MaximumMemberByteLength));
            var reason = (EncounterWithdrawalEndReason)checked((int)end.Number());
            ulong cost = end.Number();
            end.Complete();
            ended.Add(new(member, reason, cost));
        }
        endedReader.Complete();
        var cancelledBytes = reader.Field(EncounterTimingSnapshot.MaximumActors * EntityId.ByteLength);
        if (cancelledBytes.Length % EntityId.ByteLength != 0) throw new FormatException("Incomplete cancelled decision.");
        List<EntityId> cancelled = [];
        for (int offset = 0; offset < cancelledBytes.Length; offset += EntityId.ByteLength)
            cancelled.Add(EntityId.FromBytes(cancelledBytes.Slice(offset, EntityId.ByteLength)));
        reader.Complete();
        return new(sequence, ended, cancelled);
    }
}
