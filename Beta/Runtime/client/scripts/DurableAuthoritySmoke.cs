using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Hosting;

namespace OpenFantasy.Client;

/// <summary>Runs the same actual store off the engine thread and verifies its lost-reply retry path.</summary>
internal sealed class DurableAuthoritySmoke
{
    internal Task<AuthorityDiagnosticReport> RunAsync(string databasePath, string genesisPath, string commandPath) => Task.Run(() =>
    {
        CountedAuthorityState genesis = LocalAuthorityFiles.ReadGenesis(genesisPath);
        CommandEnvelope command = LocalAuthorityFiles.ReadCommand(commandPath);
        // This explicit local qualification selects accepted genesis identities, not network credentials.
        var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
        byte[] firstReceipt;
        using (var host = SqliteCountedAuthority.CreateNew(databasePath, genesis))
        {
            firstReceipt = CommandReceiptCodecV1.Encode(host.Execute(command, delivery));
        }
        using var reopened = SqliteCountedAuthority.OpenExisting(databasePath);
        CommandReceipt retry = reopened.Execute(command, delivery);
        if (!firstReceipt.AsSpan().SequenceEqual(CommandReceiptCodecV1.Encode(retry)))
        {
            throw new InvalidDataException("Reopening changed the committed command receipt.");
        }
        return new AuthorityDiagnosticReport(reopened.InspectForHost(), retry);
    });
}
