using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

public sealed record ReservoirStorageProjection(ReservoirGeometry Geometry, ulong ExistingUnits,
    ulong ReservedIncomingUnits, ReservoirFill ActualFill, ReservoirFill ProjectedFill);

/// <summary>Physical storage composition around one runoff capacity owner; every publication preserves all geometry and stock constraints.</summary>
public sealed class LiquidReservoirState
{
    public const int MaximumReservoirs = 256;
    public const int MaximumTotalColumns = 4096;
    private readonly Lazy<ContentHash> _contentHash;

    private LiquidReservoirState(WatershedRunoffCapacityState capacity, IEnumerable<ReservoirGeometry> geometries)
    {
        ArgumentNullException.ThrowIfNull(capacity);
        ArgumentNullException.ThrowIfNull(geometries);
        Capacity = capacity;
        var copy = geometries.Take(MaximumReservoirs + 1).ToArray();
        if (copy.Any(g => g is null) || copy.Length is 0 or > MaximumReservoirs
            || copy.Sum(g => g.Columns.Count) > MaximumTotalColumns
            || copy.Select(g => g.ReservoirId).Distinct().Count() != copy.Length
            || copy.Select(g => g.CustodianId).Distinct().Count() != copy.Length)
        {
            throw new ArgumentException("Reservoir ownership and total geometric support must be unique and bounded.");
        }
        Geometries = copy.OrderBy(g => g.ReservoirId).ToList().AsReadOnly();
        ValidateRolesAndSupport();
        Projections = Geometries.Select(Project).ToList().AsReadOnly();
        _contentHash = new(() => ContentHash.Compute(Capacity.Production?.SupportsConsumption == true
            ? LiquidReservoirCodecV2.Encode(this) : LiquidReservoirCodecV1.Encode(this)));
    }

    public WatershedRunoffCapacityState Capacity { get; }
    public ReadOnlyCollection<ReservoirGeometry> Geometries { get; }
    public ReadOnlyCollection<ReservoirStorageProjection> Projections { get; }
    public ContentHash ContentHash => _contentHash.Value;

    public static LiquidReservoirState Create(WatershedRunoffCapacityState capacity, IEnumerable<ReservoirGeometry> geometries) =>
        new(capacity, geometries);

    public LiquidReservoirPlan PlanApply(WatershedRunoffCapacityPlan capacityPlan)
    {
        ArgumentNullException.ThrowIfNull(capacityPlan);
        var next = capacityPlan.ApplyTo(Capacity);
        return new(this, new(next, Geometries));
    }

    private void ValidateRolesAndSupport()
    {
        var inventory = Capacity.Inventory;
        var domain = Capacity.Domain;
        foreach (var geometry in Geometries)
        {
            if (geometry.Scope != domain.Network.Scope || inventory.ContainsIdentity(geometry.ReservoirId)
                || domain.Network.MaterialIdentities.Contains(geometry.ReservoirId)
                || geometry.ReservoirId == domain.Material.Physical.DefinitionId || geometry.ReservoirId == domain.Material.Item.DefinitionId
                || (Capacity.Production?.ContainsIdentity(geometry.ReservoirId) ?? false)
                || !inventory.Policies.Any(p => p.CustodianId == geometry.CustodianId && p.OwnerActorId == domain.OwnerActorId))
            {
                throw new ArgumentException("Reservoir scope, identity roles and custody owner must agree with the complete authority.");
            }
        }
        var byCustodian = Geometries.ToDictionary(g => g.CustodianId);
        var sources = Capacity.Pending.SelectMany(p => p.Request.Inputs).Where(i => i.CustodianId is not null)
            .Select(i => i.CustodianId!.Value).Distinct();
        if (sources.Any(id => !byCustodian.ContainsKey(id)))
        {
            throw new ArgumentException("Every pending forcing source requires finite physical storage.");
        }
        foreach (var receiver in Capacity.Receivers)
        {
            if (!byCustodian.TryGetValue(receiver.CustodianId, out var geometry) || receiver.Capacity != geometry.Capacity)
            {
                throw new ArgumentException("Every receiving capacity witness must equal its physical reservoir capacity.");
            }
        }
        var support = Geometries.SelectMany(g => g.Columns.Select(c => (Column: c, g.SpillZ)))
            .GroupBy(value => (value.Column.X, value.Column.Y));
        foreach (var vertical in support)
        {
            long? previousTop = null;
            foreach (var interval in vertical.OrderBy(v => v.Column.BottomZ))
            {
                if (previousTop is { } top && interval.Column.BottomZ < top)
                {
                    throw new ArgumentException("Two reservoirs cannot own overlapping physical support.");
                }
                previousTop = interval.SpillZ;
            }
        }
    }

    private ReservoirStorageProjection Project(ReservoirGeometry geometry)
    {
        var inventory = Capacity.Inventory;
        var material = Capacity.Domain.Material;
        if (inventory.Custody.Lots.Any(l => l.CustodianId == geometry.CustodianId && l.Definition != material.Item)
            || (Capacity.Production?.Lots.Any(l => l.CustodianId == geometry.CustodianId && l.Definition != material.Item) ?? false))
        {
            throw new ArgumentException("Every physical reservoir requires one exactly calibrated stored material.");
        }
        ulong existing = Capacity.Production?.GetCombinedStoredQuantity(inventory, geometry.CustodianId)
            ?? inventory.Custody.GetStoredQuantity(geometry.CustodianId);
        ulong incoming = Capacity.Receivers.SingleOrDefault(r => r.CustodianId == geometry.CustodianId)?.IncomingUnits ?? 0;
        var actual = geometry.Fill(material.Physical.VolumePerUnit.Multiply(existing));
        var projected = geometry.Fill(material.Physical.VolumePerUnit.Multiply(checked(existing + incoming)));
        return new(geometry, existing, incoming, actual, projected);
    }
}
