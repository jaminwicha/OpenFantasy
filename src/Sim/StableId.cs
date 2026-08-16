namespace OpenFantasy.Sim;

/// <summary>
/// Stable content addressing (plan D3/D8): every procedural entity derives its
/// identity by hashing the world seed with its generative coordinates and a
/// domain tag. Pure integer math — safe for cross-platform determinism and
/// re-derivable at any time (nothing procedural is stored, only re-derived).
/// </summary>
public static class StableId
{
    /// <summary>SplitMix64 finalizer — the mixing primitive for all derivation.</summary>
    public static ulong Mix(ulong h)
    {
        h ^= h >> 30;
        h *= 0xBF58476D1CE4E5B9UL;
        h ^= h >> 27;
        h *= 0x94D049BB133111EBUL;
        h ^= h >> 31;
        return h;
    }

    public static ulong Combine(ulong seed, ulong a)
        => Mix(seed ^ Mix(a + 0x9E3779B97F4A7C15UL));

    public static ulong Combine(ulong seed, long a, long b)
        => Combine(Combine(seed, (ulong)a), (ulong)b);

    public static ulong Combine(ulong seed, long a, long b, long c)
        => Combine(Combine(seed, a, b), (ulong)c);

    /// <summary>Sub-seed for a named generation domain ("elevation", "sites", …).</summary>
    public static ulong Domain(ulong seed, string tag)
    {
        ulong h = 1469598103934665603UL; // FNV-1a offset basis
        foreach (char c in tag)
        {
            h ^= c;
            h *= 1099511628211UL;
        }
        return Mix(seed ^ h);
    }
}
