using OpenFantasy.Core.Construction;

namespace OpenFantasy.Core.Worlds;

/// <summary>One present actor's qualified work request; no supplied clock, duration or work amount.</summary>
public static class WorldActionCodecV5
{
    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Intent is not WorkConstructionIntent intent)
        {
            throw new ArgumentException("This action version encodes construction work only.", nameof(command));
        }
        return Frame("world.actor-action", Number(5), Id(command.CommandId), Id(command.PrincipalId),
            Id(command.OriginSessionId), Id(command.ActorId), Id(command.WorldId), Id(command.BranchId),
            Number(command.ExpectedActorRevision), Frame("world.action.construction", Number(1),
                Id(intent.ProjectId), Number(intent.ExpectedProjectRevision), Number(intent.ExpectedProjectStorageRevision),
                Id(intent.ToolLotId), Number(intent.ExpectedToolLotRevision)));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldActionCodec.MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 5)
            {
                throw new FormatException("Unsupported construction action version.");
            }
            EntityId command = reader.Identity();
            EntityId principal = reader.Identity();
            EntityId session = reader.Identity();
            EntityId actor = reader.Identity();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong actorRevision = reader.Number();
            var action = new WorldFrameReader(reader.Field(256), 256, 7);
            action.Tag("world.action.construction"u8);
            action.Version();
            var intent = new WorkConstructionIntent(action.Identity(), action.Number(), action.Number(), action.Identity(), action.Number());
            action.Complete();
            reader.Complete();
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, actorRevision, intent);
            if (!Encode(result).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The construction action is not canonical.");
            }
            return result;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid construction action.", error);
        }
    }

    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}
