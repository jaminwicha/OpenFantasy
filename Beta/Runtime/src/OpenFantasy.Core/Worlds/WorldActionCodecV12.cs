using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit encounter entry and pinned outcome release in the ordinary command envelope.</summary>
public static class WorldActionCodecV12
{
    public const int MaximumCommandByteLength = 512;

    public static byte[] Encode(WorldActionCommand command)
    {
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        byte[] intent = command.Intent switch
        {
            EnterEncounterIntent enter => CanonicalEncodingV1.Frame("world.action.enter-encounter",
                CanonicalEncodingV1.Unsigned64(1), enter.ScenarioId.ToByteArray(), enter.EncounterId.ToByteArray(),
                CanonicalEncodingV1.Unsigned64(enter.ExpectedEncounterCount), CanonicalEncodingV1.Unsigned64(enter.ExpectedTargetRevision)),
            ReleaseEncounterIntent release => CanonicalEncodingV1.Frame("world.action.release-encounter",
                CanonicalEncodingV1.Unsigned64(1), release.EncounterId.ToByteArray(), release.ExpectedActionHash.ToByteArray()),
            _ => throw new ArgumentException("This command version requires an encounter lifecycle intent.")
        };
        return CanonicalEncodingV1.Frame("world.actor-action", CanonicalEncodingV1.Unsigned64(12),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(),
            command.ActorId.ToByteArray(), command.WorldId.ToByteArray(), command.BranchId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(command.ExpectedActorRevision), intent);
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 12) throw new FormatException("Unsupported lifecycle command version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var intentReader = new WorldFrameReader(reader.Field(256), 256, 6);
            ReadOnlySpan<byte> tag = intentReader.Field(64);
            intentReader.Version();
            WorldActionIntent intent;
            if (tag.SequenceEqual("world.action.enter-encounter"u8))
            {
                intent = new EnterEncounterIntent(intentReader.Identity(), intentReader.Identity(), intentReader.Number(), intentReader.Number());
            }
            else if (tag.SequenceEqual("world.action.release-encounter"u8))
            {
                intent = new ReleaseEncounterIntent(intentReader.Identity(),
                    new ContentHash(Convert.ToHexString(intentReader.Exact(32)).ToLowerInvariant()));
            }
            else throw new FormatException("Unsupported lifecycle intent.");
            intentReader.Complete();
            reader.Complete();
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("The lifecycle command is not canonical.");
            return result;
        }
        catch (ArgumentException error)
        {
            throw new FormatException("Invalid lifecycle command.", error);
        }
    }
}
