namespace OpenFantasy.Core.Geography;

/// <summary>A positive-area surface rectangle with exact coordinates in 1/256 millimetre ticks.</summary>
public sealed record BoundaryRectangle
{
    public BoundaryRectangle(Int128 minU, Int128 minV, Int128 maxU, Int128 maxV)
    {
        if (minU >= maxU || minV >= maxV)
        {
            throw new ArgumentException("A boundary rectangle requires strictly positive extents.");
        }
        MinU = minU;
        MinV = minV;
        MaxU = maxU;
        MaxV = maxV;
    }

    public Int128 MinU { get; }
    public Int128 MinV { get; }
    public Int128 MaxU { get; }
    public Int128 MaxV { get; }
}
