using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class PairedRewardBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_actual_rewards_reserve_the_whole_batch_at_255_and_preserve_later_command_rollback(bool rejectSecond)
    {
        using var files = new StorageFiles();
        var fixture = new CareWorldFixture(rewardExperience: 1000, pairedRewardBoundary: true,
            invalidSecondRewardSpeed: rejectSecond);
        var journey = new PairedRewardBoundaryJourney(fixture);
        WorldSnapshot before;
        WorldSnapshot after;
        WorldStepBatch batch;
        WorldActionReceipt[] receipts;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var session = new CareWorldSession(authority);
            before = journey.Prepare(session);
            Assert.Equal(255UL, before.Progression!.Revision);
            Assert.Equal(255, before.ResourceHistory!.History.Batches.Count);
            Assert.Null(before.ResourceHistory.ArchiveAnchor);
            Assert.Equal(254, before.ResourceHistory.Evidence.OfType<WorldEncounterResourceEvidence>()
                .Count(value => value.Request.ActorId == fixture.Wolf));
            Assert.Equal(1, before.ResourceHistory.Evidence.OfType<WorldEncounterResourceEvidence>()
                .Count(value => value.Request.ActorId == fixture.Player));
            Assert.Equal(0, before.Progression.GetActor(fixture.Wolf).CurrentHp);
            Assert.Equal(2, before.Quests!.Participations.Count);
            Assert.All(before.Quests.Participations, value => Assert.Equal(ActorQuestStatus.Accepted, value.Status));

            batch = journey.CompletionBatch(before);
            Assert.Equal(2, batch.Actions.Count);
            Assert.Equal(batch.Actions.Select(value => value.ActorId).Order(), batch.Actions.Select(value => value.ActorId));
            var planner = new DeliveryQuestPlanner();
            var first = planner.Plan(before, before.Inventory, before.Production!, before.Progression,
                before.Quests, batch.Actions[0], before.Dialogues);
            Assert.True(first.Succeeded);
            var second = planner.Plan(before, first.CandidateInventory, before.Production!, first.CandidateProgression,
                first.CandidateQuests, batch.Actions[1], before.Dialogues);
            Assert.True(second.Succeeded);
            Assert.Equal(rejectSecond, (ulong)second.CandidateProgression.GetActor(batch.Actions[1].ActorId).Stats.Speed * 10 > uint.MaxValue);

            var preview = authority.Preview(batch);
            var sealedBoundary = Assert.IsType<WorldResourceArchiveSegment>(preview.SealedResourceSegment);
            Assert.Equal(before.Revision, sealedBoundary.Reference.BoundarySequence);
            Assert.Equal(WorldSnapshotCodec.Encode(before), sealedBoundary.Encode());
            Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            var decision = authority.Commit(batch);
            after = decision.Candidate;
            receipts = decision.Receipts.ToArray();
            Assert.Equal(preview.Candidate.ContentHash, after.ContentHash);
            Assert.Equal(sealedBoundary.Encode(), Assert.IsType<WorldResourceArchiveSegment>(decision.SealedResourceSegment).Encode());
            Assert.Equal(CommandReceiptStatus.Applied, receipts[0].Status);
            Assert.Equal(rejectSecond ? CommandReceiptStatus.Rejected : CommandReceiptStatus.Applied, receipts[1].Status);
            if (rejectSecond) Assert.Equal(WorldActionRejection.Unavailable, receipts[1].Rejection);

            var expected = rejectSecond ? first : second;
            Assert.Equal(expected.CandidateInventory.ContentHash, after.Inventory.ContentHash);
            Assert.Equal(expected.CandidateProgression.ContentHash, after.Progression!.ContentHash);
            Assert.Equal(expected.CandidateQuests.ContentHash, after.Quests!.ContentHash);
            int accepted = rejectSecond ? 1 : 2;
            Assert.Equal(255UL + (ulong)accepted, after.Progression.Revision);
            Assert.Equal(accepted, after.ResourceHistory!.History.Batches.Count);
            Assert.Equal(255UL, after.ResourceHistory.History.Basis.Revision);
            Assert.Equal(before.Revision, after.ResourceHistory.History.BasisWorldSequence);
            Assert.Equal(before.ContentHash, after.ResourceHistory.ArchiveAnchor!.Reference.BoundaryHash);
            Assert.Equal(batch.Actions.Take(accepted).Select(value => value.CommandId),
                after.ResourceHistory.History.Batches.Select(value => value.CommandId));
            Assert.Equal(Enumerable.Range(0, accepted).Select(value => (uint)value),
                after.ResourceHistory.History.Batches.Select(value => value.CommandOrdinal));
            Assert.All(after.ResourceHistory.History.Batches, value => Assert.Equal(after.Revision, value.WorldSequence));
            Assert.All(after.ResourceHistory.Evidence, value => Assert.IsType<WorldRewardResourceEvidence>(value));
            Assert.Equal(before.Calendar, after.Calendar);
            Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
            Assert.Equal(before.Space.Actors, after.Space.Actors);
            Assert.Equal(before.Space.Regions.Select(value => value.ComputeContentHash()),
                after.Space.Regions.Select(value => value.ComputeContentHash()));
            Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
            Assert.Equal(before.Care!.ContentHash, after.Care!.ContentHash);
            Assert.Equal(before.EncounterLifecycle!.RetainBoundary().ContentHash, after.EncounterLifecycle!.ContentHash);
            Assert.Equal(before.Reservoirs!.Geometries, after.Reservoirs!.Geometries);
            Assert.Equal(before.Reservoirs.Projections.Select(value => (value.Geometry.ReservoirId,
                    value.ExistingUnits, value.ReservedIncomingUnits, value.ActualFill.Volume, value.ProjectedFill.Volume)),
                after.Reservoirs.Projections.Select(value => (value.Geometry.ReservoirId,
                    value.ExistingUnits, value.ReservedIncomingUnits, value.ActualFill.Volume, value.ProjectedFill.Volume)));
            Assert.Equal(after.Inventory.ContentHash, after.Reservoirs.Capacity.Inventory.ContentHash);
            Assert.Equal(before.LiquidCells!.Portions, after.LiquidCells!.Portions);
            Assert.Equal(before.LiquidCells.Boundary, after.LiquidCells.Boundary);
            Assert.Equal(before.LiquidCells.Revision, after.LiquidCells.Revision);
            Assert.Equal(after.Reservoirs.ContentHash, after.LiquidCells.ReservoirHash);
            Assert.Equal(before.Inventory.Custody.Lots.Sum(value => (decimal)value.Quantity),
                after.Inventory.Custody.Lots.Sum(value => (decimal)value.Quantity));
            Assert.Equal(before.Progression.Currency.Accounts.Sum(value => (decimal)value.Balance),
                after.Progression.Currency.Accounts.Sum(value => (decimal)value.Balance));
            for (int index = 0; index < batch.Actions.Count; index++)
            {
                var command = batch.Actions[index];
                var quest = before.Quests.FindDefinition(((CompleteDeliveryQuestIntent)command.Intent).QuestId)!;
                bool applied = index < accepted;
                var participation = after.Quests.FindParticipation(command.ActorId, quest.DefinitionId)!;
                Assert.Equal(applied ? ActorQuestStatus.Completed : ActorQuestStatus.Accepted, participation.Status);
                Assert.Equal(before.Progression.Currency.GetActorAccount(command.ActorId).Balance
                    + (applied ? quest.Reward.CurrencyAmount : 0), after.Progression.Currency.GetActorAccount(command.ActorId).Balance);
                Assert.Equal(applied ? quest.Quantity : 0UL,
                    after.Inventory.Custody.Lots.Where(value => value.ReservedBy == quest.MaterialReservationId)
                        .Aggregate(0UL, (sum, value) => sum + value.Quantity));
                if (!applied)
                {
                    Assert.Equal(before.Progression.GetActor(command.ActorId).Revision, after.Progression.GetActor(command.ActorId).Revision);
                    var source = before.GetResident(command.ActorId).InventoryCustodianId;
                    Assert.Equal(before.Inventory.Custody.GetStorage(source), after.Inventory.Custody.GetStorage(source));
                    Assert.Equal(before.Inventory.Custody.Lots.Where(value => value.CustodianId == source)
                            .Select(value => (value.LotId, value.Revision, value.Quantity, value.Source, value.ReservedBy)),
                        after.Inventory.Custody.Lots.Where(value => value.CustodianId == source)
                            .Select(value => (value.LotId, value.Revision, value.Quantity, value.Source, value.ReservedBy)));
                }
            }
        }
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            var archived = authority.ReadResourceArchiveForHost(0);
            Assert.Equal(WorldSnapshotCodec.Encode(before), archived.Segment.Encode());
            Assert.Equal(after.Revision, archived.PublicationSequence);
            Assert.Equal(before.ContentHash, authority.ReadRecordedStateForHost(before.Revision).StateHash);
            foreach (var command in batch.Actions)
            {
                var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, fixture.DeliveryFor(command.ActorId)));
                Assert.Equal(WorldReceiptCodecV1.Encode(receipts.Single(value => value.CommandId == command.CommandId)),
                    WorldReceiptCodecV1.Encode(receipt));
            }
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(batch));
            Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(1UL, authority.InspectForHost().Journal.ResourceArchiveCount);
        }
    }
}

