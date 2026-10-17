using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit withdrawal cancellation; ordinary and delegated envelopes retain their original identities.</summary>
public static class WorldActionCodecV24
{
    public static byte[] Encode(WorldActionCommand command)
    {
        if (command.ControlWitness is not null || command.Intent is not CancelEncounterWithdrawalIntent intent)
            throw new ArgumentException("This version requires an ordinary withdrawal cancellation.");
        return CanonicalEncodingV1.Frame("world.actor-action", CanonicalEncodingV1.Unsigned64(24),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(),
            command.ActorId.ToByteArray(), command.WorldId.ToByteArray(), command.BranchId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(command.ExpectedActorRevision), CanonicalEncodingV1.Frame("world.action.cancel-withdrawal.v1",
                intent.EncounterId.ToByteArray(), CanonicalEncodingV1.Unsigned64(intent.ExpectedTimingRevision),
                intent.WithdrawalCommandId.ToByteArray()));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, 512, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 24) throw new FormatException("Unsupported withdrawal cancellation version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var intent = new WorldFrameReader(reader.Field(192), 192, 4);
            intent.Tag("world.action.cancel-withdrawal.v1"u8);
            var cancellation = new CancelEncounterWithdrawalIntent(intent.Identity(), intent.Number(), intent.Identity());
            intent.Complete();
            reader.Complete();
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, revision, cancellation);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("Noncanonical withdrawal cancellation.");
            return result;
        }
        catch (ArgumentException error) { throw new FormatException("Invalid withdrawal cancellation.", error); }
    }
}
