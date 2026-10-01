using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Client.Cargo;

/// <summary>A point on the field canvas, in canvas pixels; screen Y grows with world Y as on the farm.</summary>
internal readonly record struct CargoCanvasPoint(double X, double Y);

/// <summary>A canvas rectangle in pixels: the observed regions' extent, used for camera limits.</summary>
internal readonly record struct CargoCanvasRect(double X, double Y, double Width, double Height)
{
    internal double Right => X + Width;
    internal double Bottom => Y + Height;
}

/// <summary>
/// The one permitted-region, single-floor projection of a cargo-world frame. Terrain, actors, objects, the target
/// cue, camera limits and pointer picks all use it, so they agree by construction. It is a fixed affine map of
/// authority coordinates: the same 32 pixels per cell and the same origin cell as the farm's own embedding, so the
/// farm region keeps its exact canvas positions and nothing moves when the viewport or the observed set changes.
/// Only positions on the observed floor inside an observed region are admitted; everything else is refused, never
/// clamped, so a missing or unsupported observation fails closed instead of being drawn somewhere plausible.
/// </summary>
internal sealed class CargoViewProjection
{
    /// <summary>The farm embedding's origin cell; shared so the retained farm keeps its coordinates.</summary>
    internal const long OriginCellX = 96;
    internal const long OriginCellY = 320;
    internal const int PixelsPerCell = 32;
    internal const int SubunitsPerPixel = SubunitPosition.SubunitsPerCell / PixelsPerCell;

    internal CargoViewProjection(LocalRegionObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Regions.Count == 0)
        {
            throw new ArgumentException("A cargo frame requires at least the region the player stands in.", nameof(observation));
        }
        Observation = observation;
        double left = observation.Regions.Min(region => CellLeft(region.Bounds.Minimum.X));
        double top = observation.Regions.Min(region => CellTop(region.Bounds.Minimum.Y));
        double right = observation.Regions.Max(region => CellLeft(region.Bounds.Maximum.X + 1));
        double bottom = observation.Regions.Max(region => CellTop(region.Bounds.Maximum.Y + 1));
        Bounds = new CargoCanvasRect(left, top, right - left, bottom - top);
    }

    internal LocalRegionObservation Observation { get; }

    /// <summary>The union extent of the observed regions; camera padding outside it reveals no additional terrain.</summary>
    internal CargoCanvasRect Bounds { get; }

    /// <summary>Whether a public position lies on the observed floor inside an observed region.</summary>
    internal bool Admits(SubunitPosition feet) => Observation.Contains(feet);

    /// <summary>The canvas point of an admitted position; any other position is refused.</summary>
    internal CargoCanvasPoint Project(SubunitPosition feet)
    {
        if (!Admits(feet))
        {
            throw new ArgumentOutOfRangeException(nameof(feet), "This cargo view supports only positions on its observed floor and regions.");
        }
        return new CargoCanvasPoint((feet.X - OriginCellX * SubunitPosition.SubunitsPerCell) / (double)SubunitsPerPixel,
            (feet.Y - OriginCellY * SubunitPosition.SubunitsPerCell) / (double)SubunitsPerPixel);
    }

    /// <summary>The top-left canvas corner of one observed column.</summary>
    internal CargoCanvasPoint CellCorner(long cellX, long cellY) => new(CellLeft(cellX), CellTop(cellY));

    /// <summary>
    /// The standing position under one canvas point: the centre of that column on the observed floor, or null when
    /// the point is outside every observed region. Pointer picks use this exact inverse of <see cref="Project"/>.
    /// </summary>
    internal SubunitPosition? Unproject(CargoCanvasPoint point)
    {
        long cellX = OriginCellX + (long)Math.Floor(point.X / PixelsPerCell);
        long cellY = OriginCellY + (long)Math.Floor(point.Y / PixelsPerCell);
        var feet = new SubunitPosition(checked(cellX * SubunitPosition.SubunitsPerCell + SubunitPosition.SubunitsPerCell / 2),
            checked(cellY * SubunitPosition.SubunitsPerCell + SubunitPosition.SubunitsPerCell / 2), Observation.FloorFeetZ);
        return Admits(feet) ? feet : null;
    }

    private static double CellLeft(long cellX) => (cellX - OriginCellX) * (double)PixelsPerCell;
    private static double CellTop(long cellY) => (cellY - OriginCellY) * (double)PixelsPerCell;
}
