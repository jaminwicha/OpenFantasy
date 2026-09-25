using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ItemConsumptionPlannerTests
{
    [Fact]
    public void RawPrefixIsRetainedAsAdministeredMaterialWithoutAnInventedOutput()
    {
        var basis = Enabled();
        var plan = Consume(basis.Inventory, basis.Production);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        var claim = Assert.Single(plan.CandidateProduction.ConsumedItems);
        Assert.Equal(EntityId.DeriveSpawned(Id(8000), 0), claim.ConsumptionId);
        Assert.Equal(Id(10), claim.ActorId);
        Assert.Equal(Id(11), claim.RecipientActorId);
        Assert.Equal((0UL, 2UL), (claim.Ingredient.Source!.Start, claim.Ingredient.Source.End));
        var remainder = plan.CandidateInventory.Custody.GetLot(Id(70));
        Assert.Equal((2UL, 6UL, 1UL), (remainder.Source.Start, remainder.Source.End, remainder.Revision));
        Assert.Equal(1UL, plan.CandidateInventory.Custody.Revision);
        Assert.Equal(1UL, plan.CandidateInventory.Custody.GetStorage(Id(60)).Revision);
        Assert.Equal(0UL, plan.CandidateInventory.Custody.GetStorage(Id(61)).Revision);
        Assert.Equal(1UL, plan.CandidateProduction.Revision);
        Assert.Empty(plan.CandidateProduction.Lots);
        Assert.Empty(plan.CandidateProduction.Batches);
        Assert.Equal(6UL, basis.Inventory.Custody.GetLot(Id(70)).Quantity);
        Assert.Empty(basis.Production.ConsumedItems);
        plan.CandidateProduction.ValidateForAuthority(plan.CandidateInventory);
    }

    [Fact]
    public void ProducedPrefixRemainsAccountedAndCannotBeRetiredAgainAfterRoundTrip()
    {
        var basis = Enabled();
        var crafted = Craft(basis.Inventory, basis.Production);
        var ration = crafted.CandidateProduction.Lots.Single(lot => lot.Definition == Rations);
        var request = Selection(crafted.CandidateInventory, crafted.CandidateProduction,
            new(IngredientLotKind.Transformed, ration.LotId, ration.Revision, 1), Rations);
        var plan = Consume(crafted.CandidateInventory, crafted.CandidateProduction, request);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Assert.Equal((0UL, 1UL), (plan.Claim!.Ingredient.Output!.Start, plan.Claim.Ingredient.Output.End));
        var remainder = plan.CandidateProduction.Lots.Single(lot => lot.LotId == ration.LotId);
        Assert.Equal((1UL, 2UL, 1UL), (remainder.Output.Start, remainder.Output.End, remainder.Revision));
        byte[] bytes = ProductionCodecV2.Encode(plan.CandidateProduction);
        var restored = ProductionCodecV2.Decode(bytes);
        Assert.Equal(bytes, ProductionCodecV2.Encode(restored));
        restored.ValidateForAuthority(plan.CandidateInventory);
        var stale = Selection(plan.CandidateInventory, restored, request.Ingredient, Rations);
        AssertRejected(plan.CandidateInventory, restored, Consume(plan.CandidateInventory, restored, stale, 8001),
            WorldActionRejection.Unavailable);
        Assert.Throws<ArgumentException>(() => ProductionCodecV1.Encode(restored));
    }

    [Fact]
    public void AuthenticationPrecedesInspectionOfPrivateSelections()
    {
        var basis = Enabled();
        var request = new ConsumeItemRequest(Id(999), ulong.MaxValue, ulong.MaxValue, Id(998), Rations,
            new(IngredientLotKind.Transformed, Id(997), ulong.MaxValue, ulong.MaxValue));
        var plan = new ItemConsumptionPlanner().Plan(basis.Inventory, basis.Production,
            new(Id(99), Id(98)), Id(10), Id(8000), request);
        AssertRejected(basis.Inventory, basis.Production, plan, WorldActionRejection.NotAuthorized);
    }

    [Theory]
    [InlineData("production")]
    [InlineData("storage")]
    [InlineData("lot")]
    [InlineData("definition")]
    [InlineData("version")]
    [InlineData("quantity")]
    [InlineData("recipient")]
    [InlineData("custodian")]
    public void InvalidSelectionLeavesBothOwnersUntouched(string change)
    {
        var basis = Enabled();
        var request = new ConsumeItemRequest(change == "custodian" ? Id(61) : Id(60),
            change == "storage" ? 1UL : 0, change == "production" ? 1UL : 0,
            change == "recipient" ? Id(999) : Id(11), change == "definition" ? Rations
                : change == "version" ? new(Berries.DefinitionId, ContentHash.Compute("different-meaning"u8)) : Berries,
            new(IngredientLotKind.Source, Id(70), change == "lot" ? 1UL : 0, change == "quantity" ? 7UL : 2));
        WorldActionRejection expected = change is "production" or "storage" ? WorldActionRejection.RevisionConflict
            : change == "custodian" ? WorldActionRejection.NotAuthorized : WorldActionRejection.Unavailable;
        AssertRejected(basis.Inventory, basis.Production, Consume(basis.Inventory, basis.Production, request), expected);
    }

    [Theory]
    [InlineData(LotProtection.Equipped)]
    [InlineData(LotProtection.Favorite)]
    [InlineData(LotProtection.Heirloom)]
    [InlineData(LotProtection.UserReserved)]
    public void ProtectedMaterialCannotBeConsumed(LotProtection protection)
    {
        var basis = Enabled(protection);
        AssertRejected(basis.Inventory, basis.Production, Consume(basis.Inventory, basis.Production),
            WorldActionRejection.Unavailable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AReservedDoseRemainsUnavailableForBothProvenanceFamilies(bool produced)
    {
        var basis = Enabled();
        var inventory = basis.Inventory;
        var production = basis.Production;
        if (produced)
        {
            var craft = Craft(inventory, production);
            inventory = craft.CandidateInventory;
            production = craft.CandidateProduction;
        }
        EntityId selected = produced ? production.Lots.Single(l => l.Definition == Rations).LotId : Id(70);
        var custody = new CustodySnapshot(inventory.WorldId, inventory.BranchId, inventory.Custody.Revision,
            inventory.Custody.Storages, inventory.Custody.Lots.Select(l => l.LotId == selected
                ? new CountedSourceLot(l.LotId, l.CustodianId, l.Definition, l.Source, l.Revision,
                    l.Protection, Id(9000)) : l));
        inventory = new CountedAuthorityState(custody, inventory.AdmittedUnitCapacity, inventory.Actors,
            inventory.Controls, inventory.Policies, inventory.Sources, [Id(9000)]);
        production = new ProductionSnapshot(production.WorldId, production.BranchId, production.Revision,
            production.Recipes, production.Batches, production.Lots.Select(l => l.LotId == selected
                ? new TransformedLot(l.LotId, l.CustodianId, l.Definition, l.Output, l.Revision,
                    l.Protection, Id(9000)) : l), []);
        var request = Selection(inventory, production, new(produced ? IngredientLotKind.Transformed : IngredientLotKind.Source,
            selected, 0, 1), produced ? Rations : Berries);
        AssertRejected(inventory, production, Consume(inventory, production, request), WorldActionRejection.Unavailable);
    }

    [Fact]
    public void CrossWorldMaterialCompositionIsRejectedBeforeAnyCandidateIsPrepared()
    {
        var basis = Enabled();
        var foreign = new ProductionSnapshot(Id(9010), Id(9011), 0, basis.Production.Recipes, [], [], []);
        Assert.Throws<ArgumentException>(() => Consume(basis.Inventory, foreign));
    }

    [Fact]
    public void OldOwnerMustExplicitlyOptIntoTheNewMaterialInterpretation()
    {
        var basis = Create();
        byte[] before = ProductionCodecV1.Encode(basis.Production);
        AssertRejected(basis.Inventory, basis.Production, Consume(basis.Inventory, basis.Production),
            WorldActionRejection.Unavailable);
        Assert.Equal(before, ProductionCodecV1.Encode(basis.Production));
    }

    [Fact]
    public void ExistingWorldProfilesCannotSilentlyAdmitTheNewOwner()
    {
        var world = OpenFantasy.Core.Tests.Worlds.ProductionWorldFixture.Create();
        var old = world.Production!;
        var enabled = new ProductionSnapshot(old.WorldId, old.BranchId, old.Revision,
            old.Recipes, old.Batches, old.Lots, []);
        enabled.ValidateForAuthority(world.Inventory);
        Assert.Throws<ArgumentException>(() => new WorldSnapshot(world.Inventory, world.Space, world.Residents,
            world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues,
            world.Gathering, enabled));
    }

    [Theory]
    [InlineData("production")]
    [InlineData("storage")]
    [InlineData("custody")]
    [InlineData("lot")]
    public void ExhaustedRevisionRefusesWithoutPartialConsumption(string exhausted)
    {
        var basis = Enabled();
        var old = basis.Inventory;
        var custody = new CustodySnapshot(old.WorldId, old.BranchId, exhausted == "custody" ? ulong.MaxValue : 0,
            old.Custody.Storages.Select(s => s.CustodianId == Id(60) && exhausted == "storage"
                ? s with { Revision = ulong.MaxValue } : s),
            old.Custody.Lots.Select(l => l.LotId == Id(70) && exhausted == "lot"
                ? new CountedSourceLot(l.LotId, l.CustodianId, l.Definition, l.Source, ulong.MaxValue) : l));
        var inventory = new CountedAuthorityState(custody, old.AdmittedUnitCapacity, old.Actors, old.Controls,
            old.Policies, old.Sources, old.ReservationIds);
        var production = new ProductionSnapshot(old.WorldId, old.BranchId,
            exhausted == "production" ? ulong.MaxValue : 0, basis.Production.Recipes, [], [], []);
        var request = Selection(inventory, production, new(IngredientLotKind.Source, Id(70),
            inventory.Custody.GetLot(Id(70)).Revision, 2), Berries);
        AssertRejected(inventory, production, Consume(inventory, production, request), WorldActionRejection.Unavailable);
    }

    [Fact]
    public void ReusingCommandOrItsCreatedIdentityNeverConsumesAnotherPrefix()
    {
        var basis = Enabled();
        var first = Consume(basis.Inventory, basis.Production);
        var inventory = first.CandidateInventory;
        var production = first.CandidateProduction;
        var request = Selection(inventory, production,
            new(IngredientLotKind.Source, Id(70), inventory.Custody.GetLot(Id(70)).Revision, 2), Berries);
        AssertRejected(inventory, production, Consume(inventory, production, request), WorldActionRejection.Unavailable);
        var alias = new ItemConsumptionPlanner().Plan(inventory, production, Delivery, Id(10),
            first.Claim!.ConsumptionId, request);
        AssertRejected(inventory, production, alias, WorldActionRejection.Unavailable);
    }

    private static (CountedAuthorityState Inventory, ProductionSnapshot Production) Enabled(LotProtection protection = LotProtection.None)
    {
        var basis = Create(protection: protection);
        return (basis.Inventory, new ProductionSnapshot(basis.Production.WorldId, basis.Production.BranchId,
            0, basis.Production.Recipes, [], [], []));
    }

    private static ConsumeItemRequest Selection(CountedAuthorityState inventory, ProductionSnapshot production,
        IngredientRequest ingredient, CountedItemDefinition definition) => new(Id(60),
            inventory.Custody.GetStorage(Id(60)).Revision, production.Revision, Id(11), definition, ingredient);

    private static ItemConsumptionPlan Consume(CountedAuthorityState inventory, ProductionSnapshot production,
        ConsumeItemRequest? request = null, ulong command = 8000) => new ItemConsumptionPlanner().Plan(inventory,
            production, Delivery, Id(10), Id(command), request ?? Selection(inventory, production,
                new(IngredientLotKind.Source, Id(70), 0, 2), Berries));

    private static void AssertRejected(CountedAuthorityState inventory, ProductionSnapshot production,
        ItemConsumptionPlan plan, WorldActionRejection reason)
    {
        Assert.Equal(reason, plan.Rejection);
        Assert.Same(inventory, plan.CandidateInventory);
        Assert.Same(production, plan.CandidateProduction);
        Assert.Null(plan.Claim);
        Assert.Empty(plan.CreatedIds);
    }
}
