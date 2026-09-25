using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Authors initial geography and finite stock while retaining all ordinary farm services and work.</summary>
internal sealed class WatercourseFarmBuilder(WorldSnapshot farm)
{
    internal WorldSnapshot Build()
    {
        var water = new WatercourseFarmWater(farm);
        CountedAuthorityState inventory = water.CreateInventory();
        LocalSpatialSnapshot space = CreateSpace();
        WorldSnapshot authored = Root(inventory, space, farm.AdmissionVersion);
        // The channel, source entitlement and every inherited component belong to this fresh content pin.
        ContentHash admission = ContentHash.Compute(Encoding.UTF8.GetBytes("watercourse-farm.admission.v1")
            .Concat(farm.AdmissionVersion.ToByteArray()).Concat(authored.ContentHash.ToByteArray()).ToArray());
        WorldSnapshot basis = Root(inventory, space, admission);
        LiquidReservoirState reservoirs = water.CreateReservoirs(basis);
        WorldSnapshot reservoirWorld = WorldReservoirAdmission.CreateGenesis(basis, reservoirs);
        TerrainMaterialPalette palette = space.Regions[0].Rules.Materials;
        var seals = new ReservoirSealPolicy(water.Material.Item,
            new[] { palette.Surface, palette.Soil, palette.Stone }.Distinct()
                .Select(material => new ReservoirSealRule(material, 1000)));
        WorldSnapshot contained = WorldReservoirContainmentAdmission.CreateGenesis(reservoirWorld, seals);
        return WorldLiquidCellAdmission.CreateGenesis(contained, water.CreatePlacement(reservoirs, contained.Calendar));
    }

    private LocalSpatialSnapshot CreateSpace()
    {
        TerrainRegionSnapshot retained = farm.Space.Regions.Single();
        CellCoordinate[] holes = WatercourseFarmLayout.InteriorCells();
        TerrainRegionSnapshot upper = retained.Excavate(retained.Revision,
            holes.Where(retained.Bounds.Contains));
        // The original region starts at Z0. Its same generated substrate supplies the lower pool's
        // floor and walls; regions do not overlap and the channel has no unknown boundary cells.
        TerrainRegionSnapshot lower = new StratifiedTerrainGenerator().Generate(farm.Space.Scope,
            retained.Seed, retained.Rules, new(new(109, 328, -2), 12, 4, 2));
        lower = lower.Excavate(lower.Revision, holes.Where(lower.Bounds.Contains));
        return new LocalSpatialSnapshot(farm.Space.Scope, [upper, lower], farm.Space.Actors);
    }

    private WorldSnapshot Root(CountedAuthorityState inventory, LocalSpatialSnapshot space, ContentHash admission) =>
        new(inventory, space, farm.Residents, admission, farm.Calendar, 0, 0,
            gathering: farm.Gathering, production: farm.Production, progression: farm.Progression,
            quests: farm.Quests, construction: farm.Construction);
}
