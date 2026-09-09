using System.Globalization;
using System.Text.Json.Serialization;

namespace OpenFantasy.Core;

/// <summary>
/// Exact calendar time represented as signed days and normalized milliseconds.
/// This value has no wall-clock, pause, exposure or branch ownership behavior.
/// </summary>
public readonly record struct WorldInstant : IComparable<WorldInstant>
{
    public const int MillisecondsPerDay = 86_400_000;
    public long Days { get; }
    public int MillisecondsWithinDay { get; }

    [JsonConstructor]
    public WorldInstant(long days, int millisecondsWithinDay)
    {
        if (millisecondsWithinDay is < 0 or >= MillisecondsPerDay)
            throw new ArgumentOutOfRangeException(nameof(millisecondsWithinDay),
                "Milliseconds within a day must be between 0 and 86399999 inclusive.");
        Days = days;
        MillisecondsWithinDay = millisecondsWithinDay;
    }

    public WorldInstant AddMilliseconds(long milliseconds)
    {
        Int128 total = checked((Int128)Days * MillisecondsPerDay + MillisecondsWithinDay + milliseconds);
        Int128 days = total / MillisecondsPerDay;
        Int128 remainder = total % MillisecondsPerDay;
        if (remainder < 0)
        {
            days = checked(days - 1);
            remainder += MillisecondsPerDay;
        }
        return new WorldInstant(checked((long)days), checked((int)remainder));
    }

    public Int128 MillisecondsUntil(WorldInstant other) => checked(
        ((Int128)other.Days - Days) * MillisecondsPerDay + other.MillisecondsWithinDay - MillisecondsWithinDay);

    public int CompareTo(WorldInstant other)
    {
        int dayOrder = Days.CompareTo(other.Days);
        return dayOrder != 0 ? dayOrder : MillisecondsWithinDay.CompareTo(other.MillisecondsWithinDay);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Days}:{MillisecondsWithinDay:D8}");
    public static bool operator <(WorldInstant left, WorldInstant right) => left.CompareTo(right) < 0;
    public static bool operator >(WorldInstant left, WorldInstant right) => left.CompareTo(right) > 0;
    public static bool operator <=(WorldInstant left, WorldInstant right) => left.CompareTo(right) <= 0;
    public static bool operator >=(WorldInstant left, WorldInstant right) => left.CompareTo(right) >= 0;
}
