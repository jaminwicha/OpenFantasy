namespace OpenFantasy.Core.Geography;

/// <summary>One finite macro sampling grid. Grid coordinates are not sprite or local voxel coordinates.</summary>
public sealed record WatershedBounds
{
    public const int MaximumAxis = 64;
    public const int MaximumColumns = MaximumAxis * MaximumAxis;

    public WatershedBounds(long minimumX, long minimumY, int width, int depth)
    {
        if (width is < 1 or > MaximumAxis || depth is < 1 or > MaximumAxis)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Watershed dimensions exceed the finite preparation envelope.");
        }
        MinimumX = minimumX;
        MinimumY = minimumY;
        Width = width;
        Depth = depth;
        MaximumX = checked(minimumX + (width - 1));
        MaximumY = checked(minimumY + (depth - 1));
    }

    public long MinimumX { get; }
    public long MinimumY { get; }
    public long MaximumX { get; }
    public long MaximumY { get; }
    public int Width { get; }
    public int Depth { get; }
    public int ColumnCount => Width * Depth;

    public int IndexOf(long x, long y)
    {
        if (x < MinimumX || x > MaximumX || y < MinimumY || y > MaximumY)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "The macro column is outside this prepared basin.");
        }
        return checked((int)(y - MinimumY) * Width + (int)(x - MinimumX));
    }

    public (long X, long Y) CoordinateAt(int index)
    {
        ValidateIndex(index);
        return (checked(MinimumX + index % Width), checked(MinimumY + index / Width));
    }

    public bool IsBoundary(int index)
    {
        ValidateIndex(index);
        return index % Width == 0 || index % Width == Width - 1 || index / Width == 0 || index / Width == Depth - 1;
    }

    public IEnumerable<int> Neighbors(int index)
    {
        ValidateIndex(index);
        // Canonical ascending index order. No arithmetic outside the finite grid, including at Int64 limits.
        if (index >= Width) yield return index - Width;
        if (index % Width > 0) yield return index - 1;
        if (index % Width < Width - 1) yield return index + 1;
        if (index < ColumnCount - Width) yield return index + Width;
    }

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
