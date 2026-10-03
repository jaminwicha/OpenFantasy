using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>Exact ordinary-use and patient revocation requests, independent of transport authentication.</summary>
public static class WorldActionCodecV14
{
    public const int MaximumCommandByteLength = 2_048;
    private const int MaximumIntentByteLength = 1_024;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        byte[] intent = command.Intent switch
        {
            UseRestorativeItemIntent use => CanonicalEncodingV1.Frame("world.action.restorative-use", Number(1),
                use.RecipientActorId.ToByteArray(), use.UseDefinitionId.ToByteArray(), use.UseDefinitionVersion.ToByteArray(),
                EncodePermission(use), Number(use.ExpectedPatientPresenceRevision), Number(use.ExpectedPatientProgressionRevision),
                Number(use.ExpectedStorageRevision), Number(use.ExpectedProductionRevision), EncodeIngredient(use.Ingredient)),
            RevokeCarePermissionIntent revoke => CanonicalEncodingV1.Frame("world.action.revoke-care-permission", Number(1),
                revoke.PermissionId.ToByteArray(), Number(revoke.ExpectedPermissionRevision), revoke.ExpectedPermissionGrantVersion.ToByteArray()),
            _ => throw new ArgumentException("This command version requires ordinary care.", nameof(command))
        };
        byte[] bytes = CanonicalEncodingV1.Frame("world.actor-action", Number(14), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision), intent);
        if (bytes.Length > MaximumCommandByteLength) throw new ArgumentException("Ordinary care command exceeds its bounded frame.");
        return bytes;
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 14) throw new FormatException("Unsupported ordinary care command version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var payload = new WorldFrameReader(reader.Field(MaximumIntentByteLength), MaximumIntentByteLength, 11);
            ReadOnlySpan<byte> tag = payload.Field(64);
            payload.Version();
            WorldActionIntent intent;
            if (tag.SequenceEqual("world.action.restorative-use"u8))
            {
                EntityId recipient = payload.Identity(), definition = payload.Identity();
                ContentHash version = Hash(payload.Exact(32));
                var permission = DecodePermission(payload.Field(256));
                intent = new UseRestorativeItemIntent(recipient, definition, version, permission.Id, permission.Version,
                    permission.Revision, payload.Number(), payload.Number(), payload.Number(), payload.Number(),
                    DecodeIngredient(payload.Field(128)));
            }
            else if (tag.SequenceEqual("world.action.revoke-care-permission"u8))
                intent = new RevokeCarePermissionIntent(payload.Identity(), payload.Number(), Hash(payload.Exact(32)));
            else throw new FormatException("Unsupported ordinary care intent.");
            payload.Complete();
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes)) throw new FormatException("Ordinary care command is not canonical.");
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        {
            throw new FormatException("Ordinary care command violates its invariants.", error);
        }
    }

    private static byte[] EncodePermission(UseRestorativeItemIntent use) => use.PermissionId is { } id
        ? CanonicalEncodingV1.Frame("world.restorative.permission", id.ToByteArray(),
            use.PermissionGrantVersion!.ToByteArray(), Number(use.ExpectedPermissionRevision!.Value))
        : CanonicalEncodingV1.Frame("world.restorative.permission");

    private static (EntityId? Id, ContentHash? Version, ulong? Revision) DecodePermission(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 256, 4);
        reader.Tag("world.restorative.permission"u8);
        if (reader.Remaining == 0) return (null, null, null);
        var result = (reader.Identity(), Hash(reader.Exact(32)), reader.Number());
        reader.Complete();
        return result;
    }

    private static byte[] EncodeIngredient(IngredientRequest ingredient) => CanonicalEncodingV1.Frame("world.care.ingredient",
        Number((ulong)ingredient.Kind), ingredient.LotId.ToByteArray(), Number(ingredient.ExpectedRevision), Number(ingredient.Quantity));

    private static IngredientRequest DecodeIngredient(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 128, 5);
        reader.Tag("world.care.ingredient"u8);
        ulong kind = reader.Number();
        if (kind is not 1 and not 2) throw new FormatException("Unsupported dose provenance family.");
        var ingredient = new IngredientRequest((IngredientLotKind)kind, reader.Identity(), reader.Number(), reader.Number());
        reader.Complete();
        return ingredient;
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static ContentHash Hash(ReadOnlySpan<byte> value) => new(Convert.ToHexString(value).ToLowerInvariant());
}
