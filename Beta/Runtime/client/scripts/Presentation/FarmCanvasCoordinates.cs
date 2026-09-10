using Godot;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Client.Presentation;

/// <summary>The explicitly admitted single-floor farm embedding, independent of any collision shape.</summary>
internal static class FarmCanvasCoordinates
{
    internal const int MinimumCellX = 96;
    internal const int MinimumCellY = 320;
    internal const int RegionWidth = 32;
    internal const int RegionDepth = 32;
    internal const int PixelsPerCell = 32;
    internal const int SubunitsPerPixel = 8;
    internal static Rect2 LocalBounds => new(0, 0, RegionWidth * PixelsPerCell, RegionDepth * PixelsPerCell);

    internal static Vector2 Position(SubunitPosition feet)
    {
        Validate(feet);
        return new Vector2((feet.X - (long)MinimumCellX * SubunitPosition.SubunitsPerCell) / (float)SubunitsPerPixel,
            (feet.Y - (long)MinimumCellY * SubunitPosition.SubunitsPerCell) / (float)SubunitsPerPixel);
    }

    internal static void Validate(SubunitPosition feet)
    {
        if (feet.X < (long)MinimumCellX * SubunitPosition.SubunitsPerCell
            || feet.X >= (long)(MinimumCellX + RegionWidth) * SubunitPosition.SubunitsPerCell
            || feet.Y < (long)MinimumCellY * SubunitPosition.SubunitsPerCell
            || feet.Y >= (long)(MinimumCellY + RegionDepth) * SubunitPosition.SubunitsPerCell || feet.Z != 256)
        {
            throw new ArgumentOutOfRangeException(nameof(feet), "This farm view supports only public positions in its admitted region and standing floor.");
        }
    }
}
