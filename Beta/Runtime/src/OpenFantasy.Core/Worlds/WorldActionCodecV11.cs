using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Canonical queued ability intent, distinct from a subsequently confirmed timing action.</summary>
public static class WorldActionCodecV11
{
    public const int MaximumCommandByteLength = 512;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        if (command.Intent is not QueueEncounterAbilityIntent intent)
        {
            throw new ArgumentException("This action version retains an encounter ability choice.", nameof(command));
        }
        return CanonicalEncodingV1.Frame("world.actor-action", CanonicalEncodingV1.Unsigned64(11),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(),
            command.ActorId.ToByteArray(), command.WorldId.ToByteArray(), command.BranchId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(command.ExpectedActorRevision),
            CanonicalEncodingV1.Frame("world.action.queue-ability", CanonicalEncodingV1.Unsigned64(1),
                intent.EncounterId.ToByteArray(), CanonicalEncodingV1.Unsigned64(intent.ExpectedTimingRevision),
                intent.AbilityDefinitionId.ToByteArray(), intent.TargetActorId.ToByteArray()));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 11) throw new FormatException("Unsupported queued ability action version.");
            EntityId command = reader.Identity();
            EntityId principal = reader.Identity();
            EntityId session = reader.Identity();
            EntityId actor = reader.Identity();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong revision = reader.Number();
            var intentReader = new WorldFrameReader(reader.Field(256), 256, 6);
            intentReader.Tag("world.action.queue-ability"u8);
            intentReader.Version();
            var intent = new QueueEncounterAbilityIntent(intentReader.Identity(), intentReader.Number(),
                intentReader.Identity(), intentReader.Identity());
            intentReader.Complete();
            reader.Complete();
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("The queued ability is not canonical.");
            return result;
        }
        catch (ArgumentException error)
        {
            throw new FormatException("Invalid queued ability action.", error);
        }
    }
}
