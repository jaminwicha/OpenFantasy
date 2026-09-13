namespace OpenFantasy.Core.Geography;

/// <summary>Stable registry-owned allocation identity and its immutable funded slab definition.</summary>
public sealed record TerrainMaterialAllocation
{
    public TerrainMaterialAllocation(EntityId allocationId, TerrainMaterialDeposit deposit)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        AllocationId = allocationId;
        Deposit = deposit;
    }

    public EntityId AllocationId { get; }
    public TerrainMaterialDeposit Deposit { get; }
}
