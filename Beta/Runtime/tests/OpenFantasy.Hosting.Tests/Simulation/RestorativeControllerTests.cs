using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class RestorativeControllerTests
{
    [Theory]
    [InlineData(20, 60, true)]
    [InlineData(21, 60, false)]
    [InlineData(1, 60, true)]
    [InlineData(0, 60, false)]
    [InlineData(60, 60, false)]
    public void Full_dose_policy_requires_living_recipient_and_the_whole_restoration_to_fit(int hp, int maximum, bool expected)
    {
        Assert.Equal("local.full-dose-assistance.v1", LocalRestorativeController.PolicyVersion);
        Assert.Equal(expected, LocalRestorativeController.FitsFullDose(hp, maximum, 40));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Real_source_injury_selects_helper_owned_raw_or_crafted_dose_and_commits_once(bool crafted)
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        var controller = new LocalRestorativeController();
        Assert.Empty(controller.SelectActions(fixture.Genesis, _ => false, new HashSet<EntityId>()));
        var injured = fixture.ReachFullDoseInjuredExploration(session);
        Assert.InRange(injured.Progression!.GetActor(fixture.Player).CurrentHp, 1, 20);
        Assert.Empty(controller.SelectActions(injured, session.OccupiedIdentities.Contains, new HashSet<EntityId>()));
        var before = SupplyHelper(fixture, session, crafted);
        var helperControl = before.Inventory.Controls.Single(value => value.ActorId == fixture.Helper);
        var selected = Assert.Single(controller.SelectActions(before, session.OccupiedIdentities.Contains, new HashSet<EntityId>()));
        var intent = Assert.IsType<UseRestorativeItemIntent>(selected.Intent);
        Assert.Equal(fixture.Helper, selected.ActorId);
        Assert.Equal(helperControl.PrincipalId, selected.PrincipalId);
        Assert.Equal(helperControl.SessionId, selected.OriginSessionId);
        Assert.Equal(ResidentControllerKind.Host, before.GetResident(selected.ActorId).ControllerKind);
        Assert.Equal(fixture.Player, intent.RecipientActorId);
        Assert.Equal(before.Care!.Ordinary!.Permissions.Single().PermissionId, intent.PermissionId);
        Assert.Equal(crafted ? IngredientLotKind.Transformed : IngredientLotKind.Source, intent.Ingredient.Kind);
        Assert.Equal(1UL, intent.Ingredient.Quantity);
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, fixture.Helper, intent));
        Assert.Equal(before.ContentHash, session.ReadStateForHost().ContentHash);
        var restored = WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(before));
        Assert.Equal(WorldActionCodec.Encode(selected), WorldActionCodec.Encode(Assert.Single(
            controller.SelectActions(restored, session.OccupiedIdentities.Contains, new HashSet<EntityId>()))));

        var after = CareRewardJourney.Commit(session,
            fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [selected])).Candidate;
        var use = Assert.Single(after.Care!.Ordinary!.Uses);
        Assert.Equal(selected.CommandId, use.CommandId);
        Assert.Equal(fixture.Helper, use.ActorId);
        Assert.Equal(fixture.Player, use.RecipientActorId);
        Assert.Equal(before.Progression!.GetActor(fixture.Player).CurrentHp + 40,
            after.Progression!.GetActor(fixture.Player).CurrentHp);
        Assert.Equivalent(before.Progression.GetActor(fixture.Helper), after.Progression.GetActor(fixture.Helper), strict: true);
        Assert.Equivalent(before.Progression.Currency, after.Progression.Currency, strict: true);
        Assert.Equal(before.Production!.ConsumedItems.Count + 1, after.Production!.ConsumedItems.Count);
        var consumption = after.Production.ConsumedItems.Single(value => value.ConsumptionId == use.ConsumptionId);
        Assert.Equal(intent.Ingredient.LotId, consumption.Ingredient.LotId);
        Assert.Equal(intent.Ingredient.Kind, consumption.Ingredient.Kind);
        Assert.Equal(before.Calendar.AddMilliseconds(1_800), after.Calendar);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Null(after.Care.Pending);
        Assert.Equal(before.Care.Acknowledgments.Count, after.Care.Acknowledgments.Count);
        Assert.Equal(before.Care.Treatments.Count, after.Care.Treatments.Count);
        Assert.Empty(controller.SelectActions(after, session.OccupiedIdentities.Contains, new HashSet<EntityId>()));
        WorldResourceJourneyTests.RoundTrip(after);
    }

    [Fact]
    public void Existing_commitments_and_actual_patient_revocation_suppress_otherwise_eligible_aid()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        fixture.ReachFullDoseInjuredExploration(session);
        var before = SupplyHelper(fixture, session, crafted: true);
        var controller = new LocalRestorativeController();
        var selected = Assert.Single(controller.SelectActions(before, session.OccupiedIdentities.Contains, new HashSet<EntityId>()));
        Assert.Empty(controller.SelectActions(before, session.OccupiedIdentities.Contains, new HashSet<EntityId> { fixture.Helper }));
        Assert.Empty(controller.SelectActions(before, session.OccupiedIdentities.Contains, new HashSet<EntityId> { fixture.Player }));
        Assert.Equal(before.ContentHash, session.ReadStateForHost().ContentHash);
        var permission = before.Care!.Ordinary!.Permissions.Single();
        var revoked = CareRewardJourney.Commit(session, fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(before, fixture.Player,
                new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion))])).Candidate;
        Assert.False(revoked.Care!.Ordinary!.Permissions.Single().IsActive);
        Assert.Equal(before.Progression!.ContentHash, revoked.Progression!.ContentHash);
        Assert.Equal(before.Production!.ContentHash, revoked.Production!.ContentHash);
        Assert.Equal(before.Inventory.ContentHash, revoked.Inventory.ContentHash);
        Assert.Empty(controller.SelectActions(revoked, session.OccupiedIdentities.Contains, new HashSet<EntityId>()));
        Assert.Equal(WorldActionRejection.RevisionConflict,
            WorldRestorativeActions.Evaluate(revoked, fixture.Helper, (UseRestorativeItemIntent)selected.Intent));
    }

    [Fact]
    public void Actual_stale_selection_gets_one_rejection_and_is_not_retried_after_movement_time_or_recovery()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        fixture.ReachFullDoseInjuredExploration(session);
        var before = SupplyHelper(fixture, session, crafted: true);
        var controller = new LocalRestorativeController();
        var selected = Assert.Single(controller.SelectActions(before, session.OccupiedIdentities.Contains, new HashSet<EntityId>()));
        MovePlayer(fixture, session, 32);
        var returned = MovePlayer(fixture, session, -32);
        Assert.Equal(before.Space.GetActor(fixture.Player).Feet, returned.Space.GetActor(fixture.Player).Feet);
        Assert.True(returned.Space.GetActor(fixture.Player).Revision > before.Space.GetActor(fixture.Player).Revision);
        var current = Assert.Single(controller.SelectActions(returned, session.OccupiedIdentities.Contains, new HashSet<EntityId>()));
        Assert.Equal(selected.CommandId, current.CommandId);
        Assert.NotEqual(selected.ContentHash, current.ContentHash);
        var failed = session.Commit(fixture.Journey.Batch(returned, WorldBatchKind.AdvancingStep, [selected]));
        Assert.Equal(CommandReceiptStatus.Rejected, Assert.Single(failed.Receipts).Status);
        Assert.Equal(WorldActionRejection.RevisionConflict, Assert.Single(failed.Receipts).Rejection);
        Assert.Contains(selected.CommandId, session.OccupiedIdentities);
        var after = failed.Candidate;
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Empty(after.Care!.Ordinary!.Uses);
        Assert.Empty(controller.SelectActions(after, session.OccupiedIdentities.Contains, new HashSet<EntityId>()));
        for (int tick = 0; tick < 3; tick++)
        {
            after = CareRewardJourney.Commit(session, fixture.Journey.Batch(after, WorldBatchKind.AdvancingStep)).Candidate;
            Assert.Empty(controller.SelectActions(after, session.OccupiedIdentities.Contains, new HashSet<EntityId>()));
        }
        var reopened = WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(after));
        var resumed = new CareWorldSession(reopened, session.OccupiedIdentities);
        Assert.Empty(new LocalRestorativeController().SelectActions(reopened, resumed.OccupiedIdentities.Contains,
            new HashSet<EntityId>()));
        Assert.Equal(before.Production.ContentHash, reopened.Production!.ContentHash);
    }

    [Fact]
    public void Permanent_command_and_derived_claim_collisions_withhold_the_exact_choice_without_new_identities()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        fixture.ReachFullDoseInjuredExploration(session);
        var before = SupplyHelper(fixture, session, crafted: true);
        var controller = new LocalRestorativeController();
        var selected = Assert.Single(controller.SelectActions(before, _ => false, new HashSet<EntityId>()));
        foreach (EntityId occupied in new[] { selected.CommandId, EntityId.DeriveSpawned(selected.CommandId, 0),
            EntityId.DeriveSpawned(selected.CommandId, 1) })
        {
            Assert.Empty(controller.SelectActions(before, identity => identity == occupied, new HashSet<EntityId>()));
        }
        var intent = Assert.IsType<UseRestorativeItemIntent>(selected.Intent);
        var observationOnly = new UseRestorativeItemIntent(intent.RecipientActorId, intent.UseDefinitionId,
            intent.UseDefinitionVersion, intent.PermissionId, intent.PermissionGrantVersion, intent.ExpectedPermissionRevision,
            intent.ExpectedPatientPresenceRevision + 1, intent.ExpectedPatientProgressionRevision,
            intent.ExpectedStorageRevision + 1, intent.ExpectedProductionRevision + 1, intent.Ingredient);
        Assert.Equal(selected.CommandId, LocalRestorativeController.SelectionId(before, fixture.Helper, observationOnly));
        var changedResource = new UseRestorativeItemIntent(intent.RecipientActorId, intent.UseDefinitionId,
            intent.UseDefinitionVersion, intent.PermissionId, intent.PermissionGrantVersion, intent.ExpectedPermissionRevision,
            intent.ExpectedPatientPresenceRevision, intent.ExpectedPatientProgressionRevision + 1,
            intent.ExpectedStorageRevision, intent.ExpectedProductionRevision, intent.Ingredient);
        Assert.NotEqual(selected.CommandId, LocalRestorativeController.SelectionId(before, fixture.Helper, changedResource));
        var changedDose = new UseRestorativeItemIntent(intent.RecipientActorId, intent.UseDefinitionId,
            intent.UseDefinitionVersion, intent.PermissionId, intent.PermissionGrantVersion, intent.ExpectedPermissionRevision,
            intent.ExpectedPatientPresenceRevision, intent.ExpectedPatientProgressionRevision,
            intent.ExpectedStorageRevision, intent.ExpectedProductionRevision,
            new(intent.Ingredient.Kind, intent.Ingredient.LotId, intent.Ingredient.ExpectedRevision + 1, 1));
        Assert.NotEqual(selected.CommandId, LocalRestorativeController.SelectionId(before, fixture.Helper, changedDose));
        // These altered selections exercise identity arithmetic only, never world admission or a stock grant.
        Assert.Equal(before.ContentHash, session.ReadStateForHost().ContentHash);
    }

    private static WorldSnapshot SupplyHelper(OrdinaryCareWorldFixture fixture, CareWorldSession session, bool crafted)
    {
        if (crafted) return fixture.Supply(session, self: false);
        var state = session.ReadStateForHost();
        EntityId playerStorage = state.GetResident(fixture.Player).InventoryCustodianId;
        var dose = state.Inventory.Custody.Lots.Single(value => value.CustodianId == playerStorage
            && value.Definition == state.Care!.Remedies.Single().Item);
        // The player explicitly donates their original raw dose through Wren's existing deposit policy.
        var give = fixture.Journey.Command(state, fixture.Player, new GiveItemIntent(fixture.Helper,
            state.Space.GetActor(fixture.Helper).Revision, dose.LotId, dose.Revision, playerStorage,
            state.Inventory.Custody.GetStorage(playerStorage).Revision, 1));
        return CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep, [give])).Candidate;
    }

    private static WorldSnapshot MovePlayer(OrdinaryCareWorldFixture fixture, CareWorldSession session, long dy)
    {
        var state = session.ReadStateForHost();
        var control = state.Inventory.Controls.Single(value => value.ActorId == fixture.Player);
        var feet = state.Space.GetActor(fixture.Player).Feet;
        var basis = fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep);
        var after = CareRewardJourney.Commit(session, new(basis.CauseId, basis.WorldId, basis.BranchId,
            basis.ExpectedRevision, basis.ExpectedAdvancingSteps, basis.Kind,
            [new(new(control.PrincipalId, control.SessionId), fixture.Player, 0, dy)], [], basis.ProfileVersion)).Candidate;
        Assert.Equal(feet.Offset(0, dy, 0), after.Space.GetActor(fixture.Player).Feet);
        return after;
    }
}
