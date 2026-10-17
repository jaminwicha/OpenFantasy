using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care.DispositionContinuation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalCareTests
{
    [Fact]
    public void Pending_aftermath_refuses_upgrade_without_loss_then_explicit_resolution_allows_the_same_save_to_upgrade()
    {
        using var fixture = new DispositionContinuationFixture(humanHelper: false, profileVersion: 40);
        using var source = fixture.CopyPending();
        using var destination = new StorageFiles();
        byte[] original = File.ReadAllBytes(source.Path);
        var request = WorldPartyWithdrawalTransition.ForHead(fixture.Pending, fixture.Journey.Commands.NextId());
        Assert.Throws<InvalidOperationException>(() => SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, destination.Path, request));
        Assert.False(File.Exists(destination.Path));
        Assert.Equal(original, File.ReadAllBytes(source.Path));
        WorldSnapshot completed;
        using (var authority = SqliteWorldAuthority.OpenExisting(source.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            Assert.Equal(fixture.Pending.ContentHash, authority.ReadStateForHost().ContentHash);
            completed = CancelAndComplete(authority, fixture);
        }
        using (var upgraded = SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, destination.Path,
            WorldPartyWithdrawalTransition.ForHead(completed, fixture.Journey.Commands.NextId())))
        {
            var migrated = upgraded.ReadStateForHost();
            Assert.Equal(completed.Care!.ContentHash, migrated.Care!.ContentHash);
            Assert.Equal(completed.Production!.ContentHash, migrated.Production!.ContentHash);
            Assert.Equal(completed.Progression!.ContentHash, migrated.Progression!.ContentHash);
            Assert.Equal(completed.Calendar, migrated.Calendar);
            Assert.Equal(fixture.Pending.ContentHash, upgraded.ReadRecordedStateForHost(fixture.Pending.Revision).State.ContentHash);
            AssertOriginalRequest(upgraded, fixture);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(destination.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal((ushort)41, reopened.ReadStateForHost().ProfileVersion);
        Assert.Equal(completed.Care!.ContentHash, reopened.ReadStateForHost().Care!.ContentHash);
        AssertOriginalRequest(reopened, fixture);
    }

    [Fact]
    public void Collective_world_keeps_pending_care_through_ordinary_cut_restart_cancellation_and_later_cut()
    {
        using var fixture = new DispositionContinuationFixture(humanHelper: false, profileVersion: 41);
        using var files = fixture.CopyPending();
        WorldSnapshot cut;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            cut = fixture.CutPendingWithRefusedRemedy(authority, out _).Candidate;
            Assert.NotNull(cut.Care!.Pending);
        }
        WorldSnapshot completed;
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(cut), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            AssertOriginalRequest(reopened, fixture);
            completed = CancelAndComplete(reopened, fixture);
            ulong archives = reopened.InspectForHost().Journal.MaterialCareArchiveCount;
            completed = fixture.Journey.ArchiveBeforeRescue(reopened);
            Assert.Equal(archives + 1, reopened.InspectForHost().Journal.MaterialCareArchiveCount);
            Assert.Null(completed.Care!.Pending);
            Assert.Equal(0, completed.Progression!.GetActor(fixture.Journey.Patient).CurrentHp);
        }
        using var final = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(completed), WorldSnapshotCodec.Encode(final.ReadStateForHost()));
        AssertOriginalRequest(final, fixture);
        Assert.Equal(fixture.Pending.ContentHash, final.ReadRecordedStateForHost(fixture.Pending.Revision).State.ContentHash);
        Assert.Equal(cut.ContentHash, final.ReadRecordedStateForHost(cut.Revision).State.ContentHash);
    }

    private static WorldSnapshot CancelAndComplete(SqliteWorldAuthority authority, DispositionContinuationFixture fixture)
    {
        var before = authority.ReadStateForHost();
        var pending = before.Care!.Pending!;
        Assert.Equal(EncounterLifecyclePhase.Outcome, before.EncounterLifecycle!.Phase);
        var cancelled = fixture.Journey.Act(authority, fixture.Journey.Patient,
            new CancelPendingCareIntent(pending.AcknowledgmentId, pending.EncounterId, before.Care.Revision), WorldBatchKind.Care);
        Assert.Null(cancelled.Care!.Pending);
        Assert.Equal(before.Progression!.ContentHash, cancelled.Progression!.ContentHash);
        Assert.Equal(before.Production!.TotalConsumedItemCount, cancelled.Production!.TotalConsumedItemCount);
        Assert.Equal(before.Calendar, cancelled.Calendar);
        var outcome = WorldEncounterOutcomes.Determine(cancelled)!;
        fixture.Journey.Act(authority, fixture.Journey.Patient,
            new AcknowledgeEncounterOutcomeIntent(outcome.EncounterId, outcome.ActionHash), WorldBatchKind.EncounterLifecycle);
        var completed = fixture.Journey.Commit(authority, WorldBatchKind.EncounterLifecycle).Candidate;
        Assert.Equal(EncounterLifecyclePhase.Exploration, completed.EncounterLifecycle!.Phase);
        Assert.Equal(before.Progression.ContentHash, completed.Progression!.ContentHash);
        Assert.Equal(before.Calendar, completed.Calendar);
        return completed;
    }

    private static void AssertOriginalRequest(SqliteWorldAuthority authority, DispositionContinuationFixture fixture)
    {
        var command = fixture.RequestCommand;
        var receipt = authority.FindReceipt(command, new(command.PrincipalId, command.OriginSessionId));
        Assert.NotNull(receipt);
        Assert.Equal(WorldReceiptCodecV1.Encode(fixture.RequestReceipt), WorldReceiptCodecV1.Encode(receipt));
        Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
    }
}
