using OpenFantasy.Core.Gathering;

namespace OpenFantasy.Core.Worlds;

/// <summary>Gathering requests retain exact target/tool revisions without trusting client-selected yields or time.</summary>
public static class WorldActionCodecV2
{
    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        if (command.Intent is not GatherIntent gather)
        {
            throw new ArgumentException("This action version encodes gathering only.", nameof(command));
        }
        return CanonicalEncodingV1.Frame("world.actor-action", Number(2), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision),
            CanonicalEncodingV1.Frame("world.action.gather", Number(1), gather.NodeId.ToByteArray(),
                Number(gather.ExpectedNodeRevision), gather.ToolLotId.ToByteArray(), Number(gather.ExpectedToolLotRevision)));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldActionCodec.MaximumCommandByteLength, 10);
        reader.Tag("world.actor-action"u8);
        if (reader.Number() != 2)
        {
            throw new FormatException("Unsupported gathering action version.");
        }
        EntityId command = reader.Identity();
        EntityId principal = reader.Identity();
        EntityId session = reader.Identity();
        EntityId actor = reader.Identity();
        EntityId world = reader.Identity();
        EntityId branch = reader.Identity();
        ulong revision = reader.Number();
        var intent = new WorldFrameReader(reader.Field(512), 512, 6);
        intent.Tag("world.action.gather"u8);
        intent.Version();
        var gather = new GatherIntent(intent.Identity(), intent.Number(), intent.Identity(), intent.Number());
        intent.Complete();
        reader.Complete();
        try
        {
            return new WorldActionCommand(command, principal, session, actor, world, branch, revision, gather);
        }
        catch (ArgumentException error)
        {
            throw new FormatException("Gathering action violates its invariants.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
