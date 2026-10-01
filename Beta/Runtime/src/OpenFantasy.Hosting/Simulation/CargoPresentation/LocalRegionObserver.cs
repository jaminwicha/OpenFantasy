using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation.CargoPresentation;

/// <summary>
/// Projects the player's bounded terrain observation from one already selected immutable root. The session applies it
/// when it publishes a view, so the observation and the actors, offers and liquids of that view share one root and a
/// frame never touches the authority. Region projections are cached by exact bounds, revision, floor and the revisions
/// of the regions stacked under them, so an unchanged region is not recomputed at the next publication.
/// <para>
/// The floor is the one the player's feet stand on, whatever supports them: intact terrain, or construction where the
/// world admits construction support. A player outside every admitted region, or between cell floors, has no
/// observation; the client keeps presenting its last coherent frame and says so instead of guessing.
/// </para>
/// </summary>
public sealed class LocalRegionObserver
{
    /// <summary>Columns around the player's own column whose regions are observed; enough to fill one view.</summary>
    public const int ObservationRadiusCells = 24;

    /// <summary>Open cells below the floor searched for an opening's bottom and for water standing in it.</summary>
    public const int MaximumOpeningDepth = 8;

    private readonly EntityId _playerActorId;
    private readonly Dictionary<RegionKey, LocalObservedRegion> _projected = [];
    private readonly object _gate = new();

    public LocalRegionObserver(EntityId playerActorId)
    {
        _playerActorId = playerActorId;
    }

    public EntityId PlayerActorId => _playerActorId;

    /// <summary>The player's observation in one selected root, or null when its feet stand on no supported cell floor.</summary>
    internal LocalRegionObservation? Observe(WorldSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Project(state, _playerActorId, Cached);
    }

    /// <summary>The same stateless projection, for tests and other readers of one admitted root.</summary>
    internal static LocalRegionObservation? Project(WorldSnapshot state, EntityId playerActorId) =>
        Project(state, state.Space, playerActorId, (region, groundZ, stack, _) => ProjectRegion(region, groundZ, stack));

    /// <summary>The stateless projection of one root's water over explicitly supplied geometry and stances.</summary>
    internal static LocalRegionObservation? Project(WorldSnapshot state, LocalSpatialSnapshot space, EntityId playerActorId) =>
        Project(state, space, playerActorId, (region, groundZ, stack, _) => ProjectRegion(region, groundZ, stack));

    private static LocalRegionObservation? Project(WorldSnapshot state, EntityId playerActorId,
        Func<TerrainRegionSnapshot, long, IReadOnlyList<TerrainRegionSnapshot>, string, LocalObservedRegion> project) =>
        Project(state, state.Space, playerActorId, project);

