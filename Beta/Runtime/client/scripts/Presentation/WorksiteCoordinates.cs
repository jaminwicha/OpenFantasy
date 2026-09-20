using Godot;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Client.Presentation;

/// <summary>
/// Explicit oblique worksite blockout: one metre is 32 pixels on every axis. The original
/// one-metre farm floor retains its old screen position; height only changes presentation.
/// </summary>
internal static class WorksiteCoordinates
{
    private const long MinimumHeight = -16 * SubunitPosition.SubunitsPerCell;
    private const long MaximumHeight = 17 * SubunitPosition.SubunitsPerCell;

    internal static Vector2 Position(SubunitPosition feet)
    {
        Validate(feet);
        return Point(feet.X, feet.Y, feet.Z);
    }

    internal static Vector2 Point(double x, double y, double z) => new(
        (float)((x - FarmCanvasCoordinates.MinimumCellX * 256L) / FarmCanvasCoordinates.SubunitsPerPixel),
        (float)((y - FarmCanvasCoordinates.MinimumCellY * 256L - (z - 256)) / FarmCanvasCoordinates.SubunitsPerPixel));

    // Keep terrain, members, and feet on the same height band; Y-sort orders peers. The extra
    // actor unit makes a standing actor visible above the surface on which its feet rest.
    internal static int DepthIndex(double height, bool actor = false) =>
        1_000 + checked((int)Math.Floor((height - 256) / 16)) * 2 + (actor ? 1 : 0);

    internal static void Validate(SubunitPosition feet)
    {
        if (feet.X < FarmCanvasCoordinates.MinimumCellX * 256L
            || feet.X >= (FarmCanvasCoordinates.MinimumCellX + FarmCanvasCoordinates.RegionWidth) * 256L
            || feet.Y < FarmCanvasCoordinates.MinimumCellY * 256L
            || feet.Y >= (FarmCanvasCoordinates.MinimumCellY + FarmCanvasCoordinates.RegionDepth) * 256L
            || feet.Z < MinimumHeight || feet.Z > MaximumHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(feet), "A worksite appearance must remain inside its finite farm and height envelope.");
        }
    }
}
