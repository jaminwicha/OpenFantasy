using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ProductionLedgerTests
{
    [Fact]
    public void AProductBudgetCannotBeDuplicatedDiscardedOrRelabeled()
    {
        var initial = Create();
        CraftPlan result = Craft(initial.Inventory, initial.Production);
        ProductionSnapshot valid = result.CandidateProduction;
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(valid.WorldId, valid.BranchId, valid.Revision,
            valid.Recipes, valid.Batches, valid.Lots.Where(lot => lot.Definition != Offcuts)));
        TransformedLot ration = valid.Lots.Single(value => value.Definition == Rations);
        var duplicate = new TransformedLot(Id(8000), ration.CustodianId, ration.Definition, ration.Output, 0);
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(valid.WorldId, valid.BranchId, valid.Revision,
            valid.Recipes, valid.Batches, [.. valid.Lots, duplicate]));
        var wrong = new TransformedLot(ration.LotId, ration.CustodianId, Wood, ration.Output, 0);
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(valid.WorldId, valid.BranchId, valid.Revision,
            valid.Recipes, valid.Batches, valid.Lots.Select(lot => lot.LotId == ration.LotId ? wrong : lot)));
    }

    [Fact]
    public void CyclicDerivedIngredientGraphsCannotBootstrapFreeOutputs()
    {
        CraftRecipeDefinition recipe = Repack(Recipe());
        EntityId first = EntityId.DeriveSpawned(Id(6000), 0);
        EntityId second = EntityId.DeriveSpawned(Id(6001), 0);
        ProductionBatch[] batches =
        [
            new(first, Id(6000), Id(10), Id(60), recipe.DefinitionId, recipe.Version,
                [new RetiredIngredient(Id(8000), 0, Rations, new ProductionOutputInterval(second, 0, 0, 1))]),
            new(second, Id(6001), Id(10), Id(60), recipe.DefinitionId, recipe.Version,
                [new RetiredIngredient(Id(8001), 0, Rations, new ProductionOutputInterval(first, 0, 0, 1))])
        ];
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(Id(1), Id(2), 2, [recipe], batches, []));
    }

    [Fact]
    public void RawRightsCannotBeRetiredTwiceAcrossDifferentBatches()
    {
        var initial = Create();
        CraftPlan first = Craft(initial.Inventory, initial.Production);
        CraftPlan second = Craft(first.CandidateInventory, first.CandidateProduction, 6001);
        ProductionSnapshot valid = second.CandidateProduction;
        ProductionBatch original = valid.Batches.Single(batch => batch.CommandId == Id(6001));
        RetiredIngredient[] repeated = original.Ingredients.Select(value => value.Definition == Berries
            ? new RetiredIngredient(value.LotId, value.Revision, value.Definition, new SourceInterval(Id(71), 0, 2)) : value).ToArray();
        var forged = new ProductionBatch(original.BatchId, original.CommandId, original.ActorId, original.CustodianId,
            original.RecipeId, original.RecipeVersion, repeated);
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(valid.WorldId, valid.BranchId, valid.Revision,
            valid.Recipes, valid.Batches.Select(batch => batch.BatchId == forged.BatchId ? forged : batch), valid.Lots));
    }

    [Fact]
    public void PhysicalAllocationValidatesBothOutputCompositionAndInputConservation()
    {
        CraftRecipeDefinition original = Recipe();
        OutputMaterialAllocation[] wrongRow =
        [new(Berries, 0, new(19000)), new(Wood, 0, new(21000)), new(Wood, 1, new(10000))];
        Assert.Throws<ArgumentException>(() => new CraftRecipeDefinition(original.DefinitionId,
            original.PhysicalRecipe, original.Bindings, wrongRow));
        Assert.Throws<ArgumentException>(() => new CraftRecipeDefinition(original.DefinitionId,
            original.PhysicalRecipe, original.Bindings, original.Allocations.Take(2)));
        Assert.Throws<ArgumentException>(() => new CraftRecipeDefinition(original.DefinitionId,
            original.PhysicalRecipe, original.Bindings, [.. original.Allocations, original.Allocations[0]]));
        Assert.Throws<ArgumentException>(() => new CraftRecipeDefinition(original.DefinitionId,
            original.PhysicalRecipe, original.Bindings.Take(3), original.Allocations));
    }

    [Fact]
    public void InstantaneousProfileDoesNotInterpretOldAbstractWorkAsTicks()
    {
        CraftRecipeDefinition recipe = Recipe();
        MaterialRecipeDefinition original = recipe.PhysicalRecipe;
        var work120 = new MaterialRecipeDefinition(original.Package, original.DefinitionId, original.Key, new(120),
            original.SourceRecipePayloadHash, new MaterialCatalog(original.Materials), original.Terms);
        Assert.Throws<ArgumentException>(() => new CraftRecipeDefinition(recipe.DefinitionId, work120,
            recipe.Bindings, recipe.Allocations));
    }

    [Fact]
    public void DefinitionAndSnapshotCollectionsAreImmutableAndCanonical()
    {
        CraftRecipeDefinition original = Recipe();
        ItemPhysicalBinding[] bindings = original.Bindings.Reverse().ToArray();
        OutputMaterialAllocation[] allocations = original.Allocations.Reverse().ToArray();
        var copied = new CraftRecipeDefinition(original.DefinitionId, original.PhysicalRecipe, bindings, allocations);
        bindings[0] = original.Bindings[0];
        allocations[0] = original.Allocations[0];
        Assert.Equal(original.Version, copied.Version);
        var initial = Create(chained: true);
        CraftPlan result = Craft(initial.Inventory, initial.Production);
        var reordered = new ProductionSnapshot(Id(1), Id(2), 1, result.CandidateProduction.Recipes.Reverse(),
            result.CandidateProduction.Batches.Reverse(), result.CandidateProduction.Lots.Reverse());
        Assert.Equal(result.CandidateProduction.ContentHash, reordered.ContentHash);
    }

    [Fact]
    public void DecoderRoundtripsActualRawAndChainedIngredientHistory()
    {
        var initial = Create(chained: true);
        CraftPlan first = Craft(initial.Inventory, initial.Production);
        TransformedLot ration = first.CandidateProduction.Lots.Single(lot => lot.Definition == Rations);
        CraftPlan second = Craft(first.CandidateInventory, first.CandidateProduction, 6001,
            Request(first.CandidateInventory, first.CandidateProduction,
                [new(IngredientLotKind.Transformed, ration.LotId, 0, 1)], Repack(Recipe())));
        byte[] bytes = ProductionCodecV1.Encode(second.CandidateProduction);
        ProductionSnapshot decoded = ProductionCodecV1.Decode(bytes);
        decoded.ValidateForAuthority(second.CandidateInventory);
        Assert.Equal(bytes, ProductionCodecV1.Encode(decoded));
        Assert.Equal(2, decoded.Batches.Count);
        Assert.Contains(decoded.Batches.SelectMany(batch => batch.Ingredients), value => value.Output is not null);
        Assert.Equal(ProductionCodecV1.EncodeRecipe(Recipe()),
            ProductionCodecV1.EncodeRecipe(ProductionCodecV1.DecodeRecipe(ProductionCodecV1.EncodeRecipe(Recipe()))));
    }

    [Fact]
    public void DecoderRejectsEveryTruncationTrailingBytesAndOversizedFrames()
    {
        var initial = Create();
        byte[] bytes = ProductionCodecV1.Encode(Craft(initial.Inventory, initial.Production).CandidateProduction);
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] truncated = bytes[..length];
            Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode(truncated));
        }
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode([.. bytes, 0]));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode(new byte[ProductionCodecV1.MaximumSnapshotByteLength + 1]));
        byte[] badVersion = (byte[])bytes.Clone();
        badVersion[7] = 2;
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode(badVersion));
        byte[] badCount = (byte[])bytes.Clone();
        badCount[8] = 255;
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode(badCount));
    }

    [Fact]
    public void SelectionsAndLedgersEnforceFiniteBoundsBeforeUnboundedEnumeration()
    {
        CraftRecipeDefinition recipe = Recipe();
        int visited = 0;
        IEnumerable<IngredientRequest> Requests()
        {
            for (ulong index = 0; index < 10000; index++)
            {
                visited++;
                yield return new IngredientRequest(IngredientLotKind.Source, Id(10000 + index), 0, 1);
            }
        }
        Assert.Throws<ArgumentException>(() => new CraftRequest(recipe.DefinitionId, recipe.Version, 0, Id(60), 0, Requests()));
        Assert.Equal(65, visited);
        Assert.Throws<ArgumentException>(() => new CraftRequest(recipe.DefinitionId, recipe.Version, 0, Id(60), 0,
            [new(IngredientLotKind.Source, Id(70), 0, 1), new(IngredientLotKind.Transformed, Id(70), 0, 1)]));
        Assert.Throws<ArgumentException>(() => new IngredientRequest((IngredientLotKind)99, Id(70), 0, 1));
        Assert.Throws<ArgumentException>(() => new ProductionOutputInterval(Id(1), 64, 0, 1));
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(Id(1), Id(1), 0, [recipe], [], []));
    }

    [Fact]
    public void ItemAndPhysicalDefinitionsCannotAliasOrUseUnverifiedPins()
    {
        CraftRecipeDefinition recipe = Recipe();
        Assert.Throws<ArgumentException>(() => new ItemPhysicalBinding(recipe.Bindings[0].Physical.Pin(), recipe.Bindings[0].Physical));
        Assert.Throws<ArgumentException>(() => new CraftRecipeDefinition(Berries.DefinitionId,
            recipe.PhysicalRecipe, recipe.Bindings, recipe.Allocations));
        var initial = Create();
        CountedItemDefinition changed = new(Berries.DefinitionId, ContentHash.Compute("wrong-berries-pin"u8));
        CraftRecipeDefinition conflicting = Recipe(berries: changed);
        var state = new ProductionSnapshot(Id(1), Id(2), 0, [conflicting], [], []);
        Assert.Throws<ArgumentException>(() => state.ValidateForAuthority(initial.Inventory));
    }
}
