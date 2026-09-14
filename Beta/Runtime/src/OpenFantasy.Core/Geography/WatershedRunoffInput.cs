using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Geography;

/// <summary>Uniform effective runoff for a whole basin, funded by an existing reserved source interval.</summary>
public sealed class WatershedRunoffInput
{
    public WatershedRunoffInput(EntityId basinId, ulong depthMicrometres, SourceInterval source, EntityId custodianId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (depthMicrometres == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depthMicrometres), "Use an explicit dry basin for zero runoff.");
        }
        BasinId = basinId;
        DepthMicrometres = depthMicrometres;
        Source = source;
        CustodianId = custodianId;
    }

    private WatershedRunoffInput(EntityId basinId) => BasinId = basinId;
    public EntityId BasinId { get; }
    public ulong DepthMicrometres { get; }
    public SourceInterval? Source { get; }
    public EntityId? CustodianId { get; }
    public static WatershedRunoffInput Dry(EntityId basinId) => new(basinId);
}
