using System.Buffers.Binary;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Canonical explicit climb request; authority supplies capability, support and earned-effort checks.</summary>
public static class WorldActionCodecV9
{
    public const int MaximumCommandByteLength = 512;
    private const int MaximumIntentByteLength = 256;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Intent is not ClimbIntent intent)
        {
            throw new ArgumentException("This action version encodes assisted climbing only.", nameof(command));
        }
        return Frame("world.actor-action", Number(9), Id(command.CommandId), Id(command.PrincipalId),
            Id(command.OriginSessionId), Id(command.ActorId), Id(command.WorldId), Id(command.BranchId),
            Number(command.ExpectedActorRevision), Frame("world.action.climb", Number(1),
                Signed(intent.DeltaX), Signed(intent.DeltaY), Signed(intent.DeltaZ)));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 9)
            {
                throw new FormatException("Unsupported climb action version.");
            }
            EntityId command = reader.Identity();
            EntityId principal = reader.Identity();
            EntityId session = reader.Identity();
            EntityId actor = reader.Identity();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong actorRevision = reader.Number();
            var action = new WorldFrameReader(reader.Field(MaximumIntentByteLength), MaximumIntentByteLength, 5);
            action.Tag("world.action.climb"u8);
            action.Version();
            var intent = new ClimbIntent(action.Signed(), action.Signed(), action.Signed());
            action.Complete();
            reader.Complete();
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, actorRevision, intent);
            if (!Encode(result).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The climb action is not canonical.");
            }
            return result;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid climb action.", error);
        }
    }

    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}
