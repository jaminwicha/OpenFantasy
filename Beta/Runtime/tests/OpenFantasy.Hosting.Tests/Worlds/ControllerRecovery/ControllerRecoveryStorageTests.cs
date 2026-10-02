using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerRecovery;

public sealed partial class ControllerRecoveryStorageTests
{
    private static readonly Lazy<WorldSnapshot> Fresh = new(() => WorldControllerAdmission.CreateFresh(
        CargoOutcomeWorldAdmission.CreateEmbedded("controller-recovery-storage", new[] { "Traveler", "Companion" })));
    private static EntityId Actor(WorldSnapshot state) => state.Controllers!.Assignments[0].ActorId;
    private static WorldStepBatch Event(WorldSnapshot state, WorldControllerEventKind kind, ulong id, EntityId? actor = null,
        ulong elapsed = 30_000)
    {
        var assignment = state.Controllers!.Get(actor ?? Actor(state));
        var input = kind == WorldControllerEventKind.Disconnected
            ? new WorldControllerEvent(assignment.ActorId, kind, assignment.Revision, reason: WorldControllerDisconnectReason.TransportLost)
            : new WorldControllerEvent(assignment.ActorId, kind, assignment.Revision, assignment.DisconnectId,
                kind == WorldControllerEventKind.AllowanceExpired ? elapsed : 0);
        return new(new EntityId(0xfa91000000000000, id), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.ControllerLifecycle, [], [], state.ProfileVersion, input);
    }

    [Theory]
    [InlineData(36)]
    [InlineData(37)]
    [InlineData(38)]
    public void Fresh_admission_is_explicit_and_preserves_exact_canonical_genesis(int profile)
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = profile == 36 ? Fresh.Value : new RecoveryJournalFixture(true).Genesis;
        if (profile == 38) genesis = WorldCareDispositionAdmission.CreateFresh(genesis);
        byte[] bytes = WorldSnapshotCodec.Encode(genesis);
        using (var authority = SqliteWorldAuthority.CreateRecoverable(files.Path, genesis))
        {
            Assert.True(authority.SupportsControllerRecovery);
            Assert.Empty(authority.ReadControllerAllowancesForHost());
            Assert.Equal(bytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Equal(1UL, authority.BeginControllerRecoverySegment().Generation);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, bytes);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Empty(reopened.ReadControllerAllowancesForHost());
        Assert.Equal(2UL, reopened.BeginControllerRecoverySegment().Generation);
    }

    [Fact]
    public void Legacy_nine_remains_unmodified_and_unsupported_admission_is_refused()
    {
        using var files = new StorageFiles();
        using (var legacy = SqliteWorldAuthority.CreateNew(files.Path, Fresh.Value))
        {
            Assert.False(legacy.SupportsControllerRecovery);
            Assert.Throws<InvalidOperationException>(() => legacy.BeginControllerRecoverySegment());
            var disconnected = legacy.Commit(Event(Fresh.Value, WorldControllerEventKind.Disconnected, 1)).Candidate;
            var expired = legacy.Commit(Event(disconnected, WorldControllerEventKind.AllowanceExpired, 2)).Candidate;
            Assert.Equal(WorldControllerMode.Autonomous, expired.Controllers!.Get(Actor(expired)).Mode);
        }
        using (var database = WorldDatabase.Open(files.Path, false)) Assert.Equal(9, database.SchemaVersion);
        using var unsupported = new StorageFiles();
        var old = CargoOutcomeWorldAdmission.CreateEmbedded("controller-recovery-unsupported", "Traveler");
        Assert.Throws<ArgumentException>(() => SqliteWorldAuthority.CreateRecoverable(unsupported.Path, old));
        Assert.False(File.Exists(unsupported.Path));
    }

