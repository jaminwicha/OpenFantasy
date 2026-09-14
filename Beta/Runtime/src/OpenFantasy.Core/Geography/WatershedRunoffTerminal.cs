using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Physical receiving-volume witness for one terminal. Shared custodians require identical capacity.</summary>
public sealed record WatershedRunoffTerminal
{
    public WatershedRunoffTerminal(EntityId basinId, EntityId custodianId, VolumeMicrolitres capacity)
    {
        if (capacity.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        BasinId = basinId;
        CustodianId = custodianId;
        Capacity = capacity;
    }

    public EntityId BasinId { get; }
    public EntityId CustodianId { get; }
    public VolumeMicrolitres Capacity { get; }
}

public sealed record WatershedRunoffTerminalTotal(EntityId BasinId, EntityId CustodianId, ulong IncomingUnits);
public sealed record WatershedRunoffReceiverTotal(EntityId CustodianId, ulong ExistingUnits, ulong IncomingUnits,
    ulong ProjectedUnits, VolumeMicrolitres ProjectedVolume, VolumeMicrolitres Capacity);
