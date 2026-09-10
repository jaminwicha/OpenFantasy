using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

internal static class SpatialFixture
{
    internal static readonly TerrainScope Scope = new(Id(1_000_000), Id(1_000_001), Id(1_000_002));
    internal static EntityId Id(ulong value) => new(0, value);

    internal static BodyDefinition Body(long width = 180, long depth = 140, long height = 464, ulong id = 9000) =>
        new(Id(id), BodyNature.Corporeal, new BodyDimensions(width, depth, height),
            [new BodyPartDefinition(new BodySlotKey("support"), null, new BodyDimensions(16, 16, 16),
                [BodyFunction.GroundContact, BodyFunction.Sight, BodyFunction.Hearing])],
            [new LocomotionRequirement(LocomotionMode.Ground, 1)]);

    internal static ActorPresence Actor(ulong number, SubunitPosition feet, BodyDefinition? body = null,
        ulong revision = 0, TerrainScope? scope = null, IEnumerable<BodyInjury>? injuries = null)
    {
        body ??= Body();
        var form = ActorFormBinding.Corporeal(Id(number + 100), Id(number + 200), Id(number + 300), body);
        return new ActorPresence(form, new BodyConditionSnapshot(form.FormInstanceId, body, injuries ?? []),
            scope ?? Scope, feet, revision, Id(500));
    }

    internal static TerrainRegionSnapshot Terrain(long x = -4, long y = -4, int width = 16, int depth = 16,
        int height = 8, long floorZ = 0, IEnumerable<CellCoordinate>? keptSolids = null,
        TerrainScope? scope = null, EntityId? seed = null)
    {
        var pin = new CountedItemDefinition(Id(8000), ContentHash.Compute("spatial-material"u8));
        var rules = new StratifiedTerrainRules(checked(floorZ + 2), 0, 1, new TerrainMaterialPalette(pin, pin, pin));
        var snapshot = new StratifiedTerrainGenerator().Generate(scope ?? Scope, seed ?? Id(7000), rules,
            new TerrainRegionBounds(new CellCoordinate(x, y, floorZ), width, depth, height));
        var retained = (keptSolids ?? []).ToHashSet();
        CellCoordinate[] cleared = snapshot.Cells.Where(cell => cell.Coordinate.Z > floorZ && cell.IsSolid
            && !retained.Contains(cell.Coordinate)).Select(cell => cell.Coordinate).ToArray();
        foreach (CellCoordinate[] batch in cleared.Chunk(TerrainRegionSnapshot.MaximumExcavationBatch))
        {
            snapshot = snapshot.Excavate(snapshot.Revision, batch);
        }
        return snapshot;
    }

    internal static LocalSpatialSnapshot Scene(TerrainRegionSnapshot terrain, params ActorPresence[] actors) =>
        new(Scope, [terrain], actors);
}
