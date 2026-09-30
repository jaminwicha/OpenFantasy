using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.ProposalMigration;

/// <summary>The actual lived world, its explicit transition and the facts the transition must not touch.</summary>
[Collection(RetainedTransitionCollection.Name)]
public sealed class RetainedTransitionTests(RetainedTransitionFixture fixture)
{
    [Fact]
    public void Lived_source_is_the_pinned_profile27_world_and_opening_never_transitions_it()
    {
        WorldSnapshot head = fixture.SourceHead;
        Assert.Equal((27, RetainedTransitionFixture.SourceRevision, RetainedTransitionFixture.SourceSteps),
            ((int)head.ProfileVersion, head.Revision, head.AdvancingSteps));
        Assert.Equal(RetainedTransitionFixture.SourceHeadHash, head.ContentHash.Value);
        Assert.Equal(2UL, head.Coordination!.Anchor!.Reference.Ordinal);
        Assert.Null(head.Coordination.ConsentedMaterial);
        Assert.Null(head.Coordination.Proposals);
        Assert.Equal(RetainedTransitionFixture.SourceOrdinaryUses, head.Care!.TotalOrdinaryUses);
        Assert.Equal(3UL, fixture.SourceUsage.MaterialCareArchiveCount);
        // The isolated original remains byte-identical after every copy, opening and upgrade.
        Assert.Equal(RetainedTransitionFixture.SourceSha256, RetainedTransitionFixture.Sha256(fixture.OriginalSource));
        using WorldDatabase source = WorldDatabase.Open(fixture.SourcePath, create: false);
        Assert.Equal(RetainedTransitionFixture.SourceRevision, source.ReadHead().Sequence);
        Assert.DoesNotContain(source.ReadJournal(), row => row.Kind == WorldCommitKind.ProfileMigration);
    }

