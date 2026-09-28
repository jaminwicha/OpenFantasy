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
        if (snapshot.IsContinued)
        {
            throw new ArgumentException("Terrain material admission requires complete inline production history.", nameof(snapshot));
        }
        snapshot.ValidateForAuthority(inventory);
        if (snapshot.Revision > inventory.Custody.Revision
            || snapshot.Lots.Any(l => l.Revision > inventory.Custody.Revision)
            || ProductionRetirementHistory.Retirements(snapshot).Any(r => r.Ingredient.Revision >= inventory.Custody.Revision))
        {
            throw new ArgumentException("Production and retired lot revisions require an advancing complete custody root.");
        }
        Snapshot = snapshot;
        _retirements = ProductionRetirementHistory.Retirements(snapshot).Select(r => r.Ingredient).Where(i => i.Source is not null)
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
        foreach (var retirement in ProductionRetirementHistory.Retirements(Snapshot))
        {
            if (retirement.CustodianId != deposit.CoarseCustodianId && retirement.CustodianId != deposit.FineCustodianId)
            {
                continue;
            }
            if (retirement.Ingredient.Source is { } source && deposit.Intersect(source) is not null)
            {
                throw new ArgumentException("Allocated terrain must leave representation custody before production consumes it.");
            }
        }
    }

    internal void ValidateSuccessor(ProductionSnapshot next) => ProductionRetirementHistory.ValidateSuccessor(Snapshot, next);
}
