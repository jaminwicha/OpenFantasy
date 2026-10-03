using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;

public sealed class EffectiveControlContinuationTests
{
    [Fact]
    public void Accepted_retreat_pays_each_sample_after_revocation_and_returns_control_only_at_safe_boundary()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        WorldSnapshot final;
        WorldActionCommand command;
        WorldActionReceipt receipt;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            EffectiveControlCombatTests.EnterDelegatedEncounter(authority, fixture);
            var state = EffectiveControlCombatTests.ReachOwnerDecision(authority, fixture);
            var advice = WorldEncounterRetreatAdvisor.Advise(state, fixture.Owner);
            Assert.True(advice.IsFound);
            var timing = state.Pacing!.Encounter!;
            var intent = new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision,
                advice.Destination, advice.Route);
            command = fixture.Delegated(state, intent);
            var selected = authority.Commit(fixture.Commands.Batch(state, WorldBatchKind.EncounterDecision, [command]));
            receipt = Assert.Single(selected.Receipts);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(command.ContentHash, selected.Candidate.Retreat!.Active!.Command.ContentHash);
            var revoked = fixture.Revoke(authority);
            Assert.Null(revoked.EffectiveControl!.Find(fixture.Owner)!.Witness);
            Assert.NotNull(revoked.Retreat!.Active);
            Assert.False(WorldControllerBoundaries.CanRestoreHuman(revoked, fixture.Owner));
            Assert.Throws<AuthorityDispatchException>(() => fixture.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected));
            Assert.Same(revoked, authority.ReadStateForHost());

            for (int boundary = 0; authority.ReadStateForHost().Retreat!.Active is not null; boundary++)
            {
                Assert.True(boundary < 2_000, "The accepted route retains the existing finite retreat boundary budget.");
                EffectiveControlCombatTests.ResolveOneWithOtherActorsWaiting(authority, fixture);
            }
            state = authority.ReadStateForHost();
            var outcome = state.Retreat!.Last!;
            Assert.Equal(EncounterRetreatStatus.Disengaged, outcome.Status);
            Assert.Equal(EncounterRetreatReason.None, outcome.Reason);
            Assert.Equal(command.CommandId, outcome.CommandId);
            Assert.Equal(intent.Route.Count, outcome.SamplesTaken);
            Assert.Equal(intent.Destination, state.Space.GetActor(fixture.Owner).Feet);
            ulong sampleDelay = EncounterRetreatRules.SampleDelay(timing.GetActor(fixture.Owner).NormalizedSpeed);
            Assert.Equal(checked((ulong)intent.Route.Count * sampleDelay), outcome.ReadinessCharged);
            Assert.Equal(sampleDelay, state.EncounterLifecycle!.GetDebt(fixture.Owner).RemainingDelay);
            Assert.Equal(EncounterLifecyclePhase.Exploration, state.EncounterLifecycle.Phase);
            final = fixture.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected);
            Assert.Equal(WorldControllerMode.Human, final.Controllers!.Get(fixture.Owner).Mode);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, fixture.Delivery(fixture.Grantee)))));
            Assert.Same(final, authority.ReadStateForHost());
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(receipt.CommandHash, reopened.FindReceipt(command, fixture.Delivery(fixture.Grantee))!.CommandHash);
    }

    [Fact]
    public void Delegated_patient_acknowledgment_survives_revocation_and_helper_pays_one_actual_dose()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        WorldSnapshot final;
        WorldActionCommand request;
        WorldActionReceipt receipt;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            fixture.Grant(authority, WorldControlCapabilities.Recovery);
            var state = authority.ReadStateForHost();
            var scenario = state.EncounterLifecycle!.GetScenario(fixture.Journey.FirstScenario);
            fixture.Journey.WalkTo(authority, fixture.Owner, fixture.Journey.Beside(state, fixture.Owner, scenario.TargetActorId));
            state = authority.ReadStateForHost();
            fixture.Journey.WalkTo(authority, fixture.Other, fixture.Journey.Beside(state, fixture.Other, fixture.Owner));
            state = fixture.Journey.EnterAndFight(authority, fixture.Journey.FirstScenario, patientWaits: true);
            Assert.Equal(0, state.Progression!.GetActor(fixture.Owner).CurrentHp);
            fixture.LoseAndExpire(authority);
            state = fixture.Acquire(authority);
            var agreement = Assert.Single(state.Care!.Agreements);
            request = fixture.Delegated(state, new AcknowledgeCareIntent(agreement.AgreementId,
                state.EncounterActions!.Scope.EncounterId, state.EncounterLifecycle!.Revision, state.EncounterActions.ContentHash));
            var accepted = authority.Commit(fixture.Commands.Batch(state, WorldBatchKind.Care, [request]));
            receipt = Assert.Single(accepted.Receipts);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            var pending = accepted.Candidate.Care!.Pending!;
            Assert.Equal(request.CommandId, pending.CommandId);
            var revoked = fixture.Revoke(authority);
            Assert.Equal(pending, revoked.Care!.Pending);
            Assert.Equal(state.Progression!.ContentHash, revoked.Progression!.ContentHash);
            Assert.Equal(state.Inventory.ContentHash, revoked.Inventory.ContentHash);
            Assert.Equal(state.Production!.TotalConsumedItemCount, revoked.Production!.TotalConsumedItemCount);
            Assert.Null(WorldActorCommandAuthority.AuthorizeSelection(revoked, request));
            var remedy = fixture.Commands.Command(revoked, fixture.Other,
                Assert.IsType<AdministerRemedyIntent>(WorldCareActions.PrepareRemedy(revoked)));
            final = fixture.Apply(authority, WorldBatchKind.Care, remedy);
            Assert.Null(final.Care!.Pending);
            var treatment = Assert.Single(final.Care.Treatments);
            Assert.Equal(pending.AcknowledgmentId, treatment.AcknowledgmentId);
            Assert.Equal(remedy.CommandId, treatment.CommandId);
            Assert.Equal(state.Production.TotalConsumedItemCount + 1, final.Production!.TotalConsumedItemCount);
            Assert.True(final.Progression!.GetActor(fixture.Owner).CurrentHp > 0);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(authority.FindReceipt(request, fixture.Delivery(fixture.Grantee)))));
            Assert.NotNull(authority.FindReceipt(remedy, fixture.Delivery(fixture.Other)));
            Assert.Same(final, authority.ReadStateForHost());
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(receipt.CommandHash, reopened.FindReceipt(request, fixture.Delivery(fixture.Grantee))!.CommandHash);
    }
}
