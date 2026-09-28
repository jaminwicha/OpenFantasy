using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>One complete standalone candidate; refusal retains every actually supplied owner.</summary>
public sealed class MaterialConstructionPlan
{
    internal MaterialConstructionPlan(CountedAuthorityState inventory, ProductionSnapshot production,
        MaterialConstructionState state, LocalSpatialSnapshot space, WorldActionRejection rejection,
        IEnumerable<EntityId> createdIds, ConstructionCompletionEvidence? evidence = null)
    {
        CandidateInventory = inventory;
        CandidateProduction = production;
        CandidateState = state;
        CandidateSpace = space;
        Rejection = rejection;
        CreatedIds = Array.AsReadOnly(createdIds.ToArray());
        Evidence = evidence;
    }
    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public MaterialConstructionState CandidateState { get; }
    public ConstructionSnapshot CandidateConstruction => CandidateState.Construction;
    public ConstructionPhysicalSnapshot? CandidatePhysical => CandidateState.Physical;
    public LocalSpatialSnapshot CandidateSpace { get; }
    public WorldActionRejection Rejection { get; }
    public bool Succeeded => Rejection == WorldActionRejection.None;
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
    public ConstructionCompletionEvidence? Evidence { get; }
}
