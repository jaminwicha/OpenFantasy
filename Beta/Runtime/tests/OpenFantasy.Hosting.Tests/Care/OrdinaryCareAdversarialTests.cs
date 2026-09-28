using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Hosting.Tests.Care.WorldResourceJourneyTests;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class OrdinaryCareAdversarialTests
{
    [Fact]
    public void Full_health_self_use_keeps_the_actual_dose_and_resources_but_advances_normal_world_time()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        var before = fixture.Supply(session, self: true);
        var intent = SelectedIntent(before, fixture.Player, fixture.Player, self: true);
        Assert.Equal(before.Progression!.GetActor(fixture.Player).Stats.MaxHp,
            before.Progression.GetActor(fixture.Player).CurrentHp);
        Assert.Null(WorldRestorativeActions.Prepare(before, fixture.Player, fixture.Player, intent.UseDefinitionId));
        Assert.Equal(WorldActionRejection.Unavailable, WorldRestorativeActions.Evaluate(before, fixture.Player, intent));

        var after = RejectAdvance(fixture, session, fixture.Journey.Command(before, fixture.Player, intent),
            WorldActionRejection.Unavailable);

        Assert.Equal(1UL, Remaining(after, intent.Ingredient));
        Assert.Empty(after.Care!.Ordinary!.Uses);
        Assert.Empty(after.Production!.ConsumedItems);
        RoundTrip(after);
    }

    [Fact]
    public void Ordinary_self_use_cannot_bypass_actual_lethal_outcome_or_pending_aftermath()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        var defeated = fixture.Journey.PrepareDefeat(session);
        Assert.Equal(0, defeated.Progression!.GetActor(fixture.Player).CurrentHp);
        var intent = SelectedIntent(defeated, fixture.Player, fixture.Player, self: true);
        Assert.Equal(WorldActionRejection.Unavailable, WorldRestorativeActions.Evaluate(defeated, fixture.Player, intent));
        var command = fixture.Journey.Command(defeated, fixture.Player, intent);
        Assert.Throws<InvalidOperationException>(() => session.Commit(fixture.Journey.Batch(defeated,
            WorldBatchKind.AdvancingStep, [command])));
        Assert.Equal(defeated.ContentHash, session.ReadStateForHost().ContentHash);
        Assert.Throws<ArgumentException>(() => fixture.Journey.Batch(defeated, WorldBatchKind.Care, [command]));

        var pending = CareRewardJourney.Commit(session, fixture.Journey.Batch(defeated, WorldBatchKind.Care,
            [fixture.Journey.Acknowledge(defeated)])).Candidate;
        Assert.NotNull(pending.Care!.Pending);
        var attempted = fixture.Journey.Command(pending, fixture.Player,
            SelectedIntent(pending, fixture.Player, fixture.Player, self: true));
        Assert.Throws<InvalidOperationException>(() => session.Commit(fixture.Journey.Batch(pending,
            WorldBatchKind.AdvancingStep, [attempted])));
        Assert.Equal(pending.ContentHash, session.ReadStateForHost().ContentHash);
        Assert.Equal(1UL, Remaining(pending, intent.Ingredient));
        Assert.Empty(pending.Care.Ordinary!.Uses);
    }

    [Fact]
    public void Only_patient_can_revoke_and_neither_patient_impersonation_nor_stale_or_current_revoked_aid_spends_a_dose()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        fixture.ReachInjuredExploration(session);
        var state = fixture.Supply(session, self: false);
        var eligible = fixture.Intent(state, self: false);
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(state, fixture.Helper, eligible));
        var supplier = fixture.Journey.Command(state, fixture.Helper, eligible);
        var patientControl = state.Inventory.Controls.Single(value => value.ActorId == fixture.Player);
        var impersonation = new WorldActionCommand(supplier.CommandId, patientControl.PrincipalId, patientControl.SessionId,
            supplier.ActorId, supplier.WorldId, supplier.BranchId, supplier.ExpectedActorRevision, supplier.Intent);
        state = RejectAdvance(fixture, session, impersonation, WorldActionRejection.NotAuthorized);

        var permission = state.Care!.Ordinary!.Permissions.Single();
        var revoke = new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion);
        state = RejectAdvance(fixture, session, fixture.Journey.Command(state, fixture.Helper, revoke),
            WorldActionRejection.NotAuthorized);
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(state, fixture.Helper, eligible));
        var beforeRevoke = state;
        state = CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(state, fixture.Player, revoke)])).Candidate;
        Assert.False(state.Care!.Ordinary!.Permissions.Single().IsActive);
        Assert.Equal(beforeRevoke.Inventory.ContentHash, state.Inventory.ContentHash);
        Assert.Equal(beforeRevoke.Production!.ContentHash, state.Production!.ContentHash);
        Assert.Equal(beforeRevoke.Progression!.ContentHash, state.Progression!.ContentHash);
        Assert.Equal(beforeRevoke.ResourceHistory!.ContentHash, state.ResourceHistory!.ContentHash);

        state = RejectAdvance(fixture, session, fixture.Journey.Command(state, fixture.Helper, eligible),
            WorldActionRejection.RevisionConflict);
        var currentButRevoked = SelectedIntent(state, fixture.Helper, fixture.Player, self: false,
            selected: eligible.Ingredient);
        Assert.Equal(1UL, currentButRevoked.ExpectedPermissionRevision);
        Assert.Equal(WorldActionRejection.NotAuthorized,
            WorldRestorativeActions.Evaluate(state, fixture.Helper, currentButRevoked));
        Assert.Null(WorldRestorativeActions.Prepare(state, fixture.Helper, fixture.Player,
            currentButRevoked.UseDefinitionId, currentButRevoked.PermissionId));
        state = RejectAdvance(fixture, session, fixture.Journey.Command(state, fixture.Helper, currentButRevoked),
            WorldActionRejection.NotAuthorized);
        Assert.Equal(1UL, Remaining(state, eligible.Ingredient));
        Assert.Empty(state.Care!.Ordinary!.Uses);
        RoundTrip(state);
    }

    [Fact]
    public void Actual_helper_movement_out_of_reach_refuses_the_same_eligible_dose_and_return_restores_eligibility()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        fixture.ReachInjuredExploration(session);
        var before = fixture.Supply(session, self: false);
        var intent = fixture.Intent(before, self: false);
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, fixture.Helper, intent));
        var initialFeet = before.Space.GetActor(fixture.Helper).Feet;
        long range = checked((long)before.Care!.Remedies.Single().RangeSubunits);
        var outside = before.Space.GetActor(fixture.Player).Feet.Offset(0, -range - 32, 0);
        var away = MoveHelper(fixture, session, outside);
        Assert.Equal(WorldActionRejection.OutOfReach, WorldRestorativeActions.Evaluate(away, fixture.Helper, intent));
        RejectAdvance(fixture, session, fixture.Journey.Command(away, fixture.Helper, intent), WorldActionRejection.OutOfReach);
        var returned = MoveHelper(fixture, session, initialFeet);
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(returned, fixture.Helper, intent));
        Assert.Equal(before.Inventory.ContentHash, returned.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, returned.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, returned.Progression!.ContentHash);
        Assert.Equal(1UL, Remaining(returned, intent.Ingredient));
    }

    [Fact]
    public void Simultaneous_self_and_assisted_requests_restore_once_and_keep_the_losing_exact_dose()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        fixture.ReachInjuredExploration(session);
        var before = fixture.Supply(session, self: false);
        var self = fixture.Intent(before, self: true);
        var assist = fixture.Intent(before, self: false);
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, fixture.Player, self));
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, fixture.Helper, assist));
        var staleDose = SelectedIntent(before, fixture.Helper, fixture.Player, self: false,
            selected: new(assist.Ingredient.Kind, assist.Ingredient.LotId, assist.Ingredient.ExpectedRevision + 1, 1));
        before = RejectAdvance(fixture, session, fixture.Journey.Command(before, fixture.Helper, staleDose),
            WorldActionRejection.Unavailable);
        Assert.Equal(1UL, Remaining(before, assist.Ingredient));
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, fixture.Helper, assist));
        var playerCommand = fixture.Journey.Command(before, fixture.Player, self);
        var helperCommand = fixture.Journey.Command(before, fixture.Helper, assist);
        var batch = fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [helperCommand, playerCommand]);
        var winner = batch.Actions[0];
        var loser = batch.Actions[1];
        Assert.True(winner.ActorId < loser.ActorId);
        var decision = session.Commit(batch);
        var after = decision.Candidate;
        Assert.Equal(CommandReceiptStatus.Applied, decision.Receipts.Single(value => value.CommandId == winner.CommandId).Status);
        Assert.Equal(WorldActionRejection.RevisionConflict, decision.Receipts.Single(value => value.CommandId == loser.CommandId).Rejection);
        var use = Assert.Single(after.Care!.Ordinary!.Uses);
        Assert.Equal(winner.CommandId, use.CommandId);
        Assert.Equal(0U, use.CommandOrdinal);
        Assert.Equal(before.Production!.ConsumedItems.Count + 1, after.Production!.ConsumedItems.Count);
        Assert.Equal(before.ResourceHistory!.History.Batches.Count + 1, after.ResourceHistory!.History.Batches.Count);
        Assert.Equal(Math.Min(60, before.Progression!.GetActor(fixture.Player).CurrentHp + 40),
            after.Progression!.GetActor(fixture.Player).CurrentHp);
        Assert.Equal(before.Progression.GetActor(fixture.Player).Revision + 1, after.Progression.GetActor(fixture.Player).Revision);
        Assert.Equivalent(before.Progression.Currency, after.Progression.Currency, strict: true);
        var winningIntent = Assert.IsType<UseRestorativeItemIntent>(winner.Intent);
        var losingIntent = Assert.IsType<UseRestorativeItemIntent>(loser.Intent);
        Assert.Equal(0UL, Remaining(after, winningIntent.Ingredient));
        Assert.Equal(1UL, Remaining(after, losingIntent.Ingredient));
        var claim = after.Production.ConsumedItems.Single(value => value.ConsumptionId == use.ConsumptionId);
        Assert.Equal(winningIntent.Ingredient.LotId, claim.Ingredient.LotId);
        Assert.Equal(winningIntent.Ingredient.Kind, claim.Ingredient.Kind);
        Assert.Equal(1UL, claim.Ingredient.Quantity);

        // A new command carrying the old selection must not silently take the other actor's surviving dose.
        after = RejectAdvance(fixture, session, fixture.Journey.Command(after, winner.ActorId, winningIntent),
            WorldActionRejection.RevisionConflict);
        Assert.Single(after.Care!.Ordinary!.Uses);
        Assert.Equal(1UL, Remaining(after, losingIntent.Ingredient));
        RoundTrip(after);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Same_step_patient_revoke_and_helper_use_follow_actual_actor_order_without_rewriting_prior_permission(bool revokeFirst)
    {
        var fixture = FixtureWithActorOrder(revokeFirst);
        var session = new CareWorldSession(fixture.Genesis);
        fixture.ReachInjuredExploration(session);
        var before = fixture.Supply(session, self: false);
        var intent = fixture.Intent(before, self: false);
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, fixture.Helper, intent));
        var permission = before.Care!.Ordinary!.Permissions.Single();
        var revoke = fixture.Journey.Command(before, fixture.Player,
            new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion));
        var aid = fixture.Journey.Command(before, fixture.Helper, intent);
        var batch = fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [aid, revoke]);
        bool revokedFirst = batch.Actions[0].CommandId == revoke.CommandId;
        Assert.Equal(revokeFirst, revokedFirst);
        var decision = session.Commit(batch);
        var after = decision.Candidate;
        Assert.Equal(CommandReceiptStatus.Applied, decision.Receipts.Single(value => value.CommandId == revoke.CommandId).Status);
        var revocation = Assert.IsType<CarePermissionRevocation>(after.Care!.Ordinary!.Permissions.Single().Revocation);
        Assert.Equal(revokedFirst ? 0U : 1U, revocation.CommandOrdinal);
        Assert.Equal(after.Revision, revocation.WorldSequence);
        Assert.Equal(permission.GrantVersion, after.Care.Ordinary.Permissions.Single().GrantVersion);
        if (revokedFirst)
        {
            Assert.Equal(WorldActionRejection.RevisionConflict, decision.Receipts.Single(value => value.CommandId == aid.CommandId).Rejection);
            Assert.Empty(after.Care.Ordinary.Uses);
            Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
            Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
            Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
            Assert.Equal(before.ResourceHistory!.ContentHash, after.ResourceHistory!.ContentHash);
        }
        else
        {
            Assert.Equal(CommandReceiptStatus.Applied, decision.Receipts.Single(value => value.CommandId == aid.CommandId).Status);
            var use = Assert.Single(after.Care.Ordinary.Uses);
            Assert.Equal(after.Revision, use.WorldSequence);
            Assert.Equal(0U, use.CommandOrdinal);
            Assert.Equal(0UL, use.PermissionRevision);
            Assert.Equal(permission.GrantVersion, use.PermissionGrantVersion);
            Assert.Equal(0UL, Remaining(after, intent.Ingredient));
            Assert.Equal(before.Production!.ConsumedItems.Count + 1, after.Production!.ConsumedItems.Count);
        }
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.False(after.Care.Ordinary.Permissions.Single().IsActive);
        RoundTrip(after);
    }

    [Fact]
    public void Conserved_ordinary_resource_history_cannot_substitute_control_permission_kind_or_command_position()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        fixture.ReachInjuredExploration(session);
        var before = fixture.Supply(session, self: false);
        var command = fixture.Journey.Command(before, fixture.Helper, fixture.Intent(before, self: false));
        var used = CareRewardJourney.Commit(session,
            fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [command])).Candidate;
        var permission = used.Care!.Ordinary!.Permissions.Single();
        var world = CareRewardJourney.Commit(session, fixture.Journey.Batch(used, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(used, fixture.Player,
                new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion))])).Candidate;
        var history = world.ResourceHistory!;
        var original = Assert.IsType<WorldRestorativeUseResourceEvidence>(history.Evidence[^1]);
        var request = original.Request;
        var intent = Assert.IsType<UseRestorativeItemIntent>(request.Intent);
        var control = world.Inventory.Controls.Single(value => value.ActorId == fixture.Player);
        var forgedControl = new WorldActionCommand(request.CommandId, control.PrincipalId, control.SessionId, request.ActorId,
            request.WorldId, request.BranchId, request.ExpectedActorRevision, request.Intent);
        RejectEvidence(new WorldRestorativeUseResourceEvidence(forgedControl, original.UseId));
        var changedPermission = new UseRestorativeItemIntent(intent.RecipientActorId, intent.UseDefinitionId,
            intent.UseDefinitionVersion, intent.PermissionId, intent.PermissionGrantVersion, 1,
            intent.ExpectedPatientPresenceRevision, intent.ExpectedPatientProgressionRevision, intent.ExpectedStorageRevision,
            intent.ExpectedProductionRevision, intent.Ingredient);
        RejectEvidence(new WorldRestorativeUseResourceEvidence(new(request.CommandId, request.PrincipalId,
            request.OriginSessionId, request.ActorId, request.WorldId, request.BranchId, request.ExpectedActorRevision,
            changedPermission), original.UseId));
        RejectEvidence(new WorldRewardResourceEvidence(request.CommandId));
        var batches = history.History.Batches.ToArray();
        var actual = batches[^1];
        batches[^1] = new(actual.WorldSequence, actual.CommandOrdinal + 1, actual.CommandId, actual.Changes);
        var shifted = new WorldResourceHistory(history.AnchorAdmission,
            new ActorResourceHistory(history.History.Basis, history.History.BasisWorldSequence, batches),
            history.Evidence, history.ArchiveAnchor);
        shifted.History.ValidateCurrent(world.Progression!, world.Revision);
        Assert.Contains("ordered command", Assert.Throws<ArgumentException>(() => Copy(world, shifted)).Message);
        Assert.Throws<ArgumentException>(() => new WorldResourceHistory(history.AnchorAdmission,
            history.History, history.Evidence.SkipLast(1), history.ArchiveAnchor));
        Assert.Equal(world.ContentHash, session.ReadStateForHost().ContentHash);
        RoundTrip(world);

        void RejectEvidence(WorldResourceEvidence replacement)
        {
            var evidence = history.Evidence.ToArray();
            evidence[^1] = replacement;
            var changed = new WorldResourceHistory(history.AnchorAdmission, history.History, evidence, history.ArchiveAnchor);
            // All HP, MP, XP, currency, materials and actual effects are unchanged and reconstructable.
            changed.History.ValidateCurrent(world.Progression!, world.Revision);
            Assert.Throws<ArgumentException>(() => Copy(world, changed));
        }
    }

    private static UseRestorativeItemIntent SelectedIntent(WorldSnapshot world, EntityId actor, EntityId recipient,
        bool self, IngredientRequest? selected = null)
    {
        var ordinary = world.Care!.Ordinary!;
        var definition = ordinary.Definitions.Single(value => value.DeliveryMode ==
            (self ? RestorativeDeliveryMode.SelfUse : RestorativeDeliveryMode.LivingAssistance));
        var permission = self ? null : ordinary.Permissions.Single();
        EntityId storage = world.GetResident(actor).InventoryCustodianId;
        var remedy = world.Care.GetRemedy(definition.RemedyDefinitionId);
        if (selected is null)
        {
            var lot = world.Inventory.Custody.Lots.Single(value => value.CustodianId == storage && value.Definition == remedy.Item);
            selected = new(IngredientLotKind.Source, lot.LotId, lot.Revision, 1);
        }
        return new(recipient, definition.DefinitionId, definition.Version, permission?.PermissionId, permission?.GrantVersion,
            permission?.Revision, world.Space.GetActor(recipient).Revision, world.Progression!.GetActor(recipient).Revision,
            world.Inventory.Custody.GetStorage(storage).Revision, world.Production!.Revision, selected);
    }

    private static OrdinaryCareWorldFixture FixtureWithActorOrder(bool patientFirst)
    {
        // Only fresh seed-derived identities vary. Every tested branch still earns its injury,
        // recovery, victory and finite crafted stock through the unchanged production rules.
        for (int candidate = 0; candidate < 32; candidate++)
        {
            var fixture = new OrdinaryCareWorldFixture($"ordinary-care-command-order-{candidate}");
            if ((fixture.Player < fixture.Helper) == patientFirst) return fixture;
        }
        throw new InvalidOperationException("The finite fresh-seed calibration must provide both actual actor orders.");
    }

    private static WorldSnapshot RejectAdvance(OrdinaryCareWorldFixture fixture, CareWorldSession session,
        WorldActionCommand command, WorldActionRejection expected)
    {
        var before = session.ReadStateForHost();
        var decision = session.Commit(fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [command]));
        var receipt = Assert.Single(decision.Receipts);
        Assert.Equal(CommandReceiptStatus.Rejected, receipt.Status);
        Assert.Equal(expected, receipt.Rejection);
        var after = decision.Candidate;
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Care!.ContentHash, after.Care!.ContentHash);
        Assert.Equal(before.ResourceHistory!.ContentHash, after.ResourceHistory!.ContentHash);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        return after;
    }

    private static ulong Remaining(WorldSnapshot world, IngredientRequest selected) => selected.Kind == IngredientLotKind.Source
        ? world.Inventory.Custody.Lots.Where(value => value.LotId == selected.LotId).Aggregate(0UL, (sum, lot) => sum + lot.Quantity)
        : world.Production!.Lots.Where(value => value.LotId == selected.LotId).Aggregate(0UL, (sum, lot) => sum + lot.Quantity);

    private static WorldSnapshot MoveHelper(OrdinaryCareWorldFixture fixture, CareWorldSession session, SubunitPosition goal)
    {
        var state = session.ReadStateForHost();
        for (int step = 0; state.Space.GetActor(fixture.Helper).Feet != goal; step++)
        {
            Assert.True(step < 64, "A local helper range probe must remain within its finite real movement bound.");
            var route = state.Space.QueryGroundRoute(fixture.Helper, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            var feet = state.Space.GetActor(fixture.Helper).Feet;
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            var control = state.Inventory.Controls.Single(value => value.ActorId == fixture.Helper);
            var basis = fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep);
            state = CareRewardJourney.Commit(session, new(basis.CauseId, basis.WorldId, basis.BranchId,
                basis.ExpectedRevision, basis.ExpectedAdvancingSteps, basis.Kind,
                [new(new(control.PrincipalId, control.SessionId), fixture.Helper, dx, dy)], [], basis.ProfileVersion)).Candidate;
            Assert.Equal(feet.Offset(dx, dy, 0), state.Space.GetActor(fixture.Helper).Feet);
        }
        return state;
    }
}
