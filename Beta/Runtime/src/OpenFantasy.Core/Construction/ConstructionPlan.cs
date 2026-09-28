using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Private immutable candidate; the host publishes it only with its durable ordinary command outcome.</summary>
public sealed class ConstructionPlan
{
    internal ConstructionPlan(CountedAuthorityState inventory, ProductionSnapshot production,
        ConstructionSnapshot construction, LocalSpatialSnapshot space, WorldActionRejection rejection,
        IEnumerable<EntityId> createdIds, ConstructionCompletionEvidence? evidence = null)
    {
        Evidence = evidence;
        CandidateInventory = inventory;
        CandidateProduction = production;
        CandidateConstruction = construction;
        CandidateSpace = space;
        Rejection = rejection;
        CreatedIds = Array.AsReadOnly(createdIds.ToArray());
    }

    public ConstructionCompletionEvidence? Evidence { get; }
    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public ConstructionSnapshot CandidateConstruction { get; }
    public LocalSpatialSnapshot CandidateSpace { get; }
    public WorldActionRejection Rejection { get; }
    public bool Succeeded => Rejection == WorldActionRejection.None;
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
}
