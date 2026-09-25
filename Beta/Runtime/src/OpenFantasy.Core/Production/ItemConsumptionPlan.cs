using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Unpublished material candidates. They grant no HP, action eligibility or permission to publish;
/// the complete world transaction owns treatment, costs, historical identity checks and persistence.
/// </summary>
public sealed class ItemConsumptionPlan
{
    internal ItemConsumptionPlan(CountedAuthorityState inventory, ProductionSnapshot production,
        WorldActionRejection rejection, ConsumedItemClaim? claim = null)
    {
        CandidateInventory = inventory;
        CandidateProduction = production;
        Rejection = rejection;
        Claim = claim;
        CreatedIds = Array.AsReadOnly(claim is null ? Array.Empty<EntityId>() : [claim.ConsumptionId]);
    }

    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public WorldActionRejection Rejection { get; }
    public ConsumedItemClaim? Claim { get; }
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
}
