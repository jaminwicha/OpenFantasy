using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Presentation;

/// <summary>
/// An open-channel cutaway over public occupancy and committed individual volumes. The front earth
/// is omitted for visibility; this view neither computes equilibrium heads nor moves material.
/// </summary>
internal sealed partial class WatercourseView : Node2D
{
    private const int TilePixels = 32;
    private const long CosmeticFrameMilliseconds = 8_640;
    private readonly List<ChannelFloor> _floors = [];
    private LocalLiquidView? _view;
    private Texture2D _surface = null!;
    private int _frame;
    private FarmVisualAssets _assets = null!;
    private FarmActorFrame[] _actors = [];
    private IReadOnlyList<LocalExcavationView> _excavationTargets = [];

    internal void Setup(FarmVisualAssets assets) => _assets = assets;

    internal int RenderedCellCount => _view?.Cells.Count ?? 0;
    internal ulong RenderedVolumeMicrolitres => _view?.Cells.Aggregate(0UL,
        (sum, cell) => checked(sum + cell.VolumeMicrolitres)) ?? 0;

    private void EnsureSurface()
    {
        if (_surface is not null) return;
        TextureFilter = TextureFilterEnum.Nearest;
        _surface = GD.Load<Texture2D>("res://assets/watercourse/water-surface.png");
        if (_surface is null || _surface.GetWidth() != 128 || _surface.GetHeight() != 64)
        {
            throw new InvalidOperationException("The watercourse requires its native two-variant surface sheet.");
        }
    }

    internal void Present(LocalLiquidView? view, WorldInstant calendar, IReadOnlyList<FarmActorFrame> actors,
        LocalWorksiteView? worksite = null)
    {
        Visible = view is not null;
        if (view is null)
        {
            _view = null;
            _floors.Clear();
            _excavationTargets = [];
            return;
        }
        EnsureSurface();
        int frame = (int)(calendar.MillisecondsWithinDay / CosmeticFrameMilliseconds % 4);
        IReadOnlyList<LocalExcavationView> targets = worksite is null
            ? Array.Empty<LocalExcavationView>() : worksite.ExcavationTargets;
        if (ReferenceEquals(_view, view) && frame == _frame && _actors.SequenceEqual(actors)
            && _excavationTargets.SequenceEqual(targets))
        {
            return;
        }
        if (_view is null || !SameTerrain(_view, view))
        {
            ReadKnownFloors(view);
        }
        _view = view;
        _actors = actors.ToArray();
        _excavationTargets = targets;
        _frame = frame;
        QueueRedraw();
    }

    private void ReadKnownFloors(LocalLiquidView view)
    {
        var cells = new Dictionary<CellCoordinate, bool>();
        foreach (LocalLiquidTerrainView region in view.Terrain)
        {
            for (int index = 0; index < region.Occupancy.Count; index++)
            {
                cells.Add(region.Bounds.CoordinateAt(index), region.Occupancy[index]);
            }
        }
        _floors.Clear();
        for (long y = FarmCanvasCoordinates.MinimumCellY; y < FarmCanvasCoordinates.MinimumCellY + FarmCanvasCoordinates.RegionDepth; y++)
        {
            for (long x = FarmCanvasCoordinates.MinimumCellX; x < FarmCanvasCoordinates.MinimumCellX + FarmCanvasCoordinates.RegionWidth; x++)
            {
                if (!cells.TryGetValue(new(x, y, 0), out bool occupied) || occupied)
                {
                    continue;
                }
                for (long z = -1; z >= -16; z--)
                {
                    if (!cells.TryGetValue(new(x, y, z), out bool solid))
                    {
                        break;
                    }
                    if (solid)
                    {
                        _floors.Add(new(new(x, y, z + 1),
                            cells.TryGetValue(new(x, y - 1, 0), out bool northernBank) && northernBank));
                        break;
                    }
                }
            }
        }
    }

    private static bool SameTerrain(LocalLiquidView previous, LocalLiquidView current)
    {
        if (previous.Terrain.Count != current.Terrain.Count)
        {
            return false;
        }
        for (int index = 0; index < previous.Terrain.Count; index++)
        {
            LocalLiquidTerrainView before = previous.Terrain[index];
            LocalLiquidTerrainView after = current.Terrain[index];
            if (before.Bounds != after.Bounds || before.Revision != after.Revision
                || !before.Occupancy.SequenceEqual(after.Occupancy))
            {
                return false;
            }
        }
        return true;
    }

