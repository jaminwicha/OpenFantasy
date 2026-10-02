using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Canonical outcome acknowledgment (profile 35 only) in the ordinary command envelope. The intent frame carries the
/// exact episode and the final action archive hash the controller read.
/// </summary>
public static class WorldActionCodecV20
{
    public const int MaximumCommandByteLength = 512;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Intent is not AcknowledgeEncounterOutcomeIntent intent)
        {
            throw new ArgumentException("This action version retains an outcome acknowledgment.", nameof(command));
        }
        ArgumentNullException.ThrowIfNull(intent.ExpectedActionHash);
        return CanonicalEncodingV1.Frame("world.actor-action", CanonicalEncodingV1.Unsigned64(20),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(),
            command.ActorId.ToByteArray(), command.WorldId.ToByteArray(), command.BranchId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(command.ExpectedActorRevision),
            CanonicalEncodingV1.Frame("world.action.acknowledge-outcome", CanonicalEncodingV1.Unsigned64(1),
                intent.EncounterId.ToByteArray(), intent.ExpectedActionHash.ToByteArray()));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 20) throw new FormatException("Unsupported outcome acknowledgment version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var intentReader = new WorldFrameReader(reader.Field(160), 160, 4);
            intentReader.Tag("world.action.acknowledge-outcome"u8);
            intentReader.Version();
            EntityId encounter = intentReader.Identity();
            var hash = new ContentHash(Convert.ToHexString(intentReader.Exact(32)).ToLowerInvariant());
            intentReader.Complete();
            reader.Complete();
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, revision,
                new AcknowledgeEncounterOutcomeIntent(encounter, hash));
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("The outcome acknowledgment is not canonical.");
            return result;
        }
        catch (ArgumentException error)
        {
            throw new FormatException("Invalid outcome acknowledgment.", error);
        }
    }
}
