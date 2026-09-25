using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>One authored finite water entitlement, actual custody lot, and exact detailed placement.</summary>
internal sealed class WatercourseFarmWater
{
    private readonly WorldSnapshot _farm;
    private readonly EntityId _owner;
    private readonly EntityId _store;
    private readonly EntityId _source;
    private readonly EntityId _budget;
    private readonly EntityId _reservoir;

    internal WatercourseFarmWater(WorldSnapshot farm)
    {
        _farm = farm;
        _owner = farm.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Host).ActorId;
        _store = Instance("water.custody");
        _source = Instance("water.source");
        _budget = Instance("water.closed-budget");
        _reservoir = Instance("water.reservoir");
        var item = new CountedItemDefinition(Definition("items/water"),
            ContentHash.Compute("watercourse-farm.water.litre.v1"u8));
        Material = new(item, new(new PackageId("openfantasy.watercourse-farm"), Definition("physical/water"),
            "water", new MassMilligrams(1_000_000), new VolumeMicrolitres(WatercourseFarmLayout.MicrolitresPerUnit), new HardnessRating(0)));
    }

    internal ItemPhysicalBinding Material { get; }

    internal CountedAuthorityState CreateInventory()
    {
        CountedAuthorityState old = _farm.Inventory;
        var custody = new CustodySnapshot(_farm.WorldId, _farm.BranchId, 0,
            old.Custody.Storages.Append(new CustodianStorage(_store, WatercourseFarmLayout.InitialWaterUnits, 0)),
            old.Custody.Lots.Append(new CountedSourceLot(Instance("water.lot"), _store, Material.Item,
                new SourceInterval(_source, 0, WatercourseFarmLayout.InitialWaterUnits), 0)));
        return new(custody, checked(old.AdmittedUnitCapacity + WatercourseFarmLayout.InitialWaterUnits),
            old.Actors, old.Controls,
            old.Policies.Append(new CustodianAccessPolicy(_store, _owner, CustodyDepositRule.OwnerOnly)),
            old.Sources.Append(new AdmittedCountedSource(_source, Material.Item, WatercourseFarmLayout.InitialWaterUnits)),
            old.ReservationIds.Append(_budget));
    }

    internal LiquidReservoirState CreateReservoirs(WorldSnapshot basis)
    {
        var basin = new WatershedPlanner().Prepare(new(basis.Space.Scope, Instance("water.height-rule"),
            new(110, 329, 1, 1), new(0, 0), 0, Instance("water.basin")));
        var network = new WatershedNetworkPlanner().Prepare(Instance("water.network"), [basin],
            [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)]);
        var geometry = new ReservoirGeometry(basis.Space.Scope, _reservoir, _store,
            WatercourseFarmLayout.SpillZ, WatercourseFarmLayout.Columns());
        // The retained capacity owner requires a finite watershed domain. This explicitly dry,
        // canceled commitment creates no runoff, pending receipt, replenishment or extra water.
        var request = new WatershedRunoffRequest(network, new(1, 1, 0), basis.Inventory, basis.Production,
            _budget, _owner, basis.Calendar, basis.Calendar.AddMilliseconds(1), Material,
            [WatershedRunoffInput.Dry(basin.BasinId)], [new(basin.BasinId, _store, geometry.Capacity)]);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request));
        capacity = capacity.PlanCancel(_budget).ApplyTo(capacity);
        return LiquidReservoirState.Create(capacity, [geometry]);
    }

    internal LiquidCellState CreatePlacement(LiquidReservoirState reservoirs, WorldInstant boundary)
    {
        var portions = Enumerable.Range(0, 6).Select(index => new LiquidCellPortion(
            new(_reservoir, new(110 + index, 329, 0)), LiquidStockOrigin.RawSource, _source,
            (ulong)index * 1000, (ulong)(index + 1) * 1000));
        return LiquidCellState.Create(reservoirs,
            new(WatercourseFarmLayout.DownwardUnitsPerStep, WatercourseFarmLayout.LateralUnitsPerStep), boundary, portions);
    }

    private EntityId Instance(string role) => ConstructionFarmIdentityScope.InstanceId(_farm, "watercourse-farm", role);
    private static EntityId Definition(string key) => ConstructionFarmIdentityScope.DefinitionId("watercourse-farm", key);
}
