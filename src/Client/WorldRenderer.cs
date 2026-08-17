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
        new[] { new Vector2I(3, 5) },                                           // 24 plank floor
        new[] { new Vector2I(4, 5) },                                           // 25 counter
        new[] { new Vector2I(5, 5) },                                           // 26 fence
        new[] { new Vector2I(6, 5) },                                           // 27 farmland
        new[] { new Vector2I(0, 6) },                                           // 28 grate
        new[] { new Vector2I(1, 6) },                                           // 29 stairs down
        new[] { new Vector2I(2, 6) },                                           // 30 stairs up
        new[] { new Vector2I(3, 6) },                                           // 31 chest
        new[] { new Vector2I(4, 6) },                                           // 32 door
        new[] { new Vector2I(5, 6) },                                           // 33 locked door
        new[] { new Vector2I(6, 6) },                                           // 34 cracked rock (secret tell)
        new[] { new Vector2I(7, 6) },                                           // 35 slag
        new[] { new Vector2I(8, 6) },                                           // 36 rubble
        new[] { new Vector2I(9, 6) },                                           // 37 chasm
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

        var ground = _ground = new TileMapLayer { TileSet = tileSet };
        var overlay = _overlay = new TileMapLayer { TileSet = tileSet, ZIndex = 1 };
        var corners = _corners = new TileMapLayer { TileSet = tileSet, ZIndex = 1 };

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
                bool water = Array.IndexOf(WaterIds, t) >= 0;
                if (mask > 0)
                    overlay.SetCell(new Vector2I(x, y), 0, new Vector2I(mask, water ? 2 : 3));

                // Corner-aware blob pass: diagonal contacts not already covered
                // by an edge (rows 7/8) round the transitions off.
                int corner = CornerMask(map, x, y, t, mask);
                if (corner > 0)
                    corners.SetCell(new Vector2I(x, y), 0, new Vector2I(corner, water ? 7 : 8));
            }
        AddChild(ground);
        AddChild(overlay);
        AddChild(corners);
    }

    /// <summary>4-bit diagonal mask (1=NE 2=SE 4=SW 8=NW): set when the
    /// diagonal neighbor transitions but neither adjacent edge does (those
    /// are already foamed/fringed by the edge overlay).</summary>
    private static int CornerMask(WorldMap map, int x, int y, int t, int edgeMask)
    {
        bool water = Array.IndexOf(WaterIds, t) >= 0;
        if (!water && Array.IndexOf(FringeTargets, t) < 0) return 0;

        bool Hit(int nx, int ny)
        {
            int n = map.TileAt(nx, ny);
            if (n < 0) return false;
            return water ? Array.IndexOf(WaterIds, n) < 0 : Array.IndexOf(GrassyIds, n) >= 0;
        }

        int mask = 0;
        // bit layout: edge 1=N 2=E 4=S 8=W
        if (Hit(x + 1, y - 1) && (edgeMask & 3) == 0) mask |= 1;  // NE needs N,E clear
        if (Hit(x + 1, y + 1) && (edgeMask & 6) == 0) mask |= 2;  // SE needs E,S clear
        if (Hit(x - 1, y + 1) && (edgeMask & 12) == 0) mask |= 4; // SW needs S,W clear
        if (Hit(x - 1, y - 1) && (edgeMask & 9) == 0) mask |= 8;  // NW needs W,N clear
        return mask;
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

    // ------------------------------------------------------- frontier streaming

    private TileMapLayer? _ground, _overlay, _corners;
    private readonly HashSet<long> _builtFrontier = new();

    /// <summary>Draws frontier chunks around the player as they explore.
    /// Chunks fault through Map.TileAt (client-side FrontierGen installed on
    /// Welcome), so server and client always agree tile-for-tile.</summary>
    public void StreamFrontierAround(int tx, int ty)
    {
        if (_ground is null) return;
        const int radius = 4; // chunks (128 tiles) around the player
        int pcx = tx >> 5, pcy = ty >> 5;
        int staticCw = Map.Width / WorldMap.ChunkSize, staticCh = Map.Height / WorldMap.ChunkSize;
        for (int cy = pcy - radius; cy <= pcy + radius; cy++)
            for (int cx = pcx - radius; cx <= pcx + radius; cx++)
            {
                if (cx >= 0 && cy >= 0 && cx < staticCw && cy < staticCh) continue; // authored
                long key = ((long)cx << 32) ^ (uint)cy;
                if (!_builtFrontier.Add(key)) continue;
                BuildFrontierChunk(cx, cy);
            }
    }

    private void BuildFrontierChunk(int cx, int cy)
    {
        const int n = WorldMap.ChunkSize;
        for (int oy = 0; oy < n; oy++)
            for (int ox = 0; ox < n; ox++)
            {
                int x = cx * n + ox, y = cy * n + oy;
                int t = Map.TileAt(x, y);
                if (t < 0 || t >= Variants.Length) continue;
                var options = Variants[t];
                int v = ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
                _ground!.SetCell(new Vector2I(x, y), 0, options[v % options.Length]);
                int mask = TransitionMask(Map, x, y, t);
                bool water = Array.IndexOf(WaterIds, t) >= 0;
                if (mask > 0)
                    _overlay!.SetCell(new Vector2I(x, y), 0, new Vector2I(mask, water ? 2 : 3));
                int corner = CornerMask(Map, x, y, t, mask);
                if (corner > 0)
                    _corners!.SetCell(new Vector2I(x, y), 0, new Vector2I(corner, water ? 7 : 8));
            }
    }

    // ------------------------------------------------------- instanced floors

    private Node2D? _overworldRoot;
    private TileMapLayer? _instanceLayer;

    /// <summary>SiteTile (byte) → logical tile id (M7 delve tile set, row 6).</summary>
    private static readonly int[] SiteTileToLogical =
    {
        8,  // Wall        → rock
        7,  // Floor       → mine floor
        29, // StairsDown
        30, // StairsUp
        34, // SecretDoor  → cracked rock: the tell for the attentive
        32, // Door
        33, // LockedDoor
        2,  // Water       → shallow water
        35, // Slag        → glowing slag crust
        36, // Rubble
        37, // Chasm
        31, // Chest
    };

    /// <summary>Swap between the overworld and an instanced floor.</summary>
    public void SetInstance(Net.Protocol.MapDataMsg? map)
    {
        if (_overworldRoot is null)
        {
            // First call: adopt the already-built overworld children.
            _overworldRoot = new Node2D();
            var mine = new List<Node>();
            foreach (var child in GetChildren())
                if (child is TileMapLayer)
                    mine.Add(child);
            foreach (var child in mine)
            {
                RemoveChild(child);
                _overworldRoot.AddChild(child);
            }
            AddChild(_overworldRoot);
        }

        _instanceLayer?.QueueFree();
        _instanceLayer = null;
        _overworldRoot.Visible = map is null;
        if (map is null) return;

        var layer = new TileMapLayer { TileSet = ((TileMapLayer)_overworldRoot.GetChild(0)).TileSet };
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                int logical = SiteTileToLogical[Math.Min(map.Tiles[y * map.Width + x], (byte)(SiteTileToLogical.Length - 1))];
                var options = Variants[logical];
                int v = ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
                layer.SetCell(new Vector2I(x, y), 0, options[v % options.Length]);
            }
        _instanceLayer = layer;
        AddChild(layer);
    }
}
