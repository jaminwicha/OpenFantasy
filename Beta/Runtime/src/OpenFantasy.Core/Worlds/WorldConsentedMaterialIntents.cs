using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The sender's half of one paired material transaction. It names the exact goods and the observed
/// recipient presence; it reserves nothing and is never a persisted offer. Without the recipient's
/// own acceptance in the same selected step it is refused.
/// </summary>
public sealed record OfferMaterialIntent : WorldActionIntent
{
    public OfferMaterialIntent(ConsentedMaterialTerms terms, ulong expectedRecipientRevision)
    {
        ArgumentNullException.ThrowIfNull(terms);
        Terms = terms;
        ExpectedRecipientRevision = expectedRecipientRevision;
    }

    public ConsentedMaterialTerms Terms { get; }
    public ulong ExpectedRecipientRevision { get; }

    /// <summary>The sender's material claims under the identity of its own world command.</summary>
    public ConsentedMaterialCommand ToMaterialCommand(WorldActionCommand submitted)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        return new ConsentedMaterialCommand(submitted.CommandId, submitted.PrincipalId, submitted.OriginSessionId, Terms);
    }
}

/// <summary>
/// The recipient's half: it pins the exact offer identity and content and restates every term under
/// the recipient's own command identity. Friendship, care permission or an earlier acceptance never
/// substitutes for it.
/// </summary>
public sealed record AcceptMaterialIntent : WorldActionIntent
{
    public AcceptMaterialIntent(EntityId offerCommandId, CommandContentHash offerHash,
        ConsentedMaterialTerms terms, ulong expectedSenderRevision)
    {
        ArgumentNullException.ThrowIfNull(offerHash);
        ArgumentNullException.ThrowIfNull(terms);
        OfferCommandId = offerCommandId;
        OfferHash = offerHash;
        Terms = terms;
        ExpectedSenderRevision = expectedSenderRevision;
    }

    public EntityId OfferCommandId { get; }
    public CommandContentHash OfferHash { get; }
    public ConsentedMaterialTerms Terms { get; }
    public ulong ExpectedSenderRevision { get; }

    /// <summary>Copies the observed offer's identity, material hash and terms into the recipient's claims.</summary>
    public static AcceptMaterialIntent Approving(WorldActionCommand offer, ulong expectedSenderRevision)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (offer.Intent is not OfferMaterialIntent intent)
        {
            throw new ArgumentException("Only a material offer can be accepted.", nameof(offer));
        }
        return new AcceptMaterialIntent(offer.CommandId, intent.ToMaterialCommand(offer).ComputeContentHash(),
            intent.Terms, expectedSenderRevision);
    }

    /// <summary>The recipient's material claims under the identity of its own world command.</summary>
    public ConsentedMaterialAcceptance ToMaterialAcceptance(WorldActionCommand submitted)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        return new ConsentedMaterialAcceptance(submitted.CommandId, submitted.PrincipalId, submitted.OriginSessionId,
            OfferCommandId, OfferHash, Terms);
    }
}
