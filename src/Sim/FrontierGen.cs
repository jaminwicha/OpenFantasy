namespace OpenFantasy.Sim;

/// <summary>
/// Procedural frontier terrain (8B): serves 32×32 chunks beyond the authored
/// Aldermere map through the WorldMap chunk seam. Pure function of
/// (worldSeed, chunk coords) — server and client generate identical chunks
/// from the seed carried in the Welcome message. Tiles reuse the shipped
/// tileset, so walkability and vehicle domain rules apply unchanged.
/// </summary>
public sealed class FrontierGen
{
    /// <summary>Forced-ocean ring around the static map (plan D6): the
    /// authored coast stays a coast, and every frontier voyage starts at sea.</summary>
    public const int BlendBand = 24;

    private readonly BiomeField _field;
    private readonly ulong _scatterSeed;
    private readonly WorldMap _map;
    private readonly int _grass, _tree, _shallow, _ocean, _forest, _rock, _crystal,
        _sand, _flowers, _mountain, _hills, _snow, _desert, _swamp, _deadTree, _mine;

    public FrontierGen(WorldMap map, ulong worldSeed)
    {
        _map = map;
        _field = new BiomeField(worldSeed);
        _scatterSeed = StableId.Domain(worldSeed, "scatter");
        _grass = map.TileIdByName("grass");
        _tree = map.TileIdByName("tree");
        _shallow = map.TileIdByName("shallow");
        _ocean = map.TileIdByName("ocean");
        _forest = map.TileIdByName("forest_grass");
        _rock = map.TileIdByName("rock");
        _crystal = map.TileIdByName("crystal");
        _sand = map.TileIdByName("sand");
        _flowers = map.TileIdByName("flowers");
        _mountain = map.TileIdByName("mountain");
        _hills = map.TileIdByName("hills");
        _snow = map.TileIdByName("snow");
        _desert = map.TileIdByName("desert");
        _swamp = map.TileIdByName("swamp");
        _deadTree = map.TileIdByName("dead_tree");
        _mine = map.TileIdByName("mine_floor");
    }

    public int[] GenerateChunk(int cx, int cy)
    {
        const int n = WorldMap.ChunkSize;
        var chunk = new int[n * n];
        for (int oy = 0; oy < n; oy++)
            for (int ox = 0; ox < n; ox++)
                chunk[oy * n + ox] = TileFor(cx * n + ox, cy * n + oy);
        return chunk;
    }

    private int TileFor(int tx, int ty)
    {
        // Anchor stitching: a sea ring hugs the static map.
        if (tx > -BlendBand && tx < _map.Width + BlendBand &&
            ty > -BlendBand && ty < _map.Height + BlendBand)
            return _ocean;

        ulong h = StableId.Combine(_scatterSeed, tx, ty);
        int Roll(int mod) => (int)(h % (ulong)mod);

        switch (_field.Classify(tx, ty))
        {
            case Biome.Ocean:
                return _field.Elevation(tx, ty) >= 26400 ? _shallow : _ocean;
            case Biome.Coast:
                return _sand;
            case Biome.Grassland:
                return Roll(37) == 0 ? _flowers : Roll(29) == 1 ? _tree : _grass;
            case Biome.Forest:
                return Roll(5) == 0 ? _tree : _forest;
            case Biome.Jungle:
                return Roll(3) == 0 ? _tree : _forest;
            case Biome.Swamp:
                return Roll(19) == 0 ? _deadTree : _swamp;
            case Biome.Desert:
                return _desert;
            case Biome.Canyon:
                return Roll(9) == 0 ? _rock : _desert;
            case Biome.Snowfield:
                return Roll(31) == 0 ? _rock : _snow;
            case Biome.Glacier:
                return Roll(6) == 0 ? _rock : _snow;
            case Biome.Hills:
                return Roll(4) == 0 ? _hills : _grass;
            case Biome.Mountain:
                return Roll(7) == 0 ? _rock : _mountain;
            case Biome.CrystalWaste:
                return Roll(13) == 0 ? _crystal : Roll(5) == 0 ? _rock : _mine;
            default:
                return _ocean;
        }
    }
}