    [Fact]
    public void Published_transition_advances_only_the_commit_revision_and_selects_saved_proposals()
    {
        WorldSnapshot before = fixture.SourceHead;
        WorldSnapshot after = fixture.UpgradedHead;
        Assert.Equal((30, before.Revision + 1, before.AdvancingSteps),
            ((int)after.ProfileVersion, after.Revision, after.AdvancingSteps));
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.Equal(before.AdmissionVersion, after.AdmissionVersion);
        // Stock, controls, custody policies, bodies, resources, quests and time owners stay exact.
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Quests!.ContentHash, after.Quests!.ContentHash);
        Assert.Equal(before.Residents.Select(value => value.ActorId), after.Residents.Select(value => value.ActorId));
        Assert.Equal(before.Inventory.Controls, after.Inventory.Controls);
        Assert.Equal(before.Production!.Lots.Select(value => (value.LotId, value.Quantity, value.Revision)),
            after.Production!.Lots.Select(value => (value.LotId, value.Quantity, value.Revision)));
        Assert.Equal(before.Production.Revision, after.Production.Revision);
        Assert.Equal(before.Care!.TotalOrdinaryUses, after.Care!.TotalOrdinaryUses);
        Assert.Equal(before.Care.Ordinary!.Permissions.Select(value => (value.PermissionId, value.Revocation is null)),
            after.Care.Ordinary!.Permissions.Select(value => (value.PermissionId, value.Revocation is null)));
        // The lifecycle mirrors the commit revision only; its phase, clocks and encounters stay exact.
        Assert.Equal(before.EncounterLifecycle!.Revision + 1, after.EncounterLifecycle!.Revision);
        Assert.Equal((before.EncounterLifecycle.Phase, before.EncounterLifecycle.ExplorationSteps,
                before.EncounterLifecycle.TacticalTicks, before.EncounterLifecycle.EncounterCount),
            (after.EncounterLifecycle.Phase, after.EncounterLifecycle.ExplorationSteps,
                after.EncounterLifecycle.TacticalTicks, after.EncounterLifecycle.EncounterCount));
        Assert.Equal(before.Pacing!.ContentHash, after.Pacing!.ContentHash);
        // The transition mints no consent, grant or offer.
        Assert.Empty(after.Coordination!.ConsentedMaterial!.Transfers);
        Assert.Empty(after.Coordination.Proposals!.Open);
        Assert.Empty(after.Coordination.Proposals.Outcomes);
        WorldMaterialCareArchiveAnchor anchor = after.Coordination.Anchor!;
        Assert.Equal((3UL, before.Revision), (anchor.Reference.Ordinal, anchor.ClosingWorldSequence));
        Assert.Equal(before.ContentHash, anchor.Reference.BoundaryHash);
        Assert.Equal(fixture.Request.ContentHash, anchor.TransitionHash);
        Assert.True(anchor.Production.SupportsConsentedWorld);
        Assert.Empty(anchor.Proposals!.Carried);
    }

    [Fact]
    public void Published_destination_keeps_genesis_and_every_prior_row_exactly()
    {
        using WorldDatabase source = WorldDatabase.Open(fixture.SourcePath, create: false);
        using StorageFiles copy = fixture.CopyUpgraded();
        using WorldDatabase upgraded = WorldDatabase.Open(copy.Path, create: false);
        Assert.Equal(source.SchemaVersion, upgraded.SchemaVersion);
        Assert.Equal(fixture.GenesisBytes, upgraded.ReadGenesis());
        StoredWorldCommit[] prior = source.ReadJournal().ToArray();
        StoredWorldCommit[] journal = upgraded.ReadJournal().ToArray();
        Assert.Equal(prior.Length + 1, journal.Length);
        for (int index = 0; index < prior.Length; index++)
        {
            Assert.Equal((prior[index].Sequence, prior[index].Kind, prior[index].CauseId, prior[index].AfterHash),
                (journal[index].Sequence, journal[index].Kind, journal[index].CauseId, journal[index].AfterHash));
            Assert.Equal(prior[index].AcceptedBatch, journal[index].AcceptedBatch);
            Assert.Equal(prior[index].Outcomes, journal[index].Outcomes);
        }
        StoredWorldCommit transition = journal[^1];
        Assert.Equal((WorldCommitKind.ProfileMigration, fixture.Request.CauseId, RetainedTransitionFixture.SourceSteps),
            (transition.Kind, transition.CauseId, transition.AdvancingSteps));
        Assert.Equal(WorldProposalTransitionCodecV1.Encode(fixture.Request), transition.AcceptedBatch);
        Assert.Equal(source.ReadCommandOutcomes().Count(), upgraded.ReadCommandOutcomes().Count());
        StoredWorldIdentity added = Assert.Single(upgraded.ReadIdentities().Except(source.ReadIdentities()));
        Assert.Equal((fixture.Request.CauseId, WorldIdentityKind.CommitCause), (added.Identity, added.Kind));
        StoredWorldMaterialCareArchive[] priorArchives = source.ReadMaterialCareArchives().ToArray();
        StoredWorldMaterialCareArchive[] archives = upgraded.ReadMaterialCareArchives().ToArray();
        Assert.Equal(priorArchives.Length + 1, archives.Length);
        Assert.Equal(priorArchives.Select(value => (value.Reference, value.PublicationSequence)),
            archives.Take(priorArchives.Length).Select(value => (value.Reference, value.PublicationSequence)));
        Assert.All(priorArchives.Zip(archives), pair => Assert.Equal(pair.First.State, pair.Second.State));
        // The transition archive is the exact stored source head, published by the transition commit.
        Assert.Equal(source.ReadHead().State, archives[^1].State);
        Assert.Equal(RetainedTransitionFixture.SourceRevision + 1, archives[^1].PublicationSequence);
        Assert.Equal(source.ReadCheckpoints().Select(value => value.Sequence),
            upgraded.ReadCheckpoints().Select(value => value.Sequence).Where(value => value <= RetainedTransitionFixture.SourceRevision));
    }

    [Fact]
    public void Forged_stale_mismatched_reused_and_occupied_requests_fail_closed()
    {
        WorldSnapshot head = fixture.SourceHead;
        var processor = new WorldProposalTransitionProcessor();
        WorldProposalTransition valid = fixture.Request;
        ContentHash anchor = head.Coordination!.Anchor!.ContentHash;
        WorldProposalTransition[] forged =
        [
            new(valid.CauseId, head.WorldId, head.BranchId, head.Revision - 1, head.AdvancingSteps, head.ContentHash,
                head.AdmissionVersion, anchor, 3),
            new(valid.CauseId, head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps - 1, head.ContentHash,
                head.AdmissionVersion, anchor, 3),
            new(valid.CauseId, head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps, head.AdmissionVersion,
                head.AdmissionVersion, anchor, 3),
            new(valid.CauseId, head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps, head.ContentHash,
                head.ContentHash, anchor, 3),
            new(valid.CauseId, head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps, head.ContentHash,
                head.AdmissionVersion, head.ContentHash, 3),
            new(valid.CauseId, head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps, head.ContentHash,
                head.AdmissionVersion, anchor, 2),
            new(valid.CauseId, head.BranchId, head.WorldId, head.Revision, head.AdvancingSteps, head.ContentHash,
                head.AdmissionVersion, anchor, 3),
            new(valid.CauseId, head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps, head.ContentHash,
                head.AdmissionVersion, null, 0)
        ];
        Assert.All(forged, request => Assert.Throws<InvalidOperationException>(() => processor.Decide(head, request)));
        // A request cannot be reused against its own successor or any other interpretation.
        Assert.Throws<InvalidOperationException>(() => processor.Decide(fixture.UpgradedHead, valid));
        Assert.Throws<ArgumentException>(() => WorldProposalTransition.ForHead(fixture.UpgradedHead, valid.CauseId));
        // A cause that already names committed history is occupied.
        Assert.Throws<InvalidOperationException>(() => processor.Decide(head, valid, identity => identity == valid.CauseId));
        EntityId resident = head.Residents[0].ActorId;
        Assert.Throws<InvalidOperationException>(() => processor.Decide(head, WorldProposalTransition.ForHead(head, resident)));
        // The valid request remains deterministic: the same exact successor and transition archive.
        WorldProposalTransitionDecision first = processor.Decide(head, valid);
        WorldProposalTransitionDecision second = processor.Decide(head, valid);
        Assert.Equal(first.Candidate.ContentHash, second.Candidate.ContentHash);
        Assert.Equal(fixture.UpgradedHead.ContentHash, first.Candidate.ContentHash);
        Assert.Equal(first.Archive.Anchor.ContentHash, second.Archive.Anchor.ContentHash);
    }

    [Fact]
    public void Used_or_identical_destination_is_refused_before_any_copy()
    {
        using var existing = new StorageFiles();
        File.WriteAllBytes(existing.Path, [1]);
        Assert.ThrowsAny<IOException>(() => SqliteWorldAuthority.UpgradeCoordinatedToProposals(fixture.SourcePath,
            existing.Path, fixture.Request));
        Assert.ThrowsAny<IOException>(() => SqliteWorldAuthority.UpgradeCoordinatedToProposals(fixture.SourcePath,
            fixture.SourcePath, fixture.Request));
        Assert.Equal([1], File.ReadAllBytes(existing.Path));
    }

    [Fact]
    public void Transition_archive_decodes_only_with_its_authenticated_request()
    {
        using StorageFiles copy = fixture.CopyUpgraded();
        using WorldDatabase upgraded = WorldDatabase.Open(copy.Path, create: false);
        StoredWorldMaterialCareArchive[] archives = upgraded.ReadMaterialCareArchives().ToArray();
        WorldMaterialCareArchiveAnchor? previous = null;
        foreach (StoredWorldMaterialCareArchive stored in archives.Take(archives.Length - 1))
        {
            previous = WorldMaterialCareArchiveSegment.Decode(stored.Reference, stored.State, previous).Anchor;
        }
        StoredWorldMaterialCareArchive closing = archives[^1];
        WorldMaterialCareArchiveSegment transition = WorldMaterialCareArchiveSegment.DecodeTransition(
            closing.Reference, closing.State, previous, fixture.Request);
        Assert.Equal(fixture.UpgradedHead.Coordination!.Anchor!.ContentHash, transition.Anchor.ContentHash);
        // Without the request, the same bytes derive the ordinary profile-27 anchor, which no
        // saved-proposal successor can bind; a forged request cannot close this head.
        WorldMaterialCareArchiveSegment ordinary = WorldMaterialCareArchiveSegment.Decode(closing.Reference, closing.State, previous);
        Assert.NotEqual(transition.Anchor.ContentHash, ordinary.Anchor.ContentHash);
        Assert.Null(ordinary.Anchor.TransitionHash);
        var forged = new WorldProposalTransition(FixtureCause("forged"), fixture.Request.WorldId, fixture.Request.BranchId,
            fixture.Request.ExpectedRevision, fixture.Request.ExpectedAdvancingSteps, fixture.Request.ExpectedHeadHash,
            fixture.Request.SourceAdmissionVersion, fixture.Request.ExpectedAnchorHash, fixture.Request.TransitionOrdinal);
        Assert.NotEqual(transition.Anchor.ContentHash, WorldMaterialCareArchiveSegment.DecodeTransition(
            closing.Reference, closing.State, previous, forged).Anchor.ContentHash);
        var misplaced = new WorldProposalTransition(fixture.Request.CauseId, fixture.Request.WorldId, fixture.Request.BranchId,
            fixture.Request.ExpectedRevision, fixture.Request.ExpectedAdvancingSteps, fixture.Request.ExpectedHeadHash,
            fixture.Request.SourceAdmissionVersion, fixture.Request.ExpectedAnchorHash, fixture.Request.TransitionOrdinal + 1);
        Assert.Throws<FormatException>(() => WorldMaterialCareArchiveSegment.DecodeTransition(
            closing.Reference, closing.State, previous, misplaced));
        Assert.Throws<FormatException>(() => WorldMaterialCareArchiveSegment.DecodeTransition(
            closing.Reference, closing.State, null, fixture.Request));
    }

    [Fact]
    public void A_lived_world_before_its_first_cut_transitions_without_inventing_a_previous_anchor()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot genesis = WorldCoordinatedAdmission.CreateFresh(
            OpenFantasy.Hosting.Content.RestorativeJourneyAdmission.CreateEmbedded("proposal-transition-before-cut", "Traveler"));
        WorldSnapshot lived;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(source.Path, genesis))
        {
            lived = authority.Commit(new WorldStepBatch(FixtureCause("first-live-step"), genesis.WorldId,
                genesis.BranchId, 0, 0, WorldBatchKind.AdvancingStep, [], [], genesis.ProfileVersion)).Candidate;
        }
        Assert.Null(lived.Coordination!.Anchor);
        WorldProposalTransition request = WorldProposalTransition.ForHead(lived, FixtureCause("uncut-transition"));
        Assert.Null(request.ExpectedAnchorHash);
        Assert.Equal(0UL, request.TransitionOrdinal);
        using SqliteWorldAuthority upgraded = SqliteWorldAuthority.UpgradeCoordinatedToProposals(source.Path, destination.Path, request);
        WorldSnapshot after = upgraded.ReadStateForHost();
        Assert.Equal(30, (int)after.ProfileVersion);
        Assert.Equal(lived.Revision + 1, after.Revision);
        Assert.Equal(lived.AdvancingSteps, after.AdvancingSteps);
        Assert.Equal(lived.Calendar, after.Calendar);
        Assert.Equal(lived.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(lived.ContentHash, after.Coordination!.Anchor!.Reference.BoundaryHash);
        Assert.Equal(0UL, after.Coordination.Anchor.Reference.Ordinal);
        Assert.Equal(1UL, upgraded.InspectForHost().Journal.MaterialCareArchiveCount);
        Assert.Equal(lived.ContentHash, upgraded.ReadRecordedStateForHost(lived.Revision).State.ContentHash);
    }

    private static EntityId FixtureCause(string label) => RetainedTransitionFixture.Cause(label);
}
