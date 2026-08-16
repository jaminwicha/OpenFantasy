namespace OpenFantasy.Sim;

/// <summary>
/// Biome classification per design §2.2: layered elevation / temperature /
/// moisture / anomaly fields evaluated per tile, all pure functions of the
/// world seed (plan D3). Frontier terrain synthesis (8B) maps these to tiles;
/// 7A ships the fields, the classifier, and preview/verification hooks.
/// </summary>
public enum Biome
{
    Ocean,
    Coast,
    Grassland,
    Forest,
    Jungle,
    Swamp,
    Desert,
    Canyon,
    Snowfield,
    Glacier,
    Hills,
    Mountain,
    CrystalWaste,
}

public sealed class BiomeField
{
    private readonly ulong _elevSeed, _tempSeed, _moistSeed, _anomalySeed;

    public BiomeField(ulong worldSeed)
    {
        _elevSeed = StableId.Domain(worldSeed, "elevation");
        _tempSeed = StableId.Domain(worldSeed, "temperature");
        _moistSeed = StableId.Domain(worldSeed, "moisture");
        _anomalySeed = StableId.Domain(worldSeed, "anomaly");
    }

    // Feature periods in tiles (powers of two so Q16 scaling is a shift).
    // elevation 512, temperature 1024, moisture 256, anomaly 128.
    public int Elevation(long tx, long ty) => Noise.Fbm2(_elevSeed, tx << 7, ty << 7, 5);
    public int Temperature(long tx, long ty) => Noise.Fbm2(_tempSeed, tx << 6, ty << 6, 3);
    public int Moisture(long tx, long ty) => Noise.Fbm2(_moistSeed, tx << 8, ty << 8, 4);
    public int Anomaly(long tx, long ty) => Noise.Fbm2(_anomalySeed, tx << 9, ty << 9, 3);

    public Biome Classify(long tx, long ty)
    {
        int e = Elevation(tx, ty);
        if (e < 27500) return Biome.Ocean;
        if (e < 29500) return Biome.Coast;
        if (e > 47000) return Biome.Mountain;
        if (e > 43000) return Biome.Hills;

        // Aethertech anomaly overrides climate where it runs hot.
        if (Anomaly(tx, ty) > 51000) return Biome.CrystalWaste;

        // Thresholds sit on measured percentiles of the FBM fields (which
        // concentrate near mid-range) — see selftest distribution case.
        int t = Temperature(tx, ty);
        int m = Moisture(tx, ty);
        if (t > 38500) // hot
        {
            if (m < 30000) return Biome.Desert;
            if (m < 34500) return Biome.Canyon;
            return Biome.Jungle;
        }
        if (t < 28000) // cold
            return m > 40000 ? Biome.Glacier : Biome.Snowfield;
        // temperate
        if (m > 44000) return Biome.Swamp;
        if (m > 33500) return Biome.Forest;
        return Biome.Grassland;
    }

    /// <summary>
    /// FNV-1a hash of the biome classification over a square sample — the
    /// golden-seed drift detector used by the selftest and preview tooling.
    /// </summary>
    public ulong SampleHash(long x0, long y0, int size, int step)
    {
        ulong h = 1469598103934665603UL;
        for (long ty = y0; ty < y0 + size; ty += step)
            for (long tx = x0; tx < x0 + size; tx += step)
            {
                h ^= (byte)Classify(tx, ty);
                h *= 1099511628211UL;
            }
        return h;
    }
}