internal sealed class PairedRewardBoundaryJourney(CareWorldFixture fixture)
{
    internal WorldSnapshot Prepare(CareWorldSession session)
    {
        var state = fixture.BeginCombat(session);
        for (int step = 0; state.Progression!.Revision < 254; step++)
        {
            Assert.True(step < 4096, "Actual bite/wait boundaries must reach the fixed finite resource count.");
            state = fixture.NextCombatBoundary(session, playerAttacks: false);
        }
        for (int step = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; step++)
        {
            Assert.True(step < 128, "A ready source-backed strike must end the calibrated encounter.");
            var timing = state.Pacing!.Encounter!;
            var due = WorldEncounterActions.DueActors(state);
            var choices = due.Where(actor => !timing.Decisions.Any(choice => choice.ActorId == actor.ActorId))
                .Select(actor => fixture.Command(state, actor.ActorId, actor.ActorId == fixture.Player
                    ? new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision,
                        state.EncounterActions!.Combatants.Single(value => value.ActorId == fixture.Player).AbilityIds.Single(), fixture.Wolf)
                    : new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision))).ToArray();
            var kind = choices.Length != 0 ? WorldBatchKind.EncounterDecision
                : due.Count != 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance;
            state = CareWorldFixture.Commit(session, fixture.Batch(state, kind, choices)).Candidate;
        }
        Assert.Equal(255UL, state.Progression!.Revision);
        state = CareWorldFixture.Commit(session, fixture.Batch(state, WorldBatchKind.EncounterLifecycle,
            [fixture.Command(state, fixture.Player, new ReleaseEncounterIntent(state.EncounterActions!.Scope.EncounterId,
                state.EncounterActions.ContentHash))])).Candidate;
        for (int step = 0; !WorldActorAvailability.CanAct(state, fixture.Player) || !WorldActorAvailability.CanAct(state, fixture.Helper); step++)
        {
            Assert.True(step < 32);
            state = CareWorldFixture.Commit(session, fixture.Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
        }
        state = CareWorldFixture.Commit(session, fixture.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Command(state, fixture.Player, new SpeakToActorIntent(fixture.Helper, state.Space.GetActor(fixture.Helper).Revision)),
             fixture.Command(state, fixture.Helper, new SpeakToActorIntent(fixture.Player, state.Space.GetActor(fixture.Player).Revision))])).Candidate;
        var accept = state.Quests!.Definitions.Select(quest =>
        {
            var actor = quest.GiverActorId == fixture.Helper ? fixture.Player : fixture.Helper;
            return fixture.Command(state, actor, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version,
                quest.GiverActorId, state.Space.GetActor(quest.GiverActorId).Revision, 0, state.Progression!.GetActor(actor).Revision));
        }).ToArray();
        return CareWorldFixture.Commit(session, fixture.Batch(state, WorldBatchKind.HeldInteraction, accept)).Candidate;
    }

    internal WorldStepBatch CompletionBatch(WorldSnapshot state)
    {
        var commands = state.Quests!.Definitions.Select(quest =>
        {
            var actor = quest.GiverActorId == fixture.Helper ? fixture.Player : fixture.Helper;
            var storage = state.GetResident(actor).InventoryCustodianId;
            var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == storage && value.Definition == quest.Item);
            return fixture.Command(state, actor, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version,
                quest.GiverActorId, state.Space.GetActor(quest.GiverActorId).Revision, 1,
                state.Progression!.GetActor(actor).Revision, state.Inventory.Custody.GetStorage(storage).Revision,
                [new(lot.LotId, lot.Revision, quest.Quantity)]));
        });
        return fixture.Batch(state, WorldBatchKind.HeldInteraction, commands);
    }
}
