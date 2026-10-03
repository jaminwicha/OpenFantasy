using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.EffectiveControl;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.ActorSelection;

public sealed class LocalActorSelectionTests
{
    [Fact]
    public async Task Four_original_humans_keep_home_identity_and_only_the_custodian_can_select_the_absent_owner()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, third, fourth, witness) = await fixture.Acquire();
        var player = grantee.Player;
        var home = player.ReadSelectedObservation();
        var before = fixture.State;
        Assert.Equal(fixture.Grantee, home.Selection.HomeActorId);
        Assert.Equal(fixture.Grantee, home.Selection.ActorId);
        Assert.Null(home.ControlledView);

        foreach (var outsider in new[] { third.Player, fourth.Player })
        {
            await Assert.ThrowsAsync<AuthorityDispatchException>(() =>
                outsider.SelectActorAsync(outsider.ReadSelectedObservation().Selection, fixture.Owner));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() =>
                outsider.SelectActorAsync(home.Selection, fixture.Owner));
            Assert.Same(before, fixture.State);
        }

        var selected = await player.SelectActorAsync(home.Selection, fixture.Owner);
        Assert.Same(before, fixture.State);
        Assert.Equal(fixture.Grantee, player.ActorId);
        Assert.Equal(fixture.Delivery(fixture.Grantee), player.Delivery);
        Assert.Equal(fixture.Grantee, selected.HomeView.PlayerActorId);
        Assert.Equal(fixture.Owner, Assert.IsType<LocalControlledActorView>(selected.ControlledView).ActorId);
        Assert.Equal(witness, selected.Selection.ControlWitness);
        Assert.Equal(home.Selection.Generation + 1, selected.Selection.Generation);
        Assert.False(selected.IsReady);
        Assert.Equal(WorldControllerMode.Human, fixture.State.Controllers!.Get(fixture.Grantee).Mode);
        Assert.False(fixture.State.Inventory.CanControl(player.Delivery, fixture.Owner));
        Assert.Equal(fixture.Genesis.Inventory.Controls, fixture.State.Inventory.Controls);
    }

    [Fact]
    public async Task Returning_to_the_same_actor_never_revives_old_input_readiness_or_legacy_unbound_authority()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, _, _, _) = await fixture.Acquire();
        var player = grantee.Player;
        var originalHome = player.ReadSelectedObservation();
        var market = originalHome.HomeView.Markets!.Stalls.First();
        var inquiry = new LocalTradeInquiry(market.MarketId, market.Commodity, LocalTradeDirection.Buy, 1);
        Assert.Equal(LocalTradeInquiryState.Waiting, (await player.SubmitTradeInquiryAsync(inquiry)).State);
        var first = await player.SelectActorAsync(originalHome.Selection, fixture.Owner);
        Assert.Equal(LocalTradeInquiryState.Cancelled, player.ReadTradeInquiry()!.State);
        player.ResumeSelected(first);
        player.SetSelectedMovement(first.Selection, 32, 0);
        var queued = fixture.Commands.Command(fixture.State, fixture.Grantee,
            new SpeakToActorIntent(fixture.Owner, fixture.State.Space.GetActor(fixture.Owner).Revision));
        var delegated = new WorldActionCommand(queued.CommandId, queued.PrincipalId, queued.OriginSessionId,
            fixture.Owner, queued.WorldId, queued.BranchId, fixture.State.Space.GetActor(fixture.Owner).Revision,
            new SpeakToActorIntent(fixture.Grantee, fixture.State.Space.GetActor(fixture.Grantee).Revision),
            first.Selection.ControlWitness);
        Task<WorldActionReceipt> pending = player.SubmitSelectedAction(first.Selection, delegated);
        await SessionFixture.Until(() => player.Pending is not null);
        var home = await player.SelectActorAsync(first.Selection, fixture.Grantee);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => pending);
        Assert.Null(player.Pending);
        Assert.False(home.IsReady);
        Assert.Equal(0L, player.MovementX);
        Assert.Equal(0L, player.MovementY);
        Assert.Equal(first.Selection.Generation + 1, home.Selection.Generation);
        Assert.Throws<AuthorityDispatchException>(() => player.SetReady(true));
        Assert.Throws<AuthorityDispatchException>(() => player.SetMovement(32, 0));
        Assert.Throws<AuthorityDispatchException>(() => player.Resume(home.ControlStatus));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.SubmitAction(queued));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.SubmitTradeInquiryAsync(inquiry));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.CancelTradeInquiryAsync());
        Assert.Equal(LocalTradeInquiryState.Cancelled, player.ReadTradeInquiry()!.State);

        var second = await player.SelectActorAsync(home.Selection, fixture.Owner);
        Assert.Equal(home.Selection.Generation + 1, second.Selection.Generation);
        Assert.Equal(first.Selection.ActorId, second.Selection.ActorId);
        player.ResumeSelected(second);
        player.SetSelectedMovement(second.Selection, 0, 32);
        var before = fixture.State;
        Assert.Throws<AuthorityDispatchException>(() => player.ResumeSelected(first));
        Assert.Throws<AuthorityDispatchException>(() => player.PauseSelected(first.Selection));
        Assert.Throws<AuthorityDispatchException>(() => player.SetSelectedMovement(first.Selection, 0, 0));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.SelectActorAsync(first.Selection, fixture.Grantee));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.SubmitSelectedAction(first.Selection, delegated));
        Assert.Same(before, fixture.State);
        Assert.True(player.ReadSelectedObservation().IsReady);
        Assert.Equal(0L, player.MovementX);
        Assert.Equal(32L, player.MovementY);
        Assert.False(fixture.Authority.IsIdentityOccupiedForHost(delegated.CommandId));
    }

    [Fact]
    public async Task Selected_movement_journals_the_real_grantee_delivery_and_witness_without_moving_other_humans()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, _, _, witness) = await fixture.Acquire();
        var player = grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var before = fixture.State;
        var body = before.Space.GetActor(fixture.Owner);
        var offset = new (long X, long Y)[] { (32, 0), (-32, 0), (0, 32), (0, -32) }
            .First(value => before.Space.PrepareMove(fixture.Owner, body.Revision, value.X, value.Y).Status == SpatialMoveStatus.Prepared);
        player.ResumeSelected(selected);
        player.SetSelectedMovement(selected.Selection, offset.X, offset.Y);
        await fixture.Tick();
        player.PauseSelected(player.ReadSelectedObservation().Selection);
        var moved = fixture.State;
        Assert.Equal(body.Feet.Offset(offset.X, offset.Y, 0), moved.Space.GetActor(fixture.Owner).Feet);
        foreach (var actor in fixture.Actors.Where(actor => actor != fixture.Owner))
            Assert.Equal(before.Space.GetActor(actor).Feet, moved.Space.GetActor(actor).Feet);
        Assert.Equal(before.Inventory.Controls, moved.Inventory.Controls);
        foreach (var actor in fixture.Actors)
        {
            var custodian = before.GetResident(actor).InventoryCustodianId;
            Assert.Equal(before.Inventory.Custody.Lots.Where(lot => lot.CustodianId == custodian)
                .Select(lot => (lot.LotId, lot.CustodianId, lot.Quantity)),
                moved.Inventory.Custody.Lots.Where(lot => lot.CustodianId == custodian)
                    .Select(lot => (lot.LotId, lot.CustodianId, lot.Quantity)));
        }
        Assert.Equal(body.Binding, moved.Space.GetActor(fixture.Owner).Binding);
        Assert.Equal(body.Condition.ComputeContentHash(), moved.Space.GetActor(fixture.Owner).Condition.ComputeContentHash());
        Assert.Equal(before.Progression!.GetActor(fixture.Owner).CurrentHp, moved.Progression!.GetActor(fixture.Owner).CurrentHp);
        Assert.Equal(before.Progression.GetActor(fixture.Owner).CurrentMp, moved.Progression.GetActor(fixture.Owner).CurrentMp);
        Assert.Equal(before.AdvancingSteps + 1, moved.AdvancingSteps);
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
        await fixture.Stop();
        using (var reopened = SqliteWorldAuthority.OpenExisting(fixture.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            var restored = reopened.ReadStateForHost();
            AssertBodiesEqual(moved, restored);
            Assert.Equal(WorldSnapshotCodec.Encode(restored),
                WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(restored.Revision).State));
        }
        using var database = WorldDatabase.Open(fixture.Path, false);
        var movement = Assert.Single(database.ReadJournal().SelectMany(row => WorldStepCodec.Decode(row.AcceptedBatch).Movement),
            input => input.ActorId == fixture.Owner);
        Assert.Equal(fixture.Owner, movement.ActorId);
        Assert.Equal(fixture.Delivery(fixture.Grantee), movement.Delivery);
        Assert.Equal(witness, movement.ControlWitness);
        Assert.Equal(offset.X, movement.DeltaX);
        Assert.Equal(offset.Y, movement.DeltaY);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revocation_or_real_owner_return_forces_a_new_paused_home_selection(bool ownerReturns)
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, _, _, _) = await fixture.Acquire();
        var player = grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        player.ResumeSelected(selected);
        player.SetSelectedMovement(selected.Selection, 32, 0);
        var before = fixture.State;
        if (ownerReturns) await fixture.Join(fixture.Owner);
        else fixture.Revoke();
        await fixture.Tick();
        var home = player.ReadSelectedObservation();
        Assert.Equal(fixture.Grantee, home.Selection.ActorId);
        Assert.Equal(selected.Selection.Generation + 1, home.Selection.Generation);
        Assert.Null(home.Selection.ControlWitness);
        Assert.Null(home.ControlledView);
        Assert.False(home.IsReady);
        Assert.Equal(0L, player.MovementX);
        AssertBodiesEqual(before, fixture.State);
        Assert.Equal(before.AdvancingSteps, fixture.State.AdvancingSteps);
        Assert.Equal(WorldControllerMode.Human, fixture.State.Controllers!.Get(fixture.Grantee).Mode);
        Assert.Null(fixture.State.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.Throws<AuthorityDispatchException>(() => player.ResumeSelected(selected));
        Assert.Throws<AuthorityDispatchException>(() => player.SetSelectedMovement(selected.Selection, 0, 0));
        player.ResumeSelected(home);
        Assert.True(player.ReadSelectedObservation().IsReady);
    }

    [Fact]
    public async Task Selected_paid_attack_completes_its_own_waiter_and_keeps_exact_receipt_and_cost_after_switch_and_revoke()
    {
        await using var fixture = await LiveEffectiveControlFixture.Open(combatApproach: true);
        var (grantee, _) = await fixture.AcquireAfterRealLoss();
        var state = fixture.EnterEncounter();
        var player = grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        player.ResumeSelected(selected);
        var target = state.EncounterLifecycle!.GetScenario(fixture.Source.Journey.FirstScenario).TargetActorId;
        var ability = state.EncounterActions!.GetCombatant(fixture.Owner).AbilityIds.Select(state.EncounterActions.GetAbility)
            .Single(value => value.Key == EncounterFarmAdmission.StrikeKey);
        var attack = fixture.Source.Delegated(state, new QueueEncounterAbilityIntent(state.Pacing!.Encounter!.EncounterId,
            state.Pacing.Encounter.Revision, ability.DefinitionId, target));
        var ownerBefore = state.Progression!.GetActor(fixture.Owner);
        var targetBefore = state.Progression.GetActor(target);
        Task<WorldActionReceipt> pending = player.SubmitSelectedAction(selected.Selection, attack);
        await SessionFixture.Until(() => player.Pending is not null);
        await fixture.Tick();
        var receipt = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Equal(attack.CommandId, receipt.CommandId);
        Assert.Equal(player.Delivery.PrincipalId, receipt.PrincipalId);
        Assert.Equal(attack.ContentHash, receipt.CommandHash);
        Assert.Null(player.Pending);
        Assert.Contains(fixture.State.EncounterActions!.Pending, value => value.ContentHash == attack.ContentHash);
        var home = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Grantee);
        fixture.Source.Revoke(fixture.Authority);
        await fixture.Tick();
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await player.SubmitAction(attack)));
        Assert.Equal(WorldControllerMode.Human, fixture.State.Controllers!.Get(fixture.Grantee).Mode);
        Assert.False(home.IsReady);

        // Opponents explicitly choose their existing legal Wait actions; only already accepted paid work resolves.
        for (int boundary = 0; fixture.State.EncounterActions!.LastResolution?.DecisionCommandId != attack.CommandId; boundary++)
        {
            Assert.True(boundary < 64, "The accepted paid attack did not reach its ordinary resolution.");
            EffectiveControlCombatTests.ResolveOneWithOtherActorsWaiting(fixture.Authority, fixture.Source);
        }
        var final = fixture.State;
        var resolution = final.EncounterActions!.LastResolution!;
        Assert.Equal(EncounterActionResolutionStatus.Damaged, resolution.Status);
        Assert.True(resolution.Damage > 0);
        Assert.Equal(targetBefore.CurrentHp - (int)resolution.Damage, final.Progression!.GetActor(target).CurrentHp);
        Assert.Equal(ownerBefore.CurrentMp - (int)ability.ManaCost, final.Progression.GetActor(fixture.Owner).CurrentMp);
        Assert.Equal(ability.ManaCost, resolution.ManaSpent);
        Assert.DoesNotContain(final.EncounterActions.Pending, value => value.CommandId == attack.CommandId);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await player.SubmitAction(attack)));
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    private static void AssertBodiesEqual(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.Space.Actors.Select(actor => actor.ActorId), after.Space.Actors.Select(actor => actor.ActorId));
        foreach (var actor in before.Space.Actors)
        {
            var current = after.Space.GetActor(actor.ActorId);
            Assert.Equal(actor.Binding, current.Binding);
            Assert.Equal(actor.Condition.ComputeContentHash(), current.Condition.ComputeContentHash());
            Assert.Equal(actor.Feet, current.Feet);
            Assert.Equal(actor.Revision, current.Revision);
            Assert.Equal(actor.HomePlaceId, current.HomePlaceId);
        }
    }
}
