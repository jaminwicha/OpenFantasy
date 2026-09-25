using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Fresh finite stock and authored enclosure at the exact retained fragment ceiling; no live state is replaced.</summary>
internal sealed class LiquidCellLimitFixture
{
    internal static EntityId Id(ulong value) => new(0x6c697163656c6c6d, value);
    internal static readonly EntityId Reservoir = Id(10), Store = Id(11), Source = Id(12),
        Budget = Id(14), SourceLot = Id(15);
    internal const ulong Units = LiquidCellState.MaximumPortions * 2UL;
    internal ConstructionUpgradeFixture Actions { get; } = new();
    internal WorldSnapshot Genesis { get; }

    internal LiquidCellLimitFixture()
    {
        WorldSnapshot farm = ConstructionFarmAdmission.CreateGenesis(Actions.QuestGenesis("liquid-fragment-limit"));
        EntityId owner = farm.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        var water = new CountedItemDefinition(Id(20), ContentHash.Compute("liquid-cell-limit-water-v1"u8));
        var material = new ItemPhysicalBinding(water, new(new PackageId("openfantasy.liquidcelllimit"),
            Id(21), "water", new(1), new(1), new(0)));
        var inventory = new CountedAuthorityState(new(farm.WorldId, farm.BranchId, 0,
            farm.Inventory.Custody.Storages.Append(new(Store, Units, 0)),
            farm.Inventory.Custody.Lots.Append(new(SourceLot, Store, water, new(Source, 0, Units), 0))),
            checked(farm.Inventory.AdmittedUnitCapacity + Units), farm.Inventory.Actors, farm.Inventory.Controls,
            farm.Inventory.Policies.Append(new(Store, owner, CustodyDepositRule.OwnerOnly)),
            farm.Inventory.Sources.Append(new(Source, water, Units)), farm.Inventory.ReservationIds.Append(Budget));

        TerrainRegionSnapshot retained = farm.Space.Regions[0];
        var region = new StratifiedTerrainGenerator().Generate(farm.Space.Scope, retained.Seed, retained.Rules,
            new(new(-1, -1, -3), 5, 3, 6)).Excavate(0,
                [new(0, 0, -1), new(1, 0, -1), new(2, 0, -1)]);
        var space = new LocalSpatialSnapshot(farm.Space.Scope, farm.Space.Regions.Append(region), farm.Space.Actors);
        var basis = new WorldSnapshot(inventory, space, farm.Residents, farm.AdmissionVersion, farm.Calendar, 0, 0,
            gathering: farm.Gathering, production: farm.Production, progression: farm.Progression,
            quests: farm.Quests, construction: farm.Construction);

        var basin = new WatershedPlanner().Prepare(new(space.Scope, Id(4), new(0, 0, 1, 1),
            new(0, 0), 0, Id(5)), TestContext.Current.CancellationToken);
        var network = new WatershedNetworkPlanner().Prepare(Id(6), [basin],
            [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        var geometry = new ReservoirGeometry(space.Scope, Reservoir, Store, 0,
            [new(0, 0, -1), new(1, 0, -1), new(2, 0, -1)]);
        var request = new WatershedRunoffRequest(network, new(1, 1, 0), inventory, basis.Production,
            Budget, owner, basis.Calendar, basis.Calendar.AddMilliseconds(1), material,
            [WatershedRunoffInput.Dry(basin.BasinId)], [new(basin.BasinId, Store, geometry.Capacity)]);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request,
            TestContext.Current.CancellationToken));
        capacity = capacity.PlanCancel(Budget).ApplyTo(capacity);
        var reservoirs = LiquidReservoirState.Create(capacity, [geometry]);
        WorldSnapshot physical = WorldReservoirAdmission.CreateGenesis(basis, reservoirs);
        var seals = new ReservoirSealPolicy(water, new[]
            { retained.Rules.Materials.Surface, retained.Rules.Materials.Soil, retained.Rules.Materials.Stone }
            .Distinct().Select(item => new ReservoirSealRule(item, 1000)));
        WorldSnapshot contained = WorldReservoirContainmentAdmission.CreateGenesis(physical, seals);
        var portions = Enumerable.Range(0, LiquidCellState.MaximumPortions).Select(index =>
            new LiquidCellPortion(Cell(index % 2 == 0 ? 0 : 2), LiquidStockOrigin.RawSource, Source,
                (ulong)index * 2, (ulong)index * 2 + 2));
        LiquidCellState cells = LiquidCellState.Create(reservoirs, new(1, 1), contained.Calendar, portions);
        Genesis = WorldLiquidCellAdmission.CreateGenesis(contained, cells);
    }

    internal static LiquidCellKey Cell(long x) => new(Reservoir, new(x, 0, -1));
}
