using System.Security.Cryptography;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care.Dispositions;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.NativeCareDispositions;

/// <summary>Authenticates native decisions against their actual prepared history and original admitted genesis.</summary>
public sealed class NativeCareDispositionResultTests
{
    [Fact]
    public void Native_choices_publish_exactly_one_closure_without_treatment_or_outcome_acceptance()
    {
        string? manifestPath = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_CARE_DISPOSITION_RESULTS");
        Assert.SkipWhen(manifestPath is null, "The complete native decision manifest is required.");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement[] results = manifest.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(new[] { "cancel-keyboard", "cancel-pointer", "decline-controller" },
            results.Select(row => row.GetProperty("mode").GetString()).Order().ToArray());
        foreach (JsonElement row in results) Authenticate(row);
    }

    private static void Authenticate(JsonElement row)
    {
        string mode = Text(row, "mode");
        bool decline = mode == "decline-controller";
        var fixture = new CareDispositionJournalFixture(humanHelper: true);
        byte[] genesis = File.ReadAllBytes(Text(row, "genesis"));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Genesis), genesis);
        byte[] beforeBytes = File.ReadAllBytes(Text(row, "before"));
        WorldSnapshot before = WorldSnapshotCodec.Decode(beforeBytes);
        var request = before.Care!.Pending!;
        Assert.NotNull(request);
        EntityId actor = decline ? fixture.Journey.Helper : fixture.Journey.Patient;
        var control = before.Inventory.Controls.Single(value => value.ActorId == actor);
        string source = Text(row, "sourceSave");
        string save = Text(row, "save");
        foreach (string path in new[] { source, save })
            Assert.False(File.Exists(path + "-wal") || File.Exists(path + "-shm"));
        string sourceHash = Hash(source);
        string finalHash = Hash(save);
        Assert.Equal(Text(row, "finalDatabaseSha256"), finalHash);

        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.OpenExisting(source, genesis))
            Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        using (var authority = SqliteWorldAuthority.OpenExisting(save, genesis))
        {
            final = authority.ReadStateForHost();
            Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(before.Revision).State));
        }
        Assert.Equal(sourceHash, Hash(source));
        Assert.Equal(finalHash, Hash(save));

        StoredWorldCommit publication;
        StoredWorldCommandOutcome outcome;
        using (var original = WorldDatabase.Open(source, create: false))
        using (var database = WorldDatabase.Open(save, create: false))
        {
            StoredWorldCommit[] prefix = original.ReadJournal(0).ToArray();
            StoredWorldCommit[] history = database.ReadJournal(0).ToArray();
            Assert.Equal(prefix.Length + 1, history.Length);
            for (int index = 0; index < prefix.Length; index++) Compare(prefix[index], history[index]);
            publication = history[^1];
            outcome = Assert.Single(database.ReadCommandOutcomes(before.Revision));
        }
        Assert.Equal(before.Revision + 1, final.Revision);
        Assert.Equal((WorldCommitKind.Care, before.ContentHash, final.ContentHash),
            (publication.Kind, publication.BeforeHash, publication.AfterHash));
        WorldStepBatch batch = WorldStepCodec.Decode(publication.AcceptedBatch);
        Assert.Empty(batch.Movement);
        WorldActionCommand command = Assert.Single(batch.Actions);
        Assert.Equal((actor, control.PrincipalId, control.SessionId),
            (command.ActorId, command.PrincipalId, command.OriginSessionId));
        if (decline)
        {
            var intent = Assert.IsType<DeclinePendingCareIntent>(command.Intent);
            Assert.Equal(new DeclinePendingCareIntent(request.AcknowledgmentId, request.EncounterId, before.Care.Revision), intent);
        }
        else
        {
            var intent = Assert.IsType<CancelPendingCareIntent>(command.Intent);
            Assert.Equal(new CancelPendingCareIntent(request.AcknowledgmentId, request.EncounterId, before.Care.Revision), intent);
        }
        WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(outcome.Receipt);
        Assert.Equal((CommandReceiptStatus.Applied, WorldActionRejection.None, final.Revision),
            (receipt.Status, receipt.Rejection, receipt.CommitSequence));
        Assert.Equal(WorldActionCodec.Encode(command), outcome.Command);
        CareDisposition disposition = Assert.Single(final.Care!.Dispositions);
        Assert.Equal((command.CommandId, request.AcknowledgmentId, actor, final.Revision),
            (disposition.CommandId, disposition.AcknowledgmentId, disposition.DecidingActorId, disposition.WorldSequence));
        Assert.Equal(decline ? CareDispositionKind.HelperDeclined : CareDispositionKind.PatientCancelled, disposition.Kind);
        Assert.Null(final.Care.Pending);
        Assert.Empty(final.Care.Treatments);
        Assert.Equal(before.Care.TotalAcknowledgments, final.Care.TotalAcknowledgments);
        Assert.Equal(before.Care.TotalDispositions + 1, final.Care.TotalDispositions);
        Assert.Equal(before.Care.TotalTreatments, final.Care.TotalTreatments);
        Assert.Equal(before.Calendar, final.Calendar);
        Assert.Equal(before.AdvancingSteps, final.AdvancingSteps);
        Assert.Equal(before.Space.Actors.Select(value => (value.ActorId, value.Binding, value.Scope, value.Feet,
                value.Revision, value.HomePlaceId, value.Condition.ComputeContentHash())),
            final.Space.Actors.Select(value => (value.ActorId, value.Binding, value.Scope, value.Feet,
                value.Revision, value.HomePlaceId, value.Condition.ComputeContentHash())));
        Assert.Equal(before.Inventory.ContentHash, final.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, final.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, final.Progression!.ContentHash);
        Assert.Equal(before.ResourceHistory!.ContentHash, final.ResourceHistory!.ContentHash);
        Assert.Equal(before.Pacing!.ContentHash, final.Pacing!.ContentHash);
        Assert.Equal(before.EncounterOutcome!.ContentHash, final.EncounterOutcome!.ContentHash);
        Assert.Equal(before.EncounterActions!.ContentHash, final.EncounterActions!.ContentHash);
        Assert.Equal(before.EncounterLifecycle!.RetainBoundary().ContentHash, final.EncounterLifecycle!.ContentHash);
        Assert.Equivalent(before.Care.Ordinary!, final.Care.Ordinary!, strict: true);
        Assert.Equal(0, final.Progression.GetActor(fixture.Journey.Patient).CurrentHp);

        using var observed = JsonDocument.Parse(File.ReadAllText(Text(row, "report")));
        JsonElement report = observed.RootElement;
        Assert.Equal(("native-care-disposition-v1", mode, "passed"),
            (Text(report, "Kind"), Text(report, "Mode"), Text(report, "Status")));
        Assert.Equal(actor.ToString(), Text(report, "ControlledActorId"));
        Assert.Equal(request.AcknowledgmentId.ToString(), Text(report, "RequestId"));
        JsonElement submission = Assert.Single(report.GetProperty("Submissions").EnumerateArray());
        Assert.False(submission.GetProperty("IsRetry").GetBoolean());
        Assert.Equal(Convert.ToHexString(outcome.Command), Text(submission, "CommandBytes"));
        Assert.Equal(Text(submission, "CommandBytes"), Text(report, "SelectedCommandBytes"));
        Assert.Equal(Convert.ToHexString(outcome.Receipt), Text(submission, "ReceiptBytes"));
        Assert.Equal(command.CommandId.ToString(), Text(submission, "CommandId"));
        using var reopened = JsonDocument.Parse(File.ReadAllText(Text(row, "reopenReport")));
        JsonElement reopenedReport = reopened.RootElement;
        Assert.Equal(("passed", "reopen-" + mode), (Text(reopenedReport, "Status"), Text(reopenedReport, "Mode")));
        Assert.Empty(reopenedReport.GetProperty("Submissions").EnumerateArray());
        Assert.Equal(Text(report, "FinalViewHash"), Text(reopenedReport, "InitialViewHash"));
        Assert.Equal(Text(report, "FinalViewHash"), Text(reopenedReport, "FinalViewHash"));
    }

    private static void Compare(StoredWorldCommit expected, StoredWorldCommit actual)
    {
        Assert.Equal((expected.Sequence, expected.Kind, expected.CauseId, expected.AdvancingSteps, expected.BeforeHash, expected.AfterHash),
            (actual.Sequence, actual.Kind, actual.CauseId, actual.AdvancingSteps, actual.BeforeHash, actual.AfterHash));
        Assert.Equal(expected.AcceptedBatch, actual.AcceptedBatch);
        Assert.Equal(expected.Outcomes, actual.Outcomes);
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()!;
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
