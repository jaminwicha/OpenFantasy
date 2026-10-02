using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Host;

/// <summary>Explicit offline admission and inspection; ordinary inspection never starts a clock.</summary>
internal sealed class ControllerRecoveryOperator
{
    internal string Create(string database, string genesisPath)
    {
        using var authority = SqliteWorldAuthority.CreateRecoverable(database,
            LocalAuthorityFiles.ReadComposedGenesis(genesisPath));
        return Report(authority);
    }

    internal string Upgrade(string source, string destination)
    {
        using var authority = SqliteWorldAuthority.UpgradeControllerRecovery(source, destination);
        return Report(authority);
    }

    internal string Inspect(string database)
    {
        using var authority = SqliteWorldAuthority.OpenExisting(database);
        return Report(authority);
    }

    /// <summary>Qualification observes real monotonic time; its caller cannot supply earned elapsed time.</summary>
    internal string ProbeCheckpointCrash(string database, string boundary)
    {
        AuthorityCommitBoundary requested = boundary switch
        {
            "before" => AuthorityCommitBoundary.BeforeCommit,
            "after" => AuthorityCommitBoundary.AfterCommit,
            _ => throw new ArgumentException("The crash probe boundary must be before or after.")
        };
        using var authority = SqliteWorldAuthority.OpenExisting(database);
        ControllerRecoverySegment segment = authority.BeginControllerRecoverySegment();
        if (segment.Allowances.Count == 0)
            throw new ArgumentException("The checkpoint probe requires an actual outstanding allowance.");
        long startedAt = Stopwatch.GetTimestamp();
        Thread.Sleep(TimeSpan.FromMilliseconds(250));
        ulong measured = checked((ulong)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        var progress = segment.Allowances.Select(row => row with
        {
            ElapsedHostMilliseconds = row.ElapsedHostMilliseconds + Math.Min(measured,
                WorldControllerState.ReconnectAllowanceMilliseconds - row.ElapsedHostMilliseconds)
        }).ToArray();
        authority.CheckpointControllerAllowances(segment.Generation, progress, reached =>
        {
            if (reached != requested) return;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Boundary = reached.ToString(), segment.Generation, MeasuredMilliseconds = measured,
                ElapsedMilliseconds = progress.Select(row => row.ElapsedHostMilliseconds).ToArray()
            }));
            Console.Out.Flush();
            // The external process owner kills here; no disposal or finally is permitted in that proof.
            Thread.Sleep(Timeout.Infinite);
        });
        return Report(authority);
    }

    private static string Report(SqliteWorldAuthority authority) => JsonSerializer.Serialize(new
    {
        Contract = "controller-recovery-operator-v1",
        authority.SupportsControllerRecovery,
        StateHash = authority.ReadStateForHost().ContentHash.Value,
        Allowances = authority.SupportsControllerRecovery ? authority.ReadControllerAllowancesForHost() : []
    });
}
