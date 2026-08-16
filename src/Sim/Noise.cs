namespace OpenFantasy.Sim;

/// <summary>
/// Deterministic integer value noise (plan D1): fixed-point Q16 coordinates,
/// hash-derived lattice values, integer smoothstep interpolation, FBM octaves.
/// No floats, no transcendentals — identical output on every platform.
/// Outputs are in [0, 65536).
/// </summary>
public static class Noise
{
    public const int FracBits = 16;
    public const long One = 1L << FracBits;

    /// <summary>Lattice value in [0, 65536) at an integer grid point.</summary>
    private static long Lattice(ulong seed, long x, long y)
        => (long)(StableId.Combine(seed, x, y) & 0xFFFF);

    /// <summary>Integer smoothstep: t in [0, One] → s in [0, One].</summary>
    private static long SmoothStep(long t)
        => (t * t * (3 * One - 2 * t)) >> (2 * FracBits);

    private static long Lerp(long a, long b, long s)
        => a + (((b - a) * s) >> FracBits);

    /// <summary>Bilinear value noise at Q16 fixed-point coordinates.</summary>
    public static int Value2(ulong seed, long fx, long fy)
    {
        long x0 = fx >> FracBits;   // arithmetic shift: correct floor for negatives
        long y0 = fy >> FracBits;
        long tx = fx & (One - 1);
        long ty = fy & (One - 1);

        long sx = SmoothStep(tx);
        long sy = SmoothStep(ty);

        long v00 = Lattice(seed, x0, y0);
        long v10 = Lattice(seed, x0 + 1, y0);
        long v01 = Lattice(seed, x0, y0 + 1);
        long v11 = Lattice(seed, x0 + 1, y0 + 1);

        return (int)Lerp(Lerp(v00, v10, sx), Lerp(v01, v11, sx), sy);
    }

    /// <summary>
    /// Fractional Brownian motion: octaves of value noise, each at double
    /// frequency and half amplitude, normalized back to [0, 65536).
    /// </summary>
    public static int Fbm2(ulong seed, long fx, long fy, int octaves)
    {
        long total = 0, ampSum = 0, amp = One;
        for (int o = 0; o < octaves; o++)
        {
            total += Value2(StableId.Combine(seed, (ulong)o), fx, fy) * amp;
            ampSum += amp;
            amp >>= 1;
            fx <<= 1;
            fy <<= 1;
        }
        return (int)(total / ampSum);
    }
}
