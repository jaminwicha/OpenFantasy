using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Worlds;

/// <summary>Validates the complete stable load boundary owned by a structural world.</summary>
public static class WorldStructuralBinding
{
    public static void ValidateStable(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.StructuralPolicy is not { } policy)
        {
            return;
        }
        ValidateMaterialOwnership(world);
        ConstructionBearingModel model = policy.Bind(world);
        StructuralLoadResolution resolution = StructuralLoadResolution.Begin(model.Graph, []);
        while (!resolution.IsComplete)
        {
            resolution = resolution.Advance(StructuralLoadResolution.MaximumEvaluationsPerAdvance);
            if (resolution.Failures.Count != 0)
            {
                // Stable admission needs one failure-free pass. Cascading consequences belong to
                // their transaction owner; resolving them here cannot make this root admissible.
                throw new ArgumentException("A structural world requires stable complete member, actor and carried loads.", nameof(world));
            }
        }
    }

    private static void ValidateMaterialOwnership(WorldSnapshot world)
    {
        HashSet<EntityId> carried = world.Residents.Select(value => value.InventoryCustodianId).ToHashSet();
        foreach (ConstructionProject project in world.Construction!.Projects)
        {
            if (project.StockLocation != ConstructionStockLocation.BuilderCarried)
            {
                throw new ArgumentException("Structural project stock requires a represented load owner.", nameof(world));
            }
            carried.Add(project.ProjectCustodianId);
        }
        if (world.Inventory.Custody.Lots.Any(value => !carried.Contains(value.CustodianId))
            || world.Production!.Lots.Any(value => !carried.Contains(value.CustodianId)))
        {
            throw new ArgumentException("Every live material lot requires an admitted actor, carried project or installed load owner.", nameof(world));
        }
    }
}
