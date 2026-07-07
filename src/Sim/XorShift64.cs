namespace OpenFantasy.Sim;

/// <summary>
/// Deterministic xorshift64* RNG. The world owns exactly one instance;
/// nothing else in the sim may source randomness.
/// </summary>
public sealed class XorShift64
{
    public ulong State { get; private set; }

    public XorShift64(ulong seed)
    {
        State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public ulong NextULong()
    {
        ulong x = State;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        State = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Uniform int in [0, exclusiveMax).</summary>
    public int Next(int exclusiveMax)
    {
        if (exclusiveMax <= 1) return 0;
        return (int)(NextULong() % (ulong)exclusiveMax);
    }

    /// <summary>Uniform int in [min, max] inclusive.</summary>
    public int Range(int min, int max) => min + Next(max - min + 1);
}
