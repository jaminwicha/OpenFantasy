using System.Text.Json;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class LocalLiquidViewTests
{
    [Fact]
    public void Public_projection_contains_only_actual_occupied_geometry_and_known_terrain()
    {
        WorldSnapshot world = WatercourseFarmAdmission.CreateEmbedded("watercourse-public-view", "Traveler");
        var player = world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        byte[] before = WorldSnapshotCodec.Encode(world);
        LocalLiquidView view = Assert.IsType<LocalLiquidView>(new LocalWorldView(world, player).Liquids);
        Assert.Equal(6, view.Cells.Count);
        Assert.All(view.Cells, cell => Assert.Equal(LocalLiquidView.CellCapacityMicrolitres, cell.VolumeMicrolitres));
        Assert.Equal(Enumerable.Range(110, 6).Select(x => new CellCoordinate(x, 329, 0)),
            view.Cells.Select(cell => cell.Coordinate));
        Assert.Equal(world.Space.Regions.Count, view.Terrain.Count);
        foreach (var region in world.Space.Regions)
        {
            LocalLiquidTerrainView actual = Assert.Single(view.Terrain, value => value.Bounds == region.Bounds);
            Assert.Equal(region.Revision, actual.Revision);
            Assert.Equal(region.Cells.Select(cell => cell.IsSolid), actual.Occupancy);
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.IsSolid(region.Bounds.Minimum.Offset(-1, 0, 0)));
        }
        Assert.Equal(new[] { "Cells", "Terrain" }, typeof(LocalLiquidView).GetProperties().Select(p => p.Name).Order());
        Assert.Equal(new[] { "Coordinate", "VolumeMicrolitres" },
            typeof(LocalLiquidCellView).GetProperties().Select(p => p.Name).Order());
        Assert.Equal(new[] { "Bounds", "Occupancy", "Revision" },
            typeof(LocalLiquidTerrainView).GetProperties().Select(p => p.Name).Order());
        string json = JsonSerializer.Serialize(view);
        foreach (string forbidden in new[] { "ReservoirId", "Custodian", "Origin", "Source", "Start", "End",
            "LotId", "ContentHash", "Inventory", "Reservation", "Portions", "Quantity" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.Ordinal);
        }
        foreach (var identity in world.Reservoirs!.Geometries.SelectMany(geometry => new[] { geometry.ReservoirId, geometry.CustodianId })
            .Concat(world.Inventory.Sources.Select(source => source.SourceId))
            .Concat(world.Inventory.Custody.Lots.Select(lot => lot.LotId)))
        {
            Assert.DoesNotContain(JsonSerializer.Serialize(identity), json, StringComparison.Ordinal);
        }
        Assert.Equal(before, WorldSnapshotCodec.Encode(world));

        var actions = new ConstructionUpgradeFixture();
        WorldSnapshot next = new WorldStepProcessor().Decide(world, actions.Batch(world, [], [])).Candidate;
        LocalLiquidView updated = Assert.IsType<LocalLiquidView>(new LocalWorldView(next, player).Liquids);
        Assert.Equal(7, updated.Cells.Count);
        Assert.Equal(4_000_000UL, Assert.Single(updated.Cells, cell => cell.Coordinate == new CellCoordinate(116, 329, 0)).VolumeMicrolitres);
        Assert.DoesNotContain(updated.Cells, cell => cell.Coordinate.Z == -1);
        Assert.Equal(996_000_000UL, Assert.Single(updated.Cells, cell => cell.Coordinate == new CellCoordinate(115, 329, 0)).VolumeMicrolitres);
        Assert.Equal(6_000_000_000UL, updated.Cells.Aggregate(0UL, (sum, cell) => checked(sum + cell.VolumeMicrolitres)));
        Assert.Equal(6, view.Cells.Count);
    }

    [Fact]
    public void Original_farm_and_equilibrium_only_worlds_do_not_claim_detailed_liquid_placement()
    {
        WorldSnapshot farm = GatheringFarmAdmission.CreateEmbedded("watercourse-legacy", "Traveler");
        var player = farm.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        Assert.Null(new LocalWorldView(farm, player).Liquids);
        var fixture = new LiquidCellWorldFixture();
        WorldSnapshot contained = fixture.ContainedBasis;
        player = contained.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        Assert.Null(new LocalWorldView(contained, player).Liquids);
    }
}
