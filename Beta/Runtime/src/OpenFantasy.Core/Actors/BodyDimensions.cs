namespace OpenFantasy.Core.Actors;

/// <summary>Positive signed-64 extents in 1/256 cell subunits, independent of sprite pixels.</summary>
public sealed record BodyDimensions
{
    public BodyDimensions(long widthSubunits, long depthSubunits, long heightSubunits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthSubunits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depthSubunits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightSubunits);
        WidthSubunits = widthSubunits;
        DepthSubunits = depthSubunits;
        HeightSubunits = heightSubunits;
    }

    public long WidthSubunits { get; }
    public long DepthSubunits { get; }
    public long HeightSubunits { get; }
}
