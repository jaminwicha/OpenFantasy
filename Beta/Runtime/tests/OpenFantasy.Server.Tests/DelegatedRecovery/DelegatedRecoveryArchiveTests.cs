using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.DelegatedRecovery;

public sealed class DelegatedRecoveryArchiveTests(DelegatedRecoveryFixture fixture) : IClassFixture<DelegatedRecoveryFixture>
{
    [Fact]
    public void Ordinary_cut_retains_exact_original_assignment_paid_retreat_closed_gift_and_complete_prefix()
    {
        var cut = fixture.Boundaries.Single(row => row.Name == "archive");
        Assert.Equal(255, cut.Before.ResourceHistory!.History.Batches.Count);
        Assert.NotNull(cut.After.Retreat!.Active);
        Assert.Equal(fixture.Witness, cut.After.EffectiveControl!.Find(fixture.Owner)!.Witness);
        using (var before = WorldDatabase.Open(cut.BeforeDatabase, false))
        using (var after = WorldDatabase.Open(cut.AfterDatabase, false))
        {
            Assert.Equal(12, after.SchemaVersion);
            Assert.Equal(fixture.GenesisBytes, after.ReadGenesis());
            Assert.Empty(before.ReadMaterialCareArchives());
            var archived = Assert.Single(after.ReadMaterialCareArchives());
            Assert.Equal(cut.After.Revision, archived.PublicationSequence);
            Assert.Equal(WorldSnapshotCodec.Encode(cut.Before), archived.State);
            Assert.Empty(after.ReadResourceArchives());
            Assert.Equal(before.ReadJournal().Select(JournalRow),
                after.ReadJournal().Where(row => row.Sequence <= cut.Before.Revision).Select(JournalRow));
            Assert.Equal(before.ReadCommandOutcomes().Select(CommandRow),
                after.ReadCommandOutcomes().Where(row => row.Sequence <= cut.Before.Revision).Select(CommandRow));
            Assert.Subset(after.ReadIdentities().ToHashSet(), before.ReadIdentities().ToHashSet());
            Assert.Equal(before.ReadCheckpoints().Select(row => Convert.ToHexString(row.State)),
                after.ReadCheckpoints().Where(row => row.Sequence <= cut.Before.Revision).Select(row => Convert.ToHexString(row.State)));
        }
        byte[] original = File.ReadAllBytes(cut.AfterDatabase);
        using (var authority = SqliteWorldAuthority.OpenExisting(cut.AfterDatabase, fixture.GenesisBytes))
        {
            fixture.AssertPureRecovery(cut, authority.ReadStateForHost(), afterPublication: true);
            fixture.AssertPaidReceipt(authority);
            Assert.Equal(WorldSnapshotCodec.Encode(cut.Before),
                WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(cut.Before.Revision).State));
            var archived = authority.ReadMaterialCareArchiveForHost(0);
            Assert.Equal(WorldSnapshotCodec.Encode(cut.Before), archived.Segment.Encode());
            var closing = WorldSnapshotCodec.Decode(archived.Segment.Encode());
            Assert.Equal(fixture.GiftOutcome.Encode(), Assert.Single(closing.Coordination!.Proposals!.Outcomes).Encode());
            var selected = authority.ReadSelectedMaterialProposalHistory(fixture.GiftOutcome.Proposal.ProposalId,
                fixture.Owner, fixture.Delivery(fixture.Grantee), fixture.Witness);
            Assert.NotNull(selected);
            Assert.Equal(MaterialProposalStatus.Withdrawn, selected.Status);
            Assert.Null(selected.SenderPresenceRevision);
        }
        Assert.Equal(original, File.ReadAllBytes(cut.AfterDatabase));
    }

    [Fact]
    public void Revocation_refuses_new_private_authority_but_archived_participant_history_and_original_receipts_survive()
    {
        var revoked = fixture.Boundaries.Single(row => row.Name == "revoke");
        Assert.Null(revoked.After.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.Equal(WorldControlPermissionStatus.Revoked, revoked.After.ControlPermissions!.Find(fixture.Owner, fixture.Grantee)!.Status);
        Assert.Equal(EncounterRetreatCodecV1.Encode(revoked.Before.Retreat!), EncounterRetreatCodecV1.Encode(revoked.After.Retreat!));
        Assert.Equal(revoked.Before.Calendar, revoked.After.Calendar);
        Assert.Equal(revoked.Before.AdvancingSteps, revoked.After.AdvancingSteps);
        Assert.Equal(revoked.Before.Progression!.ContentHash, revoked.After.Progression!.ContentHash);
        using var authority = SqliteWorldAuthority.OpenExisting(revoked.AfterDatabase, fixture.GenesisBytes);
        fixture.AssertPaidReceipt(authority);
        EntityId subject = fixture.GiftOutcome.Proposal.ProposalId;
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedMaterialProposalHistory(subject,
            fixture.Owner, fixture.Delivery(fixture.Grantee), fixture.Witness));
        // Both permanent participants still have their own independent history entitlement after revocation.
        foreach (EntityId participant in new[] { fixture.Owner, fixture.Grantee })
        {
            var record = authority.ReadRecordedProposalForController(subject, participant, fixture.Delivery(participant),
                (_, outcome) => Assert.IsType<WorldMaterialProposalOutcome>(outcome));
            Assert.Equal(fixture.GiftOutcome.Encode(), record.Encode());
        }
        Assert.Null(authority.ReadSelectedMaterialProposalHistory(subject, fixture.Opponent,
            fixture.Delivery(fixture.Opponent), null));
        Assert.Equal(WorldSnapshotCodec.Encode(revoked.After), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    [Fact]
    public void Reopened_paid_continuation_finishes_once_with_exact_route_costs_and_original_home_bindings()
    {
        var resolution = fixture.Boundaries.Single(row => row.Name == "resolution");
        string copy = Path.Combine(fixture.DirectoryPath, "resolution-replay-" + Guid.NewGuid().ToString("N") + ".sqlite3");
        fixture.CloneBoundary("resolution", copy);
        using var authority = SqliteWorldAuthority.OpenExisting(copy, fixture.GenesisBytes);
        fixture.AssertPureRecovery(resolution, authority.ReadStateForHost(), false);
        var result = authority.Commit(resolution.Batch);
        fixture.AssertPureRecovery(resolution, result.Candidate, true);
        fixture.AssertPaidCompletion(result.Candidate);
        fixture.AssertPaidReceipt(authority);
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(resolution.Batch));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Final), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Final),
            WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(fixture.Final.Revision).State));
    }

    private static string JournalRow(StoredWorldCommit row) => $"{row.Sequence}:{row.Kind}:{row.CauseId}:{row.AdvancingSteps}:"
        + $"{Convert.ToHexString(row.AcceptedBatch)}:{Convert.ToHexString(row.Outcomes)}:{row.BeforeHash}:{row.AfterHash}";

    private static string CommandRow(StoredWorldCommandOutcome row) => $"{row.PrincipalId}:{row.CommandId}:{row.Sequence}:"
        + $"{Convert.ToHexString(row.Command)}:{Convert.ToHexString(row.Receipt)}";
}
