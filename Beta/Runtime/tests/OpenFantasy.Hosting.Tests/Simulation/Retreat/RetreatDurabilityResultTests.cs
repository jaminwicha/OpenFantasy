using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Retreat;

/// <summary>
/// Authenticates the process-interruption results named by OPENFANTASY_RETREAT_DURABILITY_RESULTS against the exported
/// unbroken originals: every case was killed by SIGKILL at both authority boundaries; each recovered or retried save
/// equals the unbroken result row for row (head, journal, receipts, identities, checkpoints and both archive families),
/// a save killed before commit recovered exactly the prior save, every journal prefix equals the unbroken session's,
/// full verified recovery reproduces the published root, and the retreat request retries to its original receipt.
/// Every save is read through a private copy. An absent result is a failure, never a pass.
/// </summary>
public sealed class RetreatDurabilityResultTests
{
    [Fact]
    public async Task Killed_and_recovered_retreat_publications_equal_the_unbroken_journal()
    {
        string resultPath = Environment.GetEnvironmentVariable("OPENFANTASY_RETREAT_DURABILITY_RESULTS") is { Length: > 0 } supplied
            ? supplied : throw new InvalidOperationException("OPENFANTASY_RETREAT_DURABILITY_RESULTS must name the durability result.");
        using JsonDocument result = JsonDocument.Parse(File.ReadAllText(resultPath));
        Assert.Equal("passed", result.RootElement.GetProperty("status").GetString());
        string fixtures = result.RootElement.GetProperty("fixtures").GetString()!;
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "manifest.json")));
        byte[] genesis = File.ReadAllBytes(Path.Combine(fixtures, manifest.RootElement.GetProperty("Genesis").GetString()!));
        WorldActionCommand retreat = WorldActionCodec.Decode(File.ReadAllBytes(Path.Combine(fixtures, "command.bin")));
        Assert.Equal(manifest.RootElement.GetProperty("CommandId").GetString(), retreat.CommandId.ToString());

        JsonElement[] kills = result.RootElement.GetProperty("processKills").EnumerateArray().ToArray();
        JsonElement[] cases = manifest.RootElement.GetProperty("Cases").EnumerateArray().ToArray();
        Assert.Equal(new[] { "queue", "sample", "disengage" }, cases.Select(value => value.GetProperty("Label").GetString()));
        foreach (JsonElement kill in kills)
        {
            Assert.True(kill.GetProperty("pid").GetInt32() > 0);
            Assert.Equal(-9, kill.GetProperty("exitCode").GetInt32());
            Assert.Equal(kill.GetProperty("boundary").GetString() == "before" ? "{\"Boundary\":\"BeforeCommit\"}" : "{\"Boundary\":\"AfterCommit\"}",
                kill.GetProperty("signal").GetString());
        }

        string scratch = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-durability-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string Copy(string path)
            {
                Assert.False(File.Exists(path + "-wal") || File.Exists(path + "-shm"), "A durability save must be closed: " + path);
                string copy = Path.Combine(scratch, Guid.NewGuid().ToString("N") + ".sqlite");
                File.Copy(path, copy);
                return copy;
            }
            string unbroken = Copy(Path.Combine(fixtures, manifest.RootElement.GetProperty("Unbroken").GetString()!));
            List<StoredWorldCommit> original;
            using (WorldDatabase database = WorldDatabase.Open(unbroken, create: false)) original = database.ReadJournal().ToList();
            JsonElement[] rows = result.RootElement.GetProperty("cases").EnumerateArray().ToArray();
            Assert.Equal(cases.Length, rows.Length);
            foreach (JsonElement fixture in cases)
            {
                string label = fixture.GetProperty("Label").GetString()!;
                ulong sequence = fixture.GetProperty("Sequence").GetUInt64();
                JsonElement row = rows.Single(value => value.GetProperty("label").GetString() == label);
                Assert.Equal(new[] { "before", "after" }, kills.Where(value => value.GetProperty("case").GetString() == label + "-before"
                    || value.GetProperty("case").GetString() == label + "-after").Select(value => value.GetProperty("boundary").GetString()));
                string before = Copy(Path.Combine(fixtures, fixture.GetProperty("Before").GetString()!));
                string after = Copy(Path.Combine(fixtures, fixture.GetProperty("After").GetString()!));
                // The unbroken after-save is exactly the unbroken session journal's prefix at this publication.
                RequirePrefix(after, original, sequence);
                RequirePrefix(before, original, sequence - 1);
                // A kill before the commit recovers exactly the prior save; every other path equals the unbroken result.
                RequireSame(before, Copy(row.GetProperty("beforeRecovered").GetString()!));
                foreach (string boundary in new[] { "normal", "before", "after" })
                {
                    string recovered = Copy(row.GetProperty(boundary).GetString()!);
                    RequireSame(after, recovered);
                    using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(recovered, genesis);
                    WorldSnapshot state = authority.ReadStateForHost();
                    Assert.Equal(sequence, state.Revision);
                    Assert.Equal(fixture.GetProperty("AfterHash").GetString(), state.ContentHash.Value);
                    // The exact step is never published twice.
                    WorldStepBatch step = WorldStepCodec.Decode(File.ReadAllBytes(Path.Combine(fixtures, fixture.GetProperty("Step").GetString()!)));
                    Assert.ThrowsAny<Exception>(() => authority.Commit(step));
                    Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
                }
                if (label == "queue")
                {
                    // The committed retreat request retries to its original receipt after recovery, without another effect.
                    string retried = Copy(row.GetProperty("after").GetString()!);
                    await using var world = new RetreatJourneyWorld(retried, WorldSnapshotCodec.Decode(genesis), reopen: true);
                    WorldSnapshot recovered = world.State;
                    world.Session.SetReady(false);
                    WorldActionReceipt receipt = await world.Session.SubmitAction(retreat);
                    Assert.Equal((retreat.CommandId, WorldActionRejection.None), (receipt.CommandId, receipt.Rejection));
                    Assert.Equal(retreat.CommandId, recovered.Retreat!.Active!.Command.CommandId);
                    Assert.Equal(recovered.ContentHash, world.State.ContentHash);
                }
            }
        }
        finally { Directory.Delete(scratch, recursive: true); }
    }

    private static void RequireSame(string expected, string actual)
    {
        using WorldDatabase left = WorldDatabase.Open(expected, create: false);
        using WorldDatabase right = WorldDatabase.Open(actual, create: false);
        WorldBackupComparison.RequireExactCopy(left, right);
    }

    private static void RequirePrefix(string path, IReadOnlyList<StoredWorldCommit> original, ulong head)
    {
        using WorldDatabase database = WorldDatabase.Open(path, create: false);
        StoredWorldCommit[] journal = database.ReadJournal().ToArray();
        Assert.Equal(head, database.ReadHead().Sequence);
        Assert.Equal((int)head, journal.Length);
        for (int index = 0; index < journal.Length; index++)
        {
            Assert.Equal(original[index].Sequence, journal[index].Sequence);
            Assert.Equal(original[index].AcceptedBatch, journal[index].AcceptedBatch);
            Assert.Equal(original[index].Outcomes, journal[index].Outcomes);
            Assert.Equal(original[index].AfterHash, journal[index].AfterHash);
        }
    }
}
