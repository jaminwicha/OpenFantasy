using System.Globalization;
using Godot;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Presentation;

/// <summary>Cached visible departures from the retained farm floor, based solely on known occupancy.</summary>
internal sealed partial class WorksiteTerrainView : Node2D
{
    private readonly List<Surface> _surfaces = [];
    private LocalWorksiteTerrainView[] _regions = [];

    internal void Setup(LocalWorksiteView view)
    {
        if (SameTerrain(view.Terrain))
        {
            return;
        }
        var cells = new Dictionary<CellCoordinate, bool>();
        foreach (LocalWorksiteTerrainView region in view.Terrain)
        {
            for (int index = 0; index < region.Occupancy.Count; index++)
            {
                cells.Add(region.Bounds.CoordinateAt(index), region.Occupancy[index]);
            }
        }
        _surfaces.Clear();
        for (int y = FarmCanvasCoordinates.MinimumCellY; y < FarmCanvasCoordinates.MinimumCellY + FarmCanvasCoordinates.RegionDepth; y++)
        {
            for (int x = FarmCanvasCoordinates.MinimumCellX; x < FarmCanvasCoordinates.MinimumCellX + FarmCanvasCoordinates.RegionWidth; x++)
            {
                long? floor = FindSurface(cells, x, y);
                // Only a proved one-metre surface retains the original flat source art.
                // Raised terrain and cuts are both real departures, not decorative props.
                if (floor == 256)
                {
                    continue;
                }
                Vector2 original = WorksiteCoordinates.Point(x * 256L, y * 256L, 256);
                Vector2? reached = floor is { } height ? WorksiteCoordinates.Point(x * 256L, y * 256L, height) : null;
                _surfaces.Add(new(new Rect2(original, new Vector2(32, 32)), reached, floor));
            }
        }
        _regions = view.Terrain.ToArray();
        QueueRedraw();
    }

    private static long? FindSurface(IReadOnlyDictionary<CellCoordinate, bool> cells, long x, long y)
    {
        if (!cells.TryGetValue(new CellCoordinate(x, y, 0), out bool solid))
        {
            return null;
        }
        // This finite contour view follows contiguous substrate upward, or known void
        // downward, from the farm's original base cell. An absent cell is never void.
        // It is not a general multi-cavern or stacked natural-overhang renderer.
        for (long z = solid ? 1 : -1; z is >= -16 and <= 16; z += solid ? 1 : -1)
        {
            if (!cells.TryGetValue(new CellCoordinate(x, y, z), out bool occupied))
            {
                return null;
            }
            if (occupied != solid)
            {
                return (solid ? z : z + 1) * 256;
            }
        }
        return null;
    }

    private bool SameTerrain(IReadOnlyList<LocalWorksiteTerrainView> regions)
    {
        if (_regions.Length != regions.Count)
        {
            return false;
        }
        for (int index = 0; index < _regions.Length; index++)
        {
            LocalWorksiteTerrainView previous = _regions[index];
            LocalWorksiteTerrainView current = regions[index];
            if (!ReferenceEquals(previous, current)
                && (previous.Bounds != current.Bounds || previous.Revision != current.Revision
                    || !previous.Occupancy.SequenceEqual(current.Occupancy)))
            {
                return false;
            }
        }
        // The immutable occupancy is compared as well: equal bounds/revisions in a different
        // admitted world must not accidentally reuse this world's projected floor geometry.
        return true;
    }

    public override void _Draw()
    {
        // Clear all displaced source tiles before drawing their elevated/depressed geometry;
        // otherwise a later source mask could erase a neighbouring raised top or cutaway.
        foreach (Surface surface in _surfaces)
        {
            DrawRect(surface.Original, new Color("263338"));
            DrawRect(surface.Original, new Color("c4b684"), filled: false, width: 1);
        }
        foreach (Surface surface in _surfaces)
        {
            if (surface.Floor is not { } floor || surface.Height is not { } height)
            {
                DrawLine(surface.Original.Position, surface.Original.End, new Color("87938e"), 1);
                continue;
            }
            Vector2 origin = surface.Original.Position;
            if (height > 256)
            {
                Vector2 front = new(0, 32);
                DrawColoredPolygon([floor + front, floor + new Vector2(32, 32),
                    origin + new Vector2(32, 32), origin + front], new Color("5c5944"));
            }
            else
            {
                DrawColoredPolygon([origin, origin + new Vector2(32, 0), floor + new Vector2(32, 0), floor], new Color("4c4a40"));
            }
            DrawRect(new Rect2(floor, new Vector2(32, 32)), new Color(height > 256 ? "8c9470" : "737465"));
            DrawRect(new Rect2(floor, new Vector2(32, 32)), new Color("3f4c43"), filled: false, width: 1);
            string level = ((height - 256) / 256.0).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) + "m";
            DrawString(ThemeDB.FallbackFont, floor + new Vector2(3, 20), level, fontSize: 11, modulate: new Color("e5dfb9"));
        }
    }

    private sealed record Surface(Rect2 Original, Vector2? Floor, long? Height);
}
