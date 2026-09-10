using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Signed coordinates at 256 subunits per cell; Z locates the actor's feet.</summary>
public readonly record struct SubunitPosition(long X, long Y, long Z)
{
    public const int SubunitsPerCell = 256;

    public SubunitPosition Offset(long x, long y, long z) =>
        new(checked(X + x), checked(Y + y), checked(Z + z));

    public CellCoordinate Cell => new(CellOf(X), CellOf(Y), CellOf(Z));

    public static long CellOf(long subunits)
    {
        long quotient = subunits / SubunitsPerCell;
        return subunits % SubunitsPerCell < 0 ? quotient - 1 : quotient;
    }
}
