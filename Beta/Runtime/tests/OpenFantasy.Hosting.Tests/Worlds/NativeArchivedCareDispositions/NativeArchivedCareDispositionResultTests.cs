using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care.DispositionContinuation;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Tests.Worlds.NativeCareDispositions;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.NativeArchivedCareDispositions;

/// <summary>Authenticates all native routes from original admission, including the retrieved archive carrying their request.</summary>
public sealed class NativeArchivedCareDispositionResultTests
{
    [Fact]
    public void Archived_pending_native_choices_and_paused_reopens_reproduce_original_genesis_history()
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_ARCHIVED_CARE_DISPOSITION_RESULTS");
        Assert.SkipWhen(path is null, "The complete archived native decision manifest is required.");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement[] rows = manifest.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(new[] { "cancel-keyboard", "cancel-pointer", "decline-controller" },
            rows.Select(row => Text(row, "mode")).Order().ToArray());
        var journey = new RecoveryJournalFixture();
        WorldSnapshot genesis = DispositionContinuationFixture.CreateGenesis(journey, humanHelper: true);
        foreach (JsonElement row in rows)
        {
            // This includes complete OpenExisting replay of source and native saves from these independently
            // reconstructed genesis bytes, exact prefix/command/receipt proof and all separate-reopen assertions.
            var (before, final) = NativeCareDispositionResultTests.Authenticate(row, genesis, journey.Patient, journey.Helper);
            AuthenticateArchive(row, before, final, journey.Patient, journey.Helper);
        }
    }

    private static void AuthenticateArchive(JsonElement row, WorldSnapshot before, WorldSnapshot final,
        EntityId patient, EntityId helper)
    {
        using var source = WorldDatabase.Open(Text(row, "sourceSave"), create: false);
        using var native = WorldDatabase.Open(Text(row, "save"), create: false);
        StoredWorldMaterialCareArchive original = Assert.Single(source.ReadMaterialCareArchives());
        StoredWorldMaterialCareArchive retained = Assert.Single(native.ReadMaterialCareArchives());
        Assert.Equal(original.Reference, retained.Reference);
        Assert.Equal(original.PublicationSequence, retained.PublicationSequence);
        Assert.Equal(original.State, retained.State);
        Assert.Equal(0UL, original.Reference.Ordinal);
        Assert.Equal(original.Reference.BoundarySequence + 1, original.PublicationSequence);
        Assert.Equal(before.Revision, original.PublicationSequence);
        WorldMaterialCareArchiveSegment archive = WorldMaterialCareArchiveSegment.Decode(original.Reference, original.State);
        Assert.Null(archive.Transition);
        Assert.Equal(archive.Anchor.ContentHash, before.Coordination!.Anchor!.ContentHash);
        Assert.Equal(archive.Anchor.ContentHash, final.Coordination!.Anchor!.ContentHash);
        WorldSnapshot closing = WorldSnapshotCodec.Decode(original.State);
        Assert.Equal(original.Reference.BoundaryHash, closing.ContentHash);
        Assert.Equal(original.Reference.BoundarySequence, closing.Revision);
        Assert.Equal(255, closing.ResourceHistory!.History.Batches.Count);
        Assert.Equal(255UL, closing.Progression!.Revision);
        Assert.All(closing.ResourceHistory.Evidence, evidence =>
        {
            var attack = Assert.IsType<WorldEncounterResourceEvidence>(evidence);
            Assert.Equal(patient, attack.Resolution.TargetActorId);
            Assert.Equal(1U, attack.Resolution.Damage);
        });
        Assert.Empty(before.ResourceHistory!.History.Batches);
        var request = Assert.IsType<CareAcknowledgment>(before.Care!.Pending);
        Assert.Equivalent(request, closing.Care!.Pending, strict: true);
        Assert.Equivalent(request, Assert.Single(before.Care.Acknowledgments), strict: true);
        Assert.Equivalent(request, before.Care.Continuation!.Pending, strict: true);
        Assert.Equivalent(request, archive.Continued.Care!.Pending, strict: true);
        Assert.Equal(archive.Continued.Care.ContentHash, before.Care.ContentHash);
        Assert.Equal(1UL, before.Care.Continuation.Ordinal);
        Assert.Equal(0UL, before.Care.Continuation.ArchivedAcknowledgments);
        Assert.Equal(0UL, before.Care.Continuation.ArchivedTreatments);
        Assert.Equal(0UL, before.Care.Continuation.ArchivedDispositions);
        Assert.Empty(before.Care.Continuation.PatientWitnesses);
        Assert.Empty(before.Care.Continuation.ClosedDefeatWitnesses);
        Assert.Equal(before.Care.Continuation.ContentHash, final.Care!.Continuation!.ContentHash);
        Assert.Equal(before.Controllers!.ContentHash, final.Controllers!.ContentHash);
        foreach (EntityId actor in new[] { patient, helper })
        {
            Assert.Equal(ResidentControllerKind.Player, closing.GetResident(actor).ControllerKind);
            Assert.Equal(WorldControllerMode.Human, closing.Controllers!.Get(actor).Mode);
            Assert.Equal(WorldControllerMode.Human, final.Controllers.Get(actor).Mode);
        }

        // The normal cut was triggered by an actual, separately refused remedy command. It neither treated
        // the patient nor closed the request, and the native command starts strictly after its publication.
        StoredWorldCommit cut = Assert.Single(source.ReadJournal(original.Reference.BoundarySequence));
        Assert.Equal((WorldCommitKind.Care, original.PublicationSequence), (cut.Kind, cut.Sequence));
        WorldActionCommand command = Assert.Single(WorldStepCodec.Decode(cut.AcceptedBatch).Actions);
        Assert.IsType<AdministerRemedyIntent>(command.Intent);
        Assert.Equal(helper, command.ActorId);
        Assert.Equal(closing.Space.GetActor(helper).Revision + 1, command.ExpectedActorRevision);
        StoredWorldCommandOutcome answer = Assert.Single(source.ReadCommandOutcomes(original.Reference.BoundarySequence));
        Assert.Equal(WorldActionCodec.Encode(command), answer.Command);
        WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(answer.Receipt);
        Assert.Equal((CommandReceiptStatus.Rejected, WorldActionRejection.RevisionConflict, before.Revision),
            (receipt.Status, receipt.Rejection, receipt.CommitSequence));
        Assert.Equal(closing.Inventory.ContentHash, before.Inventory.ContentHash);
        Assert.Equal(closing.Progression.ContentHash, before.Progression!.ContentHash);
        Assert.Equal(closing.Calendar, before.Calendar);
        Assert.Equal(closing.AdvancingSteps, before.AdvancingSteps);
        Assert.Equal(closing.Care.TotalAcknowledgments, before.Care.TotalAcknowledgments);
        Assert.Empty(before.Care.Treatments);
        Assert.Empty(before.Care.Dispositions);

        using var metadata = JsonDocument.Parse(File.ReadAllText(Text(row, "metadata")));
        JsonElement fields = metadata.RootElement;
        Assert.Equal(1, fields.GetProperty("ArchiveCount").GetInt32());
        Assert.Equal(original.Reference.BoundarySequence, fields.GetProperty("ArchiveBoundarySequence").GetUInt64());
        Assert.Equal(original.Reference.BoundaryHash.Value, Text(fields, "ArchiveBoundaryHash"));
        Assert.Equal(before.Revision, fields.GetProperty("ArchivePublicationSequence").GetUInt64());
        Assert.Equal(Convert.ToHexString(answer.Command), Text(fields, "RefusedCutCommandBytes"));
        Assert.Equal(Convert.ToHexString(answer.Receipt), Text(fields, "RefusedCutReceiptBytes"));
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
