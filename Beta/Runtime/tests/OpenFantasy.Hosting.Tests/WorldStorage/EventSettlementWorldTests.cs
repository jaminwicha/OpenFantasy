using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class EventSettlementWorldTests
{
    [Fact]
    public void Event_interpretation_requires_fresh_admission_and_preserves_the_default_coupled_fixture()
    {
        var original = new CoupledSettlementWorldFixture();
        var selected = new CoupledSettlementWorldFixture(eventOrdered: true);
        Assert.Equal(WorldSnapshotCodec.Encode(original.Genesis), WorldSnapshotCodec.Encode(selected.LegacyGenesis17));
        Assert.Equal((ushort)17, original.Genesis.ProfileVersion);
        Assert.Equal(ConstructionDescentMode.CommonTrajectory, original.Genesis.DescentMode);
        Assert.Equal((ushort)18, selected.Genesis.ProfileVersion);
        Assert.Equal(ConstructionDescentMode.EventOrdered, selected.Genesis.DescentMode);
        Assert.Equal(WorldSnapshotCodec.Encode(WorldEventSettlementAdmission.CreateGenesis(original.Genesis)),
            WorldSnapshotCodec.Encode(selected.Genesis));
        Assert.NotEqual(original.Genesis.AdmissionVersion, selected.Genesis.AdmissionVersion);
        Assert.Throws<ArgumentException>(() => WorldEventSettlementAdmission.CreateGenesis(selected.Genesis));
        using var files = new StorageFiles();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, original.Genesis))
        {
            WorldSnapshot played = authority.Commit(original.Actions.Batch(original.Genesis, [], [])).Candidate;
            Assert.Throws<ArgumentException>(() => WorldEventSettlementAdmission.CreateGenesis(played));
        }
        Assert.Equal(WorldSnapshotCodec.Encode(original.Genesis), WorldSnapshotCodec.Encode(selected.LegacyGenesis17));
    }

    [Fact]
    public void Funded_event_interpreter_reopens_each_work_prefix_and_publishes_one_common_drop_receipt_and_source()
    {
        var fixture = new CoupledSettlementWorldFixture(eventOrdered: true);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        Assert.Equal((ushort)18, before.ProfileVersion);
        Assert.Equal(ConstructionDescentMode.EventOrdered, before.DescentMode);
        Assert.Throws<ArgumentException>(() => WorldEventSettlementAdmission.CreateGenesis(before));
        // This actual journey qualifies the live event interpreter on a common drop. Unequal
        // supplied-domain trajectories are not a claim that this single-cell action can reach them.
        ConstructionDescentAssessment assessment = Assess(before, fixture.Site.Cell);
        Assert.False(assessment.IsFailed, assessment.FailureReason);
        Assert.Equal(3, assessment.ImpactDemands.Count);
        Assert.All(assessment.ImpactDemands, demand => Assert.Equal(256L, demand.DropSubunits));
        WorldStepBatch trigger = RoundTrip(fixture.NextExcavation(before));
        WorldActionCommand command = Assert.Single(trigger.Actions);
        List<WorldSnapshot> history = [fixture.Genesis, before];
        List<WorldStepBatch> continuations = [];
        var evidence = new EventSettlementEvidence(files.Path, fixture, before, trigger);
        WorldSnapshot state;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepDecision start = authority.Commit(trigger);
            state = start.Candidate;
            Assert.Empty(start.Receipts);
            Assert.Equal(new[] { WorldCommittedIdentityKind.CommitCause, WorldCommittedIdentityKind.Command },
                start.Identities.Select(value => value.Kind).Order());
            Assert.Null(authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(state, fixture.Player)));
        }
        PendingExcavation initial = Assert.IsType<PendingExcavation>(state.PendingExcavation);
        Assert.Equal(before.ContentHash, initial.BasisHash);
        Assert.Equal(before.Revision, initial.BasisRevision);
        Assert.Equal(trigger.CauseId, initial.TriggerCauseId);
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(initial.Command));
        Assert.Equal(0UL, initial.Resolution.CompletedWorkUnits);
        Assert.False(initial.Resolution.IsComplete);
        Assert.Equal(assessment.MaximumTotalWorkUnits, initial.Resolution.MaximumTotalWorkUnits);
        AssertUnchangedPending(before, state);
        history.Add(state);
        WorldActionReceipt? receipt = null;
        for (ulong count = 0; state.PendingExcavation is not null; count++)
        {
            Assert.True(count < initial.MaximumContinuationCount);
            PendingExcavation previous = state.PendingExcavation!;
            // This fixture has small closed databases and finite real work. Retaining each boundary
            // lets the independent Host probe check the complete accepted journal chain.
            evidence.Copy($"pending-{count}.sqlite");
            using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
            {
                Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
                Assert.Null(authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(state, fixture.Player)));
                if (count == 0)
                {
                    Assert.Throws<AuthorityDispatchException>(() => authority.Commit(trigger));
                    Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Actions.Batch(state, [], [])));
                    Assert.Equal(state.ContentHash, authority.InspectForHost().State.ContentHash);
                }
                WorldStepBatch continuation = RoundTrip(fixture.Continue(state));
                continuations.Add(continuation);
                WorldStepDecision next = authority.Commit(continuation);
                state = next.Candidate;
                if (state.PendingExcavation is { } pending)
                {
                    Assert.Empty(next.Receipts);
                    Assert.Equal(previous.ContinuationCount + 1, pending.ContinuationCount);
                    Assert.Equal(previous.Resolution.CompletedWorkUnits + 1, pending.Resolution.CompletedWorkUnits);
                    Assert.Equal(initial.ResultReservations, pending.ResultReservations);
                    Assert.Equal(WorldCommittedIdentityKind.CommitCause, Assert.Single(next.Identities).Kind);
                    AssertUnchangedPending(before, state);
                }
                else
                {
                    receipt = Assert.Single(next.Receipts);
                    Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                    Assert.Equal(command.CommandId, receipt.CommandId);
                    Assert.Equal(state.Revision, receipt.CommitSequence);
                    Assert.Contains(receipt.ResultId, initial.ResultReservations);
                    Assert.Equal(new[] { initial.ReservedSourceId }, next.Identities
                        .Where(value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity).Select(value => value.Identity));
                }
            }
            history.Add(state);
        }
        Assert.True(continuations.Count > 2);
        WorldActionReceipt terminal = Assert.IsType<WorldActionReceipt>(receipt);
        AssertApplied(before, state, assessment.Result, fixture, terminal);
        AssertCompletionCapacity(initial, trigger, continuations[0], terminal);
        evidence.Complete(before, state, trigger, continuations, terminal, history, assessment);
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldReceiptCodecV1.Encode(terminal), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
                authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(state, fixture.Player)))));
            foreach (WorldSnapshot recorded in history.DistinctBy(value => value.Revision).Reverse())
            {
                Assert.Equal(WorldSnapshotCodec.Encode(recorded), WorldSnapshotCodec.Encode(
                    authority.ReadRecordedStateForHost(recorded.Revision).State));
            }
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(trigger));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(continuations[^1]));
            Assert.Equal(state.ContentHash, authority.InspectForHost().State.ContentHash);
        }
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(4, database.SchemaVersion);
            StoredWorldCommandOutcome outcome = Assert.Single(database.ReadCommandOutcomes(before.Revision));
            Assert.Equal(WorldActionCodec.Encode(command), outcome.Command);
            Assert.Equal(WorldReceiptCodecV1.Encode(terminal), outcome.Receipt);
            var journal = database.ReadJournal(before.Revision).ToArray();
            Assert.Equal(continuations.Count + 1, journal.Length);
            Assert.Equal(WorldCommitKind.AdvancingStep, journal[0].Kind);
            Assert.All(journal.Skip(1), row => Assert.Equal(WorldCommitKind.ConsequenceContinuation, row.Kind));
            Assert.All(journal.SkipLast(1), row => Assert.Equal(before.AdvancingSteps, row.AdvancingSteps));
            Assert.Equal(before.AdvancingSteps + 1, journal[^1].AdvancingSteps);
            Assert.Single(database.ReadIdentities(), row => row.Identity == command.CommandId && row.Kind == WorldIdentityKind.Command);
            Assert.Single(database.ReadIdentities(), row => row.Identity == initial.ReservedSourceId && row.Kind == WorldIdentityKind.SpawnedEntity);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Successful_geometric_assessment_does_not_supply_the_excavators_spent_labor()
    {
        var fixture = new CoupledSettlementWorldFixture(eventOrdered: true);
        using var files = new StorageFiles();
        WorldSnapshot state = fixture.PrepareBeforeExcavation(files.Path);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        state = fixture.Actions.Act(authority, state, fixture.Player, new ClimbIntent(0, -256, 256));
        for (int step = 0; state.Gathering!.GetActorWork(fixture.Player).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 40);
            state = authority.Commit(fixture.Actions.Batch(state, [], [])).Candidate;
        }
        state = fixture.Actions.Act(authority, state, fixture.Player, new ClimbIntent(0, 256, -256));
        Assert.Equal(CoupledSettlementWorldFixture.Stance, state.Space.GetActor(fixture.Player).Feet);
        Assert.True(state.Gathering!.GetActorWork(fixture.Player).Effort < GatherRulesV1.EffortPerSwing);
        byte[] before = WorldSnapshotCodec.Encode(state);
        ConstructionDescentAssessment successful = Assess(state, fixture.Site.Cell);
        Assert.False(successful.IsFailed, successful.FailureReason);
        Assert.Equal(before, WorldSnapshotCodec.Encode(state));
        WorldStepDecision refused = authority.Commit(fixture.NextExcavation(state));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
        Assert.Null(refused.Candidate.PendingExcavation);
        AssertSingleInterval(state, refused.Candidate);
        Assert.Equal(state.Inventory.ContentHash, refused.Candidate.Inventory.ContentHash);
        Assert.Equal(state.ConstructionPhysical!.ContentHash, refused.Candidate.ConstructionPhysical!.ContentHash);
        Assert.Equal(state.Excavation!.ContentHash, refused.Candidate.Excavation!.ContentHash);
        Assert.Equal(state.Space.Regions.Select(region => region.ComputeContentHash()),
            refused.Candidate.Space.Regions.Select(region => region.ComputeContentHash()));
        Assert.DoesNotContain(refused.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
    }

    private static ConstructionDescentAssessment Assess(WorldSnapshot world, CellCoordinate cell)
    {
        ConstructionDescentAssessment result = ConstructionDescentAssessment.Begin(world,
            world.Space.Regions.Select(region => region.Bounds.Contains(cell) ? region.Excavate(region.Revision, [cell]) : region));
        while (!result.IsComplete)
        {
            Assert.True(result.CompletedWorkUnits < (ulong)result.MaximumTotalWorkUnits);
            result = result.Advance(256);
        }
        return result;
    }

    private static WorldStepBatch RoundTrip(WorldStepBatch batch)
    {
        byte[] bytes = WorldStepCodec.Encode(batch);
        WorldStepBatch result = WorldStepCodec.Decode(bytes);
        Assert.Equal(bytes, WorldStepCodec.Encode(result));
        Assert.Equal((ushort)18, result.ProfileVersion);
        return result;
    }

    private static void AssertUnchangedPending(WorldSnapshot before, WorldSnapshot pending)
    {
        Assert.NotNull(pending.PendingExcavation);
        Assert.Equal((ushort)18, pending.ProfileVersion);
        var stable = new WorldSnapshot(pending.Inventory, pending.Space, pending.Residents, pending.AdmissionVersion,
            pending.Calendar, before.Revision, pending.AdvancingSteps, pending.Dialogues, pending.Gathering,
            pending.Production, pending.Progression, pending.Quests, pending.Construction, excavation: pending.Excavation,
            constructionPhysical: pending.ConstructionPhysical, structuralPolicy: pending.StructuralPolicy,
            structuralSettlementPolicy: pending.StructuralSettlementPolicy, consequencePolicy: pending.ConsequencePolicy,
            coupledSettlementPolicy: pending.CoupledSettlementPolicy, descentMode: pending.DescentMode);
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(stable));
        byte[] bytes = WorldSnapshotCodec.Encode(pending);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
    }

    private static void AssertApplied(WorldSnapshot before, WorldSnapshot after, ConstructionSettlementResult expected,
        CoupledSettlementWorldFixture fixture, WorldActionReceipt receipt)
    {
        Assert.Equal((ushort)18, after.ProfileVersion);
        Assert.Null(after.PendingExcavation);
        AssertSingleInterval(before, after);
        Assert.Equal(before.AdmissionVersion, after.AdmissionVersion);
        Assert.Equal(before.StructuralPolicy!.ContentHash, after.StructuralPolicy!.ContentHash);
        Assert.Equal(before.StructuralSettlementPolicy!.ContentHash, after.StructuralSettlementPolicy!.ContentHash);
        Assert.Equal(before.ConsequencePolicy!.ContentHash, after.ConsequencePolicy!.ContentHash);
        Assert.Equal(before.CoupledSettlementPolicy!.ContentHash, after.CoupledSettlementPolicy!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(ConstructionPhysicalCodecV1.Encode(expected.Physical), ConstructionPhysicalCodecV1.Encode(after.ConstructionPhysical!));
        foreach (ConstructionMemberState member in before.ConstructionPhysical!.Members)
        {
            ConstructionMemberState next = after.ConstructionPhysical!.GetMember(member.StructureId, member.MemberIndex);
            Assert.Equal(member.Revision + 1, next.Revision);
            Assert.Equal(member.ConditionParts, next.ConditionParts);
            Assert.Equal(member.Displacement.Offset(0, 0, -256), next.Displacement);
            Assert.Equal(before.ConstructionPhysical.GetMemberReference(member.StructureId, member.MemberIndex),
                after.ConstructionPhysical.GetMemberReference(member.StructureId, member.MemberIndex));
        }
        foreach (ActorPresence actor in before.Space.Actors)
        {
            ActorPresence next = after.Space.GetActor(actor.ActorId);
            Assert.Equal(expected.Space.GetActor(actor.ActorId).Feet, next.Feet);
            Assert.Equal(expected.Space.GetActor(actor.ActorId).Revision, next.Revision);
            Assert.Equal(actor.Binding, next.Binding);
            Assert.Equal(actor.Condition.ComputeContentHash(), next.Condition.ComputeContentHash());
            uint spent = actor.ActorId == fixture.Player ? GatherRulesV1.EffortPerSwing : 0;
            Assert.Equal(Math.Min(GatherRulesV1.MaximumEffort,
                before.Gathering!.GetActorWork(actor.ActorId).Effort - spent + GatherRulesV1.EffortPerAdvancingStep),
                after.Gathering!.GetActorWork(actor.ActorId).Effort);
        }
        Assert.Equal(1UL, Assert.Single(after.Excavation!.Sites).CompletedWork);
        CountedSourceLot issued = Assert.Single(after.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        Assert.Equal(EntityId.DeriveSpawned(receipt.CommandId, 0), issued.LotId);
        Assert.Equal(after.GetResident(fixture.Player).InventoryCustodianId, issued.CustodianId);
        Assert.Equal(fixture.Site.Material.Item, issued.Definition);
        Assert.Equal(new SourceInterval(fixture.Site.CountedSourceId, 0, fixture.Site.Quantity), issued.Source);
        Assert.Equal(before.Inventory.Custody.Lots.Count + 1, after.Inventory.Custody.Lots.Count);
        Assert.All(before.Inventory.Custody.Lots, lot => Assert.Equal(lot, after.Inventory.Custody.GetLot(lot.LotId)));
        Assert.Equal(expected.Space.Regions.Select(region => region.ComputeContentHash()),
            after.Space.Regions.Select(region => region.ComputeContentHash()));
    }

    private static void AssertSingleInterval(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), after.Calendar);
        Assert.All(before.Residents, resident => Assert.Equal(resident.ExperiencedTime.Add(ExposurePacingV1.FromExplorationSteps(1)),
            after.GetResident(resident.ActorId).ExperiencedTime));
    }

    private static void AssertCompletionCapacity(PendingExcavation pending, WorldStepBatch trigger,
        WorldStepBatch continuation, WorldActionReceipt terminal)
    {
        ulong empty = (ulong)WorldReceiptCodecV1.EncodeBatch([]).Length;
        ulong terminalLength = (ulong)WorldReceiptCodecV1.EncodeBatch([terminal]).Length;
        Assert.Equal((ulong)pending.MaximumTerminalOutcomesByteLength, terminalLength);
        ulong continuationLength = (ulong)WorldStepCodec.Encode(continuation).Length;
        ulong triggerLength = (ulong)WorldStepCodec.Encode(trigger).Length;
        ulong future = pending.MaximumContinuationCount * (continuationLength + empty) + terminalLength - empty;
        ulong sequence = WorldJournalPolicy.MaximumCommitCount - pending.MaximumContinuationCount;
        var usage = new WorldJournalUsage(sequence - 1, WorldJournalPolicy.MaximumCommandOutcomeCount - 1,
            WorldJournalPolicy.MaximumAcceptedJournalBytes - future - triggerLength - empty, 0, 0);
        WorldJournalAdvance Advance(WorldJournalUsage selected, ulong next) => WorldJournalPolicy.Advance(selected,
            next, 0, 0, triggerLength, empty, pending, 18);
        _ = Advance(usage, sequence);
        Assert.Throws<WorldRecoveryLimitException>(() => Advance(usage, sequence + 1));
        Assert.Throws<WorldRecoveryLimitException>(() => Advance(usage with { CommandOutcomeCount = usage.CommandOutcomeCount + 1 }, sequence));
        Assert.Throws<WorldRecoveryLimitException>(() => Advance(usage with { AcceptedJournalBytes = usage.AcceptedJournalBytes + 1 }, sequence));
        _ = WorldJournalPolicy.Advance(usage, sequence + 1, 0, 0, triggerLength, empty);
        _ = WorldJournalPolicy.Advance(usage with { CommandOutcomeCount = usage.CommandOutcomeCount + 1 }, sequence, 0, 0, triggerLength, empty);
        _ = WorldJournalPolicy.Advance(usage with { AcceptedJournalBytes = usage.AcceptedJournalBytes + 1 }, sequence, 0, 0, triggerLength, empty);
    }
}
