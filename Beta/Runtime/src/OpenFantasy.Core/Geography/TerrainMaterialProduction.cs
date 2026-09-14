using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Shared validated production evidence. Only direct raw retirement covers allocated source units.</summary>
internal sealed class TerrainMaterialProduction
{
    private readonly ILookup<EntityId, SourceInterval> _retirements;

    internal TerrainMaterialProduction(ProductionSnapshot snapshot, CountedAuthorityState inventory)
    {
        snapshot.ValidateForAuthority(inventory);
        if (snapshot.Revision > inventory.Custody.Revision
            || snapshot.Lots.Any(l => l.Revision > inventory.Custody.Revision)
            || snapshot.Batches.SelectMany(b => b.Ingredients).Any(i => i.Revision >= inventory.Custody.Revision))
        {
            throw new ArgumentException("Production and retired lot revisions require an advancing complete custody root.");
        }
        Snapshot = snapshot;
        _retirements = snapshot.Batches.SelectMany(b => b.Ingredients).Where(i => i.Source is not null)
            .Select(i => i.Source!).OrderBy(s => s.Start).ToLookup(s => s.SourceId);
    }

    internal ProductionSnapshot Snapshot { get; }
    internal IEnumerable<SourceInterval> Retirements(EntityId sourceId) => _retirements[sourceId];

    internal void ValidateDeposit(TerrainMaterialDeposit deposit)
    {
        Snapshot.ValidateMaterialBinding(deposit.Material);
        if (Snapshot.ContainsIdentityMatching(deposit.Terrain.Network.MaterialIdentities.Contains))
        {
            throw new ArgumentException("Production cannot alias geographical identities.");
        }
        foreach (ProductionBatch batch in Snapshot.Batches)
        {
            if (batch.CustodianId != deposit.CoarseCustodianId && batch.CustodianId != deposit.FineCustodianId)
            {
                continue;
            }
            if (batch.Ingredients.Any(i => i.Source is { } source && deposit.Intersect(source) is not null))
            {
                throw new ArgumentException("Allocated terrain must leave representation custody before production consumes it.");
            }
        }
    }

    internal void ValidateSuccessor(ProductionSnapshot next)
    {
        if (Snapshot.ContentHash == next.ContentHash)
        {
            return;
        }
        if (next.Revision <= Snapshot.Revision || next.WorldId != Snapshot.WorldId || next.BranchId != Snapshot.BranchId
            || next.Recipes.Count != Snapshot.Recipes.Count
            || Snapshot.Recipes.Any(recipe => !next.Recipes.Any(r => r.DefinitionId == recipe.DefinitionId && r.Version == recipe.Version)))
        {
            throw new ArgumentException("Production reconciliation requires advancing state and unchanged recipe admission.");
        }
        Dictionary<EntityId, ProductionBatch> batches = next.Batches.ToDictionary(b => b.BatchId);
        foreach (ProductionBatch prior in Snapshot.Batches)
        {
            if (!batches.TryGetValue(prior.BatchId, out var retained)
                || !ProductionCodecV1.EncodeBatch(prior).AsSpan().SequenceEqual(ProductionCodecV1.EncodeBatch(retained)))
            {
                throw new ArgumentException("Production reconciliation cannot remove or rewrite retained consumption history.");
            }
        }
    }
}
