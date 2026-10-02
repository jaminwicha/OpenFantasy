using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care.DispositionContinuation;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerRecovery;

public sealed class ControllerRecoveryArchiveTests
{
    private const string SourceDigest = "0a22a7ede2fd679f51aa3187512d50f9758a0ffa74996b3589b1534d83bf9aa3";
    private const string GenesisDigest = "02dc0cf4f170c26d062a2230ef7bd6fdd34c1a6414b1f63c94f78fc73b1e7412";
    private const string SourceHeadDigest = "c392a07798052e1a91082914ac59cd878e946c231d88aadccb427773ac3d8329";

    [Fact]
    public void Recovery_activation_preserves_an_inherited_care_archive_and_publishes_a_later_actual_cut()
    {
        string root = RetainedFixtures();
        string source = Path.Combine(root, "archived-cancel.after.sqlite");
        string genesisPath = Path.Combine(root, "pressure.genesis.ofc");
        Assert.Equal(SourceDigest, Digest(source));
        byte[] genesisBytes = File.ReadAllBytes(genesisPath);
        Assert.Equal(GenesisDigest, ContentHash.Compute(genesisBytes).Value);
        var journey = new RecoveryJournalFixture();
        // This pure builder authenticates the admitted calibration without repeating 255 earned bites.
        WorldSnapshot genesis = DispositionContinuationFixture.CreateGenesis(journey, humanHelper: false);
        Assert.Equal(genesisBytes, WorldSnapshotCodec.Encode(genesis));

        WorldSnapshot original;
        RecordedWorldMaterialCareArchive inherited;
        using (var authority = SqliteWorldAuthority.OpenExisting(source, genesisBytes))
        {
            Assert.False(authority.SupportsControllerRecovery);
            original = authority.ReadStateForHost();
            Assert.Equal((ushort)38, original.ProfileVersion);
            Assert.Equal(1084UL, original.Revision);
            Assert.Equal(SourceHeadDigest, original.ContentHash.Value);
            Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            inherited = authority.ReadMaterialCareArchiveForHost(0);
            Assert.Equal(1083UL, inherited.PublicationSequence);
            Assert.Equal(1082UL, inherited.Segment.Reference.BoundarySequence);
            Assert.NotNull(inherited.Segment.Continued.Care!.Pending);
        }
        var request = Assert.Single(original.Care!.Acknowledgments);
        var closure = Assert.Single(original.Care.Dispositions);
        Assert.Equal(CareDispositionKind.PatientCancelled, closure.Kind);
        Assert.Equal(request.AcknowledgmentId, closure.AcknowledgmentId);
        PrimeIdentities(source, journey.Commands);

        using var destination = new StorageFiles();
        WorldSnapshot final;
        WorldSnapshot closing;
        WorldStepDecision publication;
        using (var authority = SqliteWorldAuthority.UpgradeControllerRecovery(source, destination.Path))
        {
            Assert.True(authority.SupportsControllerRecovery);
            Assert.Empty(authority.ReadControllerAllowancesForHost());
            Assert.Equal(WorldSnapshotCodec.Encode(original), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            var retained = authority.ReadMaterialCareArchiveForHost(0);
            AssertArchive(inherited, retained, original);

            WorldSnapshot state = authority.ReadStateForHost();
            journey.Act(authority, journey.Patient, new AcknowledgeEncounterOutcomeIntent(
                state.EncounterActions!.Scope.EncounterId, state.EncounterActions.ContentHash), WorldBatchKind.EncounterLifecycle);
            state = journey.Commit(authority, WorldBatchKind.EncounterLifecycle).Candidate;
            Assert.Equal(EncounterLifecyclePhase.Exploration, state.EncounterLifecycle!.Phase);
            (closing, publication) = PublishNextArchive(authority, journey);
            final = authority.ReadStateForHost();
            var segment = Assert.IsType<WorldMaterialCareArchiveSegment>(publication.SealedMaterialCareSegment);
            Assert.Equal(WorldSnapshotCodec.Encode(closing), segment.Encode());
            Assert.Equal(closing.Revision, segment.Reference.BoundarySequence);
            Assert.Equal(closing.ContentHash, segment.Reference.BoundaryHash);
            Assert.Equal(1UL, segment.Reference.Ordinal);
            Assert.Equal(2UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            Assert.Equal(original.Inventory.ContentHash, final.Inventory.ContentHash);
            Assert.Equal(original.Progression!.ContentHash, final.Progression!.ContentHash);
            Assert.Equal(0, final.Progression.GetActor(journey.Patient).CurrentHp);
            Assert.Equal(original.Care.TotalAcknowledgments, final.Care!.TotalAcknowledgments);
            Assert.Equal(original.Care.TotalTreatments, final.Care.TotalTreatments);
            Assert.Equal(original.Care.TotalDispositions, final.Care.TotalDispositions);
            Assert.Empty(final.Care.Dispositions);
            var witness = Assert.Single(final.Care.Continuation!.ClosedDefeatWitnesses);
            Assert.Equivalent(request, witness.Acknowledgment, strict: true);
            Assert.Equivalent(closure, witness.Disposition, strict: true);
            Assert.Null(witness.Treatment);
            Assert.Empty(final.Care.Continuation.PatientWitnesses);
        }

        using (var reopened = SqliteWorldAuthority.OpenExisting(destination.Path, genesisBytes))
        {
            Assert.True(reopened.SupportsControllerRecovery);
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Equal(2UL, reopened.InspectForHost().Journal.MaterialCareArchiveCount);
            AssertArchive(inherited, reopened.ReadMaterialCareArchiveForHost(0), final);
            var later = reopened.ReadMaterialCareArchiveForHost(1);
            Assert.Equal(WorldSnapshotCodec.Encode(closing), later.Segment.Encode());
            Assert.Equal(publication.Candidate.Revision, later.PublicationSequence);
            Assert.Equal(final.Revision, later.SourceHeadRevision);
            Assert.Equal(final.ContentHash, later.SourceHeadHash);
            var receipt = Assert.Single(publication.Receipts);
            Assert.True(reopened.IsIdentityOccupiedForHost(receipt.CommandId));
            Assert.True(reopened.IsIdentityOccupiedForHost(receipt.ResultId));
            Assert.True(reopened.IsIdentityOccupiedForHost(request.CommandId));
            Assert.True(reopened.IsIdentityOccupiedForHost(request.AcknowledgmentId));
            Assert.True(reopened.IsIdentityOccupiedForHost(closure.CommandId));
            Assert.True(reopened.IsIdentityOccupiedForHost(closure.DispositionId));
        }
        using (var database = WorldDatabase.Open(destination.Path, false))
            Assert.Equal(genesisBytes, database.ReadGenesis());
        Assert.Equal(SourceDigest, Digest(source));
        Assert.Equal(GenesisDigest, Digest(genesisPath));
    }

    private static (WorldSnapshot Closing, WorldStepDecision Publication) PublishNextArchive(
        SqliteWorldAuthority authority, RecoveryJournalFixture journey)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        for (int step = 0; !WorldActorAvailability.CanAct(state, journey.Helper); step++)
        {
            Assert.True(step < 64, "The retained helper pays its existing readiness before ordinary proposals.");
            state = journey.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        }
        var lot = state.Inventory.Custody.Lots.First(row => row.CustodianId == state.GetResident(journey.Helper).InventoryCustodianId);
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, journey.Helper, journey.Patient,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(journey.Patient).InventoryCustodianId, 1, lot.Revision);
        // Same admitted proposal-pressure boundary as RecoveryJournalFixture.ArchiveBeforeRescue;
        // retain each pre-commit root here as an independent oracle for the actual sealing result.
        for (int offer = 0; offer < WorldMaterialProposalBook.MaximumOutcomes; offer++)
        {
            WorldSnapshot before = state;
            var offered = journey.Commit(authority, WorldBatchKind.AdvancingStep,
                [journey.Commands.Command(state, journey.Helper, new ProposeMaterialIntent(terms, state.AdvancingSteps + 32))]);
            state = offered.Candidate;
            var open = Assert.Single(state.Coordination!.Proposals!.Open);
            WorldSnapshot beforeWithdrawal = state;
            var withdrawn = journey.Commit(authority, WorldBatchKind.AdvancingStep,
                [journey.Commands.Command(state, journey.Helper, new WithdrawMaterialProposalIntent(open.ProposalId))]);
            state = withdrawn.Candidate;
            Assert.Equal(0, state.Progression!.GetActor(journey.Patient).CurrentHp);
            if (offered.SealedMaterialCareSegment is not null)
            {
                Assert.Null(withdrawn.SealedMaterialCareSegment);
                return (before, offered);
            }
            if (withdrawn.SealedMaterialCareSegment is not null) return (beforeWithdrawal, withdrawn);
        }
        throw new InvalidOperationException("The unchanged proposal outcome capacity must publish within its finite window.");
    }

