using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Exercises authorized receipt recovery on the actual externally interrupted databases.</summary>
public sealed class RestorativeCrashResultTests
{
    [Fact]
    public void Opt_in_completed_crash_cases_retain_exact_authorized_receipts_without_new_effects()
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_RESTORATIVE_CRASH_RESULTS") is not { Length: > 0 } resultPath) return;
        using var report = JsonDocument.Parse(File.ReadAllBytes(resultPath));
        var result = report.RootElement;
        Assert.Equal("passed", Text(result, "status"));
        Assert.True(result.GetProperty("fixturesUnchanged").GetBoolean());
        Assert.True(result.GetProperty("hostUnchanged").GetBoolean());
        Assert.True(result.GetProperty("executionUnchanged").GetBoolean());
        Assert.Equal(10, result.GetProperty("externalKills").GetInt32());
        Assert.Equal(5, result.GetProperty("completeFixturePairs").GetInt32());
        string fixtures = Text(result, "fixturesDirectory");
        Assert.True(Path.IsPathFullyQualified(fixtures));
        var cases = result.GetProperty("completedCases").EnumerateArray().ToArray();
        string[] fixtureNames = ["archive-helper", "crafted-self", "helper", "raw-self", "revoke"];
        string[] boundaries = ["normal", "before", "after"];
        var expected = fixtureNames.SelectMany(fixture => boundaries.Select(boundary => (fixture, boundary))).Order().ToArray();
        Assert.Equal(expected, cases.Select(value => (Text(value, "fixture"), Text(value, "boundary"))).Order().ToArray());
        Assert.Equal(cases.Length, cases.Select(value => Text(value, "database")).Distinct(StringComparer.Ordinal).Count());
        foreach (var completed in cases) Verify(fixtures, completed);
    }

    private static void Verify(string fixtures, JsonElement completed)
    {
        string fixture = Path.Combine(fixtures, Text(completed, "fixture"));
        using var metadataDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture, "metadata.json")));
        var metadata = metadataDocument.RootElement;
        var batch = WorldStepCodec.Decode(File.ReadAllBytes(Path.Combine(fixture, "batch.ofc")));
        var command = Assert.Single(batch.Actions);
        string databasePath = Text(completed, "database");
        Assert.True(Path.IsPathFullyQualified(databasePath));
        byte[] closedDatabase = File.ReadAllBytes(databasePath);
        using (var recovered = SqliteWorldAuthority.OpenExisting(databasePath))
        {
            var state = recovered.ReadStateForHost();
            Assert.Equal((ushort)26, state.ProfileVersion);
            Assert.Equal(Text(metadata, "afterHash"), state.ContentHash.Value);
            Assert.Equal(metadata.GetProperty("afterRevision").GetUInt64(), state.Revision);
            byte[] beforeLookup = WorldSnapshotCodec.Encode(state);
            Assert.Equal(File.ReadAllBytes(Path.Combine(fixture, "after.ofc")), beforeLookup);
            var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
            var receipt = Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(command, delivery));
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(command.CommandId, receipt.CommandId);
            Assert.Equal(command.ContentHash, receipt.CommandHash);
            Assert.Equal(state.Revision, receipt.CommitSequence);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(command, delivery))));
            Assert.True(recovered.IsIdentityOccupiedForHost(command.CommandId));

            var altered = new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
                command.ActorId, command.WorldId, command.BranchId, checked(command.ExpectedActorRevision + 1), command.Intent);
            Assert.Throws<AuthorityDispatchException>(() => recovered.FindReceipt(altered, delivery));
            Assert.Throws<AuthorityDispatchException>(() => recovered.FindReceipt(command,
                new TrustedCommandDelivery(command.ActorId, command.OriginSessionId)));
            Assert.Equal(beforeLookup, WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        }
        Assert.Equal(closedDatabase, File.ReadAllBytes(databasePath));
    }

    private static string Text(JsonElement value, string property) => value.GetProperty(property).GetString()
        ?? throw new InvalidDataException("Missing crash-result string: " + property);
}
