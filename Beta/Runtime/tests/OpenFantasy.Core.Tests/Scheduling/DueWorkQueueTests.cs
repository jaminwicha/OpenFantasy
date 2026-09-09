using OpenFantasy.Core.Scheduling;

namespace OpenFantasy.Core.Tests.Scheduling;

public sealed class DueWorkQueueTests
{
    [Fact]
    public void PermutingInsertionOrderCannotChangeSnapshotOrSelection()
    {
        DueWorkQueueSnapshot baseline = SchedulingFixture.Create();
        byte[] expectedBytes = CalendarQueueCodecV1.Encode(baseline);
        var expectedIds = baseline.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 256)
            .SelectedWork.Select(entry => entry.EventId).ToArray();
        for (int rotation = 0; rotation < baseline.Entries.Count; rotation++)
        {
            ScheduledWorkEntry[] reordered = baseline.Entries.Skip(rotation)
                .Concat(baseline.Entries.Take(rotation)).Reverse().ToArray();
            DueWorkQueueSnapshot snapshot = new(baseline.Scope, baseline.Revision, reordered);
            Assert.Equal(expectedBytes, CalendarQueueCodecV1.Encode(snapshot));
            Assert.Equal(expectedIds, snapshot.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 256)
                .SelectedWork.Select(entry => entry.EventId));
        }
    }

    [Fact]
    public void PhaseSelectionLeavesOlderPlanningAndAllOtherPhasesPending()
    {
        DueWorkQueueSnapshot snapshot = SchedulingFixture.Create();
        DueWorkSelection transitions = snapshot.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 256);
        Assert.Equal(4, transitions.SelectedWork.Count);
        Assert.All(transitions.SelectedWork, entry => Assert.Equal(ScheduledWorkPhase.StateTransition, entry.Phase));
        Assert.False(transitions.HasMoreEligibleWork);
        DueWorkQueueSnapshot next = snapshot.ApplySelection(transitions);
        Assert.Contains(next.Entries, entry => entry.EventId == new EntityId(0, 1) && entry.DueAt.Days == -1);
        Assert.Contains(next.Entries, entry => entry.Phase == ScheduledWorkPhase.Observation);
        Assert.Contains(next.Entries, entry => entry.Phase == ScheduledWorkPhase.FollowUp);
        Assert.Equal(6, next.Entries.Count);
    }

    [Fact]
    public void BoundedBatchesRetainBacklogAndFutureWorkUntilExplicitAcknowledgement()
    {
        var entries = Enumerable.Range(1, DueWorkQueueSnapshot.MaximumEntryCount).Select(index =>
            SchedulingFixture.Entry((ulong)index, index <= 900 ? 0 : 1));
        DueWorkQueueSnapshot queue = new(SchedulingFixture.Scope(), 0, entries);
        HashSet<EntityId> acknowledged = [];
        int batches = 0;
        while (true)
        {
            DueWorkSelection selection = queue.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 256);
            if (selection.SelectedWork.Count == 0)
            {
                break;
            }
            int originalCount = queue.Entries.Count;
            Assert.Equal(originalCount - selection.SelectedWork.Count, selection.PendingCount);
            Assert.Equal(acknowledged.Count + selection.SelectedWork.Count < 900, selection.HasMoreEligibleWork);
            foreach (ScheduledWorkEntry entry in selection.SelectedWork)
            {
                Assert.True(acknowledged.Add(entry.EventId));
            }
            queue = queue.ApplySelection(selection);
            Assert.Equal(originalCount - selection.SelectedWork.Count, queue.Entries.Count);
            batches++;
        }
        Assert.Equal(900, acknowledged.Count);
        Assert.Equal(4, batches);
        Assert.Equal(4UL, queue.Revision);
        Assert.Equal(124, queue.Entries.Count);
        Assert.All(queue.Entries, entry => Assert.Equal(1, entry.DueAt.Days));
        Assert.Equal(124, queue.SelectDue(SchedulingFixture.Cutoff(1), ScheduledWorkPhase.StateTransition, 256).SelectedWork.Count);
    }

    [Fact]
    public void RestoredSnapshotAcceptsTheSamePlanAndContinuesIdentically()
    {
        DueWorkQueueSnapshot original = SchedulingFixture.Create();
        DueWorkSelection plan = original.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 2);
        DueWorkQueueSnapshot restored = CalendarQueueCodecV1.Decode(CalendarQueueCodecV1.Encode(original));
        DueWorkQueueSnapshot nextOriginal = original.ApplySelection(plan);
        DueWorkQueueSnapshot nextRestored = restored.ApplySelection(plan);
        Assert.Equal(CalendarQueueCodecV1.Encode(nextOriginal), CalendarQueueCodecV1.Encode(nextRestored));
        Assert.Equal(nextOriginal.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 256).SelectedWork,
            nextRestored.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 256).SelectedWork);
        Assert.Throws<InvalidOperationException>(() => nextOriginal.ApplySelection(plan));
    }

    [Fact]
    public void ChangedStateUnderTheSameScopeAndRevisionRejectsAStalePlan()
    {
        DueWorkQueueSnapshot original = SchedulingFixture.Create();
        DueWorkSelection plan = original.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 2);
        ScheduledWorkEntry old = original.Entries[0];
        ScheduledWorkEntry changed = new(old.EventId, old.DueAt, old.Phase, old.WorkDomainId, old.ActorId,
            new EntityId(0, 999), old.ContinuationOrdinal);
        DueWorkQueueSnapshot altered = new(original.Scope, original.Revision, original.Entries.Skip(1).Prepend(changed));
        string before = altered.ComputeSnapshotHash();
        Assert.Throws<InvalidOperationException>(() => altered.ApplySelection(plan));
        Assert.Equal(before, altered.ComputeSnapshotHash());
        DueWorkQueueSnapshot newer = new(original.Scope, original.Revision + 1, original.Entries);
        Assert.Throws<InvalidOperationException>(() => newer.ApplySelection(plan));
    }

    [Fact]
    public void SnapshotAndSelectionProtectCopiedCollections()
    {
        List<ScheduledWorkEntry> input = [SchedulingFixture.Entry(1), SchedulingFixture.Entry(2)];
        DueWorkQueueSnapshot snapshot = new(SchedulingFixture.Scope(), 0, input);
        input.Clear();
        DueWorkSelection selected = snapshot.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 1);
        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Single(selected.SelectedWork);
        Assert.Throws<NotSupportedException>(() => ((IList<ScheduledWorkEntry>)selected.SelectedWork).Clear());
        Assert.Equal(2, snapshot.Entries.Count);
    }

    [Fact]
    public void DuplicateEventIdsRejectEvenWhenDueTimeOrContinuationDiffers()
    {
        ScheduledWorkEntry first = SchedulingFixture.Entry(1);
        ScheduledWorkEntry later = SchedulingFixture.Entry(1, 1);
        Assert.Throws<ArgumentException>(() => new DueWorkQueueSnapshot(SchedulingFixture.Scope(), 0, [first, first]));
        Assert.Throws<ArgumentException>(() => new DueWorkQueueSnapshot(SchedulingFixture.Scope(), 0, [first, later]));
    }

    [Fact]
    public void EntryLimitRejectsWithoutConsumingAnUnboundedEnumerable()
    {
        int yielded = 0;
        IEnumerable<ScheduledWorkEntry> Endless()
        {
            while (true)
            {
                yielded++;
                yield return SchedulingFixture.Entry((ulong)yielded);
            }
        }
        Assert.Throws<ArgumentException>(() => new DueWorkQueueSnapshot(SchedulingFixture.Scope(), 0, Endless()));
        Assert.Equal(DueWorkQueueSnapshot.MaximumEntryCount + 1, yielded);
        DueWorkQueueSnapshot maximum = new(SchedulingFixture.Scope(), 0,
            Enumerable.Range(1, DueWorkQueueSnapshot.MaximumEntryCount).Select(index => SchedulingFixture.Entry((ulong)index)));
        byte[] bytes = CalendarQueueCodecV1.Encode(maximum);
        Assert.True(bytes.Length <= CalendarQueueCodecV1.MaximumSnapshotByteLength);
        Assert.Equal(DueWorkQueueSnapshot.MaximumEntryCount, CalendarQueueCodecV1.Decode(bytes).Entries.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(257)]
    public void InvalidBatchBudgetsRejectWithoutDiscardingWork(int budget)
    {
        DueWorkQueueSnapshot queue = SchedulingFixture.Create();
        string before = queue.ComputeSnapshotHash();
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.Planning, budget));
        Assert.Equal(before, queue.ComputeSnapshotHash());
    }

    [Fact]
    public void ReservedCommandPhaseAndUnknownPhasesReject()
    {
        DueWorkQueueSnapshot queue = SchedulingFixture.Create();
        foreach (ScheduledWorkPhase phase in new[] { (ScheduledWorkPhase)20, (ScheduledWorkPhase)0, (ScheduledWorkPhase)999 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SchedulingFixture.Entry(1, phase: phase));
            Assert.Throws<ArgumentOutOfRangeException>(() => queue.SelectDue(SchedulingFixture.Cutoff(), phase, 1));
        }
    }

    [Fact]
    public void RevisionExhaustionPreservesOriginalAndEmptySelectionDoesNotConsumeARevision()
    {
        DueWorkQueueSnapshot queue = new(SchedulingFixture.Scope(), ulong.MaxValue, [SchedulingFixture.Entry(1)]);
        DueWorkSelection selected = queue.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 1);
        string before = queue.ComputeSnapshotHash();
        Assert.Throws<OverflowException>(() => queue.ApplySelection(selected));
        Assert.Equal(before, queue.ComputeSnapshotHash());
        DueWorkSelection empty = queue.SelectDue(SchedulingFixture.Cutoff(-1), ScheduledWorkPhase.StateTransition, 1);
        Assert.Same(queue, queue.ApplySelection(empty));
        Assert.Single(queue.Entries);
    }

    [Fact]
    public void IndependentCalendarScopesCannotBeComparedSelectedOrAppliedTogether()
    {
        DueWorkQueueSnapshot queue = SchedulingFixture.Create();
        CalendarClockCoordinate origin = SchedulingFixture.Cutoff();
        Assert.True(origin.CompareTo(SchedulingFixture.Cutoff(1)) < 0);
        Assert.Equal(0, origin.CompareTo(new CalendarClockCoordinate(SchedulingFixture.Scope(), new WorldInstant(0, 0))));
        DueWorkSelection plan = queue.SelectDue(origin, ScheduledWorkPhase.StateTransition, 1);
        CalendarQueueScope[] others =
        [
            queue.Scope with { WorldId = new EntityId(0, 999) },
            queue.Scope with { BranchId = new EntityId(0, 999) },
            queue.Scope with { ClockId = new EntityId(0, 999) }
        ];
        foreach (CalendarQueueScope other in others)
        {
            CalendarClockCoordinate cutoff = new(other, new WorldInstant(0, 0));
            Assert.Throws<ArgumentException>(() => origin.CompareTo(cutoff));
            Assert.Throws<ArgumentException>(() => queue.SelectDue(cutoff, ScheduledWorkPhase.StateTransition, 1));
            Assert.Throws<InvalidOperationException>(() => new DueWorkQueueSnapshot(other, queue.Revision, queue.Entries).ApplySelection(plan));
        }
    }
}
