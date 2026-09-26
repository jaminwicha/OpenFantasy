using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>
/// Coupled material and vitality candidates. The caller still owns treatment permission, delivery,
/// anatomy, reach, timing, encounter reconciliation, historical identity and complete-world publication.
/// </summary>
public sealed class RestorativeItemPlan
{
    internal RestorativeItemPlan(CountedAuthorityState inventory, ProductionSnapshot production,
        ProgressionSnapshot progression, WorldActionRejection rejection, ConsumedItemClaim? claim = null,
        ActorResourceTransition? actorTransition = null)
    {
        CandidateInventory = inventory;
        CandidateProduction = production;
        CandidateProgression = progression;
        Rejection = rejection;
        Claim = claim;
        ActorTransition = actorTransition;
    }

    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public ProgressionSnapshot CandidateProgression { get; }
    public WorldActionRejection Rejection { get; }
    public ConsumedItemClaim? Claim { get; }
    public ActorResourceTransition? ActorTransition { get; }
}
