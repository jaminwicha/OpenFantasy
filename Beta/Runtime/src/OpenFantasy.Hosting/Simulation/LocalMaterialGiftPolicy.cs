using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Explicit local NPC preferences; neither a custody grant nor a treatment permission.</summary>
public sealed class LocalMaterialGiftPolicy
{
    public LocalMaterialGiftPolicy(EntityId actorId, EntityId partnerActorId, EntityId selfUseDefinitionId,
        ContentHash selfUseVersion, bool offersMedicine = true, bool answersOffers = true,
        ulong retainedDoses = 1, ulong desiredDoses = 3, ulong offerLifetimeSteps = 64)
    {
        ArgumentNullException.ThrowIfNull(selfUseVersion);
        if (actorId == partnerActorId || !offersMedicine && !answersOffers
            || retainedDoses == 0 || desiredDoses < retainedDoses
            || offerLifetimeSteps == 0 || offerLifetimeSteps > WorldMaterialProposal.MaximumLifetimeSteps)
        {
            throw new ArgumentException("A gift preference needs distinct partners, an enabled choice and bounded finite-stock rules.");
        }
        ActorId = actorId;
        PartnerActorId = partnerActorId;
        SelfUseDefinitionId = selfUseDefinitionId;
        SelfUseVersion = selfUseVersion;
        OffersMedicine = offersMedicine;
        AnswersOffers = answersOffers;
        RetainedDoses = retainedDoses;
        DesiredDoses = desiredDoses;
        OfferLifetimeSteps = offerLifetimeSteps;
    }

    public EntityId ActorId { get; }
    public EntityId PartnerActorId { get; }
    public EntityId SelfUseDefinitionId { get; }
    public ContentHash SelfUseVersion { get; }
    public bool OffersMedicine { get; }
    public bool AnswersOffers { get; }
    public ulong RetainedDoses { get; }
    public ulong DesiredDoses { get; }
    public ulong OfferLifetimeSteps { get; }
}
