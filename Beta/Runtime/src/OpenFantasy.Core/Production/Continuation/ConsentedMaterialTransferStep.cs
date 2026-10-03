using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>The complete result of deciding one approval; a refusal returns the untouched inputs.</summary>
internal sealed record ConsentedMaterialTransferOutcome(WorldActionRejection Rejection,
    CountedAuthorityState Inventory, IReadOnlyCollection<TransformedLot> ProducedLots, EntityId? MovedLotId);

/// <summary>
/// Decides one bilateral approved movement against the actual inventory and live produced lots.
/// Planning, ordered replay and the world's paired decision share this step, so recorded evidence
/// is decided again, never trusted. Consent pins the goods and the approved lot revision only; custody,
/// production and storage revisions are read from the supplied current owners as execution state.
/// It stores no offer, changes no access policy and issues no source or producer rights.
/// </summary>
internal static class ConsentedMaterialTransferStep
{
    /// <param name="isOccupied">Permanent occupancy for the submitted command and acceptance identities.</param>
    /// <param name="isCreatedOccupied">Permanent occupancy for a split child, including later segment commands.</param>
    internal static ConsentedMaterialTransferOutcome Apply(CountedAuthorityState inventory,
        IReadOnlyCollection<TransformedLot> produced, ConsentedMaterialApproval approval,
        Func<EntityId, bool> isOccupied, Func<EntityId, bool> isCreatedOccupied) =>
        ApplySelected(inventory, produced, approval, isOccupied, isCreatedOccupied, null, null);

