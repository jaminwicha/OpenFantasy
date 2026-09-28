using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Immutable finite forcing/exchange basis. Preparation neither spends the reservation nor reserves receiving capacity.</summary>
public sealed class WatershedRunoffRequest
{
    private readonly Lazy<ContentHash> _contentHash;

    public WatershedRunoffRequest(PreparedWatershedNetwork network, WatershedTerrainMapping mapping,
        CountedAuthorityState inventory, ProductionSnapshot? production, EntityId reservationId, EntityId ownerActorId,
        WorldInstant start, WorldInstant end, ItemPhysicalBinding material, IEnumerable<WatershedRunoffInput> inputs,
        IEnumerable<WatershedRunoffTerminal> terminals)
        : this(network, mapping, inventory, production, reservationId, ownerActorId, start, end, material, inputs, terminals, null)
    {
    }

    internal WatershedRunoffRequest(PreparedWatershedNetwork network, WatershedTerrainMapping mapping,
        CountedAuthorityState inventory, ProductionSnapshot? production, EntityId reservationId, EntityId ownerActorId,
        WorldInstant start, WorldInstant end, ItemPhysicalBinding material, IEnumerable<WatershedRunoffInput> inputs,
        IEnumerable<WatershedRunoffTerminal> terminals, RunoffMaterialContinuation? continuation)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(material);
        if (end <= start)
        {
            throw new ArgumentException("A runoff budget requires a nonempty half-open forcing interval.");
        }
        Network = network;
        Mapping = mapping;
        Inventory = inventory;
        Production = production;
        ReservationId = reservationId;
        OwnerActorId = ownerActorId;
        Start = start;
        End = end;
        Material = material;
        Inputs = ReadBounded(inputs, input => input.BasinId);
        Terminals = ReadBounded(terminals, terminal => terminal.BasinId);
        ContinuationQualification = continuation;
        if (production?.IsContinued == true)
        {
            (continuation ?? throw new ArgumentException("Use the actual continued capacity owner to create a runoff request."))
                .Validate(WatershedRunoffDomain.From(this), production);
        }
        _contentHash = new(() => ContentHash.Compute(Production?.IsContinued == true ? WatershedRunoffCodecV3.EncodeRequest(this)
            : Production?.SupportsConsumption == true
            ? WatershedRunoffCodecV2.EncodeRequest(this) : WatershedRunoffCodecV1.EncodeRequest(this)));
    }

    public PreparedWatershedNetwork Network { get; }
    public WatershedTerrainMapping Mapping { get; }
    public CountedAuthorityState Inventory { get; }
    public ProductionSnapshot? Production { get; }
    public EntityId ReservationId { get; }
    public EntityId OwnerActorId { get; }
    public WorldInstant Start { get; }
    public WorldInstant End { get; }
    public ItemPhysicalBinding Material { get; }
    public ReadOnlyCollection<WatershedRunoffInput> Inputs { get; }
    public ReadOnlyCollection<WatershedRunoffTerminal> Terminals { get; }
    public ContentHash ContentHash => _contentHash.Value;
    internal RunoffMaterialContinuation? ContinuationQualification { get; }

    private static ReadOnlyCollection<T> ReadBounded<T>(IEnumerable<T> values, Func<T, EntityId> identity) where T : class
    {
        ArgumentNullException.ThrowIfNull(values);
        Dictionary<EntityId, T> copy = [];
        foreach (T value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (copy.Count == PreparedWatershedNetwork.MaximumBasins || !copy.TryAdd(identity(value), value))
            {
                throw new ArgumentException("Runoff inputs and terminal records must be unique and bounded.");
            }
        }
        return copy.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList().AsReadOnly();
    }
}
