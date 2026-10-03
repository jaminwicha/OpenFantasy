using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerPermissions;

public sealed class ControllerPermissionJournalTests
{
    [Fact]
    public void Original_principals_offer_accept_revoke_and_reoffer_across_exact_genesis_reopening()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        byte[] genesis = WorldSnapshotCodec.Encode(fixture.Genesis);
        WorldSnapshot offered;
        WorldActionCommand offer;
        WorldActionReceipt offerReceipt;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            Assert.True(authority.SupportsControllerRecovery);
            Assert.Empty(authority.ReadControllerAllowancesForHost());
            offer = fixture.Offer(fixture.Genesis);
            var decision = fixture.Commit(authority, offer);
            offerReceipt = Assert.Single(decision.Receipts);
            Assert.Equal(WorldActionRejection.None, offerReceipt.Rejection);
            offered = decision.Candidate;
            Assert.Equal(WorldControlPermissionStatus.Offered, Assert.Single(offered.ControlPermissions!.Slots).Status);
            Assert.Equal(1UL, offered.ControlPermissions.Find(fixture.Owner, fixture.Grantee)!.Revision);
            Assert.Equal(WorldReceiptCodecV1.Encode(offerReceipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(authority.FindReceipt(offer, fixture.Delivery(fixture.Owner)))));
            Assert.Same(offered, authority.ReadStateForHost());
            Assert.Equal(1UL, authority.InspectForHost().Journal.CommandOutcomeCount);
        }
        WorldSnapshot accepted;
        WorldActionCommand accept;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, genesis))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(offered), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            accept = fixture.Accept(authority.ReadStateForHost());
            Assert.Equal(fixture.Grantee, accept.ActorId);
            Assert.Equal(fixture.Delivery(fixture.Grantee).PrincipalId, accept.PrincipalId);
            Assert.Equal(fixture.Delivery(fixture.Grantee).SessionId, accept.OriginSessionId);
            Assert.NotEqual(offer.PrincipalId, accept.PrincipalId);
            accepted = fixture.Apply(authority, accept);
            Assert.Equal(2UL, accepted.ControlPermissions!.Find(fixture.Owner, fixture.Grantee)!.Revision);
            Assert.NotEqual(offered.ControlPermissions.ContentHash, accepted.ControlPermissions.ContentHash);
            var acceptedSlot = Assert.Single(accepted.ControlPermissions.Slots);
            Assert.Equal(WorldControlPermissionStatus.Accepted, acceptedSlot.Status);
            Assert.Equal(offer.CommandId, acceptedSlot.GrantCommandId);
            Assert.Equal(accept.CommandId, acceptedSlot.AcceptanceCommandId);
            Assert.Equal(accept.PrincipalId, acceptedSlot.Terms!.GranteePrincipalId);
            Assert.Equal(offer.PrincipalId, acceptedSlot.Terms.OwnerPrincipalId);
            Assert.False(accepted.Inventory.CanControl(fixture.Delivery(fixture.Grantee), fixture.Owner));
            Assert.Equal(WorldControllerMode.Human, accepted.Controllers!.Get(fixture.Owner).Mode);
        }
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, genesis))
        {
            var revoked = fixture.Apply(authority, fixture.Revoke(authority.ReadStateForHost()));
            Assert.Equal(3UL, revoked.ControlPermissions!.Find(fixture.Owner, fixture.Grantee)!.Revision);
            Assert.Equal(WorldControlPermissionStatus.Revoked, Assert.Single(revoked.ControlPermissions.Slots).Status);
            var reoffered = fixture.Apply(authority, fixture.Offer(revoked, 3, WorldControlCapabilities.Traversal));
            Assert.Equal(4UL, reoffered.ControlPermissions!.Find(fixture.Owner, fixture.Grantee)!.Revision);
            Assert.NotEqual(offered.ControlPermissions.Find(fixture.Owner, fixture.Grantee)!.GrantCommandId,
                reoffered.ControlPermissions.Find(fixture.Owner, fixture.Grantee)!.GrantCommandId);
            final = fixture.Apply(authority, fixture.Accept(reoffered));
            Assert.Equal(5UL, final.ControlPermissions!.Find(fixture.Owner, fixture.Grantee)!.Revision);
            Assert.Equal(5UL, authority.InspectForHost().Journal.CommandOutcomeCount);
            PermissionStorageFixture.AssertGameplayUnchanged(fixture.Genesis, final);
            Assert.NotNull(authority.FindReceipt(offer, fixture.Delivery(fixture.Owner)));
            Assert.NotNull(authority.FindReceipt(accept, fixture.Delivery(fixture.Grantee)));
        }
        using (var database = WorldDatabase.Open(files.Path, false))
        {
            Assert.Equal(11, database.SchemaVersion);
            Assert.Equal(genesis, database.ReadGenesis());
            Assert.All(database.ReadJournal(), entry => Assert.Equal(WorldCommitKind.ControlPermission, entry.Kind));
            Assert.Equal(5, database.ReadCommandOutcomes().Count());
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(new WorldJournalRecovery().Recover(database).State));
        }
    }

    [Fact]
    public void Captured_stale_terms_and_stolen_command_identity_cannot_replace_an_accepted_grant()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var offer = fixture.Offer(fixture.Genesis);
        var offered = fixture.Apply(authority, offer);
        var captured = fixture.Accept(offered);
        var revoked = fixture.Apply(authority, fixture.Revoke(offered));
        var current = fixture.Apply(authority, fixture.Offer(revoked, 2, WorldControlCapabilities.Traversal));
        var stale = fixture.Commit(authority, captured);
        Assert.NotEqual(WorldActionRejection.None, Assert.Single(stale.Receipts).Rejection);
        Assert.Equal(current.ControlPermissions!.ContentHash, stale.Candidate.ControlPermissions!.ContentHash);
        var slot = stale.Candidate.ControlPermissions.Find(fixture.Owner, fixture.Grantee)!;
        var wrongTerms = fixture.Commands.Command(stale.Candidate, fixture.Grantee,
            new AcceptControlPermissionIntent(fixture.Owner, slot.Revision, Assert.IsType<EntityId>(slot.GrantCommandId),
                ContentHash.Compute(new byte[] { 7 })));
        var refused = fixture.Commit(authority, wrongTerms);
        Assert.NotEqual(WorldActionRejection.None, Assert.Single(refused.Receipts).Rejection);
        Assert.Equal(current.ControlPermissions.ContentHash, refused.Candidate.ControlPermissions!.ContentHash);
        var stolen = new WorldActionCommand(offer.CommandId, captured.PrincipalId, captured.OriginSessionId,
            captured.ActorId, captured.WorldId, captured.BranchId, captured.ExpectedActorRevision, captured.Intent);
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(offer, fixture.Delivery(fixture.Grantee)));
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(
            fixture.Commands.Batch(refused.Candidate, WorldBatchKind.ControlPermission, [stolen])));
        Assert.Same(refused.Candidate, authority.ReadStateForHost());
        var accepted = fixture.Apply(authority, fixture.Accept(refused.Candidate));
        Assert.False(accepted.Inventory.CanControl(fixture.Delivery(fixture.Grantee), fixture.Owner));
    }

    [Fact]
    public void Permission_actions_preserve_an_actual_reading_hold_and_its_captured_dialogue()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        fixture.Journey.WalkTo(authority, fixture.Owner,
            fixture.Journey.Beside(fixture.Genesis, fixture.Owner, fixture.Other));
        var state = authority.ReadStateForHost();
        var speak = fixture.Commands.Command(state, fixture.Owner,
            new SpeakToActorIntent(fixture.Other, state.Space.GetActor(fixture.Other).Revision));
        var held = fixture.Journey.Commit(authority, WorldBatchKind.AdvancingStep, [speak]).Candidate;
        Assert.True(held.HasReadingHold);
        var offered = fixture.Apply(authority, fixture.Offer(held));
        var accepted = fixture.Apply(authority, fixture.Accept(offered));
        var revoked = fixture.Apply(authority, fixture.Revoke(accepted));
        Assert.True(revoked.HasReadingHold);
        PermissionStorageFixture.AssertGameplayUnchanged(held, revoked);
        Assert.Equal(WorldSnapshotCodec.Encode(revoked), WorldSnapshotCodec.Encode(
            authority.ReadRecordedStateForHost(revoked.Revision).State));
    }

    [Fact]
    public void A_checksum_valid_accepted_head_cannot_replace_its_missing_acceptance_journal()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var offered = fixture.Apply(authority, fixture.Offer(fixture.Genesis));
            var accepted = fixture.Apply(authority, fixture.Accept(offered));
            Assert.Equal(WorldControlPermissionStatus.Accepted, Assert.Single(accepted.ControlPermissions!.Slots).Status);
        }
        // Retain the authentic accepted head and its checksum, but remove all acceptance evidence.
        files.Execute("DELETE FROM world_command_outcomes WHERE sequence=X'0000000000000002'; DELETE FROM world_occupied_identity WHERE sequence=X'0000000000000002'; DELETE FROM world_commits WHERE sequence=X'0000000000000002'");
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path,
            WorldSnapshotCodec.Encode(fixture.Genesis)));
        Assert.Throws<InvalidDataException>(() => RecordedWorldReader.ReadForHost(files.Path, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Permission_publication_fault_recovers_the_complete_before_or_after_transaction(bool committed)
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        var command = fixture.Offer(fixture.Genesis);
        var batch = fixture.Commands.Batch(fixture.Genesis, WorldBatchKind.ControlPermission, [command]);
        var expected = new WorldStepProcessor().Decide(fixture.Genesis, batch);
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            Assert.Throws<IOException>(() => authority.Commit(batch, boundary =>
            {
                if (boundary == (committed ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
                    throw new IOException("Injected permission publication failure.");
            }));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
            Assert.Throws<InvalidOperationException>(() => authority.FindReceipt(command, fixture.Delivery(fixture.Owner)));
            Assert.Throws<InvalidOperationException>(() => authority.ReadRecordedStateForHost(0));
            Assert.Throws<InvalidOperationException>(() => authority.Commit(batch));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(committed ? expected.Candidate : fixture.Genesis),
            WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(committed, reopened.IsIdentityOccupiedForHost(batch.CauseId));
        Assert.Equal(committed, reopened.IsIdentityOccupiedForHost(command.CommandId));
        Assert.Equal(committed, reopened.IsIdentityOccupiedForHost(Assert.Single(expected.Receipts).ResultId));
        Assert.Equal(committed, reopened.FindReceipt(command, fixture.Delivery(fixture.Owner)) is not null);
        Assert.Equal(committed ? 1UL : 0UL, reopened.InspectForHost().Journal.CommandOutcomeCount);
        Assert.Empty(reopened.ReadControllerAllowancesForHost());
    }

    [Theory]
    [InlineData("UPDATE world_commits SET after_hash=zeroblob(32) WHERE sequence=X'0000000000000003'")]
    [InlineData("UPDATE world_command_outcomes SET receipt=X'00' WHERE sequence=X'0000000000000003'")]
    [InlineData("DELETE FROM world_occupied_identity WHERE sequence=X'0000000000000003'")]
    [InlineData("UPDATE world_head SET hash=zeroblob(32)")]
    public void Later_corruption_prevents_earlier_permission_history_or_receipts_from_becoming_available(string corruption)
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var offered = fixture.Apply(authority, fixture.Offer(fixture.Genesis));
            var accepted = fixture.Apply(authority, fixture.Accept(offered));
            fixture.Apply(authority, fixture.Revoke(accepted));
        }
        files.Execute(corruption);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path,
            WorldSnapshotCodec.Encode(fixture.Genesis)));
        Assert.Throws<InvalidDataException>(() => RecordedWorldReader.ReadForHost(files.Path, 1));
    }
}
