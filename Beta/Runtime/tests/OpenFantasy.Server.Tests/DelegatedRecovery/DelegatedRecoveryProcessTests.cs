using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.DelegatedRecovery;

public sealed class DelegatedRecoveryProcessTests(DelegatedRecoveryFixture fixture) : IClassFixture<DelegatedRecoveryFixture>
{
    [Theory]
    [InlineData("archive", false)]
    [InlineData("archive", true)]
    [InlineData("revoke", false)]
    [InlineData("revoke", true)]
    [InlineData("resolution", false)]
    [InlineData("resolution", true)]
    public async Task Real_host_storage_process_death_preserves_the_complete_original_delegated_transaction(string name, bool committed)
    {
        using var temporary = new ServerFiles();
        string root = Environment.GetEnvironmentVariable("OPENFANTASY_DELEGATED_RECOVERY_EVIDENCE") ?? temporary.DirectoryPath;
        var boundary = Assert.Single(fixture.Boundaries, value => value.Name == name);
        await DelegatedRecoveryProcessCrash.Execute(fixture, boundary, committed,
            Path.Combine(root, name + (committed ? "-after" : "-before")), ServerWorld.Cancellation);
    }
}

/// <summary>Actual external termination at the existing Host transaction seam; this is not a listener/socket death.</summary>
internal static class DelegatedRecoveryProcessCrash
{
    internal static async Task<string> Execute(DelegatedRecoveryFixture fixture, DelegatedRecoveryBoundary boundary,
        bool committed, string output, CancellationToken token)
    {
        Directory.CreateDirectory(output);
        string database = Path.Combine(output, "world.sqlite3");
        fixture.CloneBoundary(boundary.Name, database);
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), fixture.GenesisBytes);
        File.WriteAllBytes(Path.Combine(output, "before.ofc"), WorldSnapshotCodec.Encode(boundary.Before));
        File.WriteAllBytes(Path.Combine(output, "expected-after.ofc"), WorldSnapshotCodec.Encode(boundary.After));
        string step = Path.Combine(output, "publication.ofc");
        File.WriteAllBytes(step, WorldStepCodec.Encode(boundary.Batch));
        byte[] beforeArchives = Archives(boundary.BeforeDatabase);
        byte[] afterArchives = Archives(boundary.AfterDatabase);
        if (boundary.Name == "archive")
            Assert.False(beforeArchives.AsSpan().SequenceEqual(afterArchives), "The archive boundary must publish actual new archive bytes.");
        File.WriteAllBytes(Path.Combine(output, "archives-before.json"), beforeArchives);
        File.WriteAllBytes(Path.Combine(output, "archives-after-expected.json"), afterArchives);
        string dotnet = Environment.GetEnvironmentVariable("OPENFANTASY_DOTNET")
            ?? throw new InvalidOperationException("External Host qualification requires OPENFANTASY_DOTNET.");
        string host = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll"));
        Assert.True(File.Exists(host), "Build the actual Host before process qualification.");
        var start = new ProcessStartInfo(dotnet)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { host, "--probe-world-step-crash", database, step, committed ? "after" : "before" })
            start.ArgumentList.Add(argument);
        var elapsed = Stopwatch.StartNew();
        using var process = Process.Start(start) ?? throw new IOException("The actual Host storage process did not start.");
        Task<string> stderr = process.StandardError.ReadToEndAsync(token);
        try
        {
            string? reached = await process.StandardOutput.ReadLineAsync(token).AsTask().WaitAsync(TimeSpan.FromSeconds(30), token);
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), "The Host publication barrier exceeded its unchanged thirty-second bound.");
            Assert.NotNull(reached);
            File.WriteAllText(Path.Combine(output, "boundary.json"), reached);
            using var signal = JsonDocument.Parse(reached);
            Assert.Equal(committed ? "AfterCommit" : "BeforeCommit", signal.RootElement.GetProperty("Boundary").GetString());
            Assert.False(process.HasExited);
            var termination = Stopwatch.StartNew();
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(5), token);
            termination.Stop();
            Assert.True(termination.Elapsed < TimeSpan.FromSeconds(5), "The actual Host termination exceeded its unchanged five-second bound.");
            Assert.NotEqual(0, process.ExitCode);

            WorldSnapshot expected = committed ? boundary.After : boundary.Before;
            using (var reopened = SqliteWorldAuthority.OpenExisting(database, fixture.GenesisBytes))
            {
                var actual = reopened.ReadStateForHost();
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(actual));
                fixture.AssertPureRecovery(boundary, actual, committed);
                fixture.AssertOriginalOwnership(actual);
                fixture.AssertPaidReceipt(reopened);
                Assert.Equal(boundary.Before.ContentHash, reopened.ReadRecordedStateForHost(boundary.Before.Revision).State.ContentHash);
                Assert.Equal(expected.ContentHash, reopened.ReadRecordedStateForHost(expected.Revision).State.ContentHash);
                Assert.Equal(committed, reopened.IsIdentityOccupiedForHost(boundary.Batch.CauseId));
                File.WriteAllBytes(Path.Combine(output, "recovered.ofc"), WorldSnapshotCodec.Encode(actual));
            }
            byte[] actualArchives = Archives(database);
            Assert.Equal(committed ? afterArchives : beforeArchives, actualArchives);
            File.WriteAllBytes(Path.Combine(output, "archives-recovered.json"), actualArchives);
            Write(output, "result.json", new
            {
                Status = "passed", Boundary = boundary.Name, Committed = committed, process.ExitCode,
                BeforeRevision = boundary.Before.Revision, AfterRevision = expected.Revision,
                StateHash = expected.ContentHash.Value, ArchivesHash = Convert.ToHexString(SHA256.HashData(actualArchives)),
                ElapsedSeconds = elapsed.Elapsed.TotalSeconds,
                TerminationSeconds = termination.Elapsed.TotalSeconds,
                Scope = "Actual Host storage-process death at its existing transaction barrier; original-genesis pure reopen. No live listener/socket failure claim."
            });
            return database;
        }
        catch (Exception error)
        {
            Write(output, "result.failed.json", new { Boundary = boundary.Name, Committed = committed, Error = error.ToString(),
                ElapsedSeconds = elapsed.Elapsed.TotalSeconds });
            throw;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(5), token);
            }
            File.WriteAllText(Path.Combine(output, "stderr.log"), await stderr);
        }
    }

    internal static byte[] Archives(string path)
    {
        using var database = WorldDatabase.Open(path, create: false);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            Resource = database.ReadResourceArchives().ToArray(),
            MaterialAndCare = database.ReadMaterialCareArchives().ToArray()
        });
    }

    internal static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
