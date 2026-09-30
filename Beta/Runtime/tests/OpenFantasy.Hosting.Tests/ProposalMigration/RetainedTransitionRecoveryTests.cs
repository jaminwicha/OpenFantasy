using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.ProposalMigration;

/// <summary>
/// Fault boundaries of the real transition append and damaged transitioned saves. These are in-process
/// injected faults through the same implementation; no operating-system process kill is claimed here.
/// </summary>
[Collection(RetainedTransitionCollection.Name)]
public sealed class RetainedTransitionRecoveryTests(RetainedTransitionFixture fixture)
{
    [Fact]
    public void Faults_before_and_after_the_transition_commit_leave_either_no_rows_or_the_complete_transition()
    {
        using StorageFiles files = fixture.CopySource();
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            (PreparedWorldProposalTransition prepared, bool checkpoint) = Prepare(database);
            int commits = database.ReadJournal().Count();
            int archives = database.ReadMaterialCareArchives().Count();
            StoredWorldIdentity[] identities = database.ReadIdentities().ToArray();
            byte[] head = database.ReadHead().State;
            Assert.Throws<IOException>(() => database.AppendProposalTransition(prepared, checkpoint, at =>
            {
                if (at == WorldProposalTransitionBoundary.BeforeCommit) throw new IOException("before transition commit");
            }));
            Assert.Equal(commits, database.ReadJournal().Count());
            Assert.Equal(archives, database.ReadMaterialCareArchives().Count());
            Assert.Equal(identities, database.ReadIdentities().ToArray());
            Assert.Equal(head, database.ReadHead().State);
            Assert.Equal(WorldDatabaseSchemaEight.Version, database.SchemaVersion);
        }
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            (PreparedWorldProposalTransition prepared, bool checkpoint) = Prepare(database);
            Assert.Throws<IOException>(() => database.AppendProposalTransition(prepared, checkpoint, at =>
            {
                if (at == WorldProposalTransitionBoundary.AfterCommit) throw new IOException("after transition commit");
            }));
        }
        // Reopening decides from durable rows alone: the whole transition committed and replays exactly.
        using SqliteWorldAuthority recovered = SqliteWorldAuthority.OpenExisting(files.Path, fixture.GenesisBytes);
        Assert.Equal(fixture.UpgradedHead.ContentHash, recovered.ReadStateForHost().ContentHash);
    }

    [Fact]
    public void A_fault_after_the_consistent_backup_publishes_nothing_and_keeps_the_source()
    {
        using StorageFiles source = fixture.CopySource();
        using var destination = new StorageFiles();
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeCoordinatedToProposals(source.Path, destination.Path,
            fixture.Request, at =>
            {
                if (at == WorldProposalTransitionBoundary.AfterBackup) throw new IOException("after backup");
            }));
        Assert.False(File.Exists(destination.Path));
        using WorldDatabase database = WorldDatabase.Open(source.Path, create: false);
        Assert.Equal(RetainedTransitionFixture.SourceRevision, database.ReadHead().Sequence);
        Assert.DoesNotContain(database.ReadJournal(), row => row.Kind == WorldCommitKind.ProfileMigration);
    }

    [Theory]
    [InlineData("forged-request")]
    [InlineData("missing-transition-archive")]
    [InlineData("damaged-lived-archive")]
    [InlineData("unscheduled-checkpoint")]
    public void Damaged_transition_context_archives_or_checkpoints_refuse_recovery(string mutation)
    {
        using StorageFiles files = fixture.CopyUpgraded();
        string latest = "(SELECT MAX(sequence) FROM world_commits)";
        WorldProposalTransition request = fixture.Request;
        string sql = mutation switch
        {
            "forged-request" => "UPDATE world_commits SET accepted_batch=x'" + Convert.ToHexString(WorldProposalTransitionCodecV1.Encode(
                new WorldProposalTransition(RetainedTransitionFixture.Cause("forged"), request.WorldId, request.BranchId,
                    request.ExpectedRevision, request.ExpectedAdvancingSteps, request.ExpectedHeadHash,
                    request.SourceAdmissionVersion, request.ExpectedAnchorHash, request.TransitionOrdinal))) + "' WHERE sequence=" + latest,
            "missing-transition-archive" => "DELETE FROM world_material_care_segments WHERE publication_sequence=" + latest,
            "damaged-lived-archive" => "UPDATE world_material_care_segments SET boundary_hash=zeroblob(32) WHERE ordinal=x'0000000000000001'",
            "unscheduled-checkpoint" => "INSERT INTO world_checkpoints SELECT sequence,advancing_steps,state,hash FROM world_head",
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        files.Execute(sql);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path, fixture.GenesisBytes));
        // Selecting an earlier valid state still authenticates the later transition and archives.
        using WorldDatabase damaged = WorldDatabase.Open(files.Path, create: false);
        Assert.Throws<InvalidDataException>(() => new WorldJournalRecovery().ReadRecordedState(damaged, 0));

    }

    private (PreparedWorldProposalTransition Prepared, bool Checkpoint) Prepare(WorldDatabase database)
    {
        PreparedWorldProposalTransition prepared = new WorldProposalTransitionPreparation().Prepare(fixture.SourceHead,
            WorldSnapshotCodec.Decode(fixture.GenesisBytes).IdentityIndex, fixture.SourceHead.ContentHash, fixture.Request,
            identity => database.ContainsIdentity(identity));
        WorldJournalAdvance advance = WorldJournalPolicy.Advance(fixture.SourceUsage, prepared.Stored.Sequence,
            prepared.Stored.AdvancingSteps, 0, (ulong)prepared.Stored.AcceptedBatch.Length,
            (ulong)prepared.Stored.Outcomes.Length, null, fixture.SourceHead.ProfileVersion, 0, (ulong)prepared.Archive.State.Length);
        return (prepared, advance.WriteCheckpoint);
    }
}
