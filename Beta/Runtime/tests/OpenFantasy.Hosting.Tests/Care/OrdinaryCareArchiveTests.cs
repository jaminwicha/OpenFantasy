using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class OrdinaryCareArchiveTests
{
    [Fact]
    public void Whole_batch_reservation_at_255_seals_before_actual_aid_and_unowned_second_writer_refusal()
    {
        var fixture = new OrdinaryCareArchiveFixture();
        using var files = new StorageFiles();
        WorldSnapshot before, after;
        WorldStepBatch batch;
        WorldActionReceipt[] receipts;
        WorldActionCommand aid, unowned;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            before = fixture.Prepare255(new CareWorldSession(authority));
            var aidIntent = fixture.Aid(before);
            var unownedIntent = fixture.UnownedSelfDose(before);
            Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, fixture.Journey.Helper, aidIntent));
            Assert.Equal(WorldActionRejection.Unavailable, WorldRestorativeActions.Evaluate(before, fixture.Journey.Player, unownedIntent));
            aid = fixture.Journey.Command(before, fixture.Journey.Helper, aidIntent);
            unowned = fixture.Journey.Command(before, fixture.Journey.Player, unownedIntent);
            batch = fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [aid, unowned]);
            Assert.Equal(2, batch.Actions.Count);
            var preview = authority.Preview(batch);
            var seal = Assert.IsType<WorldResourceArchiveSegment>(preview.SealedResourceSegment);
            Assert.Equal(WorldSnapshotCodec.Encode(before), seal.Encode());
            Assert.Equal(0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);

            var decision = authority.Commit(batch);
            after = decision.Candidate;
            receipts = decision.Receipts.ToArray();
            Assert.Equal(preview.Candidate.ContentHash, after.ContentHash);
            Assert.Equal(seal.Encode(), Assert.IsType<WorldResourceArchiveSegment>(decision.SealedResourceSegment).Encode());
            Assert.Equal(CommandReceiptStatus.Applied, receipts.Single(value => value.CommandId == aid.CommandId).Status);
            var rejected = receipts.Single(value => value.CommandId == unowned.CommandId);
            Assert.Equal(CommandReceiptStatus.Rejected, rejected.Status);
            int aidOrdinal = batch.Actions.ToList().FindIndex(value => value.CommandId == aid.CommandId);
            int rejectedOrdinal = batch.Actions.ToList().FindIndex(value => value.CommandId == unowned.CommandId);
            // If the funded helper was first, the patient/production pins are also stale by the
            // second command. If the unfunded player was first, exact lot ownership rejects it.
            Assert.Equal(aidOrdinal < rejectedOrdinal ? WorldActionRejection.RevisionConflict : WorldActionRejection.Unavailable,
                rejected.Rejection);
            var use = Assert.Single(after.Care!.Ordinary!.Uses);
            Assert.Equal(aid.CommandId, use.CommandId);
            Assert.Equal((uint)aidOrdinal, use.CommandOrdinal);
            Assert.Equal(after.Revision, use.WorldSequence);
            Assert.Equal(fixture.Journey.Player, use.RecipientActorId);
            var change = Assert.Single(after.ResourceHistory!.History.Batches);
            Assert.Equal(aid.CommandId, change.CommandId);
            Assert.Equal((uint)aidOrdinal, change.CommandOrdinal);
            Assert.IsType<WorldRestorativeUseResourceEvidence>(Assert.Single(after.ResourceHistory.Evidence));
            Assert.Equal(255UL, after.ResourceHistory.History.Basis.Revision);
            Assert.Equal(before.Revision, after.ResourceHistory.History.BasisWorldSequence);
            Assert.Equal(256UL, after.Progression!.Revision);
            Assert.Equal(before.Progression!.GetActor(fixture.Journey.Player).CurrentHp + 40,
                after.Progression.GetActor(fixture.Journey.Player).CurrentHp);
            AssertSingleDose(before, after, fixture, aid);
            Assert.Equal(before.Calendar.AddMilliseconds(1_800), after.Calendar);
            Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
            Assert.Equal(before.Space.Actors, after.Space.Actors);
            Assert.Equal(before.Progression.Currency, after.Progression.Currency);
            Assert.Empty(after.Care.Acknowledgments);
            Assert.Empty(after.Care.Treatments);
            Assert.Equal(1UL, authority.InspectForHost().Journal.ResourceArchiveCount);
        }

        byte[] closed = File.ReadAllBytes(files.Path);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            AssertArchive(authority, before, after);
            foreach (var command in batch.Actions)
                AssertReceipt(authority, fixture, command, receipts.Single(value => value.CommandId == command.CommandId));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(batch));
            Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(1UL, authority.InspectForHost().Journal.ResourceArchiveCount);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
        AssertSchemaAndPublication(files.Path, before, after);
    }

    [Fact]
    public void Actual_use_and_revoked_permission_survive_retrieved_closing_root_and_corruption_is_refused()
    {
        var fixture = new OrdinaryCareArchiveFixture();
        using var files = new StorageFiles();
        WorldSnapshot treated, revoked, after;
        WorldActionCommand aid, revoke, stale, archivedAttack;
        WorldActionReceipt aidReceipt, revokeReceipt, staleReceipt;
        WorldStepBatch staleBatch;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var session = new CareWorldSession(authority);
            var before = fixture.Prepare255(session);
            archivedAttack = before.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>().First().Request;
            aid = fixture.Journey.Command(before, fixture.Journey.Helper, fixture.Aid(before));
            var treatment = CareWorldFixture.Commit(session, fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [aid]));
            treated = treatment.Candidate;
            aidReceipt = Assert.Single(treatment.Receipts);
            Assert.Null(treatment.SealedResourceSegment);
            Assert.Equal(256, treated.ResourceHistory!.History.Batches.Count);
            AssertSingleDose(before, treated, fixture, aid);

            // The captured dose is still real. Revocation changes permission only; it does not
            // reserve or spend the remaining unit, heal again, or erase the completed use.
            var selected = fixture.Aid(treated);
            revoke = fixture.Revoke(treated);
            var revocation = CareWorldFixture.Commit(session,
                fixture.Journey.Batch(treated, WorldBatchKind.AdvancingStep, [revoke]));
            revoked = revocation.Candidate;
            revokeReceipt = Assert.Single(revocation.Receipts);
            Assert.Null(revocation.SealedResourceSegment);
            Assert.Equal(treated.Progression!.ContentHash, revoked.Progression!.ContentHash);
            Assert.Equal(treated.Production!.ContentHash, revoked.Production!.ContentHash);
            Assert.Equal(256, revoked.ResourceHistory!.History.Batches.Count);
            var permission = revoked.Care!.Ordinary!.GetPermission(fixture.Permission.PermissionId);
            Assert.False(permission.IsActive);
            Assert.Equal(fixture.Permission.GrantVersion, permission.GrantVersion);
            Assert.Equal(revoke.CommandId, permission.Revocation!.CommandId);
            Assert.Equal(aid.CommandId, Assert.Single(revoked.Care.Ordinary.Uses).CommandId);
            Assert.Equal(WorldActionRejection.RevisionConflict,
                WorldRestorativeActions.Evaluate(revoked, fixture.Journey.Helper, selected));

            stale = fixture.Journey.Command(revoked, fixture.Journey.Helper, selected);
            staleBatch = fixture.Journey.Batch(revoked, WorldBatchKind.AdvancingStep, [stale]);
            var preview = authority.Preview(staleBatch);
            Assert.Equal(WorldSnapshotCodec.Encode(revoked), Assert.IsType<WorldResourceArchiveSegment>(preview.SealedResourceSegment).Encode());
            Assert.Equal(0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            var decision = authority.Commit(staleBatch);
            after = decision.Candidate;
            staleReceipt = Assert.Single(decision.Receipts);
            Assert.Equal(CommandReceiptStatus.Rejected, staleReceipt.Status);
            Assert.Equal(WorldActionRejection.RevisionConflict, staleReceipt.Rejection);
            Assert.Equal(preview.Candidate.ContentHash, after.ContentHash);
            Assert.Empty(after.ResourceHistory!.History.Batches);
            Assert.Empty(after.ResourceHistory.Evidence);
            Assert.Equal(256UL, after.ResourceHistory.History.Basis.Revision);
            Assert.Equal(revoked.Revision, after.ResourceHistory.History.BasisWorldSequence);
            Assert.Equal(revoked.Care.ContentHash, after.Care!.ContentHash);
            Assert.Equal(revoked.Progression.ContentHash, after.Progression!.ContentHash);
            Assert.Equal(revoked.Production.ContentHash, after.Production!.ContentHash);
            Assert.Equal(revoked.Inventory.ContentHash, after.Inventory.ContentHash);
            Assert.Equal(revoked.Calendar.AddMilliseconds(1_800), after.Calendar);
            Assert.Equal(revoked.AdvancingSteps + 1, after.AdvancingSteps);
        }

        byte[] closed = File.ReadAllBytes(files.Path);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            var closing = AssertArchive(authority, revoked, after);
            Assert.Equal(256, closing.ResourceHistory!.History.Batches.Count);
            Assert.Equal(revoked.Care!.ContentHash, closing.Care!.ContentHash);
            Assert.False(closing.Care.Ordinary!.GetPermission(fixture.Permission.PermissionId).IsActive);
            Assert.Equal(aid.CommandId, Assert.Single(closing.Care.Ordinary.Uses).CommandId);
            Assert.Equal(treated.ContentHash, authority.ReadRecordedStateForHost(treated.Revision).StateHash);
            AssertReceipt(authority, fixture, aid, aidReceipt);
            AssertReceipt(authority, fixture, revoke, revokeReceipt);
            AssertReceipt(authority, fixture, stale, staleReceipt);
            Assert.True(authority.IsIdentityOccupiedForHost(archivedAttack.CommandId));
            Assert.False(after.IdentityIndex.Contains(archivedAttack.CommandId));
            var selected = fixture.Journey.Command(after, fixture.Journey.Helper, (UseRestorativeItemIntent)stale.Intent);
            var reusedIdentity = new WorldActionCommand(archivedAttack.CommandId, selected.PrincipalId, selected.OriginSessionId,
                selected.ActorId, selected.WorldId, selected.BranchId, selected.ExpectedActorRevision, selected.Intent);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(
                fixture.Journey.Batch(after, WorldBatchKind.AdvancingStep, [reusedIdentity])));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(staleBatch));
            Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(1UL, authority.InspectForHost().Journal.ResourceArchiveCount);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
        AssertSchemaAndPublication(files.Path, revoked, after);
        string[] corruptions =
        [
            "DELETE FROM world_resource_segments",
            "UPDATE world_resource_segments SET state=zeroblob(length(state))",
            "UPDATE world_resource_segments SET boundary_hash=zeroblob(32)",
            "DELETE FROM world_command_outcomes WHERE command_id=x'" + Convert.ToHexString(revoke.CommandId.ToByteArray()) + "'",
            "DELETE FROM world_occupied_identity WHERE identity=x'" + Convert.ToHexString(archivedAttack.CommandId.ToByteArray()) + "'"
        ];
        foreach (string sql in corruptions)
        {
            using var corrupted = new StorageFiles();
            File.Copy(files.Path, corrupted.Path);
            corrupted.Execute(sql);
            byte[] altered = File.ReadAllBytes(corrupted.Path);
            Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(corrupted.Path));
            Assert.Equal(altered, File.ReadAllBytes(corrupted.Path));
        }
    }

    private static void AssertSingleDose(WorldSnapshot before, WorldSnapshot after,
        OrdinaryCareArchiveFixture fixture, WorldActionCommand command)
    {
        var claim = Assert.Single(after.Production!.ConsumedItems);
        Assert.Equal(command.CommandId, claim.CommandId);
        Assert.Equal(fixture.Journey.Helper, claim.ActorId);
        Assert.Equal(fixture.Journey.Player, claim.RecipientActorId);
        Assert.Equal(IngredientLotKind.Source, claim.Ingredient.Kind);
        Assert.Equal(1UL, claim.Ingredient.Quantity);
        Assert.Equal(2UL, before.Inventory.Custody.GetLot(fixture.Journey.PotionLot).Quantity);
        Assert.Equal(1UL, after.Inventory.Custody.GetLot(fixture.Journey.PotionLot).Quantity);
        Assert.Equal(before.Inventory.Sources, after.Inventory.Sources);
        Assert.Equal(before.Inventory.AdmittedUnitCapacity, after.Inventory.AdmittedUnitCapacity);
    }

    private static WorldSnapshot AssertArchive(SqliteWorldAuthority authority, WorldSnapshot closing, WorldSnapshot head)
    {
        var retrieved = authority.ReadResourceArchiveForHost(0);
        Assert.Equal(0UL, retrieved.Segment.Reference.Ordinal);
        Assert.Equal(closing.Revision, retrieved.Segment.Reference.BoundarySequence);
        Assert.Equal(closing.ContentHash, retrieved.Segment.Reference.BoundaryHash);
        Assert.Equal(closing.Revision + 1, retrieved.PublicationSequence);
        Assert.Equal(head.Revision, retrieved.SourceHeadRevision);
        Assert.Equal(head.ContentHash, retrieved.SourceHeadHash);
        Assert.Equal(WorldSnapshotCodec.Encode(closing), retrieved.Segment.Encode());
        var boundary = WorldSnapshotCodec.Decode(retrieved.Segment.Encode());
        Assert.Equal((ushort)26, boundary.ProfileVersion);
        Assert.Equal(closing.ContentHash, boundary.ContentHash);
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(head)));
        var restored = WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(head), retrieved.Segment.Anchor);
        Assert.Equal(WorldSnapshotCodec.Encode(head), WorldSnapshotCodec.Encode(restored));
        Assert.Equal(closing.ContentHash, authority.ReadRecordedStateForHost(closing.Revision).StateHash);
        Assert.Throws<ArgumentOutOfRangeException>(() => authority.ReadResourceArchiveForHost(1));
        return boundary;
    }

    private static void AssertReceipt(SqliteWorldAuthority authority, OrdinaryCareArchiveFixture fixture,
        WorldActionCommand command, WorldActionReceipt expected) =>
        Assert.Equal(WorldReceiptCodecV1.Encode(expected), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
            authority.FindReceipt(command, fixture.Journey.DeliveryFor(command.ActorId)))));

    private static void AssertSchemaAndPublication(string path, WorldSnapshot closing, WorldSnapshot head)
    {
        using var database = WorldDatabase.Open(path, create: false);
        Assert.Equal(7, database.SchemaVersion);
        var stored = Assert.Single(database.ReadResourceArchives());
        Assert.Equal(WorldSnapshotCodec.Encode(closing), stored.State);
        Assert.Equal(closing.Revision + 1, stored.PublicationSequence);
        var commit = Assert.Single(database.ReadJournal(), value => value.Sequence == stored.PublicationSequence);
        Assert.Equal(WorldCommitKind.AdvancingStep, commit.Kind);
        Assert.Equal(closing.ContentHash, commit.BeforeHash);
        Assert.Equal(head.ContentHash, commit.AfterHash);
    }
}
