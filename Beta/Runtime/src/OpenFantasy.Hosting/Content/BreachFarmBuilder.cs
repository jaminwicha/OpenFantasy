using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Retains the ordinary farm while admitting a finite, initially solid extraction permission.</summary>
internal sealed class BreachFarmBuilder(WorldSnapshot farm)
{
    internal WorldSnapshot Build()
    {
        LocalSpatialSnapshot space = CreateSpace();
        ExcavationSite site = CreateExcavation(space);
        var water = new BreachFarmWater(farm);
        CountedAuthorityState stock = water.CreateInventory();
        var inventory = new CountedAuthorityState(stock.Custody, checked(stock.AdmittedUnitCapacity + site.Quantity),
            stock.Actors, stock.Controls, stock.Policies,
            stock.Sources.Append(new(site.CountedSourceId, site.Material.Item, site.Quantity)), stock.ReservationIds);
        WorldSnapshot authored = Root(inventory, space, farm.AdmissionVersion);
        ContentHash admission = ContentHash.Compute(Encoding.UTF8.GetBytes("breach-farm.admission.v1")
            .Concat(farm.AdmissionVersion.ToByteArray()).Concat(authored.ContentHash.ToByteArray()).ToArray());
        WorldSnapshot basis = Root(inventory, space, admission);
        LiquidReservoirState reservoirs = water.CreateReservoirs(basis);
        TerrainMaterialPalette palette = space.Regions[0].Rules.Materials;
        var seals = new ReservoirSealPolicy(water.Material.Item,
            new[] { palette.Surface, palette.Soil, palette.Stone }.Distinct()
                .Select(material => new ReservoirSealRule(material, 1000)));
        var topology = TerrainLiquidTopology.Create(reservoirs, space.Regions, seals);
        LiquidCellState cells = water.CreatePlacement(reservoirs, basis.Calendar, topology);
        return WorldTerrainLiquidAdmission.CreateGenesis(basis, reservoirs, seals, new([site]), cells);
    }

    private LocalSpatialSnapshot CreateSpace()
    {
        TerrainRegionSnapshot retained = farm.Space.Regions.Single();
        CellCoordinate[] holes = BreachFarmLayout.InitialHoles();
        TerrainRegionSnapshot upper = retained.Excavate(retained.Revision, holes.Where(retained.Bounds.Contains));
        // The same generator, seed and rules supply the receiver floor and walls below the
        // original region. All holes are initial authored geography; the divider remains solid.
        TerrainRegionSnapshot lower = new StratifiedTerrainGenerator().Generate(farm.Space.Scope,
            retained.Seed, retained.Rules, new(new(109, 328, -2), 12, 4, 2));
        lower = lower.Excavate(lower.Revision, holes.Where(lower.Bounds.Contains));
        return new LocalSpatialSnapshot(farm.Space.Scope, [upper, lower], farm.Space.Actors);
    }

    private ExcavationSite CreateExcavation(LocalSpatialSnapshot space)
    {
        TerrainCell cell = space.Regions.Single(region => region.Bounds.Contains(BreachFarmLayout.DividerCell))
            .GetCell(BreachFarmLayout.DividerCell);
        WorldResident player = farm.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player);
        var tool = farm.Gathering!.Tools.Single(value => value.Kind == GatherToolKind.Axe).Definition;
        if (!farm.Inventory.Custody.Lots.Any(lot => lot.CustodianId == player.InventoryCustodianId
            && lot.Definition == tool && lot.ReservedBy is null))
        {
            throw new ArgumentException("The breach worker must retain the actual admitted starter tool.");
        }
        // Extraction yields this original substrate item, not a newly invented ore conversion.
        // The calibration gives its complete one-cell volume and mass; no stock is issued here.
        var physical = new MaterialDefinition(new PackageId("openfantasy.breach-farm"), Definition("physical/substrate"),
            "breach_substrate", new MassMilligrams(2_500_000_000), new VolumeMicrolitres(ExcavationSite.CellVolumeMicrolitres),
            new HardnessRating(4));
        return new(Instance("excavation.site"), BreachFarmLayout.DividerCell, cell.SourceId!.Value,
            Instance("excavation.source"), player.ActorId, new ItemPhysicalBinding(cell.Material!, physical), tool,
            BreachFarmLayout.RequiredExcavationWork, 0, BreachFarmLayout.MiningStance, ExcavationReachMode.RaisedBankTop);
    }

    private WorldSnapshot Root(CountedAuthorityState inventory, LocalSpatialSnapshot space, ContentHash admission) =>
        new(inventory, space, farm.Residents, admission, farm.Calendar, 0, 0,
            gathering: farm.Gathering, production: farm.Production, progression: farm.Progression,
            quests: farm.Quests, construction: farm.Construction);

    private EntityId Instance(string role) => ConstructionFarmIdentityScope.InstanceId(farm, "breach-farm", role);
    private static EntityId Definition(string key) => ConstructionFarmIdentityScope.DefinitionId("breach-farm", key);
}