    private static void AssertArchive(RecordedWorldMaterialCareArchive expected,
        RecordedWorldMaterialCareArchive actual, WorldSnapshot head)
    {
        Assert.Equal(expected.Segment.Reference, actual.Segment.Reference);
        Assert.Equal(expected.Segment.Encode(), actual.Segment.Encode());
        Assert.Equal(expected.PublicationSequence, actual.PublicationSequence);
        Assert.Equal(head.Revision, actual.SourceHeadRevision);
        Assert.Equal(head.ContentHash, actual.SourceHeadHash);
    }

    private static void PrimeIdentities(string source, EncounterJourneyWorldFixture commands)
    {
        EntityId first = commands.NextId();
        using var database = WorldDatabase.Open(source, false);
        ulong maximum = database.ReadIdentities().Where(row => row.Identity.High == first.High)
            .Select(row => row.Identity.Low).DefaultIfEmpty(first.Low).Max();
        // The source digest above pins this finite journal; advance past every occupied ID in
        // this allocator's prefix, including gaps, rather than stopping at the first unused ID.
        while (commands.NextId().Low <= maximum) { }
    }

    private static string RetainedFixtures()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "implementation", "p02", "care-disposition-durability", "crash-fixtures");
            if (File.Exists(Path.Combine(candidate, "archived-cancel.after.sqlite"))) return candidate;
        }
        throw new FileNotFoundException("The retained authenticated archived-care fixture is required; this proof cannot silently skip.");
    }

    private static string Digest(string path) => ContentHash.Compute(File.ReadAllBytes(path)).Value;
}
