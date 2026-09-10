using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>Craft commands name exact recipe, storage and ingredient revisions without supplying outputs.</summary>
public static class WorldActionCodecV3
{
    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Intent is not CraftIntent craft)
        {
            throw new ArgumentException("This action version encodes crafting only.", nameof(command));
        }
        CraftRequest request = craft.Request;
        byte[] bytes = CanonicalEncodingV1.Frame("world.actor-action", Number(3), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision),
            CanonicalEncodingV1.Frame("world.action.craft", Number(1), request.RecipeId.ToByteArray(),
                request.RecipeVersion.ToByteArray(), Number(request.ExpectedProductionRevision), request.CustodianId.ToByteArray(),
                Number(request.ExpectedStorageRevision), CanonicalEncodingV1.Frame("world.craft.ingredients",
                    request.Ingredients.Select(ingredient => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("world.craft.ingredient",
                        Number((ulong)ingredient.Kind), ingredient.LotId.ToByteArray(), Number(ingredient.ExpectedRevision),
                        Number(ingredient.Quantity))).ToArray())));
        if (bytes.Length > WorldActionCodec.MaximumCommandByteLength)
        {
            throw new ArgumentException("The craft action exceeds the admitted command bound.", nameof(command));
        }
        return bytes;
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldActionCodec.MaximumCommandByteLength, 10);
        reader.Tag("world.actor-action"u8);
        if (reader.Number() != 3)
        {
            throw new FormatException("Unsupported craft action version.");
        }
        EntityId command = reader.Identity();
        EntityId principal = reader.Identity();
        EntityId session = reader.Identity();
        EntityId actor = reader.Identity();
        EntityId world = reader.Identity();
        EntityId branch = reader.Identity();
        ulong actorRevision = reader.Number();
        var intent = new WorldFrameReader(reader.Field(WorldActionCodec.MaximumCommandByteLength), WorldActionCodec.MaximumCommandByteLength, 8);
        intent.Tag("world.action.craft"u8);
        intent.Version();
        EntityId recipe = intent.Identity();
        ContentHash version = new(Convert.ToHexString(intent.Exact(32)).ToLowerInvariant());
        ulong productionRevision = intent.Number();
        EntityId custodian = intent.Identity();
        ulong storageRevision = intent.Number();
        var ingredients = new WorldFrameReader(intent.Field(WorldActionCodec.MaximumCommandByteLength),
            WorldActionCodec.MaximumCommandByteLength, MaterialRecipeDefinition.MaximumTermCount + 1);
        ingredients.Tag("world.craft.ingredients"u8);
        List<IngredientRequest> selections = [];
        try
        {
            while (ingredients.Remaining != 0)
            {
                var selection = new WorldFrameReader(ingredients.Field(256), 256, 5);
                selection.Tag("world.craft.ingredient"u8);
                ulong kind = selection.Number();
                if (kind is not 1 and not 2)
                {
                    throw new FormatException("Unsupported ingredient provenance family.");
                }
                selections.Add(new IngredientRequest((IngredientLotKind)kind, selection.Identity(), selection.Number(), selection.Number()));
                selection.Complete();
            }
            ingredients.Complete();
            intent.Complete();
            reader.Complete();
            var request = new CraftRequest(recipe, version, productionRevision, custodian, storageRevision, selections);
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, actorRevision, new CraftIntent(request));
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Craft selections are not canonically encoded.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Craft action violates its invariants.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
