using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>Complete bounded production history and live derived contents for one world/branch.</summary>
public sealed class ProductionSnapshot
{
    public const int MaximumRecipes = 32;
    public const int MaximumBatches = 256;
    public const int MaximumLots = CustodySnapshot.MaximumLots;
    private readonly ProductionIdentityRegistry _identities;

    public ProductionSnapshot(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<CraftRecipeDefinition> recipes, IEnumerable<ProductionBatch> batches, IEnumerable<TransformedLot> lots)
    {
        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Recipes = ProductionCollections.Copy(recipes, MaximumRecipes, value => value.DefinitionId);
        Batches = ProductionCollections.Copy(batches, MaximumBatches, value => value.BatchId);
        Lots = ProductionCollections.Copy(lots, MaximumLots, value => value.LotId);
        _identities = new ProductionIdentityRegistry(this);
        ProductionClaimValidation.Validate(this);
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<CraftRecipeDefinition> Recipes { get; }
    public ReadOnlyCollection<ProductionBatch> Batches { get; }
    public ReadOnlyCollection<TransformedLot> Lots { get; }
    public ContentHash ContentHash => ContentHash.Compute(ProductionCodecV1.Encode(this));
    public bool ContainsIdentity(EntityId identity) => _identities.Contains(identity);

    public CraftRecipeDefinition GetRecipe(EntityId recipeId) => Recipes.FirstOrDefault(value => value.DefinitionId == recipeId)
        ?? throw new ArgumentException("The crafting recipe is outside this admitted production profile.", nameof(recipeId));

    public ulong GetStoredQuantity(EntityId custodianId)
    {
        ulong quantity = 0;
        foreach (TransformedLot lot in Lots.Where(value => value.CustodianId == custodianId))
        {
            quantity = checked(quantity + lot.Quantity);
        }
        return quantity;
    }

    public ulong GetCombinedStoredQuantity(CountedAuthorityState inventory, EntityId custodianId) =>
        checked(inventory.Custody.GetStoredQuantity(custodianId) + GetStoredQuantity(custodianId));

    public void ValidateForAuthority(CountedAuthorityState inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.WorldId != WorldId || inventory.BranchId != BranchId
            || inventory.Custody.Lots.Count + Lots.Count > MaximumLots)
        {
            throw new ArgumentException("Production must share custody scope and the combined live-lot admission bound.");
        }
        _identities.ValidateAuthority(inventory);
        ProductionClaimValidation.ValidateSourceClaims(this, inventory);
        foreach (ProductionBatch batch in Batches)
        {
            if (!inventory.Actors.Contains(batch.ActorId)
                || !inventory.Policies.Any(policy => policy.CustodianId == batch.CustodianId))
            {
                throw new ArgumentException("Production history requires retained actor and custodian participants.");
            }
        }
        foreach (TransformedLot lot in Lots)
        {
            if (!inventory.Policies.Any(policy => policy.CustodianId == lot.CustodianId)
                || lot.ReservedBy is { } reservation && !inventory.ReservationIds.Contains(reservation))
            {
                throw new ArgumentException("Derived contents require an admitted custodian and explicit reservations.");
            }
        }
        foreach (CustodianStorage storage in inventory.Custody.Storages)
        {
            if ((UInt128)inventory.Custody.GetStoredQuantity(storage.CustodianId) + GetStoredQuantity(storage.CustodianId)
                > storage.UnitCapacity)
            {
                throw new ArgumentException("Combined raw and transformed contents exceed counted storage capacity.");
            }
        }
    }
}
