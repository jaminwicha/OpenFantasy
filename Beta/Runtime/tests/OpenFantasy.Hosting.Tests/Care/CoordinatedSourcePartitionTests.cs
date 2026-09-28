using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class CoordinatedSourcePartitionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Complete_admission_refuses_missing_allocated_rights_before_and_after_a_cut(bool continued, bool removeLot)
    {
        var fixture = new OrdinaryCareArchiveFixture(materialCareStress: true);
        var session = new CareWorldSession(WorldCoordinatedAdmission.CreateFresh(fixture.Genesis));
        CareWorldFixture.Commit(session, fixture.Journey.Batch(session.ReadStateForHost(), WorldBatchKind.AdvancingStep));
        var world = session.ReadStateForHost();
        if (continued) world = WorldMaterialCareArchiveSegment.Seal(world).Continued;
        var original = world.Inventory.Custody.GetLot(fixture.Journey.PotionLot);
        var missing = new CountedSourceLot(original.LotId, original.CustodianId, original.Definition,
            new(original.Source.SourceId, original.Source.Start, original.Source.End - 1), original.Revision + 1);
        var lots = world.Inventory.Custody.Lots.Where(value => value.LotId != original.LotId);
        if (!removeLot) lots = lots.Append(missing);
        var inventory = ReplaceLots(world, original, lots);

        // Standalone authority/Production accept this locally valid stock. The complete issuance
        // owner must independently reject its unaccounted disappearance.
        world.Production!.ValidateForAuthority(inventory);
        var error = Assert.Throws<ArgumentException>(() => ReconcileWorld(world, inventory));
        Assert.Contains("exactly equal the admitted issued rights", error.Message);
        Assert.Equal(600UL, original.Quantity);
    }

    [Fact]
    public void Archived_direct_retirement_cannot_be_reissued_as_live_raw_stock()
    {
        var fixture = new OrdinaryCareArchiveFixture(materialCareStress: true);
        var session = new CareWorldSession(WorldCoordinatedAdmission.CreateFresh(fixture.Genesis));
        var state = session.ReadStateForHost();
        var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var term = Assert.Single(recipe.Inputs);
        var bag = state.GetResident(fixture.Journey.Helper).InventoryCustodianId;
        var original = state.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == recipe.ItemFor(term));
        var request = new CraftRequest(recipe.DefinitionId, recipe.Version, state.Production.Revision, bag,
            state.Inventory.Custody.GetStorage(bag).Revision,
            [new(IngredientLotKind.Source, original.LotId, original.Revision, term.Count)]);
        var command = fixture.Journey.Command(state, fixture.Journey.Helper, new CraftIntent(request));
        var crafted = CareWorldFixture.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep, [command])).Candidate;
        var segment = WorldMaterialCareArchiveSegment.Seal(crafted);
        var continued = segment.Continued;
        Assert.Empty(continued.Production!.Batches);
        Assert.NotEmpty(segment.Anchor.Production.Basis.RawRetirements);
        var remainder = continued.Inventory.Custody.GetLot(original.LotId);
        Assert.True(remainder.Source.Start > original.Source.Start);
        var resurrected = new CountedSourceLot(original.LotId, original.CustodianId, original.Definition,
            original.Source, remainder.Revision + 1);
        var inventory = ReplaceLots(continued, remainder,
            continued.Inventory.Custody.Lots.Where(value => value.LotId != original.LotId).Append(resurrected));

        var materialError = Assert.Throws<ArgumentException>(() => continued.Production.ValidateForAuthority(inventory));
        Assert.Contains("Raw source units cannot remain live and retired", materialError.Message);
        var error = Assert.Throws<ArgumentException>(() => ReconcileWorld(continued, inventory));
        Assert.Contains("Raw source units cannot remain live and retired", error.Message);
        var bytes = WorldSnapshotCodec.Encode(continued);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.DecodeCoordinated(bytes, segment.Anchor)));
    }

    private static CountedAuthorityState ReplaceLots(WorldSnapshot world, CountedSourceLot changed,
        IEnumerable<CountedSourceLot> lots) => new(new(world.WorldId, world.BranchId,
            world.Inventory.Custody.Revision + 1,
            world.Inventory.Custody.Storages.Select(value => value.CustodianId == changed.CustodianId
                ? new CustodianStorage(value.CustodianId, value.UnitCapacity, value.Revision + 1) : value), lots),
            world.Inventory.AdmittedUnitCapacity, world.Inventory.Actors, world.Inventory.Controls,
            world.Inventory.Policies, world.Inventory.Sources, world.Inventory.ReservationIds);

    private static WorldSnapshot ReconcileWorld(WorldSnapshot world, CountedAuthorityState inventory)
    {
        var prior = world.Reservoirs!;
        var reservoirs = prior.PlanApply(prior.Capacity.PlanReconcile(inventory, world.Production)).ApplyTo(prior);
        return new(inventory, world.Space, world.Residents, world.AdmissionVersion, world.Calendar,
            world.Revision + 1, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production,
            world.Progression, world.Quests, world.Construction, reservoirs: reservoirs,
            reservoirSeals: world.ReservoirSeals, liquidCells: world.LiquidCells!.Rebind(reservoirs),
            excavation: world.Excavation, pacing: world.Pacing, encounterLifecycle: world.EncounterLifecycle!.RetainBoundary(),
            care: world.Care, resourceHistory: world.ResourceHistory, coordination: world.Coordination);
    }
}
