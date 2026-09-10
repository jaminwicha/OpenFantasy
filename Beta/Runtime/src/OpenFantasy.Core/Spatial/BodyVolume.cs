using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Half-open body AABB. Odd XY extents place the extra subunit on the positive side.</summary>
internal readonly record struct BodyVolume(long MinimumX, long MinimumY, long MinimumZ,
    long MaximumX, long MaximumY, long MaximumZ)
{
    internal static BodyVolume At(SubunitPosition feet, BodyDimensions dimensions)
    {
        long negativeX = dimensions.WidthSubunits / 2;
        long negativeY = dimensions.DepthSubunits / 2;
        return new BodyVolume(checked(feet.X - negativeX), checked(feet.Y - negativeY), feet.Z,
            checked(feet.X + (dimensions.WidthSubunits - negativeX)),
            checked(feet.Y + (dimensions.DepthSubunits - negativeY)),
            checked(feet.Z + dimensions.HeightSubunits));
    }

    internal BodyVolume Union(BodyVolume other) => new(
        Math.Min(MinimumX, other.MinimumX), Math.Min(MinimumY, other.MinimumY), Math.Min(MinimumZ, other.MinimumZ),
        Math.Max(MaximumX, other.MaximumX), Math.Max(MaximumY, other.MaximumY), Math.Max(MaximumZ, other.MaximumZ));

    internal bool Overlaps(BodyVolume other) => MinimumX < other.MaximumX && MaximumX > other.MinimumX
        && MinimumY < other.MaximumY && MaximumY > other.MinimumY
        && MinimumZ < other.MaximumZ && MaximumZ > other.MinimumZ;

    internal CellCoordinate MinimumCell => new(SubunitPosition.CellOf(MinimumX),
        SubunitPosition.CellOf(MinimumY), SubunitPosition.CellOf(MinimumZ));
    internal CellCoordinate MaximumCell => new(SubunitPosition.CellOf(MaximumX - 1),
        SubunitPosition.CellOf(MaximumY - 1), SubunitPosition.CellOf(MaximumZ - 1));

    // Doubled coordinates retain exact half-subunit centers for odd dimensions.
    internal (Int128 X, Int128 Y, Int128 Z) DoubledCenter =>
        ((Int128)MinimumX + MaximumX, (Int128)MinimumY + MaximumY, (Int128)MinimumZ + MaximumZ);
}
