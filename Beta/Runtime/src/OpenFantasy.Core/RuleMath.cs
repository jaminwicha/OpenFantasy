namespace OpenFantasy.Core;

/// <summary>Checked rule arithmetic; named rule caps and modifier ordering belong to their rule owner.</summary>
public static class RuleMath
{
    public const long BasisPointScale = 10_000;

    /// <summary>Applies one multiplier using a signed 128-bit product and rounding toward zero.</summary>
    public static long ApplyBasisPoints(long value, long multiplier) =>
        checked((long)(checked((Int128)value * multiplier) / BasisPointScale));

    /// <summary>Divides a nonnegative duration by a positive rate, rounding upward.</summary>
    public static long DivideDurationCeiling(long duration, long divisor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(divisor);
        long quotient = duration / divisor;
        return duration % divisor == 0 ? quotient : checked(quotient + 1);
    }
}
