using OpenFantasy.Client;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care.Dispositions;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class CareDispositionFixtureSelectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Helper_spectator_status_explains_the_actual_closure_and_separate_encounter_acceptance(bool decline)
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture(humanHelper: true);
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var pending = fixture.RequestAfterDefeat(authority);
        var request = pending.Care!.Pending!;
        PendingCareDispositionIntent intent = decline
            ? new DeclinePendingCareIntent(request.AcknowledgmentId, request.EncounterId, pending.Care.Revision)
            : new CancelPendingCareIntent(request.AcknowledgmentId, request.EncounterId, pending.Care.Revision);
        var closed = fixture.Journey.Act(authority, decline ? fixture.Journey.Helper : fixture.Journey.Patient,
            intent, WorldBatchKind.Care);
        var view = new LocalWorldView(closed, fixture.Journey.Helper);
        Assert.False(view.EncounterOutcome!.ObserverIsParticipant);
        string status = EncounterOutcomeText.Status(view.EncounterOutcome, view.Care);
        Assert.Contains(decline ? "declined" : "cancelled", status);
        Assert.Contains("no treatment was given", status);
        Assert.Contains("Encounter acceptance remains separate", status);
        Assert.Empty(closed.EncounterOutcome!.Acknowledgments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_fixture_selects_only_the_admitted_human_and_opens_unready(bool helper)
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture(humanHelper: true);
        WorldSnapshot pending;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
            pending = fixture.RequestAfterDefeat(authority);
        string genesis = files.Path + ".genesis";
        try
        {
            File.WriteAllBytes(genesis, WorldSnapshotCodec.Encode(fixture.Genesis));
            byte[] prior = File.ReadAllBytes(files.Path);
            var actor = helper ? fixture.Journey.Helper : fixture.Journey.Patient;
            await using (var session = await FarmClientSession.OpenCareDispositionFixtureAsync(files.Path, genesis,
                fixture.Journey.Patient, fixture.Journey.Helper, actor))
            {
                Assert.Equal(actor, session.World.ReadView().PlayerActorId);
                Assert.Equal(LocalWorldRunState.Paused, session.World.ReadRunState());
                Assert.Equal(pending.Care!.Pending!.AcknowledgmentId, session.World.ReadView().Care!.PendingAcknowledgmentId);
                Assert.Equal(helper, session.World.ReadView().Care!.Decision!.Decline is not null);
                Assert.Equal(!helper, session.World.ReadView().Care!.Decision!.Cancellation is not null);
            }
            Assert.Equal(prior, File.ReadAllBytes(files.Path));
        }
        finally { File.Delete(genesis); }
    }

    [Fact]
    public async Task Fixture_selection_cannot_promote_host_control_or_swap_the_agreement_participants()
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis)) { }
        string genesis = files.Path + ".genesis";
        try
        {
            File.WriteAllBytes(genesis, WorldSnapshotCodec.Encode(fixture.Genesis));
            byte[] prior = File.ReadAllBytes(files.Path);
            await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenCareDispositionFixtureAsync(
                files.Path, genesis, fixture.Journey.Patient, fixture.Journey.Helper, fixture.Journey.Helper));
            await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenCareDispositionFixtureAsync(
                files.Path, genesis, fixture.Journey.Helper, fixture.Journey.Patient, fixture.Journey.Patient));
            await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenCareDispositionFixtureAsync(
                files.Path, genesis, fixture.Journey.Patient, fixture.Journey.Helper, fixture.Journey.Observer));
            Assert.Equal(prior, File.ReadAllBytes(files.Path));
            using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
            Assert.Equal(fixture.Genesis.ContentHash, reopened.ReadStateForHost().ContentHash);
        }
        finally { File.Delete(genesis); }
    }
}
