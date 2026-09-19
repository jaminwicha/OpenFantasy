using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Spatial;

/// <summary>An explicit assisted ledge traversal request with horizontal and vertical subunit displacements.</summary>
public sealed record ClimbIntent : WorldActionIntent
{
    public const long MaximumDisplacementSubunits = 256;

    public ClimbIntent(long deltaX, long deltaY, long deltaZ)
    {
        if ((deltaX == 0) == (deltaY == 0) || deltaZ == 0
            || deltaX < -MaximumDisplacementSubunits || deltaX > MaximumDisplacementSubunits
            || deltaY < -MaximumDisplacementSubunits || deltaY > MaximumDisplacementSubunits
            || deltaZ < -MaximumDisplacementSubunits || deltaZ > MaximumDisplacementSubunits)
        {
            throw new ArgumentException("A climb needs one bounded horizontal axis and a nonzero bounded vertical displacement.");
        }
        DeltaX = deltaX;
        DeltaY = deltaY;
        DeltaZ = deltaZ;
    }

    public long DeltaX { get; }
    public long DeltaY { get; }
    public long DeltaZ { get; }
}
