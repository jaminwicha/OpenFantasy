using System.Text.Json.Serialization;

namespace OpenFantasy.Core.Exposure;

/// <summary>Nonnegative experienced time, separate from calendar location and commit ordering.</summary>
public readonly record struct LocalExposureDuration
{
    public const uint MillisecondsPerDay = 86_400_000;

    [JsonConstructor]
    public LocalExposureDuration(ulong days, uint millisecondsWithinDay)
    {
        if (millisecondsWithinDay >= MillisecondsPerDay)
        {
            throw new ArgumentOutOfRangeException(nameof(millisecondsWithinDay));
        }
        Days = days;
        MillisecondsWithinDay = millisecondsWithinDay;
    }

    public ulong Days { get; }
    public uint MillisecondsWithinDay { get; }
    public UInt128 TotalMilliseconds => (UInt128)Days * MillisecondsPerDay + MillisecondsWithinDay;
    public static LocalExposureDuration Zero => default;

    public static LocalExposureDuration FromMilliseconds(ulong milliseconds) =>
        FromTotalMilliseconds(milliseconds);

    public LocalExposureDuration Add(LocalExposureDuration other) =>
        FromTotalMilliseconds(checked(TotalMilliseconds + other.TotalMilliseconds));

    internal static LocalExposureDuration FromTotalMilliseconds(UInt128 milliseconds) => new(
        checked((ulong)(milliseconds / MillisecondsPerDay)), (uint)(milliseconds % MillisecondsPerDay));
}
