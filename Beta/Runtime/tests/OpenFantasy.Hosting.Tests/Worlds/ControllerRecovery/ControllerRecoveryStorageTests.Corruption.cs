using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerRecovery;

public sealed partial class ControllerRecoveryStorageTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("orphan")]
    [InlineData("actor")]
    [InlineData("episode")]
    [InlineData("revision")]
    public void Current_rows_must_match_recovered_world_even_when_their_checksum_is_recomputed(string corruption)
    {
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateRecoverable(files.Path, Fresh.Value))
            authority.Commit(Event(Fresh.Value, WorldControllerEventKind.Disconnected, 50));
        using (var database = WorldDatabase.Open(files.Path, false))
        {
            var original = database.ReadControllerRecovery();
            var row = Assert.Single(original.Allowances);
            ControllerAllowanceProgress[] rows = corruption switch
            {
                "missing" => [],
                "orphan" => [row, new(new EntityId(ulong.MaxValue, 1), 1, new EntityId(ulong.MaxValue, 2), 0)],
                "actor" => [row with { ActorId = new EntityId(ulong.MaxValue, 3) }],
                "episode" => [row with { DisconnectId = new EntityId(ulong.MaxValue, 4) }],
                _ => [row with { ControlRevision = row.ControlRevision + 1 }]
            };
            using var transaction = database.BeginWrite();
            database.WriteControllerRecovery(original with { Allowances = Array.AsReadOnly(rows) }, transaction);
            transaction.Commit();
        }
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(Fresh.Value)));
    }

    [Theory]
    [InlineData("policy")]
    [InlineData("progress-hash")]
    [InlineData("activation-hash")]
    [InlineData("missing-activation")]
    [InlineData("missing-segment")]
    public void Damaged_operational_metadata_is_not_ignored_by_ordinary_open(string corruption)
    {
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateRecoverable(files.Path, Fresh.Value)) { }
        files.Execute(corruption switch
        {
            "policy" => "UPDATE controller_recovery_activation SET policy=zeroblob(32)",
            "progress-hash" => "UPDATE controller_recovery_segment SET hash=zeroblob(32)",
            "activation-hash" => "UPDATE controller_recovery_activation SET hash=zeroblob(32)",
            "missing-activation" => "DELETE FROM controller_recovery_activation",
            _ => "DELETE FROM controller_recovery_segment"
        });
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }

    [Fact]
    public void Activation_unknown_episodes_are_checked_at_original_snapshot_not_later_current_head()
    {
        using var source = new StorageFiles(); using var destination = new StorageFiles();
        WorldSnapshot atActivation;
        using (var original = SqliteWorldAuthority.CreateNew(source.Path, Fresh.Value))
            atActivation = original.Commit(Event(Fresh.Value, WorldControllerEventKind.Disconnected, 60)).Candidate;
        using (var activated = SqliteWorldAuthority.UpgradeControllerRecovery(source.Path, destination.Path))
        {
            var restored = activated.Commit(Event(atActivation, WorldControllerEventKind.Reconnected, 61)).Candidate;
            activated.Commit(Event(restored, WorldControllerEventKind.Disconnected, 62));
        }
        ControllerRecoveryState forged;
        using (var database = WorldDatabase.Open(destination.Path, false))
        {
            var current = database.ReadControllerRecovery();
            forged = current with { Activation = current.Activation with { UnknownPriorElapsed = current.Allowances } };
            using var transaction = database.BeginWrite();
            database.WriteControllerRecovery(forged, transaction);
            transaction.Commit();
        }
        byte[] bytes = ControllerRecoveryState.EncodeActivation(forged.Activation);
        destination.Execute("UPDATE controller_recovery_activation SET provenance=x'" + Convert.ToHexString(bytes)
            + "',hash=x'" + Convert.ToHexString(ContentHash.Compute(bytes).ToByteArray()) + "'");
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(destination.Path, WorldSnapshotCodec.Encode(Fresh.Value)));
    }
}
