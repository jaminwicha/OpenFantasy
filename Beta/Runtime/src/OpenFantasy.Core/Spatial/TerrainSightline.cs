using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Exact bounded center segment against closed cell cubes; grazing solid edges are occluded.</summary>
internal static class TerrainSightline
{
    private const int DoubledCellSize = SubunitPosition.SubunitsPerCell * 2;

    internal static SpatialInteraction Query(TerrainVolumeQueries terrain, BodyVolume source, BodyVolume target)
        => QueryCenters(terrain, source.DoubledCenter, target.DoubledCenter, LocalSpatialSnapshot.InteractionRangeSubunits);

    internal static SpatialInteraction QueryCenters(TerrainVolumeQueries terrain,
        (Int128 X, Int128 Y, Int128 Z) from, (Int128 X, Int128 Y, Int128 Z) to, long maximumRangeSubunits)
    {
        Int128 limit = (Int128)maximumRangeSubunits * 2;
        Int128 dx = to.X - from.X;
        Int128 dy = to.Y - from.Y;
        if (dx < -limit || dx > limit || dy < -limit || dy > limit || dx * dx + dy * dy > limit * limit)
        {
            return SpatialInteraction.OutOfRange;
        }
        long minimumX = FirstTouchedCell(Int128.Min(from.X, to.X));
        long minimumY = FirstTouchedCell(Int128.Min(from.Y, to.Y));
        long minimumZ = FirstTouchedCell(Int128.Min(from.Z, to.Z));
        long maximumX = CellOf(Int128.Max(from.X, to.X));
        long maximumY = CellOf(Int128.Max(from.Y, to.Y));
        long maximumZ = CellOf(Int128.Max(from.Z, to.Z));
        bool missing = false;
        bool occluded = false;
        for (long z = minimumZ; z <= maximumZ; z++)
        {
            for (long y = minimumY; y <= maximumY; y++)
            {
                for (long x = minimumX; x <= maximumX; x++)
                {
                    var coordinate = new CellCoordinate(x, y, z);
                    if (!TouchesCell(from, to, coordinate))
                    {
                        continue;
                    }
                    TerrainCell? cell = terrain.FindCell(coordinate);
                    missing |= cell is null;
                    occluded |= cell?.IsSolid == true;
                }
            }
        }
        if (missing)
        {
            return SpatialInteraction.Unavailable;
        }
        return occluded ? SpatialInteraction.Occluded : SpatialInteraction.Reachable;
    }

    private static long CellOf(Int128 coordinate)
    {
        Int128 quotient = coordinate / DoubledCellSize;
        if (coordinate % DoubledCellSize < 0)
        {
            quotient--;
        }
        return checked((long)quotient);
    }

    private static long FirstTouchedCell(Int128 coordinate) =>
        CellOf(coordinate) - (coordinate % DoubledCellSize == 0 ? 1 : 0);

    private static bool TouchesCell((Int128 X, Int128 Y, Int128 Z) from,
        (Int128 X, Int128 Y, Int128 Z) to, CellCoordinate cell)
    {
        Fraction entering = new(0, 1);
        Fraction leaving = new(1, 1);
        return Clip(from.X, to.X, cell.X, ref entering, ref leaving)
            && Clip(from.Y, to.Y, cell.Y, ref entering, ref leaving)
            && Clip(from.Z, to.Z, cell.Z, ref entering, ref leaving);
    }

    private static bool Clip(Int128 from, Int128 to, long cell, ref Fraction entering, ref Fraction leaving)
    {
        Int128 lower = (Int128)cell * DoubledCellSize;
        Int128 upper = lower + DoubledCellSize;
        Int128 direction = to - from;
        if (direction == 0)
        {
            return from >= lower && from <= upper;
        }
        Fraction near = direction > 0 ? new(lower - from, direction) : new(from - upper, -direction);
        Fraction far = direction > 0 ? new(upper - from, direction) : new(from - lower, -direction);
        if (near.CompareTo(entering) > 0)
        {
            entering = near;
        }
        if (far.CompareTo(leaving) < 0)
        {
            leaving = far;
        }
        return entering.CompareTo(leaving) <= 0;
    }

    // Range is checked before clipping; all cross products use only bounded local differences.
    private readonly record struct Fraction(Int128 Numerator, Int128 Denominator) : IComparable<Fraction>
    {
        public int CompareTo(Fraction other) => checked(Numerator * other.Denominator)
            .CompareTo(checked(other.Numerator * Denominator));
    }
}
