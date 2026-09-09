using System.Text.Json;

namespace OpenFantasy.Sim;

public sealed class TileDef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Walkable { get; set; }
}

public struct TileRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }

    public readonly bool Contains(int tx, int ty)
        => tx >= X && ty >= Y && tx < X + W && ty < Y + H;
}

public sealed class SpawnerDef
{
    public int Id { get; set; }
    public string Monster { get; set; } = "";
    public TileRect Region { get; set; }
    public int Count { get; set; }
    public int RespawnTicks { get; set; }
}

public sealed class PointDef
{
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>Named map region (towns, dungeons, quest-explore targets).</summary>
public sealed class RegionDef
{
    public string Id { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }

    public TileRect Rect => new() { X = X, Y = Y, W = W, H = H };
}

/// <summary>Resource node placement (gathering): oak / ore / berry regions.</summary>
public sealed class NodeDef
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public TileRect Region { get; set; }
    public int Count { get; set; }
    public int RespawnTicks { get; set; }
}

/// <summary>
/// The static world: tile grid (RLE-decoded), tileset walkability, safe zone,
/// player spawn, and monster spawner definitions. Shared verbatim by server
/// (authority) and client (rendering).
/// </summary>
public sealed class WorldMap
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public List<TileDef> Tileset { get; private set; } = new();
    public List<TileRect> SafeZones { get; private set; } = new();
    public List<PointDef> Crystals { get; private set; } = new();
    public List<RegionDef> Regions { get; private set; } = new();
    public int PlayerSpawnX { get; private set; }
    public int PlayerSpawnY { get; private set; }
    public List<SpawnerDef> Spawners { get; private set; } = new();
    public List<NodeDef> Nodes { get; private set; } = new();

    private readonly Dictionary<string, RegionDef> _regionsById = new();
    private readonly Dictionary<string, int> _tileIdsByName = new();

    public RegionDef? Region(string id) => _regionsById.GetValueOrDefault(id);

    /// <summary>Tile id for a tileset name, or -1 (names are authoring-stable).</summary>
    public int TileIdByName(string name) => _tileIdsByName.GetValueOrDefault(name, -1);

    // -- Chunk store (8A) ---------------------------------------------------
    // The static map is split into 32×32 chunks at load; queries route
    // through the chunk store so a procedural frontier source (8B) can serve
    // chunks beyond the authored bounds through the same seam.

    public const int ChunkSize = 32;
    private readonly Dictionary<long, int[]> _chunks = new();
    private bool[] _walkableByTileId = Array.Empty<bool>();

    /// <summary>Procedural source for chunks outside the static bounds
    /// (installed by the World in 8B; null = hard edge).</summary>
    private Func<int, int, int[]>? _frontierSource;

    private static long ChunkKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

    public void SetFrontierSource(Func<int, int, int[]>? generate) => _frontierSource = generate;

    /// <summary>True when the coordinate lies in the authored static map.</summary>
    public bool InStaticBounds(int tx, int ty)
        => tx >= 0 && ty >= 0 && tx < Width && ty < Height;

    public int TileAt(int tx, int ty)
    {
        if (!InStaticBounds(tx, ty))
        {
            if (_frontierSource is null) return -1;
            // Plan D2: finite-but-vast — beyond ±1M tiles is hard void.
            if (tx is < -1048576 or > 1048576 || ty is < -1048576 or > 1048576) return -1;
            int fcx = tx >> 5, fcy = ty >> 5;
            if (!_chunks.TryGetValue(ChunkKey(fcx, fcy), out int[]? chunk))
            {
                chunk = _frontierSource(fcx, fcy);
                _chunks[ChunkKey(fcx, fcy)] = chunk;
            }
            return chunk[(ty & 31) * ChunkSize + (tx & 31)];
        }
        return _chunks[ChunkKey(tx >> 5, ty >> 5)][(ty & 31) * ChunkSize + (tx & 31)];
    }

    /// <summary>FNV-1a over the static tile grid — the 8A refactor gate:
    /// chunked storage must reproduce world.json byte-identically.</summary>
    public ulong StaticTileHash()
    {
        ulong h = 14695981039346656037UL;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                h ^= (byte)TileAt(x, y);
                h *= 1099511628211UL;
            }
        return h;
    }

    public bool IsWalkableTile(int tx, int ty)
    {
        int t = TileAt(tx, ty);
        return t >= 0 && t < _walkableByTileId.Length && _walkableByTileId[t];
    }

    /// <summary>Walkability at a world-unit position.</summary>
    public bool IsWalkableUnits(int ux, int uy)
        => IsWalkableTile(SimConstants.TileOf(ux), SimConstants.TileOf(uy));

    public bool IsSafeZoneTile(int tx, int ty)
    {
        foreach (var zone in SafeZones)
            if (zone.Contains(tx, ty))
                return true;
        return false;
    }

    public bool IsSafeZoneUnits(int ux, int uy)
        => IsSafeZoneTile(SimConstants.TileOf(ux), SimConstants.TileOf(uy));

    // -- Serialized shape ---------------------------------------------------

    private sealed class WorldFile
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<TileDef> Tileset { get; set; } = new();
        /// <summary>Per row: flat list of [tileId, runLength] pairs.</summary>
        public List<List<int>> Rows { get; set; } = new();
        public Dictionary<string, int> PlayerSpawn { get; set; } = new();
        public List<TileRect> SafeZones { get; set; } = new();
        public List<PointDef> Crystals { get; set; } = new();
        public List<RegionDef> Regions { get; set; } = new();
        public List<SpawnerDef> Spawners { get; set; } = new();
        public List<NodeDef> Nodes { get; set; } = new();
    }

    public static WorldMap LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new DataValidationException($"missing content file: {path}");
        WorldFile file;
        try
        {
            file = JsonSerializer.Deserialize<WorldFile>(File.ReadAllText(path), GameData.JsonOptions)
                   ?? throw new DataValidationException($"{path}: file parsed to null");
        }
        catch (JsonException e)
        {
            throw new DataValidationException($"{path}: invalid JSON — {e.Message}");
        }
        return FromFileModel(file, path);
    }

    private static WorldMap FromFileModel(WorldFile file, string path)
    {
        var map = new WorldMap
        {
            Width = file.Width,
            Height = file.Height,
            Tileset = file.Tileset,
            SafeZones = file.SafeZones,
            Crystals = file.Crystals,
            Regions = file.Regions,
            Spawners = file.Spawners,
            Nodes = file.Nodes,
        };
        foreach (var region in file.Regions)
        {
            if (region.Id.Length == 0 || !map._regionsById.TryAdd(region.Id, region))
                throw new DataValidationException($"{path}: bad/duplicate region id '{region.Id}'");
        }
        foreach (var tile in file.Tileset)
            map._tileIdsByName[tile.Name] = tile.Id;

        if (file.Width <= 0 || file.Height <= 0)
            throw new DataValidationException($"{path}: width/height must be positive");
        if (file.Rows.Count != file.Height)
            throw new DataValidationException($"{path}: expected {file.Height} rows, got {file.Rows.Count}");
        if (file.Tileset.Count == 0)
            throw new DataValidationException($"{path}: tileset is empty");

        int maxTileId = file.Tileset.Max(t => t.Id);
        map._walkableByTileId = new bool[maxTileId + 1];
        var seen = new HashSet<int>();
        foreach (var t in file.Tileset)
        {
            if (t.Id < 0 || !seen.Add(t.Id))
                throw new DataValidationException($"{path}: bad/duplicate tile id {t.Id}");
            map._walkableByTileId[t.Id] = t.Walkable;
        }

        var tiles = new int[file.Width * file.Height];
        for (int y = 0; y < file.Height; y++)
        {
            var row = file.Rows[y];
            if (row.Count % 2 != 0)
                throw new DataValidationException($"{path}: row {y} RLE list has odd length");
            int x = 0;
            for (int i = 0; i < row.Count; i += 2)
            {
                int tileId = row[i];
                int run = row[i + 1];
                if (tileId < 0 || tileId > maxTileId || !seen.Contains(tileId))
                    throw new DataValidationException($"{path}: row {y} references unknown tile id {tileId}");
                if (run <= 0 || x + run > file.Width)
                    throw new DataValidationException($"{path}: row {y} RLE overruns width at x={x}");
                for (int k = 0; k < run; k++)
                    tiles[y * file.Width + x + k] = tileId;
                x += run;
            }
            if (x != file.Width)
                throw new DataValidationException($"{path}: row {y} RLE covers {x} of {file.Width} columns");
        }

        // 8A: split the decoded grid into 32×32 chunks (the static map's
        // dimensions are chunk-aligned; partial edge chunks pad with -1).
        for (int cy = 0; cy * ChunkSize < file.Height; cy++)
            for (int cx = 0; cx * ChunkSize < file.Width; cx++)
            {
                var chunk = new int[ChunkSize * ChunkSize];
                for (int oy = 0; oy < ChunkSize; oy++)
                    for (int ox = 0; ox < ChunkSize; ox++)
                    {
                        int tx = cx * ChunkSize + ox, ty = cy * ChunkSize + oy;
                        chunk[oy * ChunkSize + ox] =
                            tx < file.Width && ty < file.Height ? tiles[ty * file.Width + tx] : -1;
                    }
                map._chunks[ChunkKey(cx, cy)] = chunk;
            }

        if (!file.PlayerSpawn.TryGetValue("x", out int sx) || !file.PlayerSpawn.TryGetValue("y", out int sy))
            throw new DataValidationException($"{path}: playerSpawn needs x and y");
        map.PlayerSpawnX = sx;
        map.PlayerSpawnY = sy;
        return map;
    }
}
