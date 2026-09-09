using System.Numerics;
using System.Text.Json;

namespace OpenFantasy.Core.Tests;

public sealed class WorldInstantTests
{
    [Theory]
    [InlineData(0, 0, -1, -1, 86399999)]
    [InlineData(-1, 86399999, 1, 0, 0)]
    [InlineData(10, 1, -86400002, 8, 86399999)]
    [InlineData(-10, 86399999, 172800001, -7, 0)]
    public void CrossingDaysNormalizesNegativeAndPositiveTime(long days, int milliseconds, long delta,
        long expectedDays, int expectedMilliseconds)
    {
        Assert.Equal(new WorldInstant(expectedDays, expectedMilliseconds), new WorldInstant(days, milliseconds).AddMilliseconds(delta));
    }

    [Fact]
    public void JsonRoundTripRetainsAndValidatesStoredCalendarFields()
    {
        var instant = new WorldInstant(-9000000000000, 86399999);
        Assert.Equal(instant, JsonSerializer.Deserialize<WorldInstant>(JsonSerializer.Serialize(instant)));
        Assert.Throws<ArgumentOutOfRangeException>(() => JsonSerializer.Deserialize<WorldInstant>(
            "{\"Days\":5,\"MillisecondsWithinDay\":-1}"));
    }

    [Fact]
    public void StoredInstantsMustAlreadyBeNormalized()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldInstant(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldInstant(0, 86400000));
    }

    [Fact]
    public void OverflowRejectsWithoutWrappingDays()
    {
        Assert.Throws<OverflowException>(() => new WorldInstant(long.MaxValue, 86399999).AddMilliseconds(1));
        Assert.Throws<OverflowException>(() => new WorldInstant(long.MinValue, 0).AddMilliseconds(-1));
        Assert.Equal(new WorldInstant(long.MinValue, 1), new WorldInstant(long.MinValue, 0).AddMilliseconds(1));
    }

    [Fact]
    public void EonScaleDifferencesRemainExactBeyondInt64Milliseconds()
    {
        var start = new WorldInstant(long.MinValue, 0);
        var end = new WorldInstant(long.MaxValue, 86399999);
        Int128 expected = ((Int128)ulong.MaxValue * 86400000) + 86399999;
        Assert.Equal(expected, start.MillisecondsUntil(end));
        Assert.Equal(-expected, end.MillisecondsUntil(start));
        Assert.True(start < end);
    }

    [Fact]
    public void LongDeltasMatchIndependentBigIntegerArithmetic()
    {
        long[] days = [long.MinValue + 200000000000, -1, 0, long.MaxValue - 200000000000];
        long[] deltas = [long.MinValue, -86400001, -1, 0, 1, 86400001, long.MaxValue];
        foreach (long day in days)
        foreach (long delta in deltas)
        {
            BigInteger total = (BigInteger)day * 86400000 + 76543210 + delta;
            BigInteger quotient = BigInteger.DivRem(total, 86400000, out var remainder);
            if (remainder < 0) { quotient--; remainder += 86400000; }
            Assert.Equal(new WorldInstant((long)quotient, (int)remainder), new WorldInstant(day, 76543210).AddMilliseconds(delta));
        }
    }
}
