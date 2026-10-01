namespace OpenFantasy.Client.Cargo;

/// <summary>
/// Frames observed terrain around the usable field between the HUD panels. Empty padding outside the observed
/// terrain lets an actor near its edge stay visible; it never admits or draws another world region.
/// </summary>
internal sealed class CargoCameraFraming
{
    internal CargoCameraFraming(CargoCanvasRect observedBounds, CargoCanvasRect viewport,
        CargoCanvasRect visibleField, CargoCanvasPoint zoom)
    {
        if (observedBounds.Width <= 0 || observedBounds.Height <= 0 || viewport.Width <= 0 || viewport.Height <= 0
            || visibleField.Width <= 0 || visibleField.Height <= 0 || zoom.X <= 0 || zoom.Y <= 0
            || visibleField.X < viewport.X || visibleField.Y < viewport.Y
            || visibleField.Right > viewport.Right || visibleField.Bottom > viewport.Bottom)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleField), "Camera framing requires a visible field inside its viewport.");
        }
        FollowOffset = new CargoCanvasPoint(
            (viewport.X + viewport.Width / 2 - visibleField.X - visibleField.Width / 2) / zoom.X,
            (viewport.Y + viewport.Height / 2 - visibleField.Y - visibleField.Height / 2) / zoom.Y);
        double halfWidth = viewport.Width / (2 * zoom.X);
        double halfHeight = viewport.Height / (2 * zoom.Y);
        Limits = new CargoCanvasRect(observedBounds.X - halfWidth + FollowOffset.X,
            observedBounds.Y - halfHeight + FollowOffset.Y,
            observedBounds.Width + 2 * halfWidth, observedBounds.Height + 2 * halfHeight);
    }

    internal CargoCanvasPoint FollowOffset { get; }
    internal CargoCanvasRect Limits { get; }
}
