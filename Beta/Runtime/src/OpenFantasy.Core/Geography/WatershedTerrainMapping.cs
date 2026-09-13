using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>Integer macro-to-cell transform anchored at horizontal zero, with an explicit vertical datum.</summary>
public sealed class WatershedTerrainMapping
{
    public const int MaximumScale = 1024;

    public WatershedTerrainMapping(int cellsPerColumn, int cellsPerElevationUnit, long elevationDatumZ)
    {
        if (cellsPerColumn is < 1 or > MaximumScale || cellsPerElevationUnit is < 1 or > MaximumScale)
        {
            throw new ArgumentOutOfRangeException(nameof(cellsPerColumn), "Terrain mapping requires bounded positive integer scales.");
        }
        CellsPerColumn = cellsPerColumn;
        CellsPerElevationUnit = cellsPerElevationUnit;
        ElevationDatumZ = elevationDatumZ;
        Version = ContentHash.Compute(WatershedTerrainCodecV1.EncodeMapping(this));
    }

    public int CellsPerColumn { get; }
    public int CellsPerElevationUnit { get; }
    public long ElevationDatumZ { get; }
    public ContentHash Version { get; }

    public WatershedColumnAddress MacroAddressOf(long localX, long localY) =>
        new(DivideFloor(localX).Quotient, DivideFloor(localY).Quotient);

    public (int X, int Y) OffsetWithinColumn(long localX, long localY) =>
        (DivideFloor(localX).Remainder, DivideFloor(localY).Remainder);

    public long SurfaceZ(long naturalElevation) =>
        checked((long)((Int128)naturalElevation * CellsPerElevationUnit + ElevationDatumZ));

    internal void ValidateFootprint(WatershedBounds bounds)
    {
        // Validate complete inclusive footprints, including the far edge of the last macro column.
        _ = checked((long)((Int128)bounds.MinimumX * CellsPerColumn));
        _ = checked((long)((Int128)bounds.MinimumY * CellsPerColumn));
        _ = checked((long)((Int128)bounds.MaximumX * CellsPerColumn + CellsPerColumn - 1));
        _ = checked((long)((Int128)bounds.MaximumY * CellsPerColumn + CellsPerColumn - 1));
    }

    private (long Quotient, int Remainder) DivideFloor(long local)
    {
        long quotient = local / CellsPerColumn;
        int remainder = (int)(local % CellsPerColumn);
        if (remainder < 0)
        {
            quotient--;
            remainder += CellsPerColumn;
        }
        return (quotient, remainder);
    }
}
