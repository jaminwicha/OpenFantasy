using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Structures;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class CoupledSettlementStoragePolicyTests
{
    [Theory]
    [InlineData(15, 3)]
    [InlineData(16, 4)]
    [InlineData(17, 4)]
    public void Fresh_worlds_keep_their_exact_schema_and_profile_when_reopened(int profile, int schema)
    {
        WorldSnapshot genesis = Genesis(profile);
        using var files = new StorageFiles();
        byte[] encoded = WorldSnapshotCodec.Encode(genesis);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.Equal(encoded, WorldSnapshotCodec.Encode(authority.InspectForHost().State));
        }
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(schema, database.SchemaVersion);
            Assert.Equal(encoded, database.ReadGenesis());
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(encoded, WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            Assert.Equal(encoded, WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(0).State));
            Assert.Equal((ushort)profile, reopened.InspectForHost().State.ProfileVersion);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(15, 4)]
    [InlineData(16, 2)]
    [InlineData(16, 3)]
    [InlineData(17, 2)]
    [InlineData(17, 3)]
    public void Recovery_refuses_a_valid_root_in_an_incompatible_exact_schema(int profile, int schema)
    {
        WorldSnapshot genesis = Genesis(profile);
        using var files = new StorageFiles();
        // This makes valid tables, rows and hashes in the wrong schema. Recovery must reject the
        // semantic profile/schema pairing, not merely malformed SQL or an edited checksum.
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true, schemaVersion: schema))
        {
            database.Initialize(WorldSnapshotCodec.Encode(genesis));
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Coupled_trigger_reserves_its_larger_dynamic_continuation_tail_at_the_unchanged_limits()
    {
        var fixture = new CoupledSettlementWorldFixture(consequenceWorkBudget: 1, maximumAssemblies: 2);
        using var files = new StorageFiles();
        WorldSnapshot basis = fixture.PrepareBeforeExcavation(files.Path);
        WorldStepBatch trigger = fixture.NextExcavation(basis);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        WorldStepDecision preview = authority.Preview(trigger);
        Assert.Equal(basis.ContentHash, authority.InspectForHost().State.ContentHash);
        WorldStepDecision accepted = authority.Commit(trigger);
        Assert.Equal(WorldSnapshotCodec.Encode(preview.Candidate), WorldSnapshotCodec.Encode(accepted.Candidate));
        PendingExcavation pending = Assert.IsType<PendingExcavation>(accepted.Candidate.PendingExcavation);
        ulong work = checked((ulong)basis.CoupledSettlementPolicy!.MaximumWorkUnits);
        ulong budget = checked((ulong)basis.ConsequencePolicy!.WorkUnitsPerContinuation);
        Assert.Equal(1 + (work + budget - 1) / budget, pending.MaximumContinuationCount);
        Assert.True(work > ConstructionSettlementResolution.MaximumWorkUnits,
            "The coupled policy must reserve more than the old single-assembly work bound.");
        WorldStepBatch Continuation(WorldSnapshot state) => new(fixture.Actions.NextIdentity(), state.WorldId,
            state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.ConsequenceContinuation,
            [], [], state.ProfileVersion);
        byte[] continuationFrame = WorldStepCodec.Encode(Continuation(accepted.Candidate));
        Assert.Equal((ushort)17, WorldStepCodec.Decode(continuationFrame).ProfileVersion);
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
        Assert.Equal((ulong)pending.MaximumTerminalOutcomesByteLength, terminalOutcomes);
        ulong futureBytes = checked(pending.MaximumContinuationCount * ((ulong)continuationFrame.Length + emptyOutcomes)
            + terminalOutcomes - emptyOutcomes);
        ulong sequence = WorldJournalPolicy.MaximumCommitCount - pending.MaximumContinuationCount;
        var exact = new WorldJournalUsage(sequence - 1, WorldJournalPolicy.MaximumCommandOutcomeCount - 1,
            WorldJournalPolicy.MaximumAcceptedJournalBytes - triggerBytes - emptyOutcomes - futureBytes, 0, 0);
        WorldJournalAdvance Advance(WorldJournalUsage usage, ulong at) => WorldJournalPolicy.Advance(usage,
            at, 0, 0, triggerBytes, emptyOutcomes, pending, basis.ProfileVersion);

        WorldJournalAdvance admitted = Advance(exact, sequence);
        Assert.Equal(sequence, admitted.Usage.CommitCount);
        Assert.Equal(WorldJournalPolicy.MaximumCommandOutcomeCount - 1, admitted.Usage.CommandOutcomeCount);
        Assert.Equal(WorldJournalPolicy.MaximumAcceptedJournalBytes - futureBytes, admitted.Usage.AcceptedJournalBytes);
        Assert.Throws<WorldRecoveryLimitException>(() => Advance(exact, sequence + 1));
        Assert.Throws<WorldRecoveryLimitException>(() => Advance(exact with
            { CommandOutcomeCount = exact.CommandOutcomeCount + 1 }, sequence));
        Assert.Throws<WorldRecoveryLimitException>(() => Advance(exact with
            { AcceptedJournalBytes = exact.AcceptedJournalBytes + 1 }, sequence));
        // Each deficient budget can hold the immediate trigger, but cannot promise its terminal outcome.
        _ = WorldJournalPolicy.Advance(exact, sequence + 1, 0, 0, triggerBytes, emptyOutcomes);
        _ = WorldJournalPolicy.Advance(exact with { CommandOutcomeCount = exact.CommandOutcomeCount + 1 },
            sequence, 0, 0, triggerBytes, emptyOutcomes);
        _ = WorldJournalPolicy.Advance(exact with { AcceptedJournalBytes = exact.AcceptedJournalBytes + 1 },
            sequence, 0, 0, triggerBytes, emptyOutcomes);
    }

    private static WorldSnapshot Genesis(int profile) => profile switch
    {
        1 => WorldServiceFixture.Genesis(),
        15 => new StructuralSettlementWorldFixture().Genesis,
        16 => new StructuralSettlementWorldFixture(consequenceWorkBudget: 1).Genesis,
        17 => new CoupledSettlementWorldFixture().Genesis,
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
}
