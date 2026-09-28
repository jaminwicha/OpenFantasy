using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class RecordedMaterialEvidenceTests
{
    [Fact]
    public void Recorded_observation_verifies_the_later_head_and_retains_detached_exact_pins()
    {
        using var journey = new MaterialEvidenceJourney();
        var genesis = journey.World;
        var first = journey.Step([journey.CraftHelperDose()]);
        var expected = Assert.Single(first.MaterialEvidence);
        journey.Step();
        var head = journey.World;
        var journalBefore = journey.Authority.InspectForHost().Journal;
        var recorded = journey.Authority.ReadMaterialEvidenceForHost(1);
        Assert.Equal(1UL, recorded.Sequence);
        Assert.Equal(genesis.ContentHash, recorded.BeforeHash);
        Assert.Equal(first.Candidate.ContentHash, recorded.AfterHash);
        Assert.Equal(expected.ContentHash, Assert.Single(recorded.Evidence).ContentHash);
        Assert.Equal(head.Revision, recorded.SourceHeadRevision);
        Assert.Equal(head.ContentHash, recorded.SourceHeadHash);
        Assert.Equal(journalBefore, journey.Authority.InspectForHost().Journal);
        Assert.Throws<NotSupportedException>(() => ((IList<WorldMaterialActionEvidence>)recorded.Evidence).Clear());
        var empty = journey.Authority.ReadMaterialEvidenceForHost(2);
        Assert.Empty(empty.Evidence);
        Assert.Equal(first.Candidate.ContentHash, empty.BeforeHash);
        Assert.Equal(head.ContentHash, empty.AfterHash);
        journey.Step();
        var later = journey.Authority.ReadMaterialEvidenceForHost(1);
        Assert.Equal(expected.ContentHash, Assert.Single(later.Evidence).ContentHash);
        Assert.Equal(journey.World.ContentHash, later.SourceHeadHash);
        Assert.Equal(head.ContentHash, recorded.SourceHeadHash);
        Assert.Equal(2UL, recorded.SourceHeadRevision);
        journey.ReopenExactly();
        byte[] closed = File.ReadAllBytes(journey.Files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(journey.Files.Path))
            Assert.Equal(expected.ContentHash, Assert.Single(reopened.ReadMaterialEvidenceForHost(1).Evidence).ContentHash);
        Assert.Equal(closed, File.ReadAllBytes(journey.Files.Path));
    }

    [Fact]
    public void Resource_cut_observation_is_retrieved_against_actual_archives_and_missing_storage_fails()
    {
        using var files = new StorageFiles();
        var fixture = new OrdinaryCareArchiveFixture();
        ulong publication;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var before = fixture.Prepare255(new CareWorldSession(authority));
            var batch = fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep,
                [fixture.Journey.Command(before, fixture.Journey.Helper, fixture.Aid(before)),
                 fixture.Journey.Command(before, fixture.Journey.Player, fixture.UnownedSelfDose(before))]);
            var observed = new WorldStepProcessor().DecideWithMaterialEvidence(before, batch, authority.IsIdentityOccupiedForHost);
            var evidence = Assert.Single(observed.MaterialEvidence);
            var segment = Assert.IsType<WorldResourceArchiveSegment>(observed.SealedResourceSegment);
            Assert.Equal(before.ContentHash, segment.Reference.BoundaryHash);
            // This pin belongs to the prepared active resource window, not to the original closing bytes.
            Assert.NotEqual(before.ContentHash, evidence.SourceWorldHash);
            Assert.Equal(before.ContentHash, evidence.InputWorldHash);
            var committed = authority.Commit(batch);
            Assert.Equal(WorldSnapshotCodec.Encode(observed.Candidate), WorldSnapshotCodec.Encode(committed.Candidate));
            publication = committed.Candidate.Revision;
            var recorded = authority.ReadMaterialEvidenceForHost(publication);
            Assert.Equal(before.ContentHash, recorded.BeforeHash);
            Assert.Equal(committed.Candidate.ContentHash, recorded.AfterHash);
            Assert.Equal(evidence.ContentHash, Assert.Single(recorded.Evidence).ContentHash);
            Assert.Equal(WorldSnapshotCodec.Encode(before), authority.ReadResourceArchiveForHost(0).Segment.Encode());
        }
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
            Assert.Single(authority.ReadMaterialEvidenceForHost(publication).Evidence);
        files.Execute("DELETE FROM world_resource_segments");
        byte[] corrupted = File.ReadAllBytes(files.Path);
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Throws<InvalidDataException>(() => new WorldJournalRecovery().ReadMaterialEvidence(database, publication));
            // Even a selected earlier empty observation must authenticate all later archives.
            Assert.Throws<InvalidDataException>(() => new WorldJournalRecovery().ReadMaterialEvidence(database, 1));
        }
        Assert.Equal(corrupted, File.ReadAllBytes(files.Path));
    }

    [Theory]
    [InlineData("UPDATE world_commits SET outcomes=x'01' WHERE sequence=x'0000000000000002'")]
    [InlineData("DELETE FROM world_occupied_identity WHERE sequence=x'0000000000000002'")]
    [InlineData("UPDATE world_command_outcomes SET receipt=x'01' WHERE sequence=x'0000000000000002'")]
    [InlineData("DELETE FROM world_checkpoints")]
    [InlineData("UPDATE world_head SET advancing_steps=x'0000000000000000'")]
    public void Selecting_an_earlier_valid_observation_still_rejects_later_journal_corruption(string mutation)
    {
        using var journey = new MaterialEvidenceJourney();
        journey.Step([journey.CraftHelperDose()]);
        // A second, genuine rejected command still owns a permanent receipt and identities.
        var command = journey.CraftHelperDose();
        var stale = new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
            command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision + 1, command.Intent);
        var result = journey.Step([stale], accepted: false);
        Assert.Equal(WorldActionRejection.RevisionConflict, Assert.Single(result.Receipts).Rejection);
        journey.Authority.Dispose();
        journey.Files.Execute(mutation);
        byte[] corrupt = File.ReadAllBytes(journey.Files.Path);
        using (var database = WorldDatabase.Open(journey.Files.Path, create: false))
            Assert.Throws<InvalidDataException>(() => new WorldJournalRecovery().ReadMaterialEvidence(database, 1));
        Assert.Equal(corrupt, File.ReadAllBytes(journey.Files.Path));
    }

    [Fact]
    public void Invalid_sequences_disposed_authority_and_unqualified_profiles_are_refused()
    {
        using var journey = new MaterialEvidenceJourney();
        Assert.Throws<ArgumentOutOfRangeException>(() => journey.Authority.ReadMaterialEvidenceForHost(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => journey.Authority.ReadMaterialEvidenceForHost(1));
        journey.Step();
        Assert.Throws<ArgumentOutOfRangeException>(() => journey.Authority.ReadMaterialEvidenceForHost(ulong.MaxValue));
        journey.Authority.Dispose();
        Assert.Throws<ObjectDisposedException>(() => journey.Authority.ReadMaterialEvidenceForHost(1));
        using var other = new StorageFiles();
        using var old = SqliteWorldAuthority.CreateNew(other.Path, CareJourneyAdmission.CreateEmbedded("legacy-observation-refusal", "Traveler"));
        Assert.Throws<InvalidOperationException>(() => old.ReadMaterialEvidenceForHost(1));
    }
}
