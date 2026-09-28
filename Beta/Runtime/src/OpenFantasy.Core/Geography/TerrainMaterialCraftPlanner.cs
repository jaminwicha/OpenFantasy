using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Geography;

/// <summary>Composes the existing authorized instant-craft operation with complete terrain ownership.</summary>
public sealed class TerrainMaterialCraftPlanner
{
    private readonly InstantCraftPlanner _crafting = new();

    public TerrainMaterialCraftPlan Plan(TerrainMaterialRegistry registry, TrustedCommandDelivery delivery,
        EntityId actorId, EntityId commandId, CraftRequest request, Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(request);
        ProductionSnapshot production = registry.Production
            ?? throw new InvalidOperationException("The registry requires attached production before crafting.");
        if (production.IsContinued)
        {
            ArgumentNullException.ThrowIfNull(historicallyOccupied);
        }
        TerrainMaterialCraftPlan Reject(WorldActionRejection rejection) => new(registry, registry, rejection, []);
        if (!registry.Inventory.CanControl(delivery, actorId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        bool Occupied(EntityId id) => registry.ContainsIdentity(id) || (historicallyOccupied?.Invoke(id) ?? false);
        if (registry.Allocations.Any(a => a.Deposit.CoarseCustodianId == request.CustodianId
            || a.Deposit.FineCustodianId == request.CustodianId) || Occupied(commandId))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        CraftRecipeDefinition? recipe = production.Recipes.SingleOrDefault(r => r.DefinitionId == request.RecipeId);
        if (recipe is not null && Enumerable.Range(0, recipe.Outputs.Count + 1)
            .Any(index => Occupied(EntityId.DeriveSpawned(commandId, (ulong)index))))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        CraftPlan craft = _crafting.Plan(registry.Inventory, production, delivery, actorId, commandId, request, Occupied);
        if (craft.Rejection != WorldActionRejection.None)
        {
            return Reject(craft.Rejection);
        }
        TerrainMaterialRegistry candidate = registry.ReconcileProduction(craft.CandidateInventory, craft.CandidateProduction);
        return new(registry, candidate, WorldActionRejection.None, craft.CreatedIds);
    }
}
