using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;

public sealed class EffectiveControlCombatTests
{
    [Fact]
    public void Accepted_delegated_attack_resolves_once_after_revoke_with_original_principal_costs_history_and_receipt()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        WorldSnapshot final;
        WorldActionCommand attack;
        WorldActionReceipt receipt;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var state = EnterDelegatedEncounter(authority, fixture);
            state = ReachOwnerDecision(authority, fixture);
            var targetId = state.EncounterLifecycle!.GetScenario(fixture.Journey.FirstScenario).TargetActorId;
            var ability = state.EncounterActions!.GetCombatant(fixture.Owner).AbilityIds
                .Select(state.EncounterActions.GetAbility).Single(value => value.Key == EncounterFarmAdmission.StrikeKey);
            var intent = new QueueEncounterAbilityIntent(state.Pacing!.Encounter!.EncounterId,
                state.Pacing.Encounter.Revision, ability.DefinitionId, targetId);
            attack = fixture.Delegated(state, intent);
            Assert.NotNull(WorldActorCommandAuthority.AuthorizeSelection(state, attack));
            Assert.Equal(WorldActionRejection.None, WorldEncounterActions.EvaluateAbility(state, fixture.Owner, intent));
            var before = state.Progression!.GetActor(fixture.Owner);
            var targetBefore = state.Progression.GetActor(intent.TargetActorId);
            var queued = authority.Commit(fixture.Commands.Batch(state, WorldBatchKind.EncounterDecision, [attack]));
            receipt = Assert.Single(queued.Receipts);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(fixture.Owner, attack.ActorId);
            Assert.Equal(fixture.Delivery(fixture.Grantee).PrincipalId, attack.PrincipalId);
            Assert.Equal(attack.PrincipalId, receipt.PrincipalId);
            Assert.Equal(attack.ContentHash, receipt.CommandHash);
            Assert.Equal(before.CurrentMp, queued.Candidate.Progression!.GetActor(fixture.Owner).CurrentMp);
            Assert.Contains(queued.Candidate.EncounterActions!.Pending, command => command.ContentHash == attack.ContentHash);
            var revoked = fixture.Revoke(authority);
            Assert.Null(revoked.EffectiveControl!.Find(fixture.Owner)!.Witness);
            Assert.Contains(revoked.EncounterActions!.Pending, command => command.ContentHash == attack.ContentHash);
            Assert.Null(WorldActorCommandAuthority.AuthorizeSelection(revoked, attack));
            Assert.False(WorldControllerBoundaries.CanRestoreHuman(revoked, fixture.Owner));
            Assert.Throws<AuthorityDispatchException>(() => fixture.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected));
            Assert.Same(revoked, authority.ReadStateForHost());

            for (int boundary = 0; ; boundary++)
            {
                Assert.True(boundary < 64, "The already accepted attack must reach its ordinary resolution within bounded decisions.");
                state = authority.ReadStateForHost();
                if (state.EncounterActions!.LastResolution is { } result && result.DecisionCommandId == attack.CommandId)
                {
                    Assert.Equal(EncounterActionResolutionStatus.Damaged, result.Status);
                    Assert.True(result.Damage > 0);
                    Assert.Equal(targetBefore.CurrentHp - (int)result.Damage, state.Progression!.GetActor(intent.TargetActorId).CurrentHp);
                    Assert.Equal(before.CurrentMp - (int)ability.ManaCost, state.Progression.GetActor(fixture.Owner).CurrentMp);
                    Assert.Equal(ability.ManaCost, result.ManaSpent);
                    Assert.True(result.Delay > 0);
                    Assert.Equal(result.Delay, state.Pacing!.Encounter!.GetActor(fixture.Owner).RemainingDelay);
                    Assert.DoesNotContain(state.EncounterActions.Pending, command => command.CommandId == attack.CommandId);
                    break;
                }
                ResolveOneWithOtherActorsWaiting(authority, fixture);
            }
            final = fixture.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected);
            Assert.Equal(WorldControllerMode.Human, final.Controllers!.Get(fixture.Owner).Mode);
            Assert.False(final.Inventory.CanControl(fixture.Delivery(fixture.Grantee), fixture.Owner));
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(authority.FindReceipt(attack, fixture.Delivery(fixture.Grantee)))));
            Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(attack, fixture.Delivery(fixture.Owner)));
            var altered = new WorldActionCommand(attack.CommandId, attack.PrincipalId, attack.OriginSessionId,
                attack.ActorId, attack.WorldId, attack.BranchId, attack.ExpectedActorRevision + 1, attack.Intent, attack.ControlWitness);
            Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(altered, fixture.Delivery(fixture.Grantee)));
            var newDecision = fixture.Delegated(final, attack.Intent, attack.ControlWitness);
            Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(newDecision, fixture.Delivery(fixture.Grantee)));
            Assert.False(authority.IsIdentityOccupiedForHost(newDecision.CommandId));
            Assert.Same(final, authority.ReadStateForHost());
        }
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(attack, fixture.Delivery(fixture.Grantee)))));
        }
        using var database = WorldDatabase.Open(files.Path, false);
        var stored = Assert.Single(database.ReadCommandOutcomes(), row => row.CommandId == attack.CommandId);
        var retained = WorldActionCodec.Decode(stored.Command);
        Assert.Equal(fixture.Delivery(fixture.Grantee).PrincipalId, retained.PrincipalId);
        Assert.Equal(fixture.Owner, retained.ActorId);
        Assert.Equal(attack.ControlWitness, retained.ControlWitness);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), stored.Receipt);
    }

    internal static WorldSnapshot EnterDelegatedEncounter(SqliteWorldAuthority authority, EffectiveControlFixture fixture)
    {
        var state = authority.ReadStateForHost();
        var scenario = state.EncounterLifecycle!.GetScenario(fixture.Journey.FirstScenario);
        fixture.Journey.WalkTo(authority, fixture.Owner, fixture.Journey.Beside(state, fixture.Owner, scenario.TargetActorId));
        fixture.Grant(authority, WorldControlCapabilities.All);
        fixture.LoseAndExpire(authority);
        state = fixture.Acquire(authority);
        var enter = fixture.Delegated(state, fixture.Commands.Enter(state, scenario.DefinitionId).Intent);
        fixture.Apply(authority, WorldBatchKind.EncounterLifecycle, enter);
        for (int transition = 0; authority.ReadStateForHost().EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; transition++)
        {
            Assert.True(transition < 3);
            state = authority.ReadStateForHost();
            authority.Commit(fixture.Commands.Batch(state, WorldBatchKind.EncounterLifecycle));
        }
        return authority.ReadStateForHost();
    }

    internal static WorldSnapshot ReachOwnerDecision(SqliteWorldAuthority authority, EffectiveControlFixture fixture)
    {
        for (int boundary = 0; ; boundary++)
        {
            Assert.True(boundary < 64);
            var state = authority.ReadStateForHost();
            if (WorldEncounterActions.DueActors(state).Any(actor => actor.ActorId == fixture.Owner)) return state;
            ResolveOneWithOtherActorsWaiting(authority, fixture);
        }
    }

    internal static void ResolveOneWithOtherActorsWaiting(SqliteWorldAuthority authority, EffectiveControlFixture fixture)
    {
        var state = authority.ReadStateForHost();
        var chosen = state.Pacing!.Encounter!.Decisions.Select(choice => choice.ActorId).ToHashSet();
        // This is a managed authority journey with explicit legal opponent Wait decisions, not a native AI-policy proof.
        var waiting = WorldEncounterActions.DueActors(state).Where(actor => actor.ActorId != fixture.Owner
            && !chosen.Contains(actor.ActorId) && !WorldEncounterRetreats.HoldsActor(state, actor.ActorId))
            .Select(actor => fixture.Commands.Wait(state, actor.ActorId)).ToArray();
        if (waiting.Length != 0)
        {
            var selected = authority.Commit(fixture.Commands.Batch(state, WorldBatchKind.EncounterDecision, waiting));
            Assert.All(selected.Receipts, item => Assert.Equal(WorldActionRejection.None, item.Rejection));
            return;
        }
        var kind = WorldEncounterActions.DueActors(state).Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
        authority.Commit(fixture.Commands.Batch(state, kind));
    }
}
