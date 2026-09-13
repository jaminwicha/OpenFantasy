namespace OpenFantasy.Core.Geography;

public enum WatershedOutletKind { Basin = 1, Terminal = 2 }

/// <summary>Closes exactly one existing basin outlet without repurposing its external-boundary identity as a face.</summary>
public sealed class WatershedOutletClosure
{
    private WatershedOutletClosure(EntityId basinId, WatershedOutletKind kind,
        EntityId? receivingBasinId, int? receivingColumnIndex, int? terminalColumnCapacity)
    {
        BasinId = basinId;
        Kind = kind;
        ReceivingBasinId = receivingBasinId;
        ReceivingColumnIndex = receivingColumnIndex;
        TerminalColumnCapacity = terminalColumnCapacity;
    }

    public EntityId BasinId { get; }
    public WatershedOutletKind Kind { get; }
    public EntityId? ReceivingBasinId { get; }
    public int? ReceivingColumnIndex { get; }
    public int? TerminalColumnCapacity { get; }

    public static WatershedOutletClosure ToBasin(EntityId basinId, EntityId receivingBasinId, int receivingColumnIndex)
    {
        if (basinId == receivingBasinId || receivingColumnIndex is < 0 or >= WatershedBounds.MaximumColumns)
        {
            throw new ArgumentException("An outlet must address a valid column in a different basin.");
        }
        return new WatershedOutletClosure(basinId, WatershedOutletKind.Basin, receivingBasinId, receivingColumnIndex, null);
    }

    public static WatershedOutletClosure ToTerminal(EntityId basinId, int columnCapacity)
    {
        if (columnCapacity is < 1 or > PreparedWatershedNetwork.MaximumColumns)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCapacity), "Terminal coverage must have a finite column capacity.");
        }
        return new WatershedOutletClosure(basinId, WatershedOutletKind.Terminal, null, null, columnCapacity);
    }
}