    private static LocalRegionObservation? Project(WorldSnapshot state, LocalSpatialSnapshot space, EntityId playerActorId,
        Func<TerrainRegionSnapshot, long, IReadOnlyList<TerrainRegionSnapshot>, string, LocalObservedRegion> project)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(space);
        if (space.Actors.FirstOrDefault(actor => actor.ActorId == playerActorId) is not { } player
            || player.Feet.Z % SubunitPosition.SubunitsPerCell != 0)
        {
            return null;
        }
        long groundZ = player.Feet.Z / SubunitPosition.SubunitsPerCell - 1;
        CellCoordinate column = player.Feet.Cell;
        if (!space.Regions.Any(region => region.Bounds.Contains(new CellCoordinate(column.X, column.Y, groundZ))))
        {
            return null;
        }
        long minimumX = column.X - ObservationRadiusCells;
        long maximumX = column.X + ObservationRadiusCells;
        long minimumY = column.Y - ObservationRadiusCells;
        long maximumY = column.Y + ObservationRadiusCells;
        LocalObservedRegion[] observed = space.Regions
            .Where(region => region.Bounds.Minimum.Z <= groundZ && region.Bounds.Maximum.Z >= groundZ
                && region.Bounds.Minimum.X <= maximumX && region.Bounds.Maximum.X >= minimumX
                && region.Bounds.Minimum.Y <= maximumY && region.Bounds.Maximum.Y >= minimumY)
            .OrderBy(region => region.Bounds.Minimum.X).ThenBy(region => region.Bounds.Minimum.Y)
            .Select(region =>
            {
                TerrainRegionSnapshot[] stack = space.Regions.Where(other => Overlaps(other.Bounds, region.Bounds)).ToArray();
                return project(region, groundZ, stack, Signature(stack));
            }).ToArray();
        return new LocalRegionObservation(playerActorId, player.Feet, observed, ObserveWater(state, observed, groundZ));
    }

    private LocalObservedRegion Cached(TerrainRegionSnapshot region, long groundZ, IReadOnlyList<TerrainRegionSnapshot> stack,
        string signature)
    {
        lock (_gate)
        {
            var key = new RegionKey(region.Bounds, groundZ, signature);
            if (!_projected.TryGetValue(key, out LocalObservedRegion? observed))
            {
                // Only the latest stack of each region is kept; older projections can never be presented again.
                foreach (RegionKey stale in _projected.Keys.Where(value => value.Bounds == region.Bounds).ToArray()) _projected.Remove(stale);
                observed = ProjectRegion(region, groundZ, stack);
                _projected.Add(key, observed);
            }
            return observed;
        }
    }

    /// <summary>
    /// Classifies every column of one region at the floor: intact ground by stratum, a raised blocker when the cell above
    /// the floor is solid, or an opening whose known depth is read through the regions stacked below it.
    /// </summary>
    private static LocalObservedRegion ProjectRegion(TerrainRegionSnapshot region, long groundZ,
        IReadOnlyList<TerrainRegionSnapshot> stack)
    {
        var ground = new LocalGroundKind[checked(region.Bounds.Width * region.Bounds.Depth)];
        var depth = new byte[ground.Length];
        for (int y = 0; y < region.Bounds.Depth; y++)
        {
            for (int x = 0; x < region.Bounds.Width; x++)
            {
                var floor = new CellCoordinate(region.Bounds.Minimum.X + x, region.Bounds.Minimum.Y + y, groundZ);
                TerrainCell cell = region.GetCell(floor);
                int index = y * region.Bounds.Width + x;
                if (!cell.IsSolid)
                {
                    ground[index] = LocalGroundKind.Open;
                    depth[index] = OpeningDepth(stack, floor);
                    continue;
                }
                ground[index] = CellAt(stack, floor.Offset(0, 0, 1))?.IsSolid == true ? LocalGroundKind.Raised : cell.Stratum switch
                {
                    TerrainStratum.Surface => LocalGroundKind.Surface,
                    TerrainStratum.Soil => LocalGroundKind.Soil,
                    TerrainStratum.Stone => LocalGroundKind.Stone,
                    _ => LocalGroundKind.Open
                };
            }
        }
        return new LocalObservedRegion(region.Bounds, region.Revision, ground, depth);
    }

    /// <summary>Open cells from the floor down to the first known solid cell; zero when that bottom is not known.</summary>
    private static byte OpeningDepth(IReadOnlyList<TerrainRegionSnapshot> stack, CellCoordinate floor)
    {
        for (int open = 1; open <= MaximumOpeningDepth; open++)
        {
            TerrainCell? below = CellAt(stack, floor.Offset(0, 0, -open));
            if (below is null) return 0;
            if (below.IsSolid) return (byte)open;
        }
        return 0;
    }

    /// <summary>
    /// Committed liquid per observed column, from the deepest searched opening cell up to the cell above the floor.
    /// The surface is the highest wet cell's base plus its exact fill; nothing is predicted or equalised.
    /// </summary>
    private static LocalObservedWater[] ObserveWater(WorldSnapshot state, IReadOnlyList<LocalObservedRegion> observed, long groundZ)
    {
        if (state.LiquidCells is not { } liquid)
        {
            return [];
        }
        var cells = new SortedDictionary<CellCoordinate, ulong>();
        foreach (var entry in liquid.Quantities)
        {
            CellCoordinate cell = entry.Key.Coordinate;
            if (entry.Value == 0 || cell.Z > groundZ + 1 || cell.Z < groundZ - MaximumOpeningDepth
                || !observed.Any(region => region.Bounds.Minimum.X <= cell.X && region.Bounds.Maximum.X >= cell.X
                    && region.Bounds.Minimum.Y <= cell.Y && region.Bounds.Maximum.Y >= cell.Y))
            {
                continue;
            }
            cells[cell] = checked(cells.GetValueOrDefault(cell) + checked(entry.Value * liquid.VolumePerUnit.Value));
        }
        return cells.GroupBy(pair => (pair.Key.X, pair.Key.Y)).Select(column =>
        {
            KeyValuePair<CellCoordinate, ulong> top = column.MaxBy(pair => pair.Key.Z);
            ulong fill = Math.Min(top.Value, LocalLiquidView.CellCapacityMicrolitres);
            long surface = checked(top.Key.Z * SubunitPosition.SubunitsPerCell
                + (long)(fill * (ulong)SubunitPosition.SubunitsPerCell / LocalLiquidView.CellCapacityMicrolitres));
            return new LocalObservedWater(column.Key.X, column.Key.Y,
                column.Aggregate(0UL, (sum, pair) => checked(sum + pair.Value)), surface);
        }).ToArray();
    }

    private static TerrainCell? CellAt(IReadOnlyList<TerrainRegionSnapshot> stack, CellCoordinate cell)
    {
        foreach (TerrainRegionSnapshot region in stack)
        {
            if (region.Bounds.Contains(cell)) return region.GetCell(cell);
        }
        return null;
    }

    private static bool Overlaps(TerrainRegionBounds a, TerrainRegionBounds b) =>
        a.Minimum.X <= b.Maximum.X && a.Maximum.X >= b.Minimum.X && a.Minimum.Y <= b.Maximum.Y && a.Maximum.Y >= b.Minimum.Y;

    private static string Signature(IEnumerable<TerrainRegionSnapshot> stack) => string.Join(";", stack.Select(region =>
        $"{region.Bounds.Minimum.X},{region.Bounds.Minimum.Y},{region.Bounds.Minimum.Z},{region.Bounds.Width},"
        + $"{region.Bounds.Depth},{region.Bounds.Height}@{region.Revision}"));

    private readonly record struct RegionKey(TerrainRegionBounds Bounds, long GroundZ, string Stack);
}
