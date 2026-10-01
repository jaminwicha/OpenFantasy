using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Tests.Content.Cargo.Refinement;

/// <summary>
/// Every retained <c>world.json</c> place the corridor regions or the authored lane intersect is enumerated, split
/// into inherited farm footprint and added geometry, and must equal the declared versioned overlay set.
/// </summary>
public sealed class CargoOverlayRefinementTests
{
    private static readonly Lazy<TerrainRegionBounds> Basis = new(() =>
    {
        WorldSnapshot farm = RestorativeJourneyAdmission.CreateEmbedded("cargo-overlays", "Traveler");
        var player = farm.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        CellCoordinate standing = farm.Space.GetActor(player).Feet.Cell;
        return farm.Space.Regions.Single(region => region.Bounds.Contains(standing)).Bounds;
    });

    [Fact]
    public void The_computed_overlays_equal_the_declared_set_and_name_the_bandit_camp_crossing()
    {
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        Assert.Equal(25, sources.Places.Count);
        CargoCorridor corridor = CargoCorridor.CreateCircuit(Basis.Value, sources);
        Assert.Equal(CargoCorridorOverlays.Declared.Select(value => value.PlaceId), corridor.Overlays.Select(value => value.PlaceId));
        Assert.All(corridor.Overlays.Zip(CargoCorridorOverlays.Declared), pair => Assert.True(pair.First.Matches(pair.Second)));
        // The farm region is the inherited footprint; only these three places lie on it, and none on added regions.
        Assert.Equal(new[] { "brasshollow", "tavern_brasshollow", "farm_verdance_small" },
            corridor.Overlays.Where(value => value.InheritedTiles != 0).Select(value => value.PlaceId));
        Assert.All(corridor.Overlays.Where(value => value.InheritedTiles != 0), value => Assert.Equal(0, value.AddedTiles));
        // Fully covered places are named explicitly; the corridor is ordinary ground over them, not their terrain.
        Assert.Equal(new[] { "farm_verdance_small", "farm_sylvas_medium", "port_gearford" }, corridor.Overlays
            .Where(value => value.InheritedTiles + value.AddedTiles == sources.Places.Single(place => place.SourceId == value.PlaceId).Tiles.Area)
            .Select(value => value.PlaceId));
        // The exact travel threat dependency: the lane crosses the bandit camp at x=346, y in [305,317).
        Assert.Equal(new CargoTileRect(346, 305, 347, 317), CargoCorridorOverlays.BanditCampCrossing);
        Assert.Equal("[346,347)x[305,317)", CargoCorridorOverlays.BanditCampCrossing.ToString());
        Assert.Equal(new[] { "brasshollow", "gearford", "bandit_camp", "farm_sylvas_medium" },
            corridor.Overlays.Where(value => value.LaneCrossings.Count != 0).Select(value => value.PlaceId));
        // Every crossing lies on the walked lane cells of the authored corners.
        Assert.All(corridor.Overlays.SelectMany(value => value.LaneCrossings), crossing =>
            Assert.True(crossing.MinimumX == 346 && crossing.MaximumX == 347 || crossing.MinimumY == 349 && crossing.MaximumY == 350));
    }

    [Fact]
    public void An_undeclared_intersection_a_changed_declaration_or_a_vanished_place_is_refused()
    {
        // A retained place moved onto the corridor is an undeclared intersection.
        using (CargoSourceDocuments moved = CargoSourceDocuments.Retained(world: text =>
            text.Replace("{\"id\":\"ruined_watchtower\",\"x\":147,\"y\":305", "{\"id\":\"ruined_watchtower\",\"x\":147,\"y\":325")))
        {
            Assert.Contains("undeclared", Assert.Throws<InvalidDataException>(() =>
                CargoCorridor.CreateCircuit(Basis.Value, moved.Bind())).Message);
        }
        // A declared place moved off the corridor leaves a stale declaration.
        using (CargoSourceDocuments vanished = CargoSourceDocuments.Retained(world: text =>
            text.Replace("{\"id\":\"port_gearford\",\"x\":330", "{\"id\":\"port_gearford\",\"x\":400")))
        {
            Assert.Contains("no longer intersects", Assert.Throws<InvalidDataException>(() =>
                CargoCorridor.CreateCircuit(Basis.Value, vanished.Bind())).Message);
        }
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        // Omitting the bandit camp from the declaration is refused rather than silently tolerated.
        CargoPlaceOverlay[] withoutCamp = CargoCorridorOverlays.Declared.Where(value => value.PlaceId != "bandit_camp").ToArray();
        Assert.Contains("undeclared", Assert.Throws<InvalidDataException>(() =>
            CargoCorridor.CreateCircuit(Basis.Value, sources, withoutCamp)).Message);
        // A declaration with a different crossing is refused, and changes the overlay version.
        CargoPlaceOverlay[] shifted = CargoCorridorOverlays.Declared.Select(value => value.PlaceId == "bandit_camp"
            ? value with { LaneCrossings = [new CargoTileRect(346, 306, 347, 317)] } : value).ToArray();
        Assert.Contains("differs", Assert.Throws<InvalidDataException>(() =>
            CargoCorridor.CreateCircuit(Basis.Value, sources, shifted)).Message);
        Assert.NotEqual(CargoCorridorOverlays.Version, CargoCorridorOverlays.VersionOf(shifted));
        Assert.Equal(CargoCorridorOverlays.Version, CargoCorridorOverlays.VersionOf(CargoCorridorOverlays.Declared.ToArray()));
    }
}
