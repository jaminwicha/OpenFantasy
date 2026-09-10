using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Custody;

/// <summary>One explicit selection from an untransformed source lot.</summary>
public sealed record SourceLotSelection
{
    public SourceLotSelection(EntityId lotId, ulong expectedRevision, ulong quantity)
    {
        ArgumentOutOfRangeException.ThrowIfZero(quantity);
        LotId = lotId;
        ExpectedRevision = expectedRevision;
        Quantity = quantity;
    }

    public EntityId LotId { get; }
    public ulong ExpectedRevision { get; }
    public ulong Quantity { get; }
}

/// <summary>
/// An authority-owned delivery bill. Required item, amount, destination and reservation come
/// from admitted work, while the contributor supplies current owned lot selections.
/// </summary>
public sealed class SourceDeliveryRequest
{
    public const int MaximumSelections = 64;

    public SourceDeliveryRequest(EntityId sourceCustodianId, ulong expectedSourceRevision,
        EntityId destinationCustodianId, CountedItemDefinition requiredItem, ulong requiredQuantity,
        EntityId reservationId, IEnumerable<SourceLotSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(requiredItem);
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentOutOfRangeException.ThrowIfZero(requiredQuantity);
        if (sourceCustodianId == destinationCustodianId)
        {
            throw new ArgumentException("A delivery must change custody.");
        }
        var sorted = new SortedDictionary<EntityId, SourceLotSelection>();
        foreach (SourceLotSelection selection in selections)
        {
            ArgumentNullException.ThrowIfNull(selection);
            if (sorted.Count == MaximumSelections || !sorted.TryAdd(selection.LotId, selection))
            {
                throw new ArgumentException("Delivery selections must be bounded and unique.", nameof(selections));
            }
        }
        if (sorted.Count == 0)
        {
            throw new ArgumentException("A delivery needs at least one selected source lot.", nameof(selections));
        }
        SourceCustodianId = sourceCustodianId;
        ExpectedSourceRevision = expectedSourceRevision;
        DestinationCustodianId = destinationCustodianId;
        RequiredItem = requiredItem;
        RequiredQuantity = requiredQuantity;
        ReservationId = reservationId;
        Selections = Array.AsReadOnly(sorted.Values.ToArray());
    }

    public EntityId SourceCustodianId { get; }
    public ulong ExpectedSourceRevision { get; }
    public EntityId DestinationCustodianId { get; }
    public CountedItemDefinition RequiredItem { get; }
    public ulong RequiredQuantity { get; }
    public EntityId ReservationId { get; }
    public ReadOnlyCollection<SourceLotSelection> Selections { get; }
}
