using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Hosting;

namespace OpenFantasy.Host;

/// <summary>Local, exclusive database operations. A network authentication adapter is not provided here.</summary>
internal sealed class LocalWorldOperator
{
    internal string Create(string databasePath, string genesisPath)
    {
        using var host = SqliteCountedAuthority.CreateNew(databasePath, LocalAuthorityFiles.ReadGenesis(genesisPath));
        return Report(host);
    }

    internal string Inspect(string databasePath)
    {
        using var host = SqliteCountedAuthority.OpenExisting(databasePath);
        return Report(host);
    }

    internal string Execute(string databasePath, string commandPath, string principal, string session, string? crashBoundary = null)
    {
        var delivery = new TrustedCommandDelivery(EntityId.Parse(principal), EntityId.Parse(session));
        CommandEnvelope command = LocalAuthorityFiles.ReadCommand(commandPath);
        AuthorityCommitBoundary? requested = crashBoundary switch
        {
            null => null,
            "before" => AuthorityCommitBoundary.BeforeCommit,
            "after" => AuthorityCommitBoundary.AfterCommit,
            _ => throw new ArgumentException("The crash probe boundary must be before or after.")
        };
        using var host = SqliteCountedAuthority.OpenExisting(databasePath);
        CommandReceipt receipt = host.Execute(command, delivery, reached =>
        {
            if (reached == requested)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { Boundary = reached.ToString() }));
                Console.Out.Flush();
                // External qualification kills the process here, without running Dispose/finally.
                Thread.Sleep(Timeout.Infinite);
            }
        });
        return Report(host, receipt);
    }

    private static string Report(SqliteCountedAuthority host, CommandReceipt? receipt = null) =>
        JsonSerializer.Serialize(new AuthorityDiagnosticReport(host.InspectForHost(), receipt));
}
