using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>Private atomic candidates; publication and historical identity admission belong to the world transaction.</summary>
public sealed class CraftPlan
{
    internal CraftPlan(CountedAuthorityState inventory, ProductionSnapshot production,
        WorldActionRejection rejection, IEnumerable<EntityId> createdIds)
    {
        CandidateInventory = inventory;
        CandidateProduction = production;
        Rejection = rejection;
        CreatedIds = Array.AsReadOnly(createdIds.ToArray());
    }

    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public WorldActionRejection Rejection { get; }
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
}
