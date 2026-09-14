using System.Collections.ObjectModel;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Geography;

/// <summary>Atomic material/production candidate with an exact complete-state precondition.</summary>
public sealed class TerrainMaterialCraftPlan
{
    private readonly TerrainMaterialRegistryPlan _plan;

    internal TerrainMaterialCraftPlan(TerrainMaterialRegistry basis, TerrainMaterialRegistry candidate,
        WorldActionRejection rejection, IEnumerable<EntityId> createdIds)
    {
        _plan = new(basis, candidate);
        Rejection = rejection;
        CreatedIds = createdIds.ToList().AsReadOnly();
    }

    public TerrainMaterialRegistry Candidate => _plan.Candidate;
    public WorldActionRejection Rejection { get; }
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
    public TerrainMaterialRegistry ApplyTo(TerrainMaterialRegistry registry) => _plan.ApplyTo(registry);
}
