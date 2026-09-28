using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>One standalone material candidate. Optional source state is supplied only when that owner participates.</summary>
public sealed class MaterialOperationPlan
{
    internal MaterialOperationPlan(CountedAuthorityState inventory, ProductionSnapshot production,
        WorldActionRejection rejection, GatherIssueEvidence? evidence = null, GatheringSnapshot? gathering = null)
    {
        CandidateInventory = inventory;
        CandidateProduction = production;
        Rejection = rejection;
        GatherEvidence = evidence;
        CandidateGathering = gathering;
    }
    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public GatheringSnapshot? CandidateGathering { get; }
    public WorldActionRejection Rejection { get; }
    public GatherIssueEvidence? GatherEvidence { get; }
}

internal static class MaterialContinuationCommit
{
    internal static void Require(CountedAuthorityState inventory, ProductionSnapshot production, Func<EntityId, bool> historical)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(historical);
        if (production.MaterialHistory is null) throw new ArgumentException("The operation requires explicit ordered material continuation.");
        production.ValidateForAuthority(inventory);
    }

    internal static bool Occupied(CountedAuthorityState inventory, ProductionSnapshot production,
        Func<EntityId, bool> historical, EntityId identity) =>
        inventory.ContainsIdentity(identity) || production.ContainsIdentity(identity) || historical(identity);

    internal static MaterialOperationPlan Prepare(CountedAuthorityState inventory, ProductionSnapshot production,
        MaterialOperation operation, GatherIssueEvidence? evidence = null, GatheringSnapshot? gathering = null)
    {
        var next = new ProductionSnapshot(production.WorldId, production.BranchId, checked(production.Revision + 1),
            production.Recipes, production.Batches, production.Lots, production.SupportsConsumption ? production.ConsumedItems : null,
            production.Continuation, production.MaterialHistory!.Append(production.Continuation!, operation));
        next.ValidateForAuthority(inventory);
        _ = next.ContentHash;
        return new(inventory, next, WorldActionRejection.None, evidence, gathering);
    }

    internal static bool HasCapacity(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        if (inventory.Custody.Lots.Count + production.Lots.Count > ProductionSnapshot.MaximumLots) return false;
        return inventory.Custody.Storages.All(storage => (UInt128)inventory.Custody.GetStoredQuantity(storage.CustodianId)
            + production.GetStoredQuantity(storage.CustodianId) <= storage.UnitCapacity);
    }
}
