namespace OpenFantasy.Core.Terrain;

/// <summary>A finite 3D region with inclusive minimum and maximum coordinates.</summary>
public sealed record TerrainRegionBounds
{
    public const int MaximumWidth = 32;
    public const int MaximumDepth = 32;
    public const int MaximumHeight = 16;
    public const int MaximumCellCount = MaximumWidth * MaximumDepth * MaximumHeight;

    public TerrainRegionBounds(CellCoordinate minimum, int width, int depth, int height)
    {
        if (width is < 1 or > MaximumWidth || depth is < 1 or > MaximumDepth ||
            height is < 1 or > MaximumHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Region dimensions exceed the supported positive envelope.");
        }
        Minimum = minimum;
        Width = width;
        Depth = depth;
        Height = height;
        Maximum = minimum.Offset(width - 1, depth - 1, height - 1);
        CellCount = checked(width * depth * height);
    }

    public CellCoordinate Minimum { get; }
    public CellCoordinate Maximum { get; }
    public int Width { get; }
    public int Depth { get; }
    public int Height { get; }
    public int CellCount { get; }

    public bool Contains(CellCoordinate coordinate) =>
        coordinate.X >= Minimum.X && coordinate.X <= Maximum.X &&
        coordinate.Y >= Minimum.Y && coordinate.Y <= Maximum.Y &&
        coordinate.Z >= Minimum.Z && coordinate.Z <= Maximum.Z;

    public int IndexOf(CellCoordinate coordinate)
    {
        if (!Contains(coordinate))
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate), "The cell is outside the available region.");
        }
        // Subtraction is safe after containment: each difference fits its small dimension.
        return checked(((int)(coordinate.Z - Minimum.Z) * Depth + (int)(coordinate.Y - Minimum.Y)) * Width
            + (int)(coordinate.X - Minimum.X));
    }

    public CellCoordinate CoordinateAt(int index)
    {
        if (index < 0 || index >= CellCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return Minimum.Offset(index % Width, index / Width % Depth, index / (Width * Depth));
    }
}
