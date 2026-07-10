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
    public TileRect SafeZone { get; private set; }
    public int PlayerSpawnX { get; private set; }
    public int PlayerSpawnY { get; private set; }
    public List<SpawnerDef> Spawners { get; private set; } = new();
    public List<NodeDef> Nodes { get; private set; } = new();

    private int[] _tiles = Array.Empty<int>();
    private bool[] _walkableByTileId = Array.Empty<bool>();

    public int TileAt(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) return -1;
        return _tiles[ty * Width + tx];
    }

    public bool IsWalkableTile(int tx, int ty)
    {
        int t = TileAt(tx, ty);
        return t >= 0 && t < _walkableByTileId.Length && _walkableByTileId[t];
    }

    /// <summary>Walkability at a world-unit position.</summary>
    public bool IsWalkableUnits(int ux, int uy)
        => IsWalkableTile(ux / SimConstants.UnitsPerTile, uy / SimConstants.UnitsPerTile);

    public bool IsSafeZoneUnits(int ux, int uy)
        => SafeZone.Contains(ux / SimConstants.UnitsPerTile, uy / SimConstants.UnitsPerTile);

    // -- Serialized shape ---------------------------------------------------

    private sealed class WorldFile
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<TileDef> Tileset { get; set; } = new();
        /// <summary>Per row: flat list of [tileId, runLength] pairs.</summary>
        public List<List<int>> Rows { get; set; } = new();
        public Dictionary<string, int> PlayerSpawn { get; set; } = new();
        public TileRect SafeZone { get; set; }
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
            SafeZone = file.SafeZone,
            Spawners = file.Spawners,
            Nodes = file.Nodes,
        };

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

        map._tiles = new int[file.Width * file.Height];
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
                    map._tiles[y * file.Width + x + k] = tileId;
                x += run;
            }
            if (x != file.Width)
                throw new DataValidationException($"{path}: row {y} RLE covers {x} of {file.Width} columns");
        }

        if (!file.PlayerSpawn.TryGetValue("x", out int sx) || !file.PlayerSpawn.TryGetValue("y", out int sy))
            throw new DataValidationException($"{path}: playerSpawn needs x and y");
        map.PlayerSpawnX = sx;
        map.PlayerSpawnY = sy;
        return map;
    }
}
