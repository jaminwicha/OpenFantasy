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
        Func<EntityId, bool>? historicallyOccupied = null) =>
        Plan(inventory, production, delivery, actorId, commandId, request, historicallyOccupied, null);

    // Only complete-world composition may supply the authorization for its selected operation.
    internal SourceDeliveryPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        TrustedCommandDelivery delivery, EntityId actorId, EntityId commandId, SourceDeliveryRequest request,
        Func<EntityId, bool>? historicallyOccupied, WorldActorAuthorization? authorization)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(request);
        if (production.IsContinued && production.Continuation?.SupportsCompleteWorld != true)
        {
            throw new ArgumentException("Source delivery requires an admitted continuation transfer contract.", nameof(production));
        }
        if (production.IsContinued && historicallyOccupied is null)
        {
            throw new ArgumentException("Continued source delivery requires permanent historical identity lookup.", nameof(historicallyOccupied));
        }
        try
        {
            return Prepare(inventory, delivery, actorId, commandId, request,
                id => production.ContainsIdentity(id) || historicallyOccupied?.Invoke(id) == true,
                candidate => production.ValidateForAuthority(candidate), () => production.ValidateForAuthority(inventory), authorization);
        }
        catch (Exception error) when (error is ArgumentException or CustodyConflictException or OverflowException)
        {
            return new SourceDeliveryPlan(inventory, WorldActionRejection.Unavailable, [], []);
        }
    }

    internal static SourceDeliveryPlan Prepare(CountedAuthorityState inventory, TrustedCommandDelivery delivery,
        EntityId actorId, EntityId commandId, SourceDeliveryRequest request, Func<EntityId, bool> occupied,
        Action<CountedAuthorityState> validateCandidate, Action? validateInitial = null, WorldActorAuthorization? authorization = null)
    {
        SourceDeliveryPlan Reject(WorldActionRejection reason) => new(inventory, reason, [], []);
        if ((authorization is null ? !inventory.CanControl(delivery, actorId)
            : !authorization.Matches(delivery, actorId, commandId) || !authorization.Permits(WorldControlCapabilities.ConversationAndQuests))
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
            validateInitial?.Invoke();
            if (inventory.ContainsIdentity(commandId) || occupied(commandId))
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
            return Move(inventory, commandId, request, occupied, validateCandidate);
        }
        catch (Exception error) when (error is ArgumentException or CustodyConflictException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }

    private static SourceDeliveryPlan Move(CountedAuthorityState inventory, EntityId commandId, SourceDeliveryRequest request,
        Func<EntityId, bool> occupied, Action<CountedAuthorityState> validateCandidate)
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
                    || createdIds.Contains(movedId) || occupied(movedId))
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
        validateCandidate(candidate);
        return new SourceDeliveryPlan(candidate, WorldActionRejection.None, createdIds, deliveredIds);
    }
}
