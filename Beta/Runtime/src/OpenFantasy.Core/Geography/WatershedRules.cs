using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>Pinned relief and spill-routing preparation. Elevations use whole terrain cells, positive upward.</summary>
public sealed class WatershedRules
{
    public const int MaximumRelief = 1024;

    public WatershedRules(long baseElevation, int relief)
    {
        if (relief is < 0 or > MaximumRelief)
        {
            throw new ArgumentOutOfRangeException(nameof(relief));
        }
        _ = checked(baseElevation + relief);
        _ = checked(baseElevation - relief);
        BaseElevation = baseElevation;
        Relief = relief;
        Version = ContentHash.Compute(WatershedCodecV1.EncodeRules(this));
    }

    public long BaseElevation { get; }
    public int Relief { get; }
    public long OutletElevation => checked(BaseElevation - Relief);
    public ContentHash Version { get; }
}
