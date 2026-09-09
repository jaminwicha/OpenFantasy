using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Exposure;

/// <summary>A versioned integer output-unit rate per experienced millisecond.</summary>
public sealed record ExposureRate
{
    public ExposureRate(EntityId outputUnitId, ContentHash rulesVersion, ulong numerator, ulong denominator)
    {
        ArgumentNullException.ThrowIfNull(rulesVersion);
        if (denominator == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator));
        }
        OutputUnitId = outputUnitId;
        RulesVersion = rulesVersion;
        Numerator = numerator;
        Denominator = denominator;
    }

    public EntityId OutputUnitId { get; }
    public ContentHash RulesVersion { get; }
    public ulong Numerator { get; }
    public ulong Denominator { get; }
}
