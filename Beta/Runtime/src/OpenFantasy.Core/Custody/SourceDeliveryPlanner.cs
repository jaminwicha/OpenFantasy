using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Custody;

/// <summary>
/// Moves an exact bill from several raw lots in one immutable operation, retaining their source
/// intervals and reserving the received material. Derived outputs require a different contract.
/// </summary>
public sealed class SourceDeliveryPlanner
{
    public SourceDeliveryPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        TrustedCommandDelivery delivery, EntityId actorId, EntityId commandId, SourceDeliveryRequest request,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(request);
        if (production.IsContinued)
        {
            throw new ArgumentException("Source delivery requires an admitted continuation transfer contract.", nameof(production));
        }
        SourceDeliveryPlan Reject(WorldActionRejection reason) => new(inventory, reason, [], []);
        if (!inventory.CanControl(delivery, actorId)
            || !inventory.AllowsTransfer(actorId, request.SourceCustodianId, request.DestinationCustodianId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (!inventory.ReservationIds.Contains(request.ReservationId))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        try
        {
            production.ValidateForAuthority(inventory);
            if (inventory.ContainsIdentity(commandId) || production.ContainsIdentity(commandId)
                || historicallyOccupied?.Invoke(commandId) == true)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            CustodianStorage source = inventory.Custody.GetStorage(request.SourceCustodianId);
            if (source.Revision != request.ExpectedSourceRevision)
            {
                return Reject(WorldActionRejection.RevisionConflict);
            }
            UInt128 total = 0;
            foreach (SourceLotSelection selected in request.Selections)
            {
                CountedSourceLot lot = inventory.Custody.GetLot(selected.LotId);
                if (lot.CustodianId != source.CustodianId || lot.Definition != request.RequiredItem
                    || lot.Protection != LotProtection.None || lot.ReservedBy is not null
                    || selected.Quantity > lot.Quantity)
                {
                    return Reject(WorldActionRejection.Unavailable);
                }
                if (lot.Revision != selected.ExpectedRevision)
                {
                    return Reject(WorldActionRejection.RevisionConflict);
                }
                total += selected.Quantity;
            }
            if (total != request.RequiredQuantity)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            return Move(inventory, production, commandId, request, historicallyOccupied);
        }
        catch (Exception error) when (error is ArgumentException or CustodyConflictException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }

    private static SourceDeliveryPlan Move(CountedAuthorityState inventory, ProductionSnapshot production,
        EntityId commandId, SourceDeliveryRequest request, Func<EntityId, bool>? historicallyOccupied)
    {
        HashSet<EntityId> selectedIds = request.Selections.Select(value => value.LotId).ToHashSet();
        List<CountedSourceLot> lots = inventory.Custody.Lots.Where(value => !selectedIds.Contains(value.LotId)).ToList();
        List<EntityId> createdIds = [];
        List<EntityId> deliveredIds = [];
        for (int index = 0; index < request.Selections.Count; index++)
        {
            SourceLotSelection selection = request.Selections[index];
            CountedSourceLot lot = inventory.Custody.GetLot(selection.LotId);
            ulong nextRevision = checked(lot.Revision + 1);
            ulong boundary = checked(lot.Source.Start + selection.Quantity);
            EntityId movedId = lot.LotId;
            ulong movedRevision = nextRevision;
            if (selection.Quantity < lot.Quantity)
            {
                // The canonical selection index allocates a distinct child even for several partial lots.
                movedId = EntityId.DeriveSpawned(commandId, checked((ulong)index));
                if (movedId == commandId || inventory.ContainsIdentity(movedId)
                    || production.ContainsIdentity(movedId) || createdIds.Contains(movedId)
                    || historicallyOccupied?.Invoke(movedId) == true)
                {
                    return new SourceDeliveryPlan(inventory, WorldActionRejection.Unavailable, [], []);
                }
                lots.Add(new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition,
                    new SourceInterval(lot.Source.SourceId, boundary, lot.Source.End), nextRevision));
                movedRevision = 0;
                createdIds.Add(movedId);
            }
            lots.Add(new CountedSourceLot(movedId, request.DestinationCustodianId, lot.Definition,
                new SourceInterval(lot.Source.SourceId, lot.Source.Start, boundary), movedRevision,
                LotProtection.None, request.ReservationId));
            deliveredIds.Add(movedId);
        }
        IEnumerable<CustodianStorage> storages = inventory.Custody.Storages.Select(storage =>
            storage.CustodianId == request.SourceCustodianId || storage.CustodianId == request.DestinationCustodianId
                ? storage with { Revision = checked(storage.Revision + 1) } : storage);
        var custody = new CustodySnapshot(inventory.WorldId, inventory.BranchId,
            checked(inventory.Custody.Revision + 1), storages, lots);
        CountedAuthorityState candidate = inventory.WithCustody(custody);
        production.ValidateForAuthority(candidate);
        return new SourceDeliveryPlan(candidate, WorldActionRejection.None, createdIds, deliveredIds);
    }
}
