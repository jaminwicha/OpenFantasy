using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

/// <summary>Requires the built Host and an explicit .NET executable; kills the existing storage operator, not a listener.</summary>
public sealed class PartyWithdrawalProcessTests
{
    [Theory]
    [InlineData("sample", false)]
    [InlineData("sample", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    [InlineData("departure", false)]
    [InlineData("departure", true)]
    public void Actual_host_death_retains_exact_paid_work_and_allows_same_save_continuation(string target, bool committed) =>
        PartyWithdrawalRecoveryTests.ExerciseInterruptedBoundary(target, committed, processKill: true);

    internal static void KillAtCommit(string database, WorldStepBatch batch, string target, bool committed,
        WorldSnapshot before, WorldSnapshot after)
    {
        string dotnet = Environment.GetEnvironmentVariable("OPENFANTASY_DOTNET")
            ?? throw new InvalidOperationException("Process qualification requires OPENFANTASY_DOTNET.");
        string host = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll"));
        Assert.True(File.Exists(host), "Build the actual Host before process qualification.");
        string root = Environment.GetEnvironmentVariable("OPENFANTASY_WITHDRAWAL_RECOVERY_EVIDENCE")
            ?? Path.GetDirectoryName(database)!;
        string output = Path.Combine(root, target + (committed ? "-after" : "-before"));
        Assert.False(Directory.Exists(output), "Retained process evidence is create-only; use a new run directory.");
        Directory.CreateDirectory(output);
        File.Copy(database, Path.Combine(output, "before.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllBytes(Path.Combine(output, "expected-after.ofc"), WorldSnapshotCodec.Encode(after));
        string step = Path.Combine(output, "publication.ofc");
        File.WriteAllBytes(step, WorldStepCodec.Encode(batch));
        var start = new ProcessStartInfo(dotnet)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { host, "--probe-world-step-crash", database, step, committed ? "after" : "before" })
            start.ArgumentList.Add(argument);
        var elapsed = Stopwatch.StartNew();
        using var process = Process.Start(start) ?? throw new IOException("The actual Host storage process did not start.");
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        try
        {
            string? reached = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), "Host publication exceeded the thirty-second barrier bound.");
            Assert.NotNull(reached);
            File.WriteAllText(Path.Combine(output, "boundary.json"), reached);
            using var signal = JsonDocument.Parse(reached);
            Assert.Equal(committed ? "AfterCommit" : "BeforeCommit", signal.RootElement.GetProperty("Boundary").GetString());
            Assert.False(process.HasExited);
            double boundarySeconds = elapsed.Elapsed.TotalSeconds;
            var termination = Stopwatch.StartNew();
            process.Kill(entireProcessTree: true);
            Assert.True(process.WaitForExit(5_000), "Host termination exceeded the five-second bound.");
            termination.Stop();
            Assert.True(termination.Elapsed < TimeSpan.FromSeconds(5));
            Assert.NotEqual(0, process.ExitCode);
            // Preserve the unrecovered SQLite/WAL bytes after confirmed process death.
            foreach (string suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(database + suffix)) File.Copy(database + suffix, Path.Combine(output, "interrupted.sqlite3" + suffix));
            File.WriteAllText(Path.Combine(output, "termination.json"), JsonSerializer.Serialize(new
            {
                process.Id, process.ExitCode, Target = target, Committed = committed,
                BoundarySeconds = boundarySeconds,
                TerminationSeconds = termination.Elapsed.TotalSeconds,
                BeforeHash = before.ContentHash.Value, AfterHash = after.ContentHash.Value
            }));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(5_000)) throw new TimeoutException("Failed to terminate the Host probe during cleanup.");
            }
            File.WriteAllText(Path.Combine(output, "stderr.log"), stderr.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
        }
    }
}
