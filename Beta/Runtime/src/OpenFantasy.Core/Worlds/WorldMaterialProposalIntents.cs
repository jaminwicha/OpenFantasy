using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// One submission of the saved proposal lifecycle. Each is a separate command with its own identity
/// and terminal receipt; none is the simultaneous pair and none is interpreted by an earlier profile.
/// </summary>
public abstract record MaterialProposalIntent : WorldActionIntent;

/// <summary>
/// The sender's saved offer of one exact lot interval. It reserves nothing and grants no control;
/// the expiry is an absolute advancing-step count of this world, never a wall clock.
/// </summary>
public sealed record ProposeMaterialIntent : MaterialProposalIntent
{
    public ProposeMaterialIntent(ConsentedMaterialTerms terms, ulong expiresAtAdvancingStep)
    {
        ArgumentNullException.ThrowIfNull(terms);
        Terms = terms;
        ExpiresAtAdvancingStep = expiresAtAdvancingStep;
    }

    public ConsentedMaterialTerms Terms { get; }
    public ulong ExpiresAtAdvancingStep { get; }

    /// <summary>The sender's material claims under the identity of its own creation command.</summary>
    public ConsentedMaterialCommand ToGrant(WorldActionCommand submitted)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        return new ConsentedMaterialCommand(submitted.CommandId, submitted.PrincipalId, submitted.OriginSessionId, Terms);
    }
}

/// <summary>
/// The recipient's own later approval. It pins the proposal identity and the saved grant's content
/// and restates every term; it supplies neither the sender's approval nor the sender's delivery.
/// </summary>
public sealed record AcceptMaterialProposalIntent : MaterialProposalIntent
{
    public AcceptMaterialProposalIntent(EntityId proposalId, CommandContentHash grantHash,
        ConsentedMaterialTerms terms, ulong expectedSenderRevision)
    {
        ArgumentNullException.ThrowIfNull(grantHash);
        ArgumentNullException.ThrowIfNull(terms);
        ProposalId = proposalId;
        GrantHash = grantHash;
        Terms = terms;
        ExpectedSenderRevision = expectedSenderRevision;
    }

    public EntityId ProposalId { get; }
    public CommandContentHash GrantHash { get; }
    public ConsentedMaterialTerms Terms { get; }
    public ulong ExpectedSenderRevision { get; }

    /// <summary>Copies the inspected proposal's identity, grant hash and terms into the recipient's claims.</summary>
    public static AcceptMaterialProposalIntent Approving(WorldMaterialProposal proposal, ulong expectedSenderRevision)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return new AcceptMaterialProposalIntent(proposal.ProposalId, proposal.GrantHash, proposal.Terms,
            expectedSenderRevision);
    }

    /// <summary>The recipient's material claims under the identity of its own acceptance command.</summary>
    public ConsentedMaterialAcceptance ToMaterialAcceptance(WorldActionCommand submitted, EntityId creationCommandId)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        return new ConsentedMaterialAcceptance(submitted.CommandId, submitted.PrincipalId, submitted.OriginSessionId,
            creationCommandId, GrantHash, Terms);
    }
}

/// <summary>The recipient's refusal of one open proposal. It moves nothing and closes that proposal.</summary>
public sealed record DeclineMaterialProposalIntent(EntityId ProposalId) : MaterialProposalIntent;

/// <summary>The sender's withdrawal of its own open proposal. It moves nothing and closes that proposal.</summary>
public sealed record WithdrawMaterialProposalIntent(EntityId ProposalId) : MaterialProposalIntent;
