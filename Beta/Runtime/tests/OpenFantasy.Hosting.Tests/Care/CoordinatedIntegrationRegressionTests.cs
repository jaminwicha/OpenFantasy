using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class CoordinatedIntegrationRegressionTests
{
    [Theory]
    [InlineData(1UL)]
    [InlineData(10UL)]
    public void New_revocations_follow_the_world_closure_even_when_the_care_frontier_is_older(ulong claimedSequence)
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldCoordinatedAdmission.CreateFresh(fixture.Genesis));
        for (int index = 0; index < 10; index++)
        {
            var state = session.ReadStateForHost();
            CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep));
        }
        var segment = WorldMaterialCareArchiveSegment.Seal(session.ReadStateForHost());
        var continued = segment.Continued;
        Assert.Equal(10UL, segment.Anchor.ClosingWorldSequence);
        Assert.Equal(0UL, segment.Anchor.LatestCareSequence);
        var permission = Assert.Single(continued.Care!.Ordinary!.Permissions);
        CareSnapshot RevokedAt(ulong sequence)
        {
            EntityId command = new(0xc00d2700, 500);
            var revoke = new CarePermissionRevocation(EntityId.DeriveSpawned(command, 0), command,
                permission.PermissionId, permission.PatientActorId, sequence, 0);
            return continued.Care.WithOrdinary(continued.Care.Ordinary.Revoke(revoke));
        }
        var backdated = RevokedAt(claimedSequence); // The standalone Care frontier is intentionally less restrictive.
        var boundary = continued.EncounterLifecycle!.RetainBoundary();
        var error = Assert.Throws<ArgumentException>(() => Copy(continued, care: backdated, lifecycle: boundary, revision: 11));
        Assert.Contains("new revocation must follow", error.Message);
        var valid = Copy(continued, care: RevokedAt(11), lifecycle: boundary, revision: 11);
        Assert.Equal(WorldSnapshotCodec.Encode(valid),
            WorldSnapshotCodec.Encode(WorldSnapshotCodec.DecodeCoordinated(WorldSnapshotCodec.Encode(valid), segment.Anchor)));
    }

    [Fact]
    public void A_joint_context_preserves_the_encounter_ability_catalog()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldCoordinatedAdmission.CreateFresh(fixture.Genesis));
        CareRewardJourney.Commit(session, fixture.Journey.Batch(session.ReadStateForHost(), WorldBatchKind.AdvancingStep));
        var continued = WorldMaterialCareArchiveSegment.Seal(session.ReadStateForHost()).Continued;
        var prior = continued.EncounterLifecycle!;
        var original = prior.Abilities[0];
        var changed = new EncounterAbilityDefinition(original.DefinitionId, original.Key, original.DisplayName,
            original.SourceVersion, original.PowerTenths == 1 ? 2U : original.PowerTenths - 1,
            original.RangeSubunits, original.ReadinessCostNumerator, original.ReadinessCostDenominator,
            original.ManaCost, original.MinimumLevel, original.RequiredFunction);
        var substituted = new EncounterLifecycleSnapshot(prior.WorldId, prior.BranchId, prior.Seed, prior.Revision,
            prior.ExplorationSteps, prior.TacticalTicks, prior.EntryTacticalTicks, prior.EncounterCount, prior.Phase,
            prior.Abilities.Select(value => value.DefinitionId == changed.DefinitionId ? changed : value),
            prior.Scenarios, prior.Debts, prior.ScenarioId, prior.Actions);
        Assert.Equal(continued.ContentHash, Copy(continued, lifecycle: prior).ContentHash);
        Assert.Throws<ArgumentException>(() => Copy(continued, lifecycle: substituted));
    }

    [Fact]
    public void Actual_crafting_after_two_cuts_keeps_its_imported_producer_and_permanent_command_checks()
    {
        var fixture = new OrdinaryCareArchiveFixture(materialCareStress: true);
        var session = new CareWorldSession(WorldCoordinatedAdmission.CreateFresh(fixture.Genesis));
        CareWorldFixture.Commit(session, fixture.Journey.Batch(session.ReadStateForHost(), WorldBatchKind.AdvancingStep));
        var first = WorldMaterialCareArchiveSegment.Seal(session.ReadStateForHost());
        session = new(first.Continued, session.OccupiedIdentities);
        var one = Craft(session, fixture);
        Assert.Equal(1UL, one.Production!.TotalBatchCount);
        var producer = Assert.Single(one.Production.Batches).BatchId;
        var second = WorldMaterialCareArchiveSegment.Seal(one);
        Assert.True(second.Continued.IdentityIndex.ContainsEntity(producer));
        Assert.Equal(producer, Assert.Single(second.Anchor.Production.Basis.BatchWitnesses).BatchId);
        session = new(second.Continued, session.OccupiedIdentities);
        var two = Craft(session, fixture);
        Assert.Equal(2UL, two.Production!.TotalBatchCount);
        Assert.Equal(2, two.Production.Lots.Count);
        Assert.Equal(WorldSnapshotCodec.Encode(two), WorldSnapshotCodec.Encode(
            WorldSnapshotCodec.DecodeCoordinated(WorldSnapshotCodec.Encode(two), second.Anchor)));
    }

    [Fact]
    public void Pending_aftermath_care_survives_a_cut_and_the_actual_remedy_executes_once()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldCoordinatedAdmission.CreateFresh(fixture.Genesis));
        var defeated = fixture.Journey.PrepareDefeat(session);
        var first = WorldMaterialCareArchiveSegment.Seal(defeated);
        session = new(first.Continued, session.OccupiedIdentities);
        var pending = CareRewardJourney.Commit(session, fixture.Journey.Batch(session.ReadStateForHost(), WorldBatchKind.Care,
            [fixture.Journey.Acknowledge(session.ReadStateForHost())])).Candidate;
        Assert.NotNull(pending.Care!.Pending);
        var second = WorldMaterialCareArchiveSegment.Seal(pending);
        Assert.Equal(pending.Care.Pending, second.Continued.Care!.Pending);
        session = new(second.Continued, session.OccupiedIdentities);
        var command = fixture.Journey.Treat(session.ReadStateForHost());
        var batch = fixture.Journey.Batch(session.ReadStateForHost(), WorldBatchKind.Care, [command]);
        var result = CareRewardJourney.Commit(session, batch);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(result.Receipts).Status);
        Assert.Equal(40, result.Candidate.Progression!.GetActor(fixture.Player).CurrentHp);
        Assert.Null(result.Candidate.Care!.Pending);
        Assert.Equal(1UL, result.Candidate.Care.TotalTreatments);
        Assert.Throws<InvalidOperationException>(() => session.Commit(batch));
    }

    private static WorldSnapshot Craft(CareWorldSession session, OrdinaryCareArchiveFixture fixture)
    {
        var state = session.ReadStateForHost();
        var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var input = Assert.Single(recipe.Inputs);
        var bag = state.GetResident(fixture.Journey.Helper).InventoryCustodianId;
        var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == recipe.ItemFor(input));
        var command = fixture.Journey.Command(state, fixture.Journey.Helper, new CraftIntent(new(recipe.DefinitionId,
            recipe.Version, state.Production.Revision, bag, state.Inventory.Custody.GetStorage(bag).Revision,
            [new(IngredientLotKind.Source, lot.LotId, lot.Revision, input.Count)])));
        return CareWorldFixture.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep, [command])).Candidate;
    }

    private static WorldSnapshot Copy(WorldSnapshot state, CareSnapshot? care = null,
        EncounterLifecycleSnapshot? lifecycle = null, ulong? revision = null) => new(
        state.Inventory, state.Space, state.Residents, state.AdmissionVersion, state.Calendar,
        revision ?? state.Revision, state.AdvancingSteps, state.Dialogues, state.Gathering, state.Production,
        state.Progression, state.Quests, state.Construction, reservoirs: state.Reservoirs,
        reservoirSeals: state.ReservoirSeals, liquidCells: state.LiquidCells, excavation: state.Excavation,
        pacing: state.Pacing, encounterLifecycle: lifecycle ?? state.EncounterLifecycle, care: care ?? state.Care,
        resourceHistory: state.ResourceHistory, coordination: state.Coordination);
}
