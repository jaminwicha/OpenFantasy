using System.Text;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class LocalEncounterActionViewTests
{
    [Fact]
    public void Observer_grants_and_actual_target_checks_produce_a_captured_authority_accepted_choice()
    {
        var fixture = new EncounterJourneyWorldFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        Assert.Null(new LocalWorldView(fixture.Genesis, fixture.Player).EncounterActions);
        var entered = authority.Commit(fixture.Batch(fixture.Genesis, WorldBatchKind.EncounterLifecycle,
            [fixture.Enter(fixture.Genesis, fixture.FirstScenario)])).Candidate;
        var stagingView = View(entered, fixture.Player);
        Assert.False(stagingView.CanWait);
        Assert.Equal(LocalEncounterActionBlockedReason.NotActive, stagingView.WaitBlockedReason);
        Assert.All(stagingView.AbilityOffers, offer => Assert.False(offer.CanQueue));
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(entered,
            WorldBatchKind.EncounterDecision, [fixture.Wait(entered, fixture.Player)])));
        for (int stage = 0; stage < 2; stage++)
        {
            var state = authority.ReadStateForHost();
            authority.Commit(fixture.Batch(state, WorldBatchKind.EncounterLifecycle));
        }

        var active = authority.ReadStateForHost();
        byte[] before = WorldSnapshotCodec.Encode(active);
        LocalEncounterActionView projected = View(active, fixture.Player);
        Assert.True(projected.CanWait);
        Assert.Equal(active.Pacing!.Encounter!.EncounterId, projected.EncounterId);
        Assert.Equal(active.Pacing.Encounter.Revision, projected.TimingRevision);
        Assert.Equal(active.Space.GetActor(fixture.Player).Revision, projected.ActorRevision);
        Assert.Equal(active.Progression!.GetActor(fixture.Player).CurrentHp, projected.CurrentHp);
        Assert.Equal(active.Progression.GetActor(fixture.Player).CurrentMp, projected.CurrentMp);
        Assert.Equal(active.Pacing.Encounter.GetActor(fixture.Player).StandardDelay, projected.WaitDelay);
        Assert.Equal(active.Residents.Count, projected.AbilityOffers.Count);
        Assert.All(projected.AbilityOffers, offer => Assert.Equal(fixture.Strike, offer.AbilityDefinitionId));
        foreach (var offer in projected.AbilityOffers)
        {
            var intent = Intent(projected, offer);
            var ability = active.EncounterActions!.GetAbility(offer.AbilityDefinitionId);
            Assert.Equal(ability.DisplayName, offer.DisplayName);
            Assert.Equal(ability.RangeSubunits, offer.RangeSubunits);
            Assert.Equal(ability.ManaCost, offer.ManaCost);
            Assert.Equal(ability.Delay(active.Pacing.Encounter.GetActor(fixture.Player).NormalizedSpeed), offer.ReadinessDelay);
            Assert.Equal(WorldEncounterActions.EvaluateAbility(active, fixture.Player, intent) == WorldActionRejection.None,
                offer.CanQueue);
        }
        LocalEncounterAbilityOffer attack = Assert.Single(projected.AbilityOffers, offer => offer.CanQueue);
        Assert.Equal(fixture.WolfFirst, attack.TargetActorId);
        Assert.All(projected.AbilityOffers.Where(offer => offer.TargetActorId != fixture.WolfFirst),
            offer => Assert.Equal(LocalEncounterActionBlockedReason.Unavailable, offer.BlockedReason));
        foreach (var blockedTarget in projected.AbilityOffers.Where(value => !value.CanQueue))
        {
            var refused = authority.Preview(fixture.Batch(active, WorldBatchKind.EncounterDecision,
                [Captured(fixture, active, projected, Intent(projected, blockedTarget))]));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
            Assert.Empty(refused.Candidate.EncounterActions!.Pending);
            Assert.Equal(active.Progression.ContentHash, refused.Candidate.Progression!.ContentHash);
        }
        var bystander = View(active, fixture.Wren);
        Assert.Empty(bystander.AbilityOffers);
        Assert.False(bystander.CanWait);
        Assert.Equal(LocalEncounterActionBlockedReason.Bystander, bystander.WaitBlockedReason);
        AssertPrivateFieldsAbsent(projected, active, fixture.Bite);
        Assert.Throws<NotSupportedException>(() => ((IList<LocalEncounterAbilityOffer>)projected.AbilityOffers).Clear());
        Assert.Equal(before, WorldSnapshotCodec.Encode(active));

        WorldActionCommand command = Captured(fixture, active, projected, Intent(projected, attack));
        var decision = authority.Commit(fixture.Batch(active, WorldBatchKind.EncounterDecision, [command]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(Assert.Single(decision.Candidate.EncounterActions!.Pending)));
        Assert.Equal(active.Progression.ContentHash, decision.Candidate.Progression!.ContentHash);
        Assert.Equal(active.EncounterActions!.Random, decision.Candidate.EncounterActions.Random);
        var queued = View(decision.Candidate, fixture.Player);
        Assert.Equal(LocalEncounterActionBlockedReason.DecisionQueued, queued.WaitBlockedReason);
        Assert.False(queued.CanWait);
        Assert.All(queued.AbilityOffers, offer => Assert.False(offer.CanQueue));
    }

    [Fact]
    public void Captured_timing_does_not_retarget_after_another_choice_and_wait_incurs_visible_recovery()
    {
        var fixture = new EncounterJourneyWorldFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        WorldSnapshot active = Activate(authority, fixture);
        var captured = View(active, fixture.Player);
        var offer = Assert.Single(captured.AbilityOffers, value => value.CanQueue);
        WorldActionCommand stale = Captured(fixture, active, captured, Intent(captured, offer));
        var queuedNpc = authority.Commit(fixture.Batch(active, WorldBatchKind.EncounterDecision,
            [fixture.Wait(active, fixture.WolfFirst)])).Candidate;
        var refusal = authority.Commit(fixture.Batch(queuedNpc, WorldBatchKind.EncounterDecision, [stale]));
        Assert.Equal(WorldActionRejection.RevisionConflict, Assert.Single(refusal.Receipts).Rejection);
        Assert.Empty(refusal.Candidate.EncounterActions!.Pending);
        Assert.Equal(queuedNpc.Pacing!.Encounter!.ContentHash, refusal.Candidate.Pacing!.Encounter!.ContentHash);

        LocalEncounterActionView fresh = View(refusal.Candidate, fixture.Player);
        Assert.True(fresh.CanWait);
        var wait = Captured(fixture, refusal.Candidate, fresh, new QueueEncounterWaitIntent(fresh.EncounterId, fresh.TimingRevision));
        var waited = authority.Commit(fixture.Batch(refusal.Candidate, WorldBatchKind.EncounterDecision, [wait]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(waited.Receipts).Status);
        for (int resolution = 0; resolution < 2 && View(authority.ReadStateForHost(), fixture.Player).RemainingDelay == 0; resolution++)
        {
            var state = authority.ReadStateForHost();
            authority.Commit(fixture.Batch(state, WorldBatchKind.EncounterResolution));
        }
        var recovered = authority.ReadStateForHost();
        var recovery = View(recovered, fixture.Player);
        Assert.Equal(fresh.WaitDelay, recovery.RemainingDelay);
        Assert.Equal(LocalEncounterActionBlockedReason.Recovering, recovery.WaitBlockedReason);
        Assert.False(recovery.CanWait);
        Assert.All(recovery.AbilityOffers, value => Assert.False(value.CanQueue));
        var unavailable = authority.Commit(fixture.Batch(recovered, WorldBatchKind.EncounterDecision,
            [fixture.Wait(recovered, fixture.Player)]));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(unavailable.Receipts).Rejection);
        Assert.Equal(active.Calendar, unavailable.Candidate.Calendar);
        Assert.Equal(active.Progression!.ContentHash, unavailable.Candidate.Progression!.ContentHash);
        Assert.Equal(active.EncounterActions!.Random, unavailable.Candidate.EncounterActions!.Random);
    }

    [Theory]
    [InlineData(1L, 0U, BodyFunction.Manipulation, LocalEncounterActionBlockedReason.OutOfReach, WorldActionRejection.OutOfReach)]
    [InlineData(768L, 1_000_000U, BodyFunction.Manipulation, LocalEncounterActionBlockedReason.Unavailable, WorldActionRejection.Unavailable)]
    [InlineData(768L, 0U, BodyFunction.NaturalWeapon, LocalEncounterActionBlockedReason.Unavailable, WorldActionRejection.Unavailable)]
    public void Actual_range_mana_and_anatomy_rules_block_known_abilities_without_client_rule_guesses(long range,
        uint mana, BodyFunction function, LocalEncounterActionBlockedReason reason, WorldActionRejection rejection)
    {
        var fixture = new EncounterCombatWorldFixture();
        var basis = fixture.Genesis;
        var original = basis.EncounterActions!.GetAbility(fixture.Strike);
        // Separate authored admission controls; this does not edit a live grant or source asset.
        var altered = new EncounterAbilityDefinition(original.DefinitionId, original.Key, original.DisplayName,
            ControlPin(basis), original.PowerTenths, range, original.ReadinessCostNumerator,
            original.ReadinessCostDenominator, mana, original.MinimumLevel, function);
        var actions = EncounterActionSnapshot.Begin(basis.EncounterActions.Scope, basis.EncounterActions.Seed,
            basis.EncounterActions.Abilities.Select(ability => ability.DefinitionId == fixture.Strike ? altered : ability),
            basis.EncounterActions.Combatants);
        var authored = Authored(basis, actions: actions);
        var view = View(authored, fixture.Player);
        var offer = Assert.Single(view.AbilityOffers, value => value.TargetActorId == fixture.Wolf);
        Assert.False(offer.CanQueue);
        Assert.Equal(reason, offer.BlockedReason);
        Assert.Equal(rejection, WorldEncounterActions.EvaluateAbility(authored, fixture.Player, Intent(view, offer)));
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, authored);
        var command = new WorldActionCommand(fixture.NextId(), fixture.Delivery.PrincipalId, fixture.Delivery.SessionId,
            fixture.Player, authored.WorldId, authored.BranchId, view.ActorRevision, Intent(view, offer));
        var refused = authority.Commit(fixture.Batch(authored, WorldBatchKind.EncounterDecision, [command]));
        Assert.Equal(rejection, Assert.Single(refused.Receipts).Rejection);
        Assert.Equal(authored.Progression!.ContentHash, refused.Candidate.Progression!.ContentHash);
        Assert.Equal(authored.EncounterActions!.ContentHash, refused.Candidate.EncounterActions!.ContentHash);
        Assert.Equal(authored.Calendar, refused.Candidate.Calendar);
    }

    [Fact]
    public void Outcome_blocks_choices_and_released_incapacitation_remains_public_after_exact_reopen()
    {
        var fixture = new EncounterJourneyWorldFixture();
        var basis = fixture.Genesis;
        var resources = basis.Progression!;
        var progression = new ProgressionSnapshot(basis.WorldId, basis.BranchId, resources.Revision,
            resources.Profiles, resources.Curves, resources.Actors.Select(actor => actor.ActorId != fixture.WolfFirst ? actor
                : new ActorProgressionState(actor.ActorId, actor.Revision, actor.Profile, actor.Curve, actor.Level,
                    actor.Experience, 1, actor.CurrentMp)), resources.Currency);
        // An explicitly wounded fresh control makes the actual accepted resolution lethal in one strike.
        var authored = Authored(basis, progression: progression);
        using var files = new StorageFiles();
        WorldSnapshot released;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, authored))
        {
            WorldSnapshot active = Activate(authority, fixture);
            var projected = View(active, fixture.Player);
            var offer = Assert.Single(projected.AbilityOffers, value => value.CanQueue);
            var strike = Captured(fixture, active, projected, Intent(projected, offer));
            authority.Commit(fixture.Batch(active, WorldBatchKind.EncounterDecision,
                [strike, fixture.Wait(active, fixture.WolfFirst)]));
            for (int resolution = 0; resolution < 2 && !WorldEncounterActions.IsOutcome(authority.ReadStateForHost()); resolution++)
            {
                var state = authority.ReadStateForHost();
                authority.Commit(fixture.Batch(state, WorldBatchKind.EncounterResolution));
            }
            var outcome = authority.ReadStateForHost();
            Assert.Equal(EncounterLifecyclePhase.Outcome, outcome.EncounterLifecycle!.Phase);
            var outcomeView = new LocalWorldView(outcome, fixture.Player);
            Assert.Equal(LocalEncounterActionBlockedReason.Outcome, outcomeView.EncounterActions!.WaitBlockedReason);
            Assert.False(outcomeView.EncounterActions.CanWait);
            Assert.All(outcomeView.EncounterActions.AbilityOffers, value => Assert.False(value.CanQueue));
            Assert.True(Assert.Single(outcomeView.EncounterActors, actor => actor.ActorId == fixture.WolfFirst).IsIncapacitated);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(outcome,
                WorldBatchKind.EncounterDecision, [fixture.Wait(outcome, fixture.Player)])));
            released = authority.Commit(fixture.Batch(outcome, WorldBatchKind.EncounterLifecycle,
                [fixture.Release(outcome)])).Candidate;
            var releasedView = new LocalWorldView(released, fixture.Player);
            Assert.Null(releasedView.EncounterActions);
            Assert.Empty(releasedView.EncounterActors);
            Assert.True(Assert.Single(releasedView.Actors, actor => actor.ActorId == fixture.WolfFirst).IsIncapacitated);
            Assert.False(Assert.Single(releasedView.Actors, actor => actor.ActorId == fixture.Player).IsIncapacitated);
            Assert.True(releasedView.EncounterLifecycle!.PlayerRecoveryDelay > 0);
        }
        using var restored = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(released), WorldSnapshotCodec.Encode(restored.ReadStateForHost()));
        Assert.True(Assert.Single(new LocalWorldView(restored.ReadStateForHost(), fixture.Player).Actors,
            actor => actor.ActorId == fixture.WolfFirst).IsIncapacitated);
    }

    [Fact]
    public void Legacy_timing_only_wait_and_nonencounter_projections_retain_their_scope()
    {
        var fixture = new EncounterPacingWorldFixture();
        var view = View(fixture.Genesis, fixture.Player);
        Assert.Empty(view.AbilityOffers);
        Assert.True(view.CanWait);
        var wait = fixture.Wait(fixture.Genesis, fixture.Player, view.TimingRevision);
        var decision = new WorldStepProcessor().Decide(fixture.Genesis,
            fixture.Batch(fixture.Genesis, WorldBatchKind.EncounterDecision, [wait]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
        Assert.Null(new LocalWorldView(fixture.Basis, fixture.Player).EncounterActions);
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Genesis), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(
            WorldSnapshotCodec.Encode(fixture.Genesis))));
    }

    private static LocalEncounterActionView View(WorldSnapshot world, EntityId actor) =>
        Assert.IsType<LocalEncounterActionView>(new LocalWorldView(world, actor).EncounterActions);

    private static QueueEncounterAbilityIntent Intent(LocalEncounterActionView view, LocalEncounterAbilityOffer offer) =>
        new(view.EncounterId, view.TimingRevision, offer.AbilityDefinitionId, offer.TargetActorId);

    private static WorldActionCommand Captured(EncounterJourneyWorldFixture fixture, WorldSnapshot world,
        LocalEncounterActionView view, WorldActionIntent intent) => new(fixture.NextId(), fixture.Delivery.PrincipalId,
        fixture.Delivery.SessionId, fixture.Player, world.WorldId, world.BranchId, view.ActorRevision, intent);

    private static WorldSnapshot Activate(SqliteWorldAuthority authority, EncounterJourneyWorldFixture fixture)
    {
        var before = authority.ReadStateForHost();
        authority.Commit(fixture.Batch(before, WorldBatchKind.EncounterLifecycle, [fixture.Enter(before, fixture.FirstScenario)]));
        for (int stage = 0; stage < 2; stage++)
        {
            var state = authority.ReadStateForHost();
            authority.Commit(fixture.Batch(state, WorldBatchKind.EncounterLifecycle));
        }
        Assert.Equal(EncounterLifecyclePhase.Active, authority.ReadStateForHost().EncounterLifecycle!.Phase);
        return authority.ReadStateForHost();
    }

    private static ContentHash ControlPin(WorldSnapshot basis) => ContentHash.Compute(
        Encoding.UTF8.GetBytes("local.encounter.projection-control.v1").Concat(basis.ContentHash.ToByteArray()).ToArray());

    private static WorldSnapshot Authored(WorldSnapshot basis, ProgressionSnapshot? progression = null,
        EncounterActionSnapshot? actions = null) => new(basis.Inventory, basis.Space, basis.Residents, ControlPin(basis),
        basis.Calendar, 0, 0, basis.Dialogues, basis.Gathering, basis.Production, progression ?? basis.Progression,
        basis.Quests, basis.Construction, reservoirs: basis.Reservoirs, excavation: basis.Excavation,
        reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells, pacing: basis.Pacing,
        encounterActions: actions ?? basis.EncounterActions, encounterLifecycle: basis.EncounterLifecycle);

    private static void AssertPrivateFieldsAbsent(LocalEncounterActionView projected, WorldSnapshot world, EntityId hiddenAbility)
    {
        string json = JsonSerializer.Serialize(projected);
        foreach (string name in new[] { "Random", "Seed", "Hash", "SourceVersion", "RulesVersion", "ActionPin", "CommandId", "Pending", "Combatants" })
            Assert.DoesNotContain(name, json, StringComparison.Ordinal);
        Assert.DoesNotContain(JsonSerializer.Serialize(hiddenAbility), json, StringComparison.Ordinal);
        Assert.DoesNotContain(world.EncounterActions!.ContentHash.ToString(), json, StringComparison.Ordinal);
        Assert.DoesNotContain(world.ContentHash.ToString(), json, StringComparison.Ordinal);
    }
}
