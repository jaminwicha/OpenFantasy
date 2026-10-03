using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

public static class WorldActionCodecV7
{
    public const int MaximumCommandByteLength = 2048;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        if (command.Intent is not ScheduleRunoffTransportIntent intent)
        {
            throw new ArgumentException("This action version encodes reservoir transport schedules only.", nameof(command));
        }
        return CanonicalEncodingV1.Frame("world.actor-action", CanonicalEncodingV1.Unsigned64(7), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), CanonicalEncodingV1.Unsigned64(command.ExpectedActorRevision),
            CanonicalEncodingV1.Frame("world.action.schedule-runoff", CanonicalEncodingV1.Unsigned64(1), intent.ExpectedTransportHash.ToByteArray(),
                CanonicalEncodingV1.Unsigned64(intent.ExpectedTransportRevision), intent.ReservationId.ToByteArray(),
                WatershedRunoffCodecV1.EncodeInstant(intent.DueAt), ReservoirTransportCodecV1.EncodeRoutes(intent.Routes)));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 7)
            {
                throw new FormatException("Unsupported transport scheduling action version.");
            }
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong actorRevision = reader.Number();
            var action = new WorldFrameReader(reader.Field(MaximumCommandByteLength), MaximumCommandByteLength, 7);
            action.Tag("world.action.schedule-runoff"u8);
            action.Version();
            var hash = new ContentHash(Convert.ToHexString(action.Exact(32)).ToLowerInvariant());
            ulong revision = action.Number();
            EntityId reservation = action.Identity();
            WorldInstant dueAt = WatershedRunoffCodecV1.ReadInstant(action.Exact(12));
            var routes = ReservoirTransportCodecV1.DecodeRoutes(action.Field(PreparedWatershedNetwork.MaximumBasins * 32));
            action.Complete();
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, actorRevision,
                new ScheduleRunoffTransportIntent(hash, revision, reservation, dueAt, routes));
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The transport schedule command is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid transport schedule command.", error);
        }
    }
}
