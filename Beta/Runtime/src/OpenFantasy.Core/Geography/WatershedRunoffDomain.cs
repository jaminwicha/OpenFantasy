using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Fixed drainage, calibration and receiver contract shared by outstanding forcing commitments.</summary>
public sealed class WatershedRunoffDomain
{
    private readonly Lazy<ContentHash> _contentHash;

    public WatershedRunoffDomain(PreparedWatershedNetwork network, WatershedTerrainMapping mapping,
        EntityId ownerActorId, ItemPhysicalBinding material, IEnumerable<WatershedRunoffTerminal> terminals)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(terminals);
        var copy = terminals.Take(PreparedWatershedNetwork.MaximumBasins + 1).ToArray();
        if (copy.Any(t => t is null) || copy.Length != network.Terminals.Count
            || copy.Select(t => t.BasinId).Distinct().Count() != copy.Length
            || copy.Any(t => !network.Terminals.Any(n => n.BasinId == t.BasinId)))
        {
            throw new ArgumentException("The runoff domain requires every terminal exactly once.");
        }
        foreach (var basin in network.Basins)
        {
            mapping.ValidateFootprint(basin.Request.Bounds);
        }
        foreach (var group in copy.GroupBy(t => t.CustodianId))
        {
            if (group.Any(t => t.Capacity != group.First().Capacity))
            {
                throw new ArgumentException("A receiver has one fixed physical capacity witness.");
            }
        }
        Network = network;
        Mapping = mapping;
        OwnerActorId = ownerActorId;
        Material = material;
        Terminals = copy.OrderBy(t => t.BasinId).ToList().AsReadOnly();
        _contentHash = new(() => ContentHash.Compute(WatershedRunoffCapacityCodecV1.EncodeDomain(this)));
    }

    public PreparedWatershedNetwork Network { get; }
    public WatershedTerrainMapping Mapping { get; }
    public EntityId OwnerActorId { get; }
    public ItemPhysicalBinding Material { get; }
    public ReadOnlyCollection<WatershedRunoffTerminal> Terminals { get; }
    public ContentHash ContentHash => _contentHash.Value;

    internal static WatershedRunoffDomain From(WatershedRunoffRequest request) =>
        new(request.Network, request.Mapping, request.OwnerActorId, request.Material, request.Terminals);

    internal void ValidateAuthority(CountedAuthorityState inventory, ProductionSnapshot? production)
    {
        var network = Network;
        if (inventory.WorldId != network.Scope.WorldId || inventory.BranchId != network.Scope.BranchId
            || !inventory.Actors.Contains(OwnerActorId)
            || inventory.ContainsIdentityMatching(network.MaterialIdentities.Contains)
            || inventory.ContainsIdentity(Material.Physical.DefinitionId)
            || network.MaterialIdentities.Contains(Material.Physical.DefinitionId)
            || network.MaterialIdentities.Contains(Material.Item.DefinitionId))
        {
            throw new ArgumentException("Runoff scope, ownership, reservation and geographical/physical roles must be compatible.");
        }
        var pins = inventory.Sources.Where(s => s.Definition.DefinitionId == Material.Item.DefinitionId).ToArray();
        if (pins.Any(s => s.Definition != Material.Item)
            || pins.Length == 0 && inventory.ContainsIdentity(Material.Item.DefinitionId))
        {
            throw new ArgumentException("Runoff calibration cannot change an admitted item pin or alias a different role.");
        }
        if (inventory.Custody.Storages.Any(s => s.Revision > inventory.Custody.Revision)
            || inventory.Custody.Lots.Any(l => l.Revision > inventory.Custody.Revision))
        {
            throw new ArgumentException("Custody entities cannot exceed their complete root revision.");
        }
        if (production is not null)
        {
            production.ValidateForAuthority(inventory);
            production.ValidateMaterialBinding(Material);
            if (production.ContainsIdentityMatching(network.MaterialIdentities.Contains)
                || production.Revision > inventory.Custody.Revision
                || production.Lots.Any(l => l.Revision > inventory.Custody.Revision)
                || ProductionRetirementHistory.Retirements(production).Any(r => r.Ingredient.Revision >= inventory.Custody.Revision))
            {
                throw new ArgumentException("Production roles and revisions must agree with the runoff custody basis.");
            }
        }
    }

    internal void ValidateReceivers(CountedAuthorityState inventory, ProductionSnapshot? production)
    {
        foreach (var terminal in Terminals)
        {
            if (!inventory.Policies.Any(p => p.CustodianId == terminal.CustodianId && p.OwnerActorId == OwnerActorId)
                || inventory.Custody.Lots.Any(l => l.CustodianId == terminal.CustodianId && l.Definition != Material.Item)
                || (production?.Lots.Any(l => l.CustodianId == terminal.CustodianId && l.Definition != Material.Item) ?? false))
            {
                throw new ArgumentException("Each receiver requires the same owner and exact calibrated raw/derived contents.");
            }
        }
    }
}