    internal static ConsentedMaterialTransferOutcome Apply(CountedAuthorityState inventory,
        IReadOnlyCollection<TransformedLot> produced, ConsentedMaterialApproval approval,
        Func<EntityId, bool> isOccupied, Func<EntityId, bool> isCreatedOccupied,
        WorldActorAuthorization sender, WorldActorAuthorization recipient)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(recipient);
        return ApplySelected(inventory, produced, approval, isOccupied, isCreatedOccupied, sender, recipient);
    }

    private static ConsentedMaterialTransferOutcome ApplySelected(CountedAuthorityState inventory,
        IReadOnlyCollection<TransformedLot> produced, ConsentedMaterialApproval approval,
        Func<EntityId, bool> isOccupied, Func<EntityId, bool> isCreatedOccupied,
        WorldActorAuthorization? sender, WorldActorAuthorization? recipient)
    {
        ConsentedMaterialTerms terms = approval.Command.Terms;
        ConsentedMaterialTransferOutcome Reject(WorldActionRejection reason) => new(reason, inventory, produced, null);
        // Authentication is decided first: an unapproved caller learns nothing about stock or identities.
        if (!(sender is null || recipient is null ? approval.IsApprovedByBothParticipants(inventory)
            : approval.IsApprovedByBothParticipants(inventory, sender, recipient)))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }

        EntityId commandId = approval.Command.CommandId;
        EntityId acceptanceId = approval.Acceptance.AcceptanceId;
        if (commandId == acceptanceId || isOccupied(commandId) || isOccupied(acceptanceId))
        {
            return Reject(WorldActionRejection.Unavailable);
        }

        CustodianStorage? source = inventory.Custody.Storages
            .FirstOrDefault(value => value.CustodianId == terms.SourceCustodianId);
        CustodianStorage? destination = inventory.Custody.Storages
            .FirstOrDefault(value => value.CustodianId == terms.DestinationCustodianId);
        if (source is null || destination is null)
        {
            return Reject(WorldActionRejection.Unavailable);
        }

        var move = new Movement(inventory, produced, approval, source, destination, isCreatedOccupied);
        return terms.LotKind switch
        {
            ConsentedMaterialLotKind.Produced => move.Produced(),
            ConsentedMaterialLotKind.CountedSource => move.CountedSource(),
            _ => Reject(WorldActionRejection.Unavailable)
        };
    }

    private sealed class Movement
    {
        private readonly CountedAuthorityState inventory;
        private readonly IReadOnlyCollection<TransformedLot> produced;
        private readonly ConsentedMaterialApproval approval;
        private readonly CustodianStorage source;
        private readonly CustodianStorage destination;
        private readonly Func<EntityId, bool> isCreatedOccupied;
        private readonly ConsentedMaterialTerms _terms;

        internal Movement(CountedAuthorityState currentInventory, IReadOnlyCollection<TransformedLot> currentProduced,
            ConsentedMaterialApproval decided, CustodianStorage sourceStorage, CustodianStorage destinationStorage,
            Func<EntityId, bool> createdOccupancy)
        {
            inventory = currentInventory;
            produced = currentProduced;
            approval = decided;
            source = sourceStorage;
            destination = destinationStorage;
            isCreatedOccupied = createdOccupancy;
            _terms = decided.Command.Terms;
        }

        internal ConsentedMaterialTransferOutcome Produced()
        {
            TransformedLot? lot = produced.FirstOrDefault(value => value.LotId == _terms.LotId);
            // Genuine reservation and equipped stock stay where they are; manual flags travel with the lot.
            if (lot is null || lot.Definition != _terms.Definition || lot.CustodianId != _terms.SourceCustodianId
                || lot.ReservedBy is not null || (lot.Protection & LotProtection.Equipped) != 0
                || _terms.Quantity > lot.Quantity)
            {
                return Reject(WorldActionRejection.Unavailable);
            }

            if (lot.Revision != _terms.ExpectedLotRevision)
            {
                return Reject(WorldActionRejection.RevisionConflict);
            }

            bool split = _terms.Quantity < lot.Quantity;
            if (!Fits(inventory, produced, _terms.Quantity) || split && !HasLotCapacity())
            {
                return Reject(WorldActionRejection.Unavailable);
            }

            List<TransformedLot> lots = produced.Where(value => value.LotId != lot.LotId).ToList();
            EntityId movedId = lot.LotId;
            if (split)
            {
                movedId = EntityId.DeriveSpawned(approval.Command.CommandId, CustodyTransferPlanner.SplitChildOrdinal);
                if (IsChildOccupied(movedId))
                {
                    return Reject(WorldActionRejection.Unavailable);
                }

                ulong boundary = checked(lot.Output.Start + _terms.Quantity);
                lots.Add(new TransformedLot(lot.LotId, lot.CustodianId, lot.Definition,
                    new ProductionOutputInterval(lot.Output.BatchId, lot.Output.OutputIndex, boundary, lot.Output.End),
                    checked(lot.Revision + 1), lot.Protection, lot.ReservedBy));
                lots.Add(new TransformedLot(movedId, destination.CustodianId, lot.Definition,
                    new ProductionOutputInterval(lot.Output.BatchId, lot.Output.OutputIndex, lot.Output.Start, boundary),
                    0, lot.Protection, lot.ReservedBy));
            }
            else
            {
                lots.Add(new TransformedLot(lot.LotId, destination.CustodianId, lot.Definition, lot.Output,
                    checked(lot.Revision + 1), lot.Protection, lot.ReservedBy));
            }

            CustodySnapshot custody = inventory.Custody;
            var moved = new CustodySnapshot(custody.WorldId, custody.BranchId, checked(custody.Revision + 1),
                custody.Storages.Select(storage => storage.CustodianId == _terms.SourceCustodianId
                    || storage.CustodianId == _terms.DestinationCustodianId
                        ? storage with { Revision = checked(storage.Revision + 1) } : storage), custody.Lots);
            return new(WorldActionRejection.None, inventory.WithCustody(moved), lots, movedId);
        }

        internal ConsentedMaterialTransferOutcome CountedSource()
        {
            CountedSourceLot? lot = inventory.Custody.Lots.FirstOrDefault(value => value.LotId == _terms.LotId);
            if (lot is null || lot.Definition != _terms.Definition
                || _terms.Quantity < lot.Quantity && !HasLotCapacity())
            {
                return Reject(WorldActionRejection.Unavailable);
            }

            try
            {
                // Execution revisions come from the actual current owners; only the lot revision was approved.
                CustodyTransferPlan plan = new CustodyTransferPlanner().Plan(inventory.Custody,
                    approval.Command.ToRawMovementRequest(inventory.Custody.Revision, source.Revision,
                        destination.Revision));
                CountedAuthorityState candidate = inventory.WithCustody(plan.Candidate);
                if (plan.MovedLotId != _terms.LotId && IsChildOccupied(plan.MovedLotId)
                    || !Fits(candidate, produced, 0))
                {
                    return Reject(WorldActionRejection.Unavailable);
                }

                return new(WorldActionRejection.None, candidate, produced, plan.MovedLotId);
            }
            catch (CustodyConflictException conflict)
            {
                return Reject(conflict.Conflict == CustodyConflict.RevisionConflict
                    ? WorldActionRejection.RevisionConflict : WorldActionRejection.Unavailable);
            }
        }

        private ConsentedMaterialTransferOutcome Reject(WorldActionRejection reason) =>
            new(reason, inventory, produced, null);

        private bool IsChildOccupied(EntityId childId) => childId == approval.Command.CommandId
            || childId == approval.Acceptance.AcceptanceId || isCreatedOccupied(childId);

        private bool HasLotCapacity() =>
            inventory.Custody.Lots.Count + produced.Count < ProductionSnapshot.MaximumLots;

        // Raw and produced contents share one storage; wide arithmetic keeps the comparison exact.
        private bool Fits(CountedAuthorityState current, IReadOnlyCollection<TransformedLot> lots, ulong arriving)
        {
            UInt128 stored = current.Custody.GetStoredQuantity(destination.CustodianId);
            foreach (TransformedLot lot in lots.Where(value => value.CustodianId == destination.CustodianId))
            {
                stored += lot.Quantity;
            }

            return stored + arriving <= destination.UnitCapacity;
        }
    }
}
