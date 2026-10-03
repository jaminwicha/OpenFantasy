using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

public static class WorldControlPermissionCodecV1
{
    public const int MaximumStateByteLength = 16 * 1024;
    internal const int MaximumTermsByteLength = 512;
    private const int MaximumSlotByteLength = 768;

    public static byte[] Encode(WorldControlPermissionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return CanonicalEncodingV1.Frame("world.control-permissions.v1", WorldControlPermissionState.RulesPin.ToByteArray(),
            CanonicalEncodingV1.Frame("world.control-permission-slots.v1", state.Slots.Select(slot => (ReadOnlyMemory<byte>)
                CanonicalEncodingV1.Frame("world.control-permission-slot.v1", slot.OwnerActorId.ToByteArray(), slot.GranteeActorId.ToByteArray(),
                    Number(slot.Revision), Number(slot.ChangedSequence), slot.Terms is null ? [] : EncodeTerms(slot.Terms),
                    Number(slot.OfferedSequence), slot.AcceptanceCommandId?.ToByteArray() ?? [], Number(slot.AcceptedSequence))).ToArray()));
    }

    public static WorldControlPermissionState Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumStateByteLength, 3);
            reader.Tag("world.control-permissions.v1"u8);
            if (!reader.Exact(32).SequenceEqual(WorldControlPermissionState.RulesPin.ToByteArray()))
                throw new FormatException("Unknown control permission policy.");
            var rows = new WorldFrameReader(reader.Field(MaximumStateByteLength), MaximumStateByteLength, WorldControlPermissionState.MaximumSlots + 1);
            rows.Tag("world.control-permission-slots.v1"u8);
            List<WorldControlPermissionSlot> slots = [];
            while (rows.Remaining != 0)
            {
                var row = new WorldFrameReader(rows.Field(MaximumSlotByteLength), MaximumSlotByteLength, 9);
                row.Tag("world.control-permission-slot.v1"u8);
                var owner = row.Identity(); var grantee = row.Identity();
                ulong revision = row.Number(), changed = row.Number();
                var termsBytes = row.Field(MaximumTermsByteLength);
                var terms = termsBytes.IsEmpty ? null : DecodeTerms(termsBytes);
                ulong offered = row.Number();
                var acceptBytes = row.Field(16);
                EntityId? accept = acceptBytes.IsEmpty ? null : EntityId.FromBytes(acceptBytes);
                ulong accepted = row.Number();
                row.Complete();
                slots.Add(new(owner, grantee, revision, changed, terms, offered, accept, accepted));
            }
            rows.Complete(); reader.Complete();
            var result = new WorldControlPermissionState(slots);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("Noncanonical control permission state.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid control permission state.", error);
        }
    }

    public static byte[] EncodeTerms(WorldControlPermissionTerms terms) => CanonicalEncodingV1.Frame("world.control-permission-terms.v1",
        terms.WorldId.ToByteArray(), terms.BranchId.ToByteArray(), terms.OwnerActorId.ToByteArray(),
        terms.OwnerPrincipalId.ToByteArray(), terms.OwnerSessionId.ToByteArray(), terms.GranteeActorId.ToByteArray(),
        terms.GranteePrincipalId.ToByteArray(), terms.GranteeSessionId.ToByteArray(), terms.GrantCommandId.ToByteArray(), Number((ulong)terms.Capabilities));

    private static WorldControlPermissionTerms DecodeTerms(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumTermsByteLength, 11);
        reader.Tag("world.control-permission-terms.v1"u8);
        var terms = new WorldControlPermissionTerms(reader.Identity(), reader.Identity(), reader.Identity(), reader.Identity(),
            reader.Identity(), reader.Identity(), reader.Identity(), reader.Identity(), reader.Identity(), (WorldControlCapabilities)reader.Number());
        reader.Complete();
        return terms;
    }
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
