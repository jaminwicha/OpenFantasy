namespace OpenFantasy.Core.Terrain;

/// <summary>Signed whole-cell coordinates; positive Z is upward. Sprite pixels are unrelated units.</summary>
public readonly record struct CellCoordinate(long X, long Y, long Z) : IComparable<CellCoordinate>
{
    public CellCoordinate Offset(long x, long y, long z) =>
        new(checked(X + x), checked(Y + y), checked(Z + z));

    /// <summary>Canonical storage order is Z, then Y, then X; X varies fastest.</summary>
    public int CompareTo(CellCoordinate other)
    {
        int order = Z.CompareTo(other.Z);
        if (order != 0)
        {
            return order;
        }
        order = Y.CompareTo(other.Y);
        return order != 0 ? order : X.CompareTo(other.X);
    }
}
