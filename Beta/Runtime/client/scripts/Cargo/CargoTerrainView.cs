using System.Security.Cryptography;
using System.Text;
using Godot;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Client.Cargo;

/// <summary>What one observed column was drawn as: a retained atlas tile, a raised blocker, an opening or water.</summary>
internal readonly record struct CargoDrawnColumn(long CellX, long CellY, string Choice);

/// <summary>The terrain this view actually drew last: its observed shape, region revisions and every column's choice.</summary>
internal sealed record CargoDrawnTerrain(string Shape, IReadOnlyList<ulong> Revisions, int Columns, int WaterColumns,
    int RaisedColumns, int OpenColumns, string ChoiceDigest, ulong Frame);

/// <summary>
/// Draws exactly the observed ground of a cargo frame through its projection. Each observed column's ground kind picks
/// a tile of the retained farm atlas, and the farm's own source floor is used only for the farm's own cells. A raised
/// blocker is drawn as a bank above the floor rather than as walkable ground, an opening is drawn dark by its known depth,
/// committed water is drawn with the retained watercourse surface, and the player's own work targets are outlined.
/// Nothing outside the observed regions is drawn and no source tile array is repeated over another place. It redraws only
/// when the observed regions, their revisions, the water or the work targets change, and records what it last drew.
/// </summary>
internal sealed partial class CargoTerrainView : Node2D
{
    private const int AtlasTile = 16;
    private static readonly Color OpenGround = new("10181a");
    private static readonly Color ShallowOpening = new("263338");
    private static readonly Color RaisedFace = new("3a3a2c");
    private static readonly Color LowWater = new(0.19f, 0.34f, 0.36f, 0.85f);
    private FarmVisualAssets _assets = null!;
    private Texture2D? _surface;
    private CargoViewProjection? _projection;
    private CellCoordinate[] _workTargets = [];
    private string _shape = "";

    /// <summary>The terrain drawn at the latest draw, or null before the first regional draw.</summary>
    internal CargoDrawnTerrain? LastDrawn { get; private set; }

    internal void Setup(FarmVisualAssets assets)
    {
        _assets = assets;
        TextureFilter = TextureFilterEnum.Nearest;
    }

    internal void Present(CargoViewProjection? projection, IReadOnlyList<CellCoordinate>? workTargets = null)
    {
        Visible = projection is not null;
        CellCoordinate[] targets = workTargets?.ToArray() ?? [];
        string shape = projection is null ? "" : ShapeOf(projection.Observation)
            + "|" + string.Join(";", targets.Select(cell => $"{cell.X},{cell.Y},{cell.Z}"));
        _projection = projection;
        _workTargets = targets;
        if (shape == _shape) return;
        _shape = shape;
        QueueRedraw();
    }

    /// <summary>The observed regions, revisions, floor and water this frame presents, as one comparable text.</summary>
    internal static string ShapeOf(LocalRegionObservation observation) =>
        string.Join(";", observation.Regions.Select(region =>
            $"{region.Bounds.Minimum.X},{region.Bounds.Minimum.Y},{region.Bounds.Minimum.Z}@{region.Revision}"))
        + "|" + observation.FloorFeetZ + "|" + string.Join(";", observation.Water.Select(water =>
            $"{water.CellX},{water.CellY}:{water.VolumeMicrolitres}/{water.SurfaceSubunits}"));

    /// <summary>
    /// The draw choice of one observed column: water over an opening or a flooded floor, an opening by depth, a raised
    /// blocker by stratum, or the retained atlas tile of intact ground (the farm's own source tile on its own cells).
    /// </summary>
    internal static string ChoiceOf(LocalRegionObservation observation, LocalObservedRegion region, long cellX, long cellY,
        Func<long, long, int?> sourceGround)
    {
        LocalGroundKind ground = region.GroundAt(cellX, cellY);
        if (observation.WaterAt(cellX, cellY) is { } water)
        {
            return water.SurfaceSubunits >= observation.FloorFeetZ - Core.Spatial.SubunitPosition.SubunitsPerCell / 4
                ? "water" : "low-water";
        }
        return ground switch
        {
            LocalGroundKind.Open => "open-" + region.OpeningDepthAt(cellX, cellY),
            LocalGroundKind.Raised => "raised",
            LocalGroundKind.Surface when sourceGround(cellX, cellY) is { } source => "tile-" + source,
            _ => "tile-" + GroundTile(ground)
        };
    }

