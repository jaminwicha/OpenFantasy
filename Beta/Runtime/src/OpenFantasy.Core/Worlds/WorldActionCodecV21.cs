using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Exact pending request, episode and care revision within the ordinary authenticated action envelope.</summary>
public static class WorldActionCodecV21
{
    public const int MaximumCommandByteLength = 512;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        string tag = command.Intent switch
        {
            CancelPendingCareIntent => "world.action.cancel-care",
            DeclinePendingCareIntent => "world.action.decline-care",
            _ => throw new ArgumentException("This action version requires a pending-care disposition.", nameof(command))
        };
        var intent = (PendingCareDispositionIntent)command.Intent;
        return CanonicalEncodingV1.Frame("world.actor-action", CanonicalEncodingV1.Unsigned64(21),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(),
            command.ActorId.ToByteArray(), command.WorldId.ToByteArray(), command.BranchId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(command.ExpectedActorRevision),
            CanonicalEncodingV1.Frame(tag, CanonicalEncodingV1.Unsigned64(1), intent.AcknowledgmentId.ToByteArray(),
                intent.EncounterId.ToByteArray(), CanonicalEncodingV1.Unsigned64(intent.ExpectedCareRevision)));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 21) throw new FormatException("Unsupported pending-care disposition action version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var intentReader = new WorldFrameReader(reader.Field(160), 160, 5);
            ReadOnlySpan<byte> tag = intentReader.Field(64);
            intentReader.Version();
            EntityId acknowledgment = intentReader.Identity(), encounter = intentReader.Identity();
            ulong careRevision = intentReader.Number();
            PendingCareDispositionIntent intent = tag.SequenceEqual("world.action.cancel-care"u8)
                ? new CancelPendingCareIntent(acknowledgment, encounter, careRevision)
                : tag.SequenceEqual("world.action.decline-care"u8)
                    ? new DeclinePendingCareIntent(acknowledgment, encounter, careRevision)
                    : throw new FormatException("Unsupported pending-care disposition intent.");
            intentReader.Complete();
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes)) throw new FormatException("The care disposition action is not canonical.");
            return restored;
        }
        catch (ArgumentException error)
        {
            throw new FormatException("Invalid pending-care disposition action.", error);
        }
    }
}
