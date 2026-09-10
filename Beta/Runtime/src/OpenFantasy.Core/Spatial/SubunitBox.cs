namespace OpenFantasy.Core.Spatial;

/// <summary>Positive half-open absolute integer bounds; no material volume or pixel geometry is inferred.</summary>
public sealed record SubunitBox
{
    public SubunitBox(SubunitPosition minimum, SubunitPosition maximum)
    {
        if (minimum.X >= maximum.X || minimum.Y >= maximum.Y || minimum.Z >= maximum.Z)
        {
            throw new ArgumentException("A spatial box requires strictly positive extents on all axes.");
        }
        Minimum = minimum;
        Maximum = maximum;
    }

    public SubunitPosition Minimum { get; }
    public SubunitPosition Maximum { get; }
    internal BodyVolume Volume => new(Minimum.X, Minimum.Y, Minimum.Z, Maximum.X, Maximum.Y, Maximum.Z);
}
