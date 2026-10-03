using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>One present actor's excavation request pinned to its site, terrain, tool and destination revisions.</summary>
public static class WorldActionCodecV8
{
    public const int MaximumCommandByteLength = 512;
    private const int MaximumIntentByteLength = 256;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        if (command.Intent is not ExcavateIntent intent)
        {
            throw new ArgumentException("This action version encodes excavation only.", nameof(command));
        }
        return Frame("world.actor-action", Number(8), Id(command.CommandId), Id(command.PrincipalId),
            Id(command.OriginSessionId), Id(command.ActorId), Id(command.WorldId), Id(command.BranchId),
            Number(command.ExpectedActorRevision), Frame("world.action.excavate", Number(1),
                Id(intent.SiteId), Number(intent.ExpectedSiteRevision), Number(intent.ExpectedTerrainRevision),
                Id(intent.ToolLotId), Number(intent.ExpectedToolLotRevision), Number(intent.ExpectedDestinationRevision)));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 8)
            {
                throw new FormatException("Unsupported excavation action version.");
            }
            EntityId command = reader.Identity();
            EntityId principal = reader.Identity();
            EntityId session = reader.Identity();
            EntityId actor = reader.Identity();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong actorRevision = reader.Number();
            var action = new WorldFrameReader(reader.Field(MaximumIntentByteLength), MaximumIntentByteLength, 8);
            action.Tag("world.action.excavate"u8);
            action.Version();
            var intent = new ExcavateIntent(action.Identity(), action.Number(), action.Number(),
                action.Identity(), action.Number(), action.Number());
            action.Complete();
            reader.Complete();
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, actorRevision, intent);
            if (!Encode(result).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The excavation action is not canonical.");
            }
            return result;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid excavation action.", error);
        }
    }

    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}
