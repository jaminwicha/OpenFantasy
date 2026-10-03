using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>Canonical bounded component bytes. Decoding proves structure, never world membership or authority.</summary>
public static class EncounterWithdrawalCodecV1
{
    public const int MaximumEncodedByteLength = 200 * 1024;
    private const int MaximumMemberByteLength = EncounterRetreatState.MaximumCommandByteLength + 512;

    public static byte[] Encode(EncounterWithdrawalParty party)
    {
        ArgumentNullException.ThrowIfNull(party);
        byte[] required = party.RequiredActors.SelectMany(actor => actor.ToByteArray()).ToArray();
        byte[] members = CanonicalEncodingV1.Frame("encounter.withdrawal-members.v1",
            party.Members.Select(member => (ReadOnlyMemory<byte>)EncodeMember(member)).ToArray());
        byte[] bytes = CanonicalEncodingV1.Frame("encounter.withdrawal-party.v1", CanonicalEncodingV1.Unsigned64(1),
            EncounterWithdrawalParty.RulesPin.ToByteArray(), party.Scope.WorldId.ToByteArray(),
            party.Scope.BranchId.ToByteArray(), party.Scope.EncounterId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(party.Side), CanonicalEncodingV1.Unsigned64(party.Revision), required, members);
        if (bytes.Length > MaximumEncodedByteLength) throw new ArgumentException("Withdrawal component exceeds its byte bound.");
        return bytes;
    }

    public static EncounterWithdrawalParty Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 10);
            reader.Tag("encounter.withdrawal-party.v1"u8);
            reader.Version();
            if (!reader.Exact(32).SequenceEqual(EncounterWithdrawalParty.RulesPin.ToByteArray()))
                throw new FormatException("Unknown withdrawal component rules.");
            var scope = new EncounterTimingScope(reader.Identity(), reader.Identity(), reader.Identity());
            ushort side = checked((ushort)reader.Number());
            ulong revision = reader.Number();
            ReadOnlySpan<byte> requiredBytes = reader.Field(EncounterWithdrawalParty.MaximumMembers * EntityId.ByteLength);
            if (requiredBytes.Length % EntityId.ByteLength != 0) throw new FormatException("Incomplete withdrawal member identity.");
            List<EntityId> required = [];
            for (int offset = 0; offset < requiredBytes.Length; offset += EntityId.ByteLength)
                required.Add(EntityId.FromBytes(requiredBytes.Slice(offset, EntityId.ByteLength)));
            ReadOnlySpan<byte> memberBytes = reader.Field(MaximumEncodedByteLength);
            reader.Complete();
            var membersReader = new WorldFrameReader(memberBytes, MaximumEncodedByteLength, EncounterWithdrawalParty.MaximumMembers + 1);
            membersReader.Tag("encounter.withdrawal-members.v1"u8);
            List<EncounterWithdrawalMember> members = [];
            while (membersReader.Remaining > 0) members.Add(DecodeMember(membersReader.Field(MaximumMemberByteLength)));
            membersReader.Complete();
            var party = new EncounterWithdrawalParty(scope, side, revision, required, members);
            if (!Encode(party).AsSpan().SequenceEqual(bytes)) throw new FormatException("Withdrawal bytes are not canonical.");
            return party;
        }
        catch (Exception error) when (error is ArgumentException or InvalidCastException or OverflowException)
        {
            throw new FormatException("Withdrawal component violates its invariants.", error);
        }
    }

    private static byte[] EncodeMember(EncounterWithdrawalMember member) => CanonicalEncodingV1.Frame(
        "encounter.withdrawal-member.v1", WorldActionCodec.Encode(member.Command), EncounterRetreatCodecV1.Position(member.Origin),
        CanonicalEncodingV1.Unsigned64(member.NormalizedSpeed), CanonicalEncodingV1.Unsigned64((ulong)member.SamplesWalked));

    private static EncounterWithdrawalMember DecodeMember(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumMemberByteLength, 5);
        reader.Tag("encounter.withdrawal-member.v1"u8);
        var command = WorldActionCodec.Decode(reader.Field(EncounterRetreatState.MaximumCommandByteLength));
        var origin = EncounterRetreatCodecV1.ReadPosition(reader.Field(EncounterRetreatCodecV1.PositionByteLength));
        uint speed = checked((uint)reader.Number());
        int samples = checked((int)reader.Number());
        reader.Complete();
        return new(command, origin, speed, samples);
    }
}
