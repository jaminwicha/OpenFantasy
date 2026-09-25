using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Content;

/// <summary>Initial terrain and ordinary dry-bank waypoints, with no actor displacement or earned work.</summary>
public static class BreachFarmLayout
{
    public const long FirstUpperX = 110;
    public const long LastUpperX = 115;
    public const long LastReceiverX = 119;
    public const long ChannelY = 329;
    public const long SpillZ = 1;
    public const ulong InitialWaterUnits = 6_000;
    public const ulong MicrolitresPerUnit = 1_000_000;
    public const ulong DownwardUnitsPerStep = 25;
    public const ulong LateralUnitsPerStep = 4;
    public const ulong RequiredExcavationWork = 3;

    public static CellCoordinate DividerCell => new(116, ChannelY, 0);
    public static SubunitPosition MiningStance => new(116 * 256 + 128, 328 * 256 + 128, 256);
    public static SubunitPosition NorthBank => new(114 * 256 + 128, 328 * 256 + 128, 256);
    public static SubunitPosition ReceiverBank => new(120 * 256 + 128, 329 * 256 + 128, 256);

    internal static ReservoirColumn[] Columns() => Enumerable.Range(110, 10)
        .Select(x => new ReservoirColumn(x, ChannelY, x <= DividerCell.X ? 0 : -1))
        .Concat(Enumerable.Range(117, 3).Select(x => new ReservoirColumn(x, ChannelY + 1, -1)))
        .ToArray();

    internal static CellCoordinate[] InitialHoles() => Columns().SelectMany(column =>
        Enumerable.Range(0, checked((int)(SpillZ - column.BottomZ)))
            .Select(offset => new CellCoordinate(column.X, column.Y, column.BottomZ + offset)))
        .Where(cell => cell != DividerCell).ToArray();
}
