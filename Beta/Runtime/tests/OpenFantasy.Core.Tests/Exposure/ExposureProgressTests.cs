using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Tests.Exposure;

public sealed class ExposureProgressTests
{
    private static EntityId Id(ulong value) => new(0, value);
    private static ExposureRate Rate(ulong numerator = 1, ulong denominator = 3) =>
        new(Id(2), ContentHash.Compute("exposure-rules-v1"u8), numerator, denominator);

    [Fact]
    public void IndependentArithmeticAndCanonicalVectorsMatch()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "exposure-v1.json")));
        foreach (JsonElement sample in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            UInt128 total = UInt128.Parse(sample.GetProperty("milliseconds").GetString()!, CultureInfo.InvariantCulture);
            var duration = new LocalExposureDuration((ulong)(total / LocalExposureDuration.MillisecondsPerDay),
                (uint)(total % LocalExposureDuration.MillisecondsPerDay));
            ExposureRate rate = Rate(ulong.Parse(sample.GetProperty("numerator").GetString()!, CultureInfo.InvariantCulture),
                ulong.Parse(sample.GetProperty("denominator").GetString()!, CultureInfo.InvariantCulture));
            ExposureProgress result = ExposureProgress.Begin(Id(1), rate).Advance(duration);
            Assert.Equal(sample.GetProperty("units").GetString(), result.CompletedUnits.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(sample.GetProperty("remainder").GetString(), result.Remainder.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(sample.GetProperty("hex").GetString(),
                Convert.ToHexString(ExposureEncodingV1.Encode(result.Capture())).ToLowerInvariant());
            Assert.Equal(sample.GetProperty("hash").GetString(), result.ComputeContentHash().Value);
        }
    }

    [Fact]
    public void PartitionedExposureAndCapturedRemainderMatchOneInterval()
    {
        ExposureRate rate = Rate(7, 13);
        ExposureProgress start = ExposureProgress.Begin(Id(1), rate);
        ExposureProgress whole = start.Advance(LocalExposureDuration.FromMilliseconds(10_003));
        ExposureProgress pieces = start;
        foreach (ulong part in new ulong[] { 1, 17, 999, 2, 8984 })
        {
            pieces = ExposureProgress.Restore(pieces.Capture(), rate)
                .Advance(LocalExposureDuration.FromMilliseconds(part));
        }
        Assert.Equal(whole.ComputeContentHash(), pieces.ComputeContentHash());
        Assert.Equal(LocalExposureDuration.Zero, start.Elapsed);
    }

    [Fact]
    public void ZeroAcceptedProgressDoesNotAccrueOrQueueLaterCatchUp()
    {
        ExposureProgress one = ExposureProgress.Begin(Id(1), Rate())
            .Advance(LocalExposureDuration.FromMilliseconds(1));
        for (int held = 0; held < 100; held++)
        {
            Assert.Same(one, one.Advance(ExposurePacingV1.FromExplorationSteps(0)));
        }
        ExposureProgress next = one.Advance(LocalExposureDuration.FromMilliseconds(1));
        Assert.Equal(0UL, next.CompletedUnits);
        Assert.Equal(2UL, next.Remainder);
        Assert.Equal(2U, next.Elapsed.MillisecondsWithinDay);
    }

    [Fact]
    public void SelectedExplorationAndTacticalPacingConvertOnlyTheirOwnAcceptedUnits()
    {
        Assert.Equal(new LocalExposureDuration(1, 0), ExposurePacingV1.FromExplorationSteps(48_000));
        Assert.Equal(LocalExposureDuration.FromMilliseconds(50), ExposurePacingV1.FromTacticalUnits(5));
        Assert.Equal(LocalExposureDuration.FromMilliseconds(1000), ExposurePacingV1.FromTacticalUnits(100));
        Assert.Equal(1_800U, ExposurePacingV1.FromExplorationSteps(1).MillisecondsWithinDay);
    }

    [Fact]
    public void EonScaleLowRateAndMaximumProductsRemainExact()
    {
        ExposureProgress slow = ExposureProgress.Begin(Id(1), Rate(1, ulong.MaxValue))
            .Advance(new LocalExposureDuration(ulong.MaxValue, 0));
        Assert.Equal(86_400_000UL, slow.CompletedUnits);
        Assert.Equal(0UL, slow.Remainder);
        ExposureProgress huge = ExposureProgress.Begin(Id(1), Rate(ulong.MaxValue, ulong.MaxValue))
            .Advance(LocalExposureDuration.FromMilliseconds(ulong.MaxValue));
        Assert.Equal(ulong.MaxValue, huge.CompletedUnits);
        Assert.Equal(0UL, huge.Remainder);
    }

    [Fact]
    public void InvalidRatesCarriesAndRestoredTotalsReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rate(1, 0));
        ExposureProgress valid = ExposureProgress.Begin(Id(1), Rate()).Advance(LocalExposureDuration.FromMilliseconds(1));
        ExposureProgressState state = valid.Capture();
        Assert.Throws<ArgumentException>(() => ExposureProgress.Restore(state with { CompletedUnits = 1 }, valid.Rate));
        Assert.Throws<ArgumentException>(() => ExposureProgress.Restore(state with { Remainder = 3 }, valid.Rate));
        Assert.Throws<ArgumentException>(() => ExposureProgress.Restore(state with { Elapsed = default }, valid.Rate));
        Assert.Throws<ArgumentException>(() => ExposureProgress.Restore(state, Rate(1, 4)));
        Assert.Throws<ArgumentException>(() => ExposureEncodingV1.Encode(state with { Remainder = 3 }));
        ExposureRate newRules = new(Id(2), ContentHash.Compute("changed rules"u8), 1, 3);
        Assert.Throws<ArgumentException>(() => ExposureProgress.Restore(state, newRules));
    }

    [Fact]
    public void OverflowPreservesOriginalAndZeroRateRetainsElapsedTime()
    {
        ExposureProgress maximum = ExposureProgress.Begin(Id(1), Rate(ulong.MaxValue, 1))
            .Advance(LocalExposureDuration.FromMilliseconds(1));
        ContentHash before = maximum.ComputeContentHash();
        Assert.Throws<OverflowException>(() => maximum.Advance(LocalExposureDuration.FromMilliseconds(1)));
        Assert.Equal(before, maximum.ComputeContentHash());
        ExposureProgress stopped = ExposureProgress.Begin(Id(1), Rate(0, 7))
            .Advance(new LocalExposureDuration(ulong.MaxValue, 86_399_999));
        Assert.Equal(0UL, stopped.CompletedUnits);
        Assert.Throws<OverflowException>(() => stopped.Advance(LocalExposureDuration.FromMilliseconds(1)));
        Assert.Equal(ulong.MaxValue, stopped.Elapsed.Days);
    }

    [Fact]
    public void DurationJsonAndNormalizationCannotSilentlyResetExperiencedTime()
    {
        var duration = new LocalExposureDuration(9000, 17);
        Assert.Equal(duration, JsonSerializer.Deserialize<LocalExposureDuration>(JsonSerializer.Serialize(duration)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalExposureDuration(0, 86_400_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => JsonSerializer.Deserialize<LocalExposureDuration>(
            "{\"Days\":1,\"MillisecondsWithinDay\":86400000}"));
        Assert.Equal(new LocalExposureDuration(2, 0), new LocalExposureDuration(1, 86_399_999)
            .Add(LocalExposureDuration.FromMilliseconds(1)));
    }
}
