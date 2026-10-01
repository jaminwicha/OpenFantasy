using OpenFantasy.Client.Cargo;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Hosting.Tests.Client.Cargo;

/// <summary>
/// The one cargo projection: the farm keeps its embedding, the inverse is exact, the observation is local and every
/// position outside it or off its floor is refused rather than drawn somewhere plausible.
/// </summary>
public sealed class CargoViewProjectionTests
{
    [Fact]
    public void Farm_cells_keep_the_farm_embedding_and_pointer_mapping_is_the_exact_inverse()
    {
        WorldSnapshot genesis = CargoTestWorld.Genesis;
        EntityId player = CargoTestWorld.Player(genesis);
        LocalRegionObservation observation = LocalRegionObserver.Project(genesis, player)!;
        var projection = new CargoViewProjection(observation);
        SubunitPosition feet = genesis.Space.GetActor(player).Feet;
        Assert.Equal(new CargoCanvasPoint((feet.X - 96L * 256) / 8.0, (feet.Y - 320L * 256) / 8.0), projection.Project(feet));
        // The farm origin cell's centre lands where the fixed farm grid has always drawn it.
        var origin = new SubunitPosition(96L * 256 + 128, 320L * 256 + 128, observation.FloorFeetZ);
        Assert.Equal(new CargoCanvasPoint(16, 16), projection.Project(origin));
        Assert.Equal(origin, projection.Unproject(new CargoCanvasPoint(16, 16)));
        Assert.Equal(feet, projection.Unproject(projection.Project(feet)));
        Assert.Equal(origin, projection.Unproject(new CargoCanvasPoint(0.5, 31.5)));
        Assert.Equal(0, projection.Bounds.X % CargoViewProjection.PixelsPerCell);
    }

    [Fact]
    public void The_observation_is_local_and_unsupported_or_distant_positions_fail_closed()
    {
        WorldSnapshot genesis = CargoTestWorld.Genesis;
        EntityId player = CargoTestWorld.Player(genesis);
        CargoCorridor corridor = NativeCargoWorld.Corridor(genesis);
        LocalRegionObservation observation = LocalRegionObserver.Project(genesis, player)!;
        var projection = new CargoViewProjection(observation);
        SubunitPosition feet = genesis.Space.GetActor(player).Feet;
        // Only regions near the player are observed, never the whole stored corridor.
        Assert.InRange(observation.Regions.Count, 1, 4);
        Assert.True(corridor.AllRegions.Count() > observation.Regions.Count);
        Assert.NotNull(observation.RegionAt(feet.Cell.X, feet.Cell.Y));
        SubunitPosition gearford = CargoCorridor.FeetAt(corridor.DestinationApproach);
        Assert.False(projection.Admits(gearford));
        Assert.Throws<ArgumentOutOfRangeException>(() => projection.Project(gearford));
        Assert.Null(projection.Unproject(new CargoCanvasPoint(1_000_000, 1_000_000)));
        // Another floor is unsupported even inside an observed region.
        SubunitPosition above = feet.Offset(0, 0, SubunitPosition.SubunitsPerCell);
        Assert.False(projection.Admits(above));
        Assert.Throws<ArgumentOutOfRangeException>(() => projection.Project(above));
        // No observation exists for an actor that is not present.
        Assert.Null(LocalRegionObserver.Project(genesis, CargoTestWorld.NewId()));
        Assert.Throws<ArgumentException>(() => new CargoViewProjection(new LocalRegionObservation(player, feet, [])));
    }

    [Fact]
    public void Moving_the_observed_window_never_moves_an_authority_position_on_the_canvas()
    {
        WorldSnapshot genesis = CargoTestWorld.Genesis;
        CargoCircuitMarkets markets = CargoTestWorld.Markets(genesis);
        EntityId player = CargoTestWorld.Player(genesis);
        LocalRegionObservation farm = LocalRegionObserver.Project(genesis, player)!;
        var atFarm = new CargoViewProjection(farm);
        var atKegga = new CargoViewProjection(LocalRegionObserver.Project(genesis, markets.Destination.MerchantActorId)!);
        SubunitPosition kegga = genesis.Space.GetActor(markets.Destination.MerchantActorId).Feet;
        Assert.NotEqual(atFarm.Bounds, atKegga.Bounds);
        Assert.False(atFarm.Admits(kegga));
        Assert.True(atKegga.Admits(kegga));
        // A narrower window over the same column keeps that column's canvas position exactly.
        SubunitPosition feet = genesis.Space.GetActor(player).Feet;
        var narrow = new CargoViewProjection(new LocalRegionObservation(player, feet, [farm.RegionAt(feet.Cell.X, feet.Cell.Y)!]));
        Assert.Equal(atFarm.Project(feet), narrow.Project(feet));
        Assert.Equal(atFarm.Project(feet), new CargoViewProjection(LocalRegionObserver.Project(genesis,
            markets.Origin.MerchantActorId)!) is { } atMarla && atMarla.Admits(feet) ? atMarla.Project(feet) : atFarm.Project(feet));
    }

    [Fact]
    public void Observed_ground_carries_no_material_lineage_or_other_floor()
    {
        Assert.Equal(new[] { "Bounds", "Ground", "OpeningDepth", "Revision" },
            typeof(LocalObservedRegion).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "CellX", "CellY", "SurfaceSubunits", "VolumeMicrolitres" },
            typeof(LocalObservedWater).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        LocalRegionObservation observation = LocalRegionObserver.Project(CargoTestWorld.Genesis,
            CargoTestWorld.Player(CargoTestWorld.Genesis))!;
        Assert.All(observation.Regions, region =>
            Assert.Equal(region.Bounds.Width * region.Bounds.Depth, region.Ground.Count));
        Assert.Contains(observation.Regions.SelectMany(region => region.Ground), ground => ground == LocalGroundKind.Surface);
    }
}
