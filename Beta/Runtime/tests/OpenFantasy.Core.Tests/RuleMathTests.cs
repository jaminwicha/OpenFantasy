namespace OpenFantasy.Core.Tests;

public sealed class RuleMathTests
{
    [Theory]
    [InlineData(3, 5000, 1)]
    [InlineData(-3, 5000, -1)]
    [InlineData(3, -5000, -1)]
    [InlineData(-3, -5000, 1)]
    [InlineData(long.MaxValue, 10000, long.MaxValue)]
    [InlineData(long.MinValue, 10000, long.MinValue)]
    public void BasisPointsUseWideIntermediateAndRoundTowardZero(long value, long multiplier, long expected)
        => Assert.Equal(expected, RuleMath.ApplyBasisPoints(value, multiplier));

    [Fact]
    public void BasisPointResultOverflowRejects()
    {
        Assert.Throws<OverflowException>(() => RuleMath.ApplyBasisPoints(long.MaxValue, 10001));
        Assert.Throws<OverflowException>(() => RuleMath.ApplyBasisPoints(long.MinValue, -10000));
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(10000, 100, 100)]
    [InlineData(10000, 101, 100)]
    [InlineData(10000, 99, 102)]
    [InlineData(long.MaxValue, 2, 4611686018427387904)]
    [InlineData(long.MaxValue, long.MaxValue, 1)]
    public void DurationsRoundUpWithoutOverflow(long duration, long divisor, long expected)
        => Assert.Equal(expected, RuleMath.DivideDurationCeiling(duration, divisor));

    [Fact]
    public void InvalidDurationInputsReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleMath.DivideDurationCeiling(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleMath.DivideDurationCeiling(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleMath.DivideDurationCeiling(1, -1));
    }
}
