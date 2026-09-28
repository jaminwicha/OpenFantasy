using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Conserved aggregate routing potential, not water occupancy, arrival times or a hydraulic integration step.</summary>
public sealed class PreparedWatershedRunoff
{
    private readonly Dictionary<EntityId, ulong[]> _contributions;
    private readonly Lazy<ContentHash> _contentHash;

    internal PreparedWatershedRunoff(WatershedRunoffRequest request, Dictionary<EntityId, ulong[]> contributions,
        ulong inputUnits, IEnumerable<WatershedRunoffTerminalTotal> terminals, IEnumerable<WatershedRunoffReceiverTotal> receivers)
    {
        Request = request;
        _contributions = contributions.ToDictionary(pair => pair.Key, pair => (ulong[])pair.Value.Clone());
        TotalInputUnits = inputUnits;
        TotalInputVolume = request.Material.Physical.VolumePerUnit.Multiply(inputUnits);
        _ = request.Material.Physical.MassPerUnit.Multiply(inputUnits);
        Terminals = terminals.OrderBy(t => t.BasinId).ToList().AsReadOnly();
        Receivers = receivers.OrderBy(r => r.CustodianId).ToList().AsReadOnly();
        _contentHash = new(() => ContentHash.Compute(Request.Production?.Continuation?.SupportsReservedTransport == true ? WatershedRunoffCodecV4.Encode(this)
            : Request.Production?.IsContinued == true ? WatershedRunoffCodecV3.Encode(this)
            : Request.Production?.SupportsConsumption == true
            ? WatershedRunoffCodecV2.Encode(this) : WatershedRunoffCodecV1.Encode(this)));
    }

    public WatershedRunoffRequest Request { get; }
    public ulong TotalInputUnits { get; }
    public VolumeMicrolitres TotalInputVolume { get; }
    public ReadOnlyCollection<WatershedRunoffTerminalTotal> Terminals { get; }
    public ReadOnlyCollection<WatershedRunoffReceiverTotal> Receivers { get; }
    public ContentHash ContentHash => _contentHash.Value;

    public ulong ContributingUnits(EntityId basinId, int columnIndex)
    {
        if (!_contributions.TryGetValue(basinId, out var values) || columnIndex < 0 || columnIndex >= values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex), "The column is outside the prepared runoff budget.");
        }
        return values[columnIndex];
    }

    public void EnsureCurrent(WatershedRunoffRequest current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ContentHash != Request.ContentHash)
        {
            throw new InvalidOperationException("The complete runoff basis changed; preparation must be repeated before admission.");
        }
    }
}
