using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;

public sealed class EffectiveControlRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Assignment_publication_fault_recovers_exactly_one_side_of_the_transaction(bool committed)
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        WorldSnapshot before;
        WorldStepDecision expected;
        WorldStepBatch batch;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            fixture.Grant(authority);
            before = fixture.LoseAndExpire(authority);
            var witness = fixture.NextWitness(before);
            batch = new(witness.AssignmentId, before.WorldId, before.BranchId, before.Revision,
                before.AdvancingSteps, WorldBatchKind.ControlAssignment, [], [], before.ProfileVersion,
                controlAssignmentEvent: new(fixture.Owner, WorldControlAssignmentEventKind.Acquire, 0, witness));
            expected = new WorldStepProcessor().Decide(before, batch);
            Assert.Throws<IOException>(() => authority.Commit(batch, boundary =>
            {
                if (boundary == (committed ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
                {
                    throw new IOException("Injected effective-control publication failure.");
                }
            }));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
            Assert.Throws<InvalidOperationException>(() => authority.Commit(batch));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        var actual = reopened.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(committed ? expected.Candidate : before), WorldSnapshotCodec.Encode(actual));
        Assert.Equal(committed, reopened.IsIdentityOccupiedForHost(batch.CauseId));
        Assert.Equal(committed, actual.EffectiveControl!.Find(fixture.Owner)?.Witness is not null);
        Assert.Equal(before.Inventory.ContentHash, actual.Inventory.ContentHash);
        Assert.Equal(before.Calendar, actual.Calendar);
        Assert.Equal(before.AdvancingSteps, actual.AdvancingSteps);
        Assert.Empty(reopened.ReadControllerAllowancesForHost());
    }

    [Fact]
    public void Authentic_active_head_cannot_hide_a_missing_acquisition_commit()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            fixture.Grant(authority);
            fixture.LoseAndExpire(authority);
            var acquired = fixture.Acquire(authority);
            Assert.Equal(5UL, acquired.Revision);
            Assert.NotNull(acquired.EffectiveControl!.Find(fixture.Owner)!.Witness);
        }
        files.Execute("DELETE FROM world_occupied_identity WHERE sequence=X'0000000000000005'; DELETE FROM world_commits WHERE sequence=X'0000000000000005'");
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis)));
        Assert.Throws<InvalidDataException>(() => RecordedWorldReader.ReadForHost(files.Path, 2));
    }
}
