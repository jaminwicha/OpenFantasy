using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Content;

/// <summary>Authored bank waypoints in the retained farm locality; none is an actor placement command.</summary>
public static class WatercourseFarmLayout
{
    public const long FirstChannelX = 110;
    public const long LastChannelX = 119;
    public const long ChannelY = 329;
    public const long SpillZ = 1;
    public const ulong InitialWaterUnits = 6_000;
    public const ulong MicrolitresPerUnit = 1_000_000;
    public const ulong DownwardUnitsPerStep = 25;
    public const ulong LateralUnitsPerStep = 4;

    public static SubunitPosition HeadBank => new(109 * 256 + 128, 329 * 256 + 128, 256);
    public static SubunitPosition NorthBank => new(114 * 256 + 128, 328 * 256 + 128, 256);
    public static SubunitPosition PoolBank => new(120 * 256 + 128, 329 * 256 + 128, 256);

    internal static ReservoirColumn[] Columns() => Enumerable.Range(110, 10)
        .Select(x => new ReservoirColumn(x, ChannelY, x < 116 ? 0 : -1))
        .Concat(Enumerable.Range(117, 3).Select(x => new ReservoirColumn(x, ChannelY + 1, -1)))
        .ToArray();

    internal static CellCoordinate[] InteriorCells() => Columns().SelectMany(column =>
        Enumerable.Range(0, checked((int)(SpillZ - column.BottomZ)))
            .Select(offset => new CellCoordinate(column.X, column.Y, column.BottomZ + offset))).ToArray();
}
