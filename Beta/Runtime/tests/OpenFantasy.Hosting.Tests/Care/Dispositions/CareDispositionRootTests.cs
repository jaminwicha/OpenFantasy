using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care.Dispositions;

public sealed class CareDispositionRootTests
{
    [Fact]
    public void Only_fresh_recovery_roots_explicitly_select_dispositions_and_old_frames_keep_their_interpretation()
    {
        var fixture = new RecoveryJournalFixture();
        var old = fixture.Genesis;
        byte[] oldBytes = WorldSnapshotCodecV37.Encode(old);
        var current = WorldCareDispositionAdmission.CreateFresh(old);
        Assert.Equal((ushort)38, current.ProfileVersion);
        Assert.True(current.Care!.SupportsDispositions);
        Assert.NotEqual(old.AdmissionVersion, current.AdmissionVersion);
        byte[] bytes = WorldSnapshotCodec.Encode(current);
        Assert.Equal(bytes, WorldSnapshotCodecV38.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Equal(oldBytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(oldBytes)));
        Assert.False(WorldSnapshotCodec.Decode(oldBytes).Care!.SupportsDispositions);
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV37.Encode(current));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV37.Decode(bytes));
        Assert.Throws<ArgumentException>(() => CareCodecV4.Encode(current.Care));
        Assert.Throws<ArgumentException>(() => WorldCareDispositionAdmission.CreateFresh(current));
        Assert.Throws<ArgumentException>(() => WorldCareDispositionAdmission.CreateFresh(fixture.Legacy));
        var lived = new WorldStepProcessor().Decide(old, fixture.Commands.Batch(old, WorldBatchKind.AdvancingStep)).Candidate;
        Assert.Throws<ArgumentException>(() => WorldCareDispositionAdmission.CreateFresh(lived));
        Assert.Equal(WorldCareDispositionAdmission.RulesPin.ToByteArray(), bytes[^32..]);
        bytes[^1] ^= 1;
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(bytes));
    }

    [Fact]
    public void Pending_patient_control_can_return_without_closing_the_request_or_advancing_gameplay()
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture(humanHelper: true);
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var pending = fixture.RequestAfterDefeat(authority);
        EntityId patient = fixture.Journey.Patient;
        Assert.True(WorldControllerBoundaries.CanRestoreHuman(pending, patient));
        Assert.True(WorldControllerBoundaries.CanRestoreHuman(pending, fixture.Journey.Helper));
        var state = pending;
        void Event(WorldControllerEvent input) => state = authority.Commit(new WorldStepBatch(fixture.Journey.Commands.NextId(),
            state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.ControllerLifecycle,
            [], [], state.ProfileVersion, input)).Candidate;
        Event(new(patient, WorldControllerEventKind.Disconnected, state.Controllers!.Get(patient).Revision,
            reason: WorldControllerDisconnectReason.TransportLost));
        Event(new(patient, WorldControllerEventKind.AllowanceExpired, state.Controllers!.Get(patient).Revision,
            state.Controllers.Get(patient).DisconnectId, WorldControllerState.ReconnectAllowanceMilliseconds));
        Event(new(patient, WorldControllerEventKind.Reconnected, state.Controllers!.Get(patient).Revision,
            state.Controllers.Get(patient).DisconnectId));
        Assert.Equal(pending.Care!.ContentHash, state.Care!.ContentHash);
        Assert.Equal(pending.Calendar, state.Calendar);
        Assert.Equal(pending.AdvancingSteps, state.AdvancingSteps);
        Assert.Equal(pending.Inventory.ContentHash, state.Inventory.ContentHash);
        Assert.Equal(pending.Progression!.ContentHash, state.Progression!.ContentHash);
        Assert.Equal(pending.Pacing!.ContentHash, state.Pacing!.ContentHash);
        var request = state.Care.Pending!;
        var closed = fixture.Journey.Act(authority, patient,
            new CancelPendingCareIntent(request.AcknowledgmentId, request.EncounterId, state.Care.Revision), WorldBatchKind.Care);
        Assert.Null(closed.Care!.Pending);
        Assert.Single(closed.Care.Dispositions);
    }
}
