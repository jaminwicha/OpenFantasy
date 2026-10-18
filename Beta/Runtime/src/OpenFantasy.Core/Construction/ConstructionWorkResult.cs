using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Unpublished coupled construction/material candidate; not an admitted world, earned clock or receipt.</summary>
public sealed class ConstructionWorkResult
{
    internal ConstructionWorkResult(CountedAuthorityState inventory, ProductionSnapshot production,
        ConstructionSnapshot construction, WorldActionRejection rejection, IEnumerable<EntityId> createdIds,
        SpatialStructure? structure = null, LocalSpatialSnapshot? completeSpace = null,
        ConstructionCompletionEvidence? evidence = null)
    {
        CandidateInventory = inventory;
        CandidateProduction = production;
        CandidateConstruction = construction;
        CandidateStructure = structure;
        CompleteSpace = completeSpace;
        Evidence = evidence;
        Rejection = rejection;
        CreatedIds = Array.AsReadOnly(createdIds.ToArray());
    }

    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public ConstructionSnapshot CandidateConstruction { get; }
    public SpatialStructure? CandidateStructure { get; }
    public WorldActionRejection Rejection { get; }
    public bool Succeeded => Rejection == WorldActionRejection.None;
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
    internal LocalSpatialSnapshot? CompleteSpace { get; }
    internal ConstructionCompletionEvidence? Evidence { get; }
}

internal sealed record ConstructionCompletionRequest(CountedAuthorityState AfterInventory,
    ConstructionSnapshot Completed, ConstructionProject Project, ProductionBatch Batch);
