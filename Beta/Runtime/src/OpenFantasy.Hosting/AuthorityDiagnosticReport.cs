using OpenFantasy.Core.Commands;

namespace OpenFantasy.Hosting;

/// <summary>Explicit operator diagnostics, deliberately separate from ordinary player observations.</summary>
public sealed class AuthorityDiagnosticReport
{
    public AuthorityDiagnosticReport(AuthorityInspection inspection, CommandReceipt? receipt = null)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        CommitSequence = inspection.CommitSequence;
        StateHash = inspection.State.ContentHash.Value;
        CustodyRevision = inspection.State.Custody.Revision;
        Database = inspection.Database;
        ReceiptHex = receipt is null ? null : Convert.ToHexString(CommandReceiptCodecV1.Encode(receipt)).ToLowerInvariant();
    }

    public string Contract => "durable-counted-authority-v1";
    public ulong CommitSequence { get; }
    public string StateHash { get; }
    public ulong CustodyRevision { get; }
    public SqliteAuthorityDiagnostics Database { get; }
    public string? ReceiptHex { get; }
}
