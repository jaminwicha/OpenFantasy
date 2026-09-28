using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Shared validated production evidence. Only direct raw retirement covers allocated source units.</summary>
internal sealed class TerrainMaterialProduction
{
    private readonly ILookup<EntityId, DirectRawRetirementRange> _retirements;
    private readonly DirectRawRetirementRange[] _rawRetirements;

    internal TerrainMaterialProduction(ProductionSnapshot snapshot, CountedAuthorityState inventory)
    {
        if (snapshot.IsContinued && snapshot.Continuation?.SupportsTerrainRepresentation != true)
        {
            throw new ArgumentException("Terrain material admission requires complete inline production history. Continued terrain instead requires its actual qualified registry.", nameof(snapshot));
        }
        snapshot.ValidateForAuthority(inventory);
        if (snapshot.Revision > inventory.Custody.Revision
            || snapshot.Lots.Any(l => l.Revision > inventory.Custody.Revision)
            || ProductionRetirementHistory.CurrentRetirements(snapshot).Any(r => r.Ingredient.Revision >= inventory.Custody.Revision))
        {
            throw new ArgumentException("Production and retired lot revisions require an advancing complete custody root.");
        }
        Snapshot = snapshot;
        _rawRetirements = (snapshot.Continuation?.Basis.RawRetirements.AsEnumerable() ?? Enumerable.Empty<DirectRawRetirementRange>()).Concat(
            ProductionRetirementHistory.CurrentRetirements(snapshot).Where(value => value.Ingredient.Source is not null)
                .Select(value => new DirectRawRetirementRange(value.CustodianId, value.Ingredient.Definition, value.Ingredient.Source!))).ToArray();
        _retirements = _rawRetirements.OrderBy(value => value.Source.Start).ToLookup(value => value.Source.SourceId);
        if (snapshot.MaterialHistory?.TerrainRepresentation is { } boundary)
        {
            ValidateRepresentationCustodians(boundary.Allocations);
            if (snapshot.ContainsIdentityMatching(id => boundary.Terrain.Network.MaterialIdentities.Contains(id)
                || boundary.Allocations.Any(value => value.AllocationId == id)))
                throw new ArgumentException("Terrain allocation and geographical identities cannot alias material roles.");
        }
    }

    internal ProductionSnapshot Snapshot { get; }
    internal IEnumerable<SourceInterval> Retirements(EntityId sourceId) => _retirements[sourceId].Select(value => value.Source);

    internal void ValidateDeposit(TerrainMaterialDeposit deposit)
    {
        Snapshot.ValidateMaterialBinding(deposit.Material);
        if (Snapshot.ContainsIdentityMatching(deposit.Terrain.Network.MaterialIdentities.Contains))
        {
            throw new ArgumentException("Production cannot alias geographical identities.");
        }
        foreach (var retirement in _rawRetirements)
        {
            if (retirement.CustodianId != deposit.CoarseCustodianId && retirement.CustodianId != deposit.FineCustodianId)
            {
                continue;
            }
            if (retirement.Source is { } source && deposit.Intersect(source) is not null)
            {
                throw new ArgumentException("Allocated terrain must leave representation custody before production consumes it.");
            }
        }
    }

    internal void ValidateRepresentationCustodians(IEnumerable<TerrainMaterialAllocation> allocations)
    {
        var values = allocations.ToArray();
        var custodians = values.SelectMany(value => new[] { value.Deposit.CoarseCustodianId, value.Deposit.FineCustodianId }).ToHashSet();
        if (_rawRetirements.Any(retirement => custodians.Contains(retirement.CustodianId)
            && values.Any(value => value.Deposit.Intersect(retirement.Source) is not null)))
            throw new ArgumentException("Allocated terrain cannot be retired in any representation custodian, including imported ranges.");
    }

    internal void ValidateSuccessor(ProductionSnapshot next) => ProductionRetirementHistory.ValidateSuccessor(Snapshot, next);
}
