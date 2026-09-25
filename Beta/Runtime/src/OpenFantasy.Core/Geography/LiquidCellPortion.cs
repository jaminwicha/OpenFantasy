namespace OpenFantasy.Core.Geography;

public enum LiquidStockOrigin { RawSource = 1, ProducedLot = 2 }

/// <summary>Placement of existing source units or produced-lot offsets; this creates no material rights.</summary>
public sealed record LiquidCellPortion
{
    public LiquidCellPortion(LiquidCellKey cell, LiquidStockOrigin origin, EntityId originId, ulong start, ulong end)
    {
        if (!Enum.IsDefined(origin) || end <= start)
        {
            throw new ArgumentException("A liquid portion requires a known origin and positive half-open interval.");
        }
        Cell = cell;
        Origin = origin;
        OriginId = originId;
        Start = start;
        End = end;
    }

    public LiquidCellKey Cell { get; }
    public LiquidStockOrigin Origin { get; }
    public EntityId OriginId { get; }
    public ulong Start { get; }
    public ulong End { get; }
    public ulong Quantity => End - Start;
}
