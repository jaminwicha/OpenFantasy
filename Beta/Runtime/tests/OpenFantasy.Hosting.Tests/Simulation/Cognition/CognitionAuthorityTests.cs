using OpenFantasy.Core.Cognition;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Cognition;

/// <summary>The cognition interpretation through the existing SQLite authority: one root, one publisher.</summary>
public sealed class CognitionAuthorityTests
{
    [Fact]
    public void Cognition_world_publishes_reopens_and_recovers_original_history_in_schema_twelve()
    {
        var fixture = new CognitionWorldFixture();
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Decide(fixture.Wren).Status);
        ulong decided = fixture.State.Revision;
        WorldSnapshot afterDecision = fixture.State;
        Assert.Equal(CommandReceiptStatus.Applied, fixture.GiveAxeToWren().Status);
        Assert.NotNull(fixture.CognitionOf(fixture.Wren).Wake);

        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            foreach (byte[] batch in fixture.Batches) authority.Commit(WorldStepCodec.Decode(batch));
            Assert.Equal(fixture.State.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            WorldSnapshot head = reopened.ReadStateForHost();
            Assert.Equal(fixture.State.ContentHash, head.ContentHash);
            Assert.Equal(fixture.CognitionOf(fixture.Wren).Wake, head.Cognition!.For(fixture.Wren).Wake);
            Assert.Equal(afterDecision.ContentHash, reopened.ReadRecordedStateForHost(decided).State.ContentHash);
            Assert.Equal(fixture.Genesis.ContentHash, reopened.ReadRecordedStateForHost(0).State.ContentHash);
        }
        using var database = WorldDatabase.Open(files.Path, false);
        Assert.Equal(12, database.SchemaVersion);
        Assert.DoesNotContain(database.ReadJournal(), row => row.Kind == WorldCommitKind.ProfileMigration);
    }

    [Fact]
    public void A_cognition_overflow_stops_the_durable_authority_without_publishing()
    {
        var fixture = new CognitionWorldFixture();
        for (int look = 0; look < ActorCognition.MaximumEvidence; look++)
            Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        WorldSnapshot head = fixture.State;
        WorldActionCommand gift = fixture.Command(fixture.Player,
            fixture.GiftOf(fixture.Player, fixture.Wren, fixture.Owned(fixture.Player, fixture.Axe), 1));
        var overflowing = new WorldStepBatch(fixture.NextId(), head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps,
            WorldBatchKind.AdvancingStep, [], [gift], head.ProfileVersion);

        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            foreach (byte[] batch in fixture.Batches) authority.Commit(WorldStepCodec.Decode(batch));
            Assert.Throws<CognitionCapacityException>(() => authority.Commit(overflowing));
            // The authority is stopped: nothing further is accepted until it is reopened.
            Assert.Throws<InvalidOperationException>(() => authority.Commit(overflowing));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(head.ContentHash, reopened.ReadStateForHost().ContentHash);
        Assert.Throws<CognitionCapacityException>(() => reopened.Commit(overflowing));
    }
}
