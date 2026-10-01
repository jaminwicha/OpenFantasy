using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Canonical committed-retreat request (profile 34 only) in the ordinary command envelope. The intent frame carries
/// the encounter, expected timing revision, captured destination and the packed cardinal route, one byte per sample.
/// </summary>
public static class WorldActionCodecV19
{
    public const int MaximumCommandByteLength = 640;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Intent is not QueueEncounterRetreatIntent intent)
        {
            throw new ArgumentException("This action version retains a committed retreat request.", nameof(command));
        }
        byte[] route = intent.Route.Select(direction => (byte)direction).ToArray();
        return CanonicalEncodingV1.Frame("world.actor-action", CanonicalEncodingV1.Unsigned64(19),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(),
            command.ActorId.ToByteArray(), command.WorldId.ToByteArray(), command.BranchId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(command.ExpectedActorRevision),
            CanonicalEncodingV1.Frame("world.action.queue-retreat", CanonicalEncodingV1.Unsigned64(1),
                intent.EncounterId.ToByteArray(), CanonicalEncodingV1.Unsigned64(intent.ExpectedTimingRevision),
                EncounterRetreatCodecV1.Position(intent.Destination), route));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 19) throw new FormatException("Unsupported retreat action version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var intentReader = new WorldFrameReader(reader.Field(320), 320, 6);
            intentReader.Tag("world.action.queue-retreat"u8);
            intentReader.Version();
            EntityId encounter = intentReader.Identity();
            ulong timing = intentReader.Number();
            var destination = EncounterRetreatCodecV1.ReadPosition(intentReader.Field(EncounterRetreatCodecV1.PositionByteLength));
            ReadOnlySpan<byte> route = intentReader.Field(EncounterRetreatRules.MaximumRouteSamples);
            intentReader.Complete();
            reader.Complete();
            var directions = new EncounterRetreatDirection[route.Length];
            for (int index = 0; index < route.Length; index++)
            {
                directions[index] = (EncounterRetreatDirection)route[index];
            }
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, revision,
                new QueueEncounterRetreatIntent(encounter, timing, destination, directions));
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("The retreat request is not canonical.");
            return result;
        }
        catch (ArgumentException error)
        {
            throw new FormatException("Invalid retreat request.", error);
        }
    }
}
