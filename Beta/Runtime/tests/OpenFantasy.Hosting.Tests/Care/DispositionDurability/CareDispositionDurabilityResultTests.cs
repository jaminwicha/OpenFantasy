using System.Security.Cryptography;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care.DispositionContinuation;
using OpenFantasy.Hosting.Tests.Care.Dispositions;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care.DispositionDurability;

/// <summary>Authenticates each externally killed save against its original genesis and all uninterrupted tables.</summary>
public sealed class CareDispositionDurabilityResultTests
{
    internal static readonly string[] Labels = ["cancel", "decline", "cut-pending", "archived-cancel", "archived-decline"];

    [Fact]
    public void Process_kills_recover_original_pending_or_terminal_history_without_duplicate_effects()
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_CARE_DISPOSITION_DURABILITY_RESULTS");
        Assert.SkipWhen(path is null, "The complete process-interruption report is required.");
        using var report = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement result = report.RootElement;
        Assert.Equal("passed", Text(result, "status"));
        Assert.True(result.GetProperty("sourceFixturesUnchanged").GetBoolean());
        Assert.True(result.GetProperty("executionUnchanged").GetBoolean());
        string fixtures = Text(result, "fixtures");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtures, "manifest.json")));
        JsonElement[] cases = manifest.RootElement.GetProperty("Cases").EnumerateArray().ToArray();
        Assert.Equal("care-disposition-durability-fixtures-v1", Text(manifest.RootElement, "Kind"));
        Assert.Equal(Labels, cases.Select(value => Text(value, "Label")));
        JsonElement[] rows = result.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Labels, rows.Select(value => Text(value, "label")));
        JsonElement[] kills = result.GetProperty("processKills").EnumerateArray().ToArray();
        Assert.Equal(10, kills.Length);
        var ordinary = new CareDispositionJournalFixture(humanHelper: true);
        WorldSnapshot pressure = DispositionContinuationFixture.CreateGenesis(new RecoveryJournalFixture(), humanHelper: false);
        foreach (JsonElement item in cases)
        {
            string label = Text(item, "Label");
            JsonElement row = rows.Single(value => Text(value, "label") == label);
            byte[] genesis = File.ReadAllBytes(Path.Combine(fixtures, Text(item, "Genesis")));
            Assert.Equal(WorldSnapshotCodec.Encode(Text(item, "GenesisKind") == "ordinary" ? ordinary.Genesis : pressure), genesis);
            string beforePath = Path.Combine(fixtures, Text(item, "Before"));
            string afterPath = Path.Combine(fixtures, Text(item, "After"));
            byte[] batchBytes = File.ReadAllBytes(Path.Combine(fixtures, Text(item, "Step")));
            WorldStepBatch batch = WorldStepCodec.Decode(batchBytes);
            WorldActionCommand command = Assert.Single(batch.Actions);
            WorldSnapshot before = Read(beforePath, genesis);
            WorldSnapshot after = Read(afterPath, genesis);
            Assert.Equal(item.GetProperty("Sequence").GetUInt64(), after.Revision);
            Assert.Equal(before.Revision + 1, after.Revision);
            Assert.Equal(Text(item, "BeforeHash"), before.ContentHash.Value);
            Assert.Equal(Text(item, "AfterHash"), after.ContentHash.Value);
            CareDispositionDurabilityFixtureTests.RequireSame(beforePath, Text(row, "beforeRecovered"));
            Assert.NotNull(before.Care!.Pending);
            Assert.Equal(label == "cut-pending", after.Care!.Pending is not null);
            Assert.Equal(before.Care.TotalTreatments, after.Care.TotalTreatments);
            foreach (string boundary in new[] { "before", "after" })
            {
                JsonElement killed = kills.Single(value => Text(value, "case") == label + "-" + boundary);
                Assert.Equal(boundary, Text(killed, "boundary"));
                Assert.Equal(-9, killed.GetProperty("exitCode").GetInt32());
                Assert.Equal(boundary == "before" ? "{\"Boundary\":\"BeforeCommit\"}" : "{\"Boundary\":\"AfterCommit\"}",
                    Text(killed, "signal"));
                Assert.True(killed.GetProperty("successfulWaitsWithinBounds").GetBoolean());
                Assert.Equal(Text(row, boundary), Text(killed, "database"));
            }
            foreach (string boundary in new[] { "normal", "before", "after" })
            {
                string database = Text(row, boundary);
                CareDispositionDurabilityFixtureTests.RequireSame(afterPath, database);
                string originalHash = Hash(database);
                using (var authority = SqliteWorldAuthority.OpenExisting(database, genesis))
                {
                    Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
                    Assert.Equal(item.GetProperty("ArchiveCount").GetUInt64(), authority.InspectForHost().Journal.MaterialCareArchiveCount);
                    var delivery = ConstructionUpgradeFixture.Delivery(after, command.ActorId);
                    var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, delivery));
                    Assert.Equal(Text(item, "Receipt"), Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)));
                    Assert.Equal(label == "cut-pending" ? WorldActionRejection.RevisionConflict : WorldActionRejection.None, receipt.Rejection);
                    Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
                    Assert.True(authority.IsIdentityOccupiedForHost(receipt.ResultId));
                    if (label != "cut-pending")
                        Assert.True(authority.IsIdentityOccupiedForHost(Assert.Single(after.Care.Dispositions).DispositionId));
                    Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(command,
                        new TrustedCommandDelivery(command.ActorId, command.OriginSessionId)));
                    var altered = new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
                        command.ActorId, command.WorldId, command.BranchId, checked(command.ExpectedActorRevision + 1), command.Intent);
                    Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(altered, delivery));
                    Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
                }
                Assert.Equal(originalHash, Hash(database));
                using var original = WorldDatabase.Open(beforePath, create: false);
                using var final = WorldDatabase.Open(database, create: false);
                StoredWorldCommit[] prefix = original.ReadJournal().ToArray();
                StoredWorldCommit[] history = final.ReadJournal().ToArray();
                Assert.Equal(prefix.Length + 1, history.Length);
                for (int index = 0; index < prefix.Length; index++)
                {
                    Assert.Equal(prefix[index].AcceptedBatch, history[index].AcceptedBatch);
                    Assert.Equal(prefix[index].Outcomes, history[index].Outcomes);
                    Assert.Equal(prefix[index].AfterHash, history[index].AfterHash);
                }
                Assert.Equal(batchBytes, history[^1].AcceptedBatch);
            }
        }
    }

    private static WorldSnapshot Read(string path, byte[] genesis)
    {
        string original = Hash(path);
        WorldSnapshot state;
        using (var authority = SqliteWorldAuthority.OpenExisting(path, genesis)) state = authority.ReadStateForHost();
        Assert.Equal(original, Hash(path));
        return state;
    }

    private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString()!;
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
