using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit care requests retain captured authority revisions and exact ingredient selection.</summary>
public static class WorldActionCodecV13
{
    public const int MaximumCommandByteLength = 1024;
    private const int MaximumIntentByteLength = 512;
    private const int MaximumIngredientByteLength = 128;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        byte[] intent = command.Intent switch
        {
            AcknowledgeCareIntent acknowledge => CanonicalEncodingV1.Frame("world.action.acknowledge-care",
                Number(1), acknowledge.AgreementId.ToByteArray(), acknowledge.EncounterId.ToByteArray(),
                Number(acknowledge.ExpectedLifecycleRevision), acknowledge.ExpectedActionHash.ToByteArray()),
            AdministerRemedyIntent administer => CanonicalEncodingV1.Frame("world.action.administer-remedy",
                Number(1), administer.AcknowledgmentId.ToByteArray(), Number(administer.ExpectedCareRevision),
                Number(administer.ExpectedPatientPresenceRevision), Number(administer.ExpectedPatientProgressionRevision),
                Number(administer.ExpectedStorageRevision), Number(administer.ExpectedProductionRevision),
                EncodeIngredient(administer.Ingredient)),
            _ => throw new ArgumentException("This command version requires a care intent.", nameof(command))
        };
        byte[] bytes = CanonicalEncodingV1.Frame("world.actor-action", Number(13), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision), intent);
        if (bytes.Length > MaximumCommandByteLength)
        {
            throw new ArgumentException("The care command exceeds its finite byte bound.", nameof(command));
        }
        return bytes;
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 13)
            {
                throw new FormatException("Unsupported care command version.");
            }
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var intentReader = new WorldFrameReader(reader.Field(MaximumIntentByteLength), MaximumIntentByteLength, 9);
            ReadOnlySpan<byte> tag = intentReader.Field(64);
            intentReader.Version();
            WorldActionIntent intent;
            if (tag.SequenceEqual("world.action.acknowledge-care"u8))
            {
                intent = new AcknowledgeCareIntent(intentReader.Identity(), intentReader.Identity(), intentReader.Number(),
                    new ContentHash(Convert.ToHexString(intentReader.Exact(32)).ToLowerInvariant()));
            }
            else if (tag.SequenceEqual("world.action.administer-remedy"u8))
            {
                EntityId acknowledgment = intentReader.Identity();
                ulong care = intentReader.Number(), presence = intentReader.Number(), progression = intentReader.Number();
                ulong storage = intentReader.Number(), production = intentReader.Number();
                intent = new AdministerRemedyIntent(acknowledgment, care, presence, progression, storage, production,
                    DecodeIngredient(intentReader.Field(MaximumIngredientByteLength)));
            }
            else
            {
                throw new FormatException("Unsupported care intent.");
            }
            intentReader.Complete();
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The care command is not canonical.");
            }
            return restored;
        }
        catch (ArgumentException error)
        {
            throw new FormatException("The care command violates its invariants.", error);
        }
    }

    private static byte[] EncodeIngredient(IngredientRequest ingredient) =>
        CanonicalEncodingV1.Frame("world.care.ingredient", Number((ulong)ingredient.Kind), ingredient.LotId.ToByteArray(),
            Number(ingredient.ExpectedRevision), Number(ingredient.Quantity));

    private static IngredientRequest DecodeIngredient(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumIngredientByteLength, 5);
        reader.Tag("world.care.ingredient"u8);
        ulong kind = reader.Number();
        if (kind is not 1 and not 2)
        {
            throw new FormatException("Unsupported care ingredient provenance family.");
        }
        var ingredient = new IngredientRequest((IngredientLotKind)kind, reader.Identity(), reader.Number(), reader.Number());
        reader.Complete();
        return ingredient;
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