    public override void _Draw()
    {
        if (_projection is null || _assets is null) return;
        LocalRegionObservation observation = _projection.Observation;
        var digest = new StringBuilder();
        int columns = 0, water = 0, raised = 0, open = 0;
        foreach (LocalObservedRegion region in observation.Regions)
        {
            for (int y = 0; y < region.Bounds.Depth; y++)
            {
                for (int x = 0; x < region.Bounds.Width; x++)
                {
                    long cellX = region.Bounds.Minimum.X + x;
                    long cellY = region.Bounds.Minimum.Y + y;
                    CargoCanvasPoint corner = _projection.CellCorner(cellX, cellY);
                    var destination = new Rect2((float)corner.X, (float)corner.Y, CargoViewProjection.PixelsPerCell,
                        CargoViewProjection.PixelsPerCell);
                    string choice = ChoiceOf(observation, region, cellX, cellY, (cx, cy) => _assets.SourceGroundAt(cx, cy));
                    DrawColumn(region, cellX, cellY, destination, choice);
                    digest.Append(cellX).Append(',').Append(cellY).Append('=').Append(choice).Append(';');
                    columns++;
                    if (choice.Contains("water", StringComparison.Ordinal)) water++;
                    else if (choice == "raised") raised++;
                    else if (choice.StartsWith("open-", StringComparison.Ordinal)) open++;
                }
            }
            CargoCanvasPoint start = _projection.CellCorner(region.Bounds.Minimum.X, region.Bounds.Minimum.Y);
            DrawRect(new Rect2((float)start.X, (float)start.Y, region.Bounds.Width * CargoViewProjection.PixelsPerCell,
                region.Bounds.Depth * CargoViewProjection.PixelsPerCell), new Color(0.16f, 0.23f, 0.2f, 0.55f), filled: false, width: 1);
        }
        DrawWorkTargets(observation);
        LastDrawn = new CargoDrawnTerrain(ShapeOf(observation), observation.Regions.Select(region => region.Revision).ToArray(),
            columns, water, raised, open, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digest.ToString()))),
            (ulong)Engine.GetFramesDrawn());
    }

    /// <summary>The digest of every column choice the observation implies, for comparison with what was drawn.</summary>
    internal string ExpectedDigest(LocalRegionObservation observation)
    {
        var digest = new StringBuilder();
        foreach (LocalObservedRegion region in observation.Regions)
        {
            for (int y = 0; y < region.Bounds.Depth; y++)
            {
                for (int x = 0; x < region.Bounds.Width; x++)
                {
                    long cellX = region.Bounds.Minimum.X + x;
                    long cellY = region.Bounds.Minimum.Y + y;
                    digest.Append(cellX).Append(',').Append(cellY).Append('=')
                        .Append(ChoiceOf(observation, region, cellX, cellY, (cx, cy) => _assets.SourceGroundAt(cx, cy))).Append(';');
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digest.ToString())));
    }

    private void DrawColumn(LocalObservedRegion region, long cellX, long cellY, Rect2 destination, string choice)
    {
        LocalGroundKind ground = region.GroundAt(cellX, cellY);
        if (choice.StartsWith("open-", StringComparison.Ordinal) || ground == LocalGroundKind.Open)
        {
            DrawRect(destination, region.OpeningDepthAt(cellX, cellY) == 1 ? ShallowOpening : OpenGround);
        }
        else
        {
            LocalGroundKind tileKind = ground == LocalGroundKind.Raised ? LocalGroundKind.Stone : ground;
            int tile = tileKind == LocalGroundKind.Surface && _assets.SourceGroundAt(cellX, cellY) is { } source
                ? source : GroundTile(tileKind);
            Vector2I atlas = FarmGroundView.AtlasCoordinate(tile, unchecked((int)cellX), unchecked((int)cellY));
            DrawTextureRectRegion(_assets.Atlas, destination, new Rect2(atlas.X * AtlasTile, atlas.Y * AtlasTile, AtlasTile, AtlasTile));
            if (ground == LocalGroundKind.Raised)
            {
                // A bank above the floor: darkened top with a lit rim and a front face, never walkable-looking grass.
                DrawRect(destination, new Color(0, 0, 0, 0.45f));
                DrawRect(new Rect2(destination.Position + new Vector2(0, 24), new Vector2(32, 8)), RaisedFace);
                DrawLine(destination.Position, destination.Position + new Vector2(32, 0), new Color("c4b684"), 2);
            }
        }
        if (choice == "water")
        {
            Texture2D surface = Surface();
            int variant = (int)((cellX + cellY * 3) & 1);
            DrawTextureRectRegion(surface, destination, new Rect2(0, variant * 32, 32, 32));
        }
        else if (choice == "low-water")
        {
            DrawRect(new Rect2(destination.Position + new Vector2(0, 10), new Vector2(32, 22)), LowWater);
        }
    }

    private void DrawWorkTargets(LocalRegionObservation observation)
    {
        long floorZ = observation.FloorFeetZ / Core.Spatial.SubunitPosition.SubunitsPerCell - 1;
        foreach (CellCoordinate cell in _workTargets)
        {
            if (observation.RegionAt(cell.X, cell.Y) is null || cell.Z > floorZ + 1 || cell.Z < floorZ - LocalRegionObserver.MaximumOpeningDepth)
                continue;
            CargoCanvasPoint corner = _projection!.CellCorner(cell.X, cell.Y);
            DrawRect(new Rect2((float)corner.X + 1, (float)corner.Y + 1, CargoViewProjection.PixelsPerCell - 2,
                CargoViewProjection.PixelsPerCell - 2), new Color("c4b684"), filled: false, width: 1);
        }
    }

    private Texture2D Surface()
    {
        if (_surface is not null) return _surface;
        _surface = GD.Load<Texture2D>("res://assets/watercourse/water-surface.png");
        if (_surface is null || _surface.GetWidth() != 128 || _surface.GetHeight() != 64)
            throw new InvalidOperationException("Cargo water requires the retained two-variant watercourse surface sheet.");
        return _surface;
    }

    /// <summary>
    /// The retained atlas tile for an observed ground kind outside the farm's own source floor: grass for intact
    /// surface ground, bare earth for exposed soil and the stone tile for exposed stone.
    /// </summary>
    internal static int GroundTile(LocalGroundKind ground) => ground switch
    {
        LocalGroundKind.Surface => 0,
        LocalGroundKind.Soil => 4,
        LocalGroundKind.Stone => 28,
        _ => throw new InvalidDataException("Open or raised ground has no floor tile.")
    };
}
