using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerRecovery;

public sealed partial class ControllerRecoveryStorageTests
{
    [Theory]
    [InlineData("fresh-after")]
    [InlineData("upgraded-after")]
    [InlineData("before")]
    public void Replay_enforces_capped_expiry_only_after_explicit_recovery_activation(string history)
    {
        using var source = new StorageFiles(); using var destination = new StorageFiles();
        WorldSnapshot disconnected, final;
        if (history == "fresh-after")
        {
            using var fresh = SqliteWorldAuthority.CreateRecoverable(destination.Path, Fresh.Value);
            disconnected = fresh.Commit(Event(Fresh.Value, WorldControllerEventKind.Disconnected, 70)).Candidate;
            final = ExpireWithMeasuredCheckpoint(fresh, disconnected);
        }
        else
        {
            using (var legacy = SqliteWorldAuthority.CreateNew(source.Path, Fresh.Value))
            {
                disconnected = legacy.Commit(Event(Fresh.Value, WorldControllerEventKind.Disconnected, 70)).Candidate;
                if (history == "before") legacy.Commit(Event(disconnected, WorldControllerEventKind.AllowanceExpired, 71, elapsed: 30_001));
            }
            using var upgraded = SqliteWorldAuthority.UpgradeControllerRecovery(source.Path, destination.Path);
            final = history == "before" ? upgraded.ReadStateForHost() : ExpireWithMeasuredCheckpoint(upgraded, disconnected);
        }

        if (history != "before")
        {
            // Construct an otherwise self-consistent journal: elapsed evidence is not a canonical
            // root field, so this Core-valid batch has the same cause, identities, outcomes and head.
            var excessive = Event(disconnected, WorldControllerEventKind.AllowanceExpired, 71, elapsed: 30_001);
            Assert.Equal(final.ContentHash, new WorldStepProcessor().Decide(disconnected, excessive).Candidate.ContentHash);
            byte[] bytes = WorldStepCodec.Encode(excessive);
            destination.Execute("UPDATE world_commits SET accepted_batch=x'" + Convert.ToHexString(bytes)
                + "' WHERE sequence=x'" + Convert.ToHexString(WorldStorageBinary.Number(final.Revision)) + "'");
            Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(destination.Path, WorldSnapshotCodec.Encode(Fresh.Value)));
        }
        else
        {
            using var reopened = SqliteWorldAuthority.OpenExisting(destination.Path, WorldSnapshotCodec.Encode(Fresh.Value));
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Empty(reopened.ReadControllerAllowancesForHost());
        }
    }

    private static WorldSnapshot ExpireWithMeasuredCheckpoint(SqliteWorldAuthority authority, WorldSnapshot disconnected)
    {
        var segment = authority.BeginControllerRecoverySegment();
        authority.CheckpointControllerAllowances(segment.Generation,
            segment.Allowances.Select(row => row with { ElapsedHostMilliseconds = 30_000 }).ToArray());
        return authority.Commit(Event(disconnected, WorldControllerEventKind.AllowanceExpired, 71)).Candidate;
    }
}
