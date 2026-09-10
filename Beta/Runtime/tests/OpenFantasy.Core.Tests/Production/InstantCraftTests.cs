using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class InstantCraftTests
{
    [Fact]
    public void CraftRetiresExactPrefixesAndProducesDistinctProductAndWasteClaims()
    {
        var initial = Create();
        CraftPlan result = Craft(initial.Inventory, initial.Production);
        Assert.Equal(WorldActionRejection.None, result.Rejection);
        Assert.Equal(initial.Inventory.Sources, result.CandidateInventory.Sources);
        Assert.Equal(10UL, result.CandidateInventory.AdmittedUnitCapacity);
        Assert.Equal(7UL, result.CandidateInventory.Custody.GetStoredQuantity(Id(60)));
        Assert.Equal(3UL, result.CandidateProduction.GetStoredQuantity(Id(60)));
        Assert.Equal(10UL, result.CandidateProduction.GetCombinedStoredQuantity(result.CandidateInventory, Id(60)));
        Assert.Equal(new SourceInterval(Id(71), 2, 6), result.CandidateInventory.Custody.GetLot(Id(70)).Source);
        Assert.Equal(new SourceInterval(Id(73), 1, 4), result.CandidateInventory.Custody.GetLot(Id(72)).Source);
        Assert.Equal(1UL, result.CandidateInventory.Custody.GetLot(Id(70)).Revision);
        ProductionBatch batch = Assert.Single(result.CandidateProduction.Batches);
        Assert.Equal(EntityId.DeriveSpawned(Id(6000), 0), batch.BatchId);
        Assert.Equal(2UL, batch.Ingredients.Single(value => value.Definition == Berries).Quantity);
        Assert.Equal(1UL, batch.Ingredients.Single(value => value.Definition == Wood).Quantity);
        Assert.Equal(2UL, result.CandidateProduction.Lots.Single(value => value.Definition == Rations).Quantity);
        Assert.Equal(1UL, result.CandidateProduction.Lots.Single(value => value.Definition == Offcuts).Quantity);
        Assert.Equal(3, result.CreatedIds.Count);
        Assert.Equal(1UL, result.CandidateProduction.Revision);
        Assert.Equal(1UL, result.CandidateInventory.Custody.Revision);
        Assert.Equal(1UL, result.CandidateInventory.Custody.GetStorage(Id(60)).Revision);
        Assert.Equal(0UL, result.CandidateInventory.Custody.GetStorage(Id(61)).Revision);
        Assert.Empty(initial.Production.Batches);
        Assert.Equal(6UL, initial.Inventory.Custody.GetLot(Id(70)).Quantity);
        result.CandidateProduction.ValidateForAuthority(result.CandidateInventory);
    }

    [Fact]
    public void EveryOutputHasExplicitCompositionWithoutDuplicatingWholeInputRights()
    {
        CraftRecipeDefinition recipe = Recipe();
        Assert.Equal(90000UL, recipe.PhysicalRecipe.InputMass.Value);
        Assert.Equal(80000UL, recipe.PhysicalRecipe.OutputMass.Value);
        Assert.Equal(10000UL, recipe.PhysicalRecipe.WasteMass.Value);
        Assert.Equal(1UL, recipe.PhysicalRecipe.RequiredWork.Value);
        Assert.Equal(20000UL, recipe.Allocations.Single(value => value.InputItem == Berries && value.OutputIndex == 0).MassPerOutputUnit.Value);
        Assert.Equal(20000UL, recipe.Allocations.Single(value => value.InputItem == Wood && value.OutputIndex == 0).MassPerOutputUnit.Value);
        Assert.Equal(10000UL, recipe.Allocations.Single(value => value.InputItem == Wood && value.OutputIndex == 1).MassPerOutputUnit.Value);
        Assert.DoesNotContain(recipe.Allocations, value => value.InputItem == Berries && value.OutputIndex == 1);
    }

    [Fact]
    public void ChainingConsumesDerivedIntervalsAndRetainsTheRawRetirementOnlyOnce()
    {
        var initial = Create(chained: true);
        CraftPlan first = Craft(initial.Inventory, initial.Production);
        TransformedLot ration = first.CandidateProduction.Lots.Single(value => value.Definition == Rations);
        CraftRecipeDefinition repack = first.CandidateProduction.GetRecipe(Id(5204));
        CraftRequest request = Request(first.CandidateInventory, first.CandidateProduction,
            [new(IngredientLotKind.Transformed, ration.LotId, 0, 1)], repack);
        CraftPlan second = Craft(first.CandidateInventory, first.CandidateProduction, 6001, request);
        Assert.Equal(WorldActionRejection.None, second.Rejection);
        Assert.Equal(2, second.CandidateProduction.Batches.Count);
        Assert.Equal(2, second.CandidateProduction.Batches.SelectMany(value => value.Ingredients)
            .Count(value => value.Kind == IngredientLotKind.Source));
        RetiredIngredient derived = Assert.Single(second.CandidateProduction.Batches.SelectMany(value => value.Ingredients),
            value => value.Kind == IngredientLotKind.Transformed);
        Assert.Equal(new ProductionOutputInterval(ration.Output.BatchId, 0, 0, 1), derived.Output);
        TransformedLot remainder = second.CandidateProduction.Lots.Single(value => value.LotId == ration.LotId);
        Assert.Equal(new ProductionOutputInterval(ration.Output.BatchId, 0, 1, 2), remainder.Output);
        Assert.Equal(1UL, remainder.Revision);
        second.CandidateProduction.ValidateForAuthority(second.CandidateInventory);
        Assert.Equal(second.CandidateProduction.ContentHash,
            ProductionCodecV1.Decode(ProductionCodecV1.Encode(second.CandidateProduction)).ContentHash);
    }

    [Fact]
    public void RepeatedCraftingCannotConsumeRetiredSourceRightsAgain()
    {
        var initial = Create();
        CraftPlan first = Craft(initial.Inventory, initial.Production);
        CraftPlan second = Craft(first.CandidateInventory, first.CandidateProduction, 6001);
        CraftPlan third = Craft(second.CandidateInventory, second.CandidateProduction, 6002);
        Assert.Equal(WorldActionRejection.None, third.Rejection);
        Assert.DoesNotContain(third.CandidateInventory.Custody.Lots, lot => lot.LotId == Id(70));
        var unavailable = new CraftRequest(Recipe().DefinitionId, Recipe().Version, third.CandidateProduction.Revision,
            Id(60), third.CandidateInventory.Custody.GetStorage(Id(60)).Revision,
            [new(IngredientLotKind.Source, Id(70), 3, 2), new(IngredientLotKind.Source, Id(72), 3, 1)]);
        CraftPlan rejected = Craft(third.CandidateInventory, third.CandidateProduction, 6003, unavailable);
        Assert.Equal(WorldActionRejection.Unavailable, rejected.Rejection);
        Assert.Same(third.CandidateInventory, rejected.CandidateInventory);
        Assert.Same(third.CandidateProduction, rejected.CandidateProduction);
    }

    [Fact]
    public void InputSelectionOrderDoesNotChangeCanonicalCandidates()
    {
        var initial = Create();
        CraftRequest ordinary = Request(initial.Inventory, initial.Production);
        CraftRequest reversed = Request(initial.Inventory, initial.Production, ordinary.Ingredients.Reverse());
        CraftPlan first = Craft(initial.Inventory, initial.Production, request: ordinary);
        CraftPlan second = Craft(initial.Inventory, initial.Production, request: reversed);
        Assert.Equal(first.CandidateInventory.ContentHash, second.CandidateInventory.ContentHash);
        Assert.Equal(first.CandidateProduction.ContentHash, second.CandidateProduction.ContentHash);
    }

    [Fact]
    public void AuthenticationAndCustodianControlPrecedePrivateRevisionOrLotInspection()
    {
        var initial = Create();
        CraftRequest bogus = new(Id(999), ContentHash.Compute("wrong"u8), ulong.MaxValue, Id(999), ulong.MaxValue,
            [new(IngredientLotKind.Source, Id(999), ulong.MaxValue, ulong.MaxValue)]);
        CraftPlan result = new InstantCraftPlanner().Plan(initial.Inventory, initial.Production,
            new TrustedCommandDelivery(Id(900), Id(901)), Id(10), Id(6000), bogus);
        Assert.Equal(WorldActionRejection.NotAuthorized, result.Rejection);
        CraftPlan foreign = new InstantCraftPlanner().Plan(initial.Inventory, initial.Production,
            new TrustedCommandDelivery(Id(21), Id(31)), Id(11), Id(6000), Request(initial.Inventory, initial.Production));
        Assert.Equal(WorldActionRejection.NotAuthorized, foreign.Rejection);
    }

    [Fact]
    public void StaleWrongKindMissingAndExcessIngredientSelectionsRejectAtomically()
    {
        var initial = Create();
        IngredientRequest[][] alternatives =
        [
            [new(IngredientLotKind.Source, Id(70), 1, 2), new(IngredientLotKind.Source, Id(72), 0, 1)],
            [new(IngredientLotKind.Transformed, Id(70), 0, 2), new(IngredientLotKind.Source, Id(72), 0, 1)],
            [new(IngredientLotKind.Source, Id(999), 0, 2), new(IngredientLotKind.Source, Id(72), 0, 1)],
            [new(IngredientLotKind.Source, Id(70), 0, 7), new(IngredientLotKind.Source, Id(72), 0, 1)],
            [new(IngredientLotKind.Source, Id(70), 0, 1), new(IngredientLotKind.Source, Id(72), 0, 1)]
        ];
        foreach (IngredientRequest[] inputs in alternatives)
        {
            CraftPlan result = Craft(initial.Inventory, initial.Production, request: Request(initial.Inventory, initial.Production, inputs));
            Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
            Assert.Same(initial.Inventory, result.CandidateInventory);
            Assert.Same(initial.Production, result.CandidateProduction);
            Assert.Empty(result.CreatedIds);
        }
    }

    [Fact]
    public void ProtectedIngredientsAreNeverConsumedImplicitly()
    {
        foreach (LotProtection protection in new[] { LotProtection.Equipped, LotProtection.Favorite,
            LotProtection.Heirloom, LotProtection.UserReserved })
        {
            var initial = Create(protection: protection);
            Assert.Equal(WorldActionRejection.Unavailable, Craft(initial.Inventory, initial.Production).Rejection);
        }
    }

    [Fact]
    public void SourceAdmissionCannotBeExpandedOrRelabeledToPretendThatAProductIsRaw()
    {
        var initial = Create();
        CraftPlan result = Craft(initial.Inventory, initial.Production);
        TransformedLot product = result.CandidateProduction.Lots.Single(value => value.Definition == Rations);
        var raw = new CountedSourceLot(product.LotId, product.CustodianId, product.Definition,
            new SourceInterval(Id(71), 0, 2), 0);
        Assert.Throws<ArgumentException>(() => new CountedAuthorityState(new CustodySnapshot(Id(1), Id(2), 1,
            initial.Inventory.Custody.Storages, [raw]), 10, initial.Inventory.Actors, initial.Inventory.Controls,
            initial.Inventory.Policies, initial.Inventory.Sources));
        Assert.Throws<ArgumentException>(() => result.CandidateProduction.ValidateForAuthority(initial.Inventory));
    }

    [Fact]
    public void CombinedCapacityCountsWasteAndDerivedProductsAlongsideRawRemainders()
    {
        var initial = Create(capacity: 10);
        CraftPlan result = Craft(initial.Inventory, initial.Production);
        Assert.Equal(WorldActionRejection.None, result.Rejection);
        CountedAuthorityState old = result.CandidateInventory;
        var smaller = new CustodySnapshot(Id(1), Id(2), old.Custody.Revision,
            [new(Id(60), 9, 1), new(Id(61), 10, 0)], old.Custody.Lots);
        var inventory = new CountedAuthorityState(smaller, old.AdmittedUnitCapacity, old.Actors, old.Controls, old.Policies, old.Sources);
        Assert.Throws<ArgumentException>(() => result.CandidateProduction.ValidateForAuthority(inventory));
    }

    [Fact]
    public void OverflowRejectsCandidatesWithoutMutatingAnyIngredient()
    {
        var initial = Create();
        var terminal = new ProductionSnapshot(Id(1), Id(2), ulong.MaxValue, initial.Production.Recipes, [], []);
        CraftPlan result = Craft(initial.Inventory, terminal);
        Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
        Assert.Same(initial.Inventory, result.CandidateInventory);
        Assert.Same(terminal, result.CandidateProduction);
    }

    [Fact]
    public void ACurrentIdentityCollisionCannotSpendIngredients()
    {
        var initial = Create();
        EntityId occupied = EntityId.DeriveSpawned(Id(6000), 1);
        CountedAuthorityState old = initial.Inventory;
        var inventory = new CountedAuthorityState(old.Custody, old.AdmittedUnitCapacity,
            [.. old.Actors, occupied], old.Controls, old.Policies, old.Sources);
        CraftPlan result = Craft(inventory, initial.Production);
        Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
        Assert.Same(inventory, result.CandidateInventory);
        Assert.Same(initial.Production, result.CandidateProduction);
    }
}
