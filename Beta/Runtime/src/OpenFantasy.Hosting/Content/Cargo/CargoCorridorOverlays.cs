using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>
/// One retained <c>world.json</c> place the corridor overlays: its tiles on the inherited farm region, its tiles on
/// added corridor regions, and every rectangle where the authored walking lane crosses it. Overlaying a place imports
/// none of its meaning: the ground there is the basis region's ordinary flat support, not town, castle, port, farm or
/// threat terrain, and no resident, structure or encounter of that place is admitted.
/// </summary>
public sealed record CargoPlaceOverlay(string PlaceId, long InheritedTiles, long AddedTiles,
    IReadOnlyList<CargoTileRect> LaneCrossings)
{
    public bool Matches(CargoPlaceOverlay other) => other is not null && PlaceId == other.PlaceId
        && InheritedTiles == other.InheritedTiles && AddedTiles == other.AddedTiles
        && LaneCrossings.SequenceEqual(other.LaneCrossings);
}

/// <summary>
/// The declared, versioned set of retained places the circuit corridor overlays, and the computation it is checked
/// against. The corridor is refused when a retained place intersects a corridor region or the lane without a
/// declaration, or when a declaration no longer matches the actual intersection.
/// </summary>
public static class CargoCorridorOverlays
{
    public const string BanditCampPlaceId = "bandit_camp";

    /// <summary>
    /// Version one, in source order. Three places lie on the inherited farm region (the farm admission already maps
    /// them); seven lie on added corridor regions. The lane crosses Brasshollow and Gearford (the two merchant
    /// places), the Sylvas farm plot and the bandit camp.
    /// </summary>
    public static IReadOnlyList<CargoPlaceOverlay> Declared { get; } = Array.AsReadOnly(new CargoPlaceOverlay[]
    {
        new("brasshollow", 403, 0, [new(97, 349, 127, 350)]),
        new("tavern_brasshollow", 63, 0, []),
        new("gearford", 0, 208, [new(346, 141, 347, 154)]),
        new("tavern_gearford", 0, 49, []),
        new("castle_aldermere", 0, 406, []),
        new("standing_stones", 0, 12, []),
        new(BanditCampPlaceId, 0, 84, [new(346, 305, 347, 317)]),
        new("farm_verdance_small", 63, 0, []),
        new("farm_sylvas_medium", 0, 108, [new(346, 330, 347, 339)]),
        new("port_gearford", 0, 140, [])
    });

    /// <summary>The declared set's canonical version, part of the cargo admission identity.</summary>
    public static ContentHash Version { get; } = VersionOf(Declared);

    /// <summary>
    /// The exact travel threat dependency: the lane crosses the retained hostile <c>bandit_camp</c> at x=346,
    /// y in [305,317). No encounter is admitted there; a corridor threat must be admitted on these cells.
    /// </summary>
    public static CargoTileRect BanditCampCrossing { get; } =
        Declared.Single(overlay => overlay.PlaceId == BanditCampPlaceId).LaneCrossings.Single();

    internal static ContentHash VersionOf(IReadOnlyList<CargoPlaceOverlay> overlays)
    {
        List<byte[]> fields = [];
        foreach (CargoPlaceOverlay overlay in overlays)
        {
            fields.Add(CargoCanonicalFrame.Text(overlay.PlaceId));
            fields.Add(CargoCanonicalFrame.Signed64(overlay.InheritedTiles));
            fields.Add(CargoCanonicalFrame.Signed64(overlay.AddedTiles));
            fields.Add(CargoCanonicalFrame.Unsigned64((ulong)overlay.LaneCrossings.Count));
            foreach (CargoTileRect crossing in overlay.LaneCrossings)
            {
                fields.Add(CargoCanonicalFrame.Signed64(crossing.MinimumX));
                fields.Add(CargoCanonicalFrame.Signed64(crossing.MinimumY));
                fields.Add(CargoCanonicalFrame.Signed64(crossing.MaximumX));
                fields.Add(CargoCanonicalFrame.Signed64(crossing.MaximumY));
            }
        }
        return CargoCanonicalFrame.Hash("cargo-world.corridor-overlays.v1", fields);
    }

    /// <summary>
    /// Every retained place, in source order, that intersects the basis region, an added region or a straight lane
    /// segment between consecutive corners. Lane crossings are reported per segment as exact tile rectangles.
    /// </summary>
    public static IReadOnlyList<CargoPlaceOverlay> Compute(IEnumerable<CargoSourcePlace> places, TerrainRegionBounds basis,
        IEnumerable<TerrainRegionBounds> added, IReadOnlyList<CellCoordinate> laneCorners)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(added);
        ArgumentNullException.ThrowIfNull(laneCorners);
        CargoTileRect inherited = Tiles(basis);
        CargoTileRect[] corridor = added.Select(Tiles).ToArray();
        CargoTileRect[] segments = laneCorners.Zip(laneCorners.Skip(1), Segment).ToArray();
        List<CargoPlaceOverlay> overlays = [];
        foreach (CargoSourcePlace place in places)
        {
            CargoTileRect tiles = place.Tiles;
            long inheritedTiles = tiles.Intersect(inherited)?.Area ?? 0;
            long addedTiles = corridor.Sum(region => tiles.Intersect(region)?.Area ?? 0);
            CargoTileRect[] crossings = segments.Select(segment => tiles.Intersect(segment))
                .OfType<CargoTileRect>().ToArray();
            if (inheritedTiles != 0 || addedTiles != 0 || crossings.Length != 0)
            {
                overlays.Add(new CargoPlaceOverlay(place.SourceId, inheritedTiles, addedTiles, Array.AsReadOnly(crossings)));
            }
        }
        return overlays.AsReadOnly();
    }

    /// <summary>Refuses an undeclared, changed or vanished overlay; nothing is repaired or ignored.</summary>
    internal static void Check(IReadOnlyList<CargoPlaceOverlay> actual, IReadOnlyList<CargoPlaceOverlay> declared)
    {
        foreach (CargoPlaceOverlay overlay in actual)
        {
            CargoPlaceOverlay expected = declared.SingleOrDefault(value => value.PlaceId == overlay.PlaceId)
                ?? throw new InvalidDataException("The corridor intersects an undeclared retained place: " + overlay.PlaceId);
            if (!expected.Matches(overlay))
            {
                throw new InvalidDataException("The corridor's overlay of a retained place differs from its declaration: "
                    + overlay.PlaceId);
            }
        }
        foreach (CargoPlaceOverlay overlay in declared.Where(value => actual.All(found => found.PlaceId != value.PlaceId)))
        {
            throw new InvalidDataException("A declared overlay no longer intersects the corridor: " + overlay.PlaceId);
        }
    }

    internal static CargoTileRect Tiles(TerrainRegionBounds bounds) => new(bounds.Minimum.X, bounds.Minimum.Y,
        bounds.Minimum.X + bounds.Width, bounds.Minimum.Y + bounds.Depth);

    private static CargoTileRect Segment(CellCoordinate from, CellCoordinate to)
    {
        if (from.X != to.X && from.Y != to.Y)
        {
            throw new InvalidDataException("Authored corridor segments are cardinal.");
        }
        return new CargoTileRect(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Max(from.X, to.X) + 1,
            Math.Max(from.Y, to.Y) + 1);
    }
}
