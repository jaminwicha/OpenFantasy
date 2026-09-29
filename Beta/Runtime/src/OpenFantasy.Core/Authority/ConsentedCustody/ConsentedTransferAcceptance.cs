using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Authority.ConsentedCustody;

/// <summary>
/// The recipient's immutable approval of one exact sender command. It restates the complete terms
/// and pins the command identity and content hash; it is not a reusable permission, a deposit
/// policy or a care consent, and constructing it authenticates nobody.
/// </summary>
public sealed class ConsentedTransferAcceptance
{
    public const ulong CurrentVersion = 1;

    public ConsentedTransferAcceptance(EntityId acceptanceId, EntityId principalId, EntityId originSessionId,
        EntityId commandId, CommandContentHash commandHash, ConsentedTransferTerms terms)
    {
        ArgumentNullException.ThrowIfNull(commandHash);
        ArgumentNullException.ThrowIfNull(terms);
        AcceptanceId = acceptanceId;
        PrincipalId = principalId;
        OriginSessionId = originSessionId;
        CommandId = commandId;
        CommandHash = commandHash;
        Terms = terms;
    }

    public ulong Version => CurrentVersion;
    public EntityId AcceptanceId { get; }
    public EntityId PrincipalId { get; }
    public EntityId OriginSessionId { get; }
    public EntityId CommandId { get; }
    public CommandContentHash CommandHash { get; }
    public ConsentedTransferTerms Terms { get; }

    /// <summary>Copies the observed command's identity, hash and terms into the recipient's own claims.</summary>
    public static ConsentedTransferAcceptance Approving(ConsentedTransferCommand command, EntityId acceptanceId,
        EntityId principalId, EntityId originSessionId)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new ConsentedTransferAcceptance(acceptanceId, principalId, originSessionId, command.CommandId,
            command.ComputeContentHash(), command.Terms);
    }

    /// <summary>Compares supplied data only: identity, complete content hash and every restated term.</summary>
    public bool Binds(ConsentedTransferCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommandId == command.CommandId && Terms == command.Terms
            && CommandHash == command.ComputeContentHash();
    }

    public CommandContentHash ComputeContentHash() =>
        CommandContentHash.Compute(ConsentedTransferCodecV1.Encode(this));
}
