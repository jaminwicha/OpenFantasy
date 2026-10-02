using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

/// <summary>External process termination, independently reopened against the original world.</summary>
public sealed class ControllerRecoveryProcessTests
{
    private static readonly Lazy<WorldSnapshot> Genesis = new(() => WorldControllerAdmission.CreateFresh(
        CargoOutcomeWorldAdmission.CreateEmbedded("controller-recovery-crash", "Traveler")));

    [Theory]
    [InlineData("disconnect", false)]
    [InlineData("disconnect", true)]
    [InlineData("expire", false)]
    [InlineData("expire", true)]
    [InlineData("reconnect", false)]
    [InlineData("reconnect", true)]
    [InlineData("checkpoint", false)]
    [InlineData("checkpoint", true)]
    public async Task A_killed_host_preserves_atomic_controller_rows_and_measured_checkpoints(string operation, bool committed)
    {
        using var temporary = new ServerFiles();
        string root = Environment.GetEnvironmentVariable("OPENFANTASY_CONTROLLER_RECOVERY_PROCESS_EVIDENCE")
            ?? temporary.DirectoryPath;
        string output = Path.Combine(root, operation + (committed ? "-after" : "-before"));
        Directory.CreateDirectory(output);
        string database = Path.Combine(output, "world.sqlite");
        WorldSnapshot genesis = Genesis.Value;
        EntityId actor = genesis.Controllers!.Assignments.Single().ActorId;
        WorldSnapshot before;
        ControllerAllowanceProgress[] oldRows;
        WorldStepBatch? change = null;
        using (var authority = SqliteWorldAuthority.CreateRecoverable(database, genesis))
        {
            if (operation != "disconnect") authority.Commit(Event(genesis, actor, WorldControllerEventKind.Disconnected, 1));
            ControllerRecoverySegment segment = authority.BeginControllerRecoverySegment();
            if (operation == "expire")
            {
                // Trusted fixture calibration; the live host's measured duration is qualified separately.
                authority.CheckpointControllerAllowances(segment.Generation, segment.Allowances.Select(row => row with
                    { ElapsedHostMilliseconds = WorldControllerState.ReconnectAllowanceMilliseconds }).ToArray());
            }
            before = authority.ReadStateForHost();
            oldRows = authority.ReadControllerAllowancesForHost().ToArray();
            if (operation != "checkpoint")
            {
                WorldControllerEventKind kind = operation switch
                {
                    "disconnect" => WorldControllerEventKind.Disconnected,
                    "expire" => WorldControllerEventKind.AllowanceExpired,
                    _ => WorldControllerEventKind.Reconnected
                };
                change = Event(before, actor, kind, 2);
                File.WriteAllBytes(Path.Combine(output, "event.ofc"), WorldStepCodec.Encode(change));
            }
        }
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
        File.WriteAllBytes(Path.Combine(output, "before.ofc"), WorldSnapshotCodec.Encode(before));
        string dotnet = Environment.GetEnvironmentVariable("OPENFANTASY_DOTNET")
            ?? throw new InvalidOperationException("The process proof requires the qualified dotnet executable.");
        string host = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll"));
        Assert.True(File.Exists(host), "Build the actual Host before its process qualification.");
        var start = new ProcessStartInfo(dotnet)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(host);
        start.ArgumentList.Add(operation == "checkpoint" ? "--probe-controller-checkpoint-crash" : "--probe-world-step-crash");
        start.ArgumentList.Add(database);
        if (operation != "checkpoint") start.ArgumentList.Add(Path.Combine(output, "event.ofc"));
        start.ArgumentList.Add(committed ? "after" : "before");
        using var process = Process.Start(start) ?? throw new IOException("Host did not start.");
        Task<string> stderr = process.StandardError.ReadToEndAsync(ServerWorld.Cancellation);
        var elapsed = Stopwatch.StartNew();
        string? reached = null;
        try
        {
            reached = await process.StandardOutput.ReadLineAsync(ServerWorld.Cancellation)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(30), ServerWorld.Cancellation);
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30));
            Assert.NotNull(reached);
            File.WriteAllText(Path.Combine(output, "boundary.json"), reached);
            using JsonDocument boundary = JsonDocument.Parse(reached);
            Assert.Equal(committed ? "AfterCommit" : "BeforeCommit", boundary.RootElement.GetProperty("Boundary").GetString());
            Assert.False(process.HasExited);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(ServerWorld.Cancellation).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            Assert.NotEqual(0, process.ExitCode);

            using var reopened = SqliteWorldAuthority.OpenExisting(database, WorldSnapshotCodec.Encode(genesis));
            WorldSnapshot expected = committed && change is not null
                ? new WorldStepProcessor().Decide(before, change).Candidate : before;
            Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Equal(before.ContentHash, reopened.ReadRecordedStateForHost(before.Revision).State.ContentHash);
            ControllerAllowanceProgress[] rows = reopened.ReadControllerAllowancesForHost().ToArray();
            if (operation == "checkpoint" && committed)
            {
                Assert.Equal(oldRows.Length, rows.Length);
                ulong measured = boundary.RootElement.GetProperty("MeasuredMilliseconds").GetUInt64();
                Assert.InRange(measured, 250UL, 30_000UL);
                Assert.Equal(boundary.RootElement.GetProperty("ElapsedMilliseconds").EnumerateArray().Select(value => value.GetUInt64()),
                    rows.Select(row => row.ElapsedHostMilliseconds));
                Assert.Equal(oldRows[0] with { ElapsedHostMilliseconds = measured }, Assert.Single(rows));
            }
            else if (!committed) Assert.Equal(oldRows, rows);
            else if (operation == "disconnect")
                Assert.Equal(new ControllerAllowanceProgress(actor, expected.Controllers!.Get(actor).Revision,
                    expected.Controllers.Get(actor).DisconnectId!.Value, 0), Assert.Single(rows));
            else Assert.Empty(rows);
            if (change is not null) Assert.Equal(committed, reopened.IsIdentityOccupiedForHost(change.CauseId));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
            {
                Status = "passed", Operation = operation, Committed = committed, ExitCode = process.ExitCode,
                StateHash = expected.ContentHash.Value, Rows = rows, ElapsedSeconds = elapsed.Elapsed.TotalSeconds
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(ServerWorld.Cancellation)
                    .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            }
            File.WriteAllText(Path.Combine(output, "stderr.log"), await stderr);
        }
    }

    private static WorldStepBatch Event(WorldSnapshot state, EntityId actor, WorldControllerEventKind kind, ulong id)
    {
        WorldControllerAssignment current = state.Controllers!.Get(actor);
        var input = new WorldControllerEvent(actor, kind, current.Revision,
            kind == WorldControllerEventKind.Disconnected ? null : current.DisconnectId,
            kind == WorldControllerEventKind.AllowanceExpired ? WorldControllerState.ReconnectAllowanceMilliseconds : 0,
            kind == WorldControllerEventKind.Disconnected ? WorldControllerDisconnectReason.TransportLost : null);
        return new(new EntityId(0xca11cafe00000000, id), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.ControllerLifecycle, [], [], state.ProfileVersion, input);
    }
}