    [Fact]
    public void Explicit_upgrade_retains_history_and_original_unknown_episode_provenance_after_later_events()
    {
        using var source = new StorageFiles(); using var destination = new StorageFiles();
        WorldSnapshot activation;
        using (var original = SqliteWorldAuthority.CreateNew(source.Path, Fresh.Value))
            activation = original.Commit(Event(Fresh.Value, WorldControllerEventKind.Disconnected, 10)).Candidate;
        byte[] originalBytes = File.ReadAllBytes(source.Path);
        WorldSnapshot final;
        using (var upgraded = SqliteWorldAuthority.UpgradeControllerRecovery(source.Path, destination.Path))
        {
            Assert.True(upgraded.SupportsControllerRecovery);
            Assert.Equal(WorldSnapshotCodec.Encode(activation), WorldSnapshotCodec.Encode(upgraded.ReadStateForHost()));
            Assert.Equal(0UL, Assert.Single(upgraded.ReadControllerAllowancesForHost()).ElapsedHostMilliseconds);
            var returned = upgraded.Commit(Event(activation, WorldControllerEventKind.Reconnected, 11)).Candidate;
            final = upgraded.Commit(Event(returned, WorldControllerEventKind.Disconnected, 12)).Candidate;
        }
        Assert.Equal(originalBytes, File.ReadAllBytes(source.Path));
        using (var database = WorldDatabase.Open(destination.Path, false))
        {
            var metadata = database.ReadControllerRecovery();
            Assert.True(metadata.Activation.Upgraded);
            Assert.Equal(activation.Revision, metadata.Activation.SourceRevision);
            Assert.Equal(activation.ContentHash, metadata.Activation.SourceHash);
            Assert.Equal(activation.Controllers!.Get(Actor(activation)).DisconnectId,
                Assert.Single(metadata.Activation.UnknownPriorElapsed).DisconnectId);
            Assert.NotEqual(Assert.Single(metadata.Activation.UnknownPriorElapsed).DisconnectId,
                Assert.Single(metadata.Allowances).DisconnectId);
        }
        using (var reopened = SqliteWorldAuthority.OpenExisting(destination.Path, WorldSnapshotCodec.Encode(Fresh.Value)))
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeControllerRecovery(source.Path, destination.Path));
    }

    [Fact]
    public void Exact_current_set_and_fenced_nondecreasing_progress_gate_every_expiry_entrypoint()
    {
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateRecoverable(files.Path, Fresh.Value);
        var first = authority.Commit(Event(Fresh.Value, WorldControllerEventKind.Disconnected, 20)).Candidate;
        EntityId other = Fresh.Value.Controllers!.Assignments[1].ActorId;
        var state = authority.Commit(Event(first, WorldControllerEventKind.Disconnected, 21, other)).Candidate;
        var segment = authority.BeginControllerRecoverySegment();
        var rows = segment.Allowances.Select(row => row with { ElapsedHostMilliseconds = 12_345 }).ToArray();
        authority.CheckpointControllerAllowances(segment.Generation, rows);
        Assert.Throws<InvalidDataException>(() => authority.CheckpointControllerAllowances(segment.Generation, rows[..1]));
        Assert.Throws<InvalidDataException>(() => authority.CheckpointControllerAllowances(segment.Generation,
            [rows[0] with { ControlRevision = 99 }, rows[1]]));
        Assert.Throws<InvalidDataException>(() => authority.CheckpointControllerAllowances(segment.Generation,
            [rows[0] with { DisconnectId = new EntityId(9, 9) }, rows[1]]));
        Assert.Throws<InvalidOperationException>(() => authority.CheckpointControllerAllowances(segment.Generation,
            rows.Select(row => row with { ElapsedHostMilliseconds = 12_344 }).ToArray()));
        Assert.Throws<InvalidDataException>(() => authority.CheckpointControllerAllowances(segment.Generation,
            rows.Select(row => row with { ElapsedHostMilliseconds = 30_001 }).ToArray()));
        var replacement = authority.BeginControllerRecoverySegment();
        Assert.Throws<InvalidOperationException>(() => authority.CheckpointControllerAllowances(segment.Generation, rows));
        var expiry = Event(state, WorldControllerEventKind.AllowanceExpired, 22);
        Assert.Throws<InvalidOperationException>(() => authority.Preview(expiry));
        Assert.Throws<InvalidOperationException>(() => authority.Commit(expiry));
        Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
        authority.CheckpointControllerAllowances(replacement.Generation,
            rows.Select(row => row with { ElapsedHostMilliseconds = 30_000 }).ToArray());
        Assert.Throws<InvalidOperationException>(() => authority.Commit(Event(state, WorldControllerEventKind.AllowanceExpired, 23, elapsed: 30_001)));
        var expired = authority.Commit(expiry).Candidate;
        Assert.Single(authority.ReadControllerAllowancesForHost());
        var restored = authority.Commit(Event(expired, WorldControllerEventKind.Reconnected, 24, other)).Candidate;
        Assert.Empty(authority.ReadControllerAllowancesForHost());
        Assert.Equal(state.AdvancingSteps, restored.AdvancingSteps);
        Assert.Equal(0UL, authority.InspectForHost().Journal.CommandOutcomeCount);
    }
}
