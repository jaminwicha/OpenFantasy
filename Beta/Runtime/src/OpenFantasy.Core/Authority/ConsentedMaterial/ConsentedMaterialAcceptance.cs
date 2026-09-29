using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Authority.ConsentedMaterial;

/// <summary>
/// The recipient's immutable approval of one exact sender command. It restates the complete terms
/// and pins the command identity and content hash; it is not a reusable permission, a deposit
/// policy or a treatment consent, and constructing it authenticates nobody.
/// </summary>
public sealed class ConsentedMaterialAcceptance
{
    public const ulong CurrentVersion = 1;

    public ConsentedMaterialAcceptance(EntityId acceptanceId, EntityId principalId, EntityId originSessionId,
        EntityId commandId, CommandContentHash commandHash, ConsentedMaterialTerms terms)
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
    public ConsentedMaterialTerms Terms { get; }

    /// <summary>Copies the observed command's identity, hash and terms into the recipient's own claims.</summary>
    public static ConsentedMaterialAcceptance Approving(ConsentedMaterialCommand command, EntityId acceptanceId,
        EntityId principalId, EntityId originSessionId)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new ConsentedMaterialAcceptance(acceptanceId, principalId, originSessionId, command.CommandId,
            command.ComputeContentHash(), command.Terms);
    }

    /// <summary>Compares supplied data only: identity, complete content hash and every restated term.</summary>
    public bool Binds(ConsentedMaterialCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommandId == command.CommandId && Terms == command.Terms
            && CommandHash == command.ComputeContentHash();
    }

    public CommandContentHash ComputeContentHash() =>
        CommandContentHash.Compute(ConsentedMaterialCodecV1.Encode(this));
}
