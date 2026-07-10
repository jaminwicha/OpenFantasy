using Godot;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// Renders the static world from the same world.json the server simulates.
/// Pure presentation — collision truth stays on the server.
/// </summary>
public partial class WorldRenderer : Node2D
{
    public const int TilePx = 16;

    public WorldMap Map { get; private set; } = null!;

    // Logical tile id → atlas variants (col, row) in assets/tiles.png (v2 atlas).
    private static readonly Vector2I[][] Variants =
    {
        new[] { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(2, 0) },   // 0 grass
        new[] { new Vector2I(3, 0) },                                           // 1 tree
        new[] { new Vector2I(4, 0), new Vector2I(5, 0), new Vector2I(6, 0) },   // 2 water
        new[] { new Vector2I(7, 0), new Vector2I(8, 0), new Vector2I(9, 0) },   // 3 road
        new[] { new Vector2I(10, 0), new Vector2I(11, 0) },                     // 4 cobble
        new[] { new Vector2I(12, 0) },                                          // 5 wall
        new[] { new Vector2I(13, 0), new Vector2I(14, 0), new Vector2I(15, 0) },// 6 forest
        new[] { new Vector2I(0, 1), new Vector2I(1, 1), new Vector2I(2, 1) },   // 7 mine
        new[] { new Vector2I(3, 1), new Vector2I(4, 1) },                       // 8 rock
        new[] { new Vector2I(5, 1) },                                           // 9 crystal
        new[] { new Vector2I(6, 1), new Vector2I(7, 1), new Vector2I(8, 1) },   // 10 sand
        new[] { new Vector2I(9, 1), new Vector2I(10, 1) },                      // 11 flowers
        new[] { new Vector2I(11, 1) },                                          // 12 forge
        new[] { new Vector2I(0, 4), new Vector2I(1, 4) },                       // 13 ocean
        new[] { new Vector2I(2, 4) },                                           // 14 mountain
        new[] { new Vector2I(3, 4) },                                           // 15 hills
        new[] { new Vector2I(4, 4), new Vector2I(5, 4), new Vector2I(6, 4) },   // 16 snow
        new[] { new Vector2I(7, 4), new Vector2I(8, 4), new Vector2I(9, 4) },   // 17 desert
        new[] { new Vector2I(10, 4), new Vector2I(11, 4) },                     // 18 swamp
        new[] { new Vector2I(12, 4) },                                          // 19 bridge
        new[] { new Vector2I(13, 4), new Vector2I(14, 4) },                     // 20 castle floor
        new[] { new Vector2I(0, 5), new Vector2I(1, 5) },                       // 21 tower floor
        new[] { new Vector2I(2, 5) },                                           // 22 dock
        new[] { new Vector2I(15, 4) },                                          // 23 dead tree
    };

    private static readonly int[] WaterIds = { 2, 13 };       // shallow, ocean
    private static readonly int[] GrassyIds = { 0, 6, 11 };   // fringe sources
    private static readonly int[] FringeTargets = { 3, 10, 17, 18 }; // road, sand, desert, swamp

    public void Build(WorldMap map)
    {
        Map = map;

        var atlas = GD.Load<Texture2D>("res://assets/tiles.png");
        var tileSet = new TileSet { TileSize = new Vector2I(TilePx, TilePx) };
        var source = new TileSetAtlasSource
        {
            Texture = atlas,
            TextureRegionSize = new Vector2I(TilePx, TilePx),
        };
        int cols = atlas.GetWidth() / TilePx;
        int rows = atlas.GetHeight() / TilePx;
        for (int cy = 0; cy < rows; cy++)
            for (int cx = 0; cx < cols; cx++)
                source.CreateTile(new Vector2I(cx, cy));
        tileSet.AddSource(source, 0);

        var ground = new TileMapLayer { TileSet = tileSet };
        var overlay = new TileMapLayer { TileSet = tileSet, ZIndex = 1 };

        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                int t = map.TileAt(x, y);
                if (t < 0 || t >= Variants.Length) continue;

                var options = Variants[t];
                // Deterministic variant hash — stable across sessions.
                int v = ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
                ground.SetCell(new Vector2I(x, y), 0, options[v % options.Length]);

                int mask = TransitionMask(map, x, y, t);
                if (mask > 0)
                {
                    int row = Array.IndexOf(WaterIds, t) >= 0 ? 2 : 3; // foam vs grass fringe
                    overlay.SetCell(new Vector2I(x, y), 0, new Vector2I(mask, row));
                }
            }
        AddChild(ground);
        AddChild(overlay);
    }

    /// <summary>4-bit edge mask (1=N 2=E 4=S 8=W) for shoreline foam / grass fringe.</summary>
    private static int TransitionMask(WorldMap map, int x, int y, int t)
    {
        bool water = Array.IndexOf(WaterIds, t) >= 0;
        if (!water && Array.IndexOf(FringeTargets, t) < 0) return 0;

        int mask = 0;
        Span<(int dx, int dy, int bit)> dirs = stackalloc[] { (0, -1, 1), (1, 0, 2), (0, 1, 4), (-1, 0, 8) };
        foreach (var (dx, dy, bit) in dirs)
        {
            int n = map.TileAt(x + dx, y + dy);
            if (n < 0) continue;
            if (water)
            {
                if (Array.IndexOf(WaterIds, n) < 0) mask |= bit; // any land side gets foam
            }
            else
            {
                if (Array.IndexOf(GrassyIds, n) >= 0) mask |= bit;
            }
        }
        return mask;
    }

    /// <summary>World-unit position (sim space) → pixel position.</summary>
    public static Vector2 UnitsToPixels(int ux, int uy)
        => new(ux * (float)TilePx / SimConstants.UnitsPerTile,
               uy * (float)TilePx / SimConstants.UnitsPerTile);
}
