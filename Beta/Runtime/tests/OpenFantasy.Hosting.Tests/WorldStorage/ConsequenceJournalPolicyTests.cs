using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConsequenceJournalPolicyTests
{
    [Fact]
    public void Trigger_requires_exact_worst_completion_headroom_in_each_unchanged_journal_envelope()
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: 1);
        using var files = new StorageFiles();
        WorldSnapshot basis = fixture.PrepareBeforeExcavation(files.Path);
        WorldStepBatch trigger = fixture.NextExcavation(basis);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        WorldStepDecision accepted = authority.Commit(trigger);
        PendingExcavation pending = Assert.IsType<PendingExcavation>(accepted.Candidate.PendingExcavation);
        WorldStepBatch Continuation(WorldSnapshot state) => new(fixture.Actions.NextIdentity(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.ConsequenceContinuation, [], [], profileVersion: 16);
        ulong continuationBytes = (ulong)WorldStepCodec.Encode(Continuation(accepted.Candidate)).Length;
        WorldStepDecision terminal = accepted;
        for (ulong count = 0; terminal.Candidate.PendingExcavation is not null; count++)
        {
            Assert.True(count < pending.MaximumContinuationCount);
            terminal = authority.Commit(Continuation(terminal.Candidate));
        }
        Assert.Single(terminal.Receipts);
        ulong triggerBytes = (ulong)WorldStepCodec.Encode(trigger).Length;
        ulong emptyOutcomes = (ulong)WorldReceiptCodecV1.EncodeBatch([]).Length;
        ulong terminalOutcomes = (ulong)WorldReceiptCodecV1.EncodeBatch(terminal.Receipts).Length;
        ulong futureBytes = pending.MaximumContinuationCount * (continuationBytes + emptyOutcomes)
            + terminalOutcomes - emptyOutcomes;
        ulong sequence = WorldJournalPolicy.MaximumCommitCount - pending.MaximumContinuationCount;
        var fitsExactly = new WorldJournalUsage(sequence - 1, WorldJournalPolicy.MaximumCommandOutcomeCount - 1,
            WorldJournalPolicy.MaximumAcceptedJournalBytes - triggerBytes - emptyOutcomes - futureBytes, 0, 0);

        WorldJournalAdvance acceptedBudget = WorldJournalPolicy.Advance(fitsExactly, sequence, 0, 0,
            triggerBytes, emptyOutcomes, pending);
        Assert.Equal(sequence, acceptedBudget.Usage.CommitCount);
        Assert.Equal(WorldJournalPolicy.MaximumCommandOutcomeCount - 1, acceptedBudget.Usage.CommandOutcomeCount);
        Assert.Equal(WorldJournalPolicy.MaximumAcceptedJournalBytes - futureBytes, acceptedBudget.Usage.AcceptedJournalBytes);
        // The reservation is checked, not charged early to published usage. Each would still fit
        // its immediate trigger row, but one unit less room cannot guarantee terminal publication.
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(fitsExactly,
            sequence + 1, 0, 0, triggerBytes, emptyOutcomes, pending));
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(fitsExactly with
            { CommandOutcomeCount = fitsExactly.CommandOutcomeCount + 1 }, sequence, 0, 0, triggerBytes, emptyOutcomes, pending));
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(fitsExactly with
            { AcceptedJournalBytes = fitsExactly.AcceptedJournalBytes + 1 }, sequence, 0, 0, triggerBytes, emptyOutcomes, pending));
        _ = WorldJournalPolicy.Advance(fitsExactly, sequence + 1, 0, 0, triggerBytes, emptyOutcomes);
        _ = WorldJournalPolicy.Advance(fitsExactly with { CommandOutcomeCount = fitsExactly.CommandOutcomeCount + 1 },
            sequence, 0, 0, triggerBytes, emptyOutcomes);
        _ = WorldJournalPolicy.Advance(fitsExactly with { AcceptedJournalBytes = fitsExactly.AcceptedJournalBytes + 1 },
            sequence, 0, 0, triggerBytes, emptyOutcomes);
    }
}
