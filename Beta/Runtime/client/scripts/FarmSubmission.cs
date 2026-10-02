using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Client;

/// <summary>
/// One observation of the client's world-command boundary: the exact presented offer that was submitted, the command
/// built from it with its canonical bytes, and the host's receipt task. It is passive evidence for disposable probes and
/// tests: it grants nothing, changes nothing and is never retained by the session. A retry of the same offer object
/// reports the original command and receipt task, never a new identity.
/// </summary>
internal sealed class FarmSubmission
{
    private readonly byte[] _canonicalCommand;

    internal FarmSubmission(FarmActionOffer offer, WorldActionCommand command, Task<WorldActionReceipt> receipt, bool isRetry)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(receipt);
        Offer = offer;
        Command = command;
        // The exact bytes the authority stores beside the receipt in its command-outcome journal.
        _canonicalCommand = WorldActionCodec.Encode(command);
        Receipt = receipt;
        IsRetry = isRetry;
    }

    /// <summary>The presented offer object that was submitted, as selected; never a fresher equivalent.</summary>
    internal FarmActionOffer Offer { get; }

    internal WorldActionCommand Command { get; }

    internal EntityId CommandId => Command.CommandId;

    /// <summary>A fresh copy of the canonical command bytes; no observer can alter the captured evidence.</summary>
    internal byte[] CanonicalCommandBytes() => (byte[])_canonicalCommand.Clone();

    /// <summary>The host's answer: an applied or rejected receipt, or the dispatch failure that refused the command.</summary>
    internal Task<WorldActionReceipt> Receipt { get; }

    /// <summary>The same offer object was submitted again; its original command and receipt task are reported.</summary>
    internal bool IsRetry { get; }
}
