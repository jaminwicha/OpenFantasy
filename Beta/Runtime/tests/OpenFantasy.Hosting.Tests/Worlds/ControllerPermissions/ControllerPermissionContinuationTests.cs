using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerPermissions;

public sealed class ControllerPermissionContinuationTests
{
    [Fact]
    public void Accepted_permission_survives_measured_allowance_backup_expiry_and_owner_return_without_becoming_control()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        using var copy = new StorageFiles();
        byte[] genesis = WorldSnapshotCodec.Encode(fixture.Genesis);
        WorldSnapshot saved;
        ControllerAllowanceProgress savedProgress;
        ContentHash permissionHash;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var offered = fixture.Apply(authority, fixture.Offer(fixture.Genesis));
            var accepted = fixture.Apply(authority, fixture.Accept(offered));
            permissionHash = accepted.ControlPermissions!.ContentHash;
            saved = fixture.Event(authority, fixture.Owner, WorldControllerEventKind.Disconnected);
            var segment = authority.BeginControllerRecoverySegment();
            savedProgress = Assert.Single(segment.Allowances) with { ElapsedHostMilliseconds = 12_345 };
            authority.CheckpointControllerAllowances(segment.Generation, [savedProgress]);
            authority.BackupForHost(copy.Path);
        }
        WorldSnapshot final;
        using (var reopened = SqliteWorldAuthority.OpenExisting(copy.Path, genesis))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(saved), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Equal(savedProgress, Assert.Single(reopened.ReadControllerAllowancesForHost()));
            Assert.Equal(permissionHash, saved.ControlPermissions!.ContentHash);
            Assert.Throws<InvalidOperationException>(() => fixture.Event(reopened, fixture.Owner, WorldControllerEventKind.AllowanceExpired));
            Assert.Equal(WorldSnapshotCodec.Encode(saved), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Equal(WorldControllerMode.ReconnectAllowance, reopened.ReadStateForHost().Controllers!.Get(fixture.Owner).Mode);
            Assert.Equal(savedProgress, Assert.Single(reopened.ReadControllerAllowancesForHost()));
            var segment = reopened.BeginControllerRecoverySegment();
            reopened.CheckpointControllerAllowances(segment.Generation,
                [Assert.Single(segment.Allowances) with { ElapsedHostMilliseconds = 30_000 }]);
            var expired = fixture.Event(reopened, fixture.Owner, WorldControllerEventKind.AllowanceExpired);
            Assert.Equal(WorldControllerMode.Autonomous, expired.Controllers!.Get(fixture.Owner).Mode);
            Assert.Equal(permissionHash, expired.ControlPermissions!.ContentHash);
            Assert.Empty(reopened.ReadControllerAllowancesForHost());
            Assert.False(expired.Inventory.CanControl(fixture.Delivery(fixture.Grantee), fixture.Owner));
            final = fixture.Event(reopened, fixture.Owner, WorldControllerEventKind.Reconnected);
            Assert.Equal(WorldControllerMode.Human, final.Controllers!.Get(fixture.Owner).Mode);
            Assert.Equal(permissionHash, final.ControlPermissions!.ContentHash);
            Assert.Equal(fixture.Genesis.Calendar, final.Calendar);
            Assert.Equal(fixture.Genesis.AdvancingSteps, final.AdvancingSteps);
            Assert.Equal(fixture.Genesis.Inventory.ContentHash, final.Inventory.ContentHash);
            Assert.Equal(fixture.Genesis.Progression!.ContentHash, final.Progression!.ContentHash);
        }
        using (var original = SqliteWorldAuthority.OpenExisting(files.Path, genesis))
        {
            Assert.Equal(savedProgress, Assert.Single(original.ReadControllerAllowancesForHost()));
            Assert.Equal(WorldSnapshotCodec.Encode(saved), WorldSnapshotCodec.Encode(original.ReadStateForHost()));
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(copy.Path, genesis);
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        Assert.Equal(permissionHash, recovered.ReadRecordedStateForHost(2).State.ControlPermissions!.ContentHash);
    }

    [Fact]
    public void An_actual_material_archive_cut_preserves_the_accepted_permission_and_its_original_commands_in_a_copy()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        using var copy = new StorageFiles();
        byte[] genesis = WorldSnapshotCodec.Encode(fixture.Genesis);
        WorldSnapshot final;
        WorldSnapshot closing;
        WorldStepDecision publication;
        WorldActionCommand offer;
        WorldActionCommand accept;
        ContentHash permissions;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            offer = fixture.Offer(fixture.Genesis);
            var offered = fixture.Apply(authority, offer);
            accept = fixture.Accept(offered);
            var accepted = fixture.Apply(authority, accept);
            permissions = accepted.ControlPermissions!.ContentHash;
            Assert.Equal(0UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            (closing, publication) = PublishArchive(authority, fixture);
            final = authority.ReadStateForHost();
            var sealedArchive = Assert.IsType<WorldMaterialCareArchiveSegment>(publication.SealedMaterialCareSegment);
            Assert.Equal(WorldSnapshotCodec.Encode(closing), sealedArchive.Encode());
            Assert.Equal(closing.ContentHash, sealedArchive.Reference.BoundaryHash);
            Assert.Equal(permissions, closing.ControlPermissions!.ContentHash);
            Assert.Equal(permissions, final.ControlPermissions!.ContentHash);
            Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            Assert.True(final.AdvancingSteps > accepted.AdvancingSteps);
            Assert.Equal(accepted.Inventory.ContentHash, final.Inventory.ContentHash);
            Assert.Equal(accepted.Progression!.ContentHash, final.Progression!.ContentHash);
            authority.BackupForHost(copy.Path);
        }
        using (var reopened = SqliteWorldAuthority.OpenExisting(copy.Path, genesis))
        {
            Assert.True(reopened.SupportsControllerRecovery);
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            var retrieved = reopened.ReadMaterialCareArchiveForHost(0);
            Assert.Equal(WorldSnapshotCodec.Encode(closing), retrieved.Segment.Encode());
            Assert.Equal(publication.Candidate.Revision, retrieved.PublicationSequence);
            Assert.Equal(final.ContentHash, retrieved.SourceHeadHash);
            Assert.Equal(permissions, retrieved.Segment.Continued.ControlPermissions!.ContentHash);
            Assert.NotNull(reopened.FindReceipt(offer, fixture.Delivery(fixture.Owner)));
            Assert.NotNull(reopened.FindReceipt(accept, fixture.Delivery(fixture.Grantee)));
            Assert.True(reopened.IsIdentityOccupiedForHost(offer.CommandId));
            Assert.True(reopened.IsIdentityOccupiedForHost(accept.CommandId));
            Assert.Equal(WorldControlPermissionStatus.Accepted,
                Assert.Single(reopened.ReadStateForHost().ControlPermissions!.Slots).Status);
        }
        using var database = WorldDatabase.Open(copy.Path, false);
        Assert.Equal(11, database.SchemaVersion);
        Assert.Equal(genesis, database.ReadGenesis());
    }

    [Fact]
    public void Schema_eleven_rejects_a_forged_later_activation_even_when_its_snapshot_and_all_checksums_are_authentic()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        WorldSnapshot later;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var offered = fixture.Apply(authority, fixture.Offer(fixture.Genesis));
            later = fixture.Apply(authority, fixture.Accept(offered));
        }
        ControllerRecoveryState forged;
        using (var database = WorldDatabase.Open(files.Path, false))
        {
            var retained = database.ReadControllerRecovery();
            // The later root really exists. Only its claimed schema-nine activation provenance is false.
            forged = retained with { Activation = new(true, later.Revision, later.ContentHash, []) };
        }
        byte[] provenance = ControllerRecoveryState.EncodeActivation(forged.Activation);
        files.Execute($"UPDATE controller_recovery_activation SET provenance=X'{Convert.ToHexString(provenance)}', hash=X'{Convert.ToHexString(ContentHash.Compute(provenance).ToByteArray())}'; UPDATE controller_recovery_segment SET hash=X'{Convert.ToHexString(forged.Hash.ToByteArray())}'");
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path,
            WorldSnapshotCodec.Encode(fixture.Genesis)));
        Assert.Throws<InvalidDataException>(() => RecordedWorldReader.ReadForHost(files.Path, 1));
    }

    private static (WorldSnapshot Closing, WorldStepDecision Publication) PublishArchive(
        SqliteWorldAuthority authority, PermissionStorageFixture fixture)
    {
        var state = authority.ReadStateForHost();
        var helper = fixture.Other;
        var lot = state.Inventory.Custody.Lots.First(value => value.CustodianId == state.GetResident(helper).InventoryCustodianId);
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, helper, fixture.Owner,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(fixture.Owner).InventoryCustodianId, 1, lot.Revision);
        for (int index = 0; index <= WorldMaterialProposalBook.MaximumOutcomes; index++)
        {
            WorldSnapshot before = state;
            var offered = fixture.Journey.Commit(authority, WorldBatchKind.AdvancingStep,
                [fixture.Commands.Command(state, helper, new ProposeMaterialIntent(terms, state.AdvancingSteps + 32))]);
            state = offered.Candidate;
            if (offered.SealedMaterialCareSegment is not null) return (before, offered);
            var open = Assert.Single(state.Coordination!.Proposals!.Open);
            before = state;
            var withdrawn = fixture.Journey.Commit(authority, WorldBatchKind.AdvancingStep,
                [fixture.Commands.Command(state, helper, new WithdrawMaterialProposalIntent(open.ProposalId))]);
            state = withdrawn.Candidate;
            if (withdrawn.SealedMaterialCareSegment is not null) return (before, withdrawn);
        }
        throw new InvalidOperationException("Ordinary proposal outcome pressure must cut within its finite capacity window.");
    }
}