    public override void _Draw()
    {
        if (_view is null)
        {
            return;
        }
        foreach (ChannelFloor floor in _floors)
        {
            Vector2 original = Point(floor.Cell, 1);
            DrawRect(new Rect2(original, new Vector2(TilePixels, TilePixels)), new Color("283f3d"));
        }
        foreach (ChannelFloor floor in _floors)
        {
            Vector2 original = Point(floor.Cell, 1);
            Vector2 bottom = Point(floor.Cell, floor.Cell.Z);
            if (floor.HasRearBank)
            {
                DrawColoredPolygon([original, original + new Vector2(TilePixels, 0),
                    bottom + new Vector2(TilePixels, 0), bottom], new Color("69624c"));
                DrawLine(original, original + new Vector2(TilePixels, 0), new Color("96a276"), 2);
            }
            DrawRect(new Rect2(bottom, new Vector2(TilePixels, TilePixels)), new Color("676e5a"));
        }
        // Draw actual lower layers first. Separated upper portions retain their own Z; a
        // column is never collapsed into the equilibrium height of its summed quantity.
        foreach (LocalLiquidCellView cell in _view.Cells.OrderBy(cell => cell.Coordinate.Z)
            .ThenBy(cell => cell.Coordinate.Y).ThenBy(cell => cell.Coordinate.X))
        {
            DrawVolume(cell);
        }
        DrawOccupiedDryBank();
        DrawExcavationTops();
    }

    private void DrawExcavationTops()
    {
        foreach (LocalExcavationView target in _excavationTargets.Where(target =>
            target.ReachMode == ExcavationReachMode.RaisedBankTop))
        {
            LocalLiquidTerrainView? region = _view!.Terrain.SingleOrDefault(value => value.Bounds.Contains(target.Cell));
            if (region is null)
            {
                continue;
            }
            // A solid target is marked at its top. After removal, move this unfilled cue to
            // the removed cell's base so the rear bank cannot read as an unbroken target lid.
            // Occupancy, not work progress or predicted water, chooses the boundary plane.
            Vector2 origin = Point(target.Cell, target.Cell.Z + (region.IsSolid(target.Cell) ? 1 : 0));
            var outline = new Rect2(origin + Vector2.One, new Vector2(TilePixels - 2, TilePixels - 2));
            DrawRect(outline, new Color("c4b684"), filled: false, width: 1);
        }
    }

    private void DrawOccupiedDryBank()
    {
        // A cutaway must not make a person on the omitted front earth look immersed. Restore
        // the actual native ground tile under each dry actor where the cutaway overlaps it.
        // Only proved solid footing with known empty space above may receive this foreground cap.
        var patches = new HashSet<CellCoordinate>();
        foreach (FarmActorFrame actor in _actors.Where(actor => actor.Feet.Z == 256))
        {
            var cell = new CellCoordinate(actor.Feet.X / 256, actor.Feet.Y / 256, 0);
            LocalLiquidTerrainView? region = _view!.Terrain.SingleOrDefault(value => value.Bounds.Contains(cell));
            var air = cell with { Z = 1 };
            LocalLiquidTerrainView? above = _view.Terrain.SingleOrDefault(value => value.Bounds.Contains(air));
            if (region is null || above is null || !region.IsSolid(cell) || above.IsSolid(air) || !patches.Add(cell))
            {
                continue;
            }
            var destination = new Rect2(Point(cell, 1), new Vector2(TilePixels, TilePixels));
            if (!_floors.Any(floor => destination.Intersects(new Rect2(Point(floor.Cell, 1),
                new Vector2(TilePixels, TilePixels * (2 - floor.Cell.Z))))))
            {
                continue;
            }
            int x = checked((int)cell.X), y = checked((int)cell.Y);
            Vector2I atlas = FarmGroundView.AtlasCoordinate(_assets.GroundAt(x - FarmCanvasCoordinates.MinimumCellX,
                y - FarmCanvasCoordinates.MinimumCellY), x, y);
            DrawTextureRectRegion(_assets.Atlas, destination, new Rect2(atlas.X * 16, atlas.Y * 16, 16, 16));
            DrawLine(destination.Position, destination.Position + new Vector2(TilePixels, 0), new Color("96a276"), 2);
        }
    }

    private void DrawVolume(LocalLiquidCellView cell)
    {
        double depth = cell.VolumeMicrolitres / (double)LocalLiquidView.CellCapacityMicrolitres;
        Vector2 bottom = Point(cell.Coordinate, cell.Coordinate.Z);
        Vector2 surface = Point(cell.Coordinate, cell.Coordinate.Z + depth);
        DrawColoredPolygon([surface + new Vector2(0, TilePixels), surface + new Vector2(TilePixels, TilePixels),
            bottom + new Vector2(TilePixels, TilePixels), bottom + new Vector2(0, TilePixels)], new Color("31565b"));
        int variant = (int)((cell.Coordinate.X + cell.Coordinate.Y * 3) & 1);
        // Native art supplies only the horizontal surface. Volume geometry above is exact
        // public fill, and switching cosmetic frames creates no liquid or directed current.
        DrawTextureRectRegion(_surface, new Rect2(surface, new Vector2(TilePixels, TilePixels)),
            new Rect2(_frame * TilePixels, variant * TilePixels, TilePixels, TilePixels));
    }

    private static Vector2 Point(CellCoordinate cell, double height) =>
        WorksiteCoordinates.Point(cell.X * 256.0, cell.Y * 256.0, height * 256.0);

    private sealed record ChannelFloor(CellCoordinate Cell, bool HasRearBank);
}
