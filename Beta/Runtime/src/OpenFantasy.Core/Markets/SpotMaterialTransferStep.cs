using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Markets;

internal sealed record SpotMaterialOutcome(CountedAuthorityState Inventory,
    IReadOnlyCollection<TransformedLot> ProducedLots, EntityId? MovedLotId, SpotExchangeRejection Rejection);

/// <summary>Exact goods arithmetic after payment approval. This boundary grants no permission.</summary>
internal sealed class SpotMaterialTransferStep(CountedAuthorityState inventory,
    IReadOnlyCollection<TransformedLot> produced, Func<EntityId, bool> occupied)
{
    private const LotProtection AnyProtection = LotProtection.Equipped | LotProtection.Favorite
        | LotProtection.Heirloom | LotProtection.UserReserved;

    internal SpotMaterialOutcome Apply(SpotQuote quote, SpotAcceptance acceptance) =>
        quote.LotKind == SpotLotKind.CountedSource
            ? MoveCountedSource(quote, acceptance) : MoveProduced(quote, acceptance);

    private SpotMaterialOutcome Reject(SpotExchangeRejection reason) => new(inventory, produced, null, reason);

    private SpotMaterialOutcome MoveCountedSource(SpotQuote quote, SpotAcceptance acceptance)
    {
        CountedSourceLot? lot = inventory.Custody.Lots.FirstOrDefault(value => value.LotId == quote.LotId);
        SpotExchangeRejection stock = CheckStock(lot?.Definition, lot?.CustodianId, lot?.Revision, lot?.Quantity,
            lot?.Protection, lot?.ReservedBy, quote, acceptance);
        if (stock != SpotExchangeRejection.None)
        {
            return Reject(stock);
        }

        if (!Fits(quote.BuyerCustodianId, acceptance.Quantity)
            || acceptance.Quantity < lot!.Quantity && inventory.Custody.Lots.Count + produced.Count >= ProductionSnapshot.MaximumLots)
        {
            return Reject(SpotExchangeRejection.InsufficientCapacity);
        }

        try
        {
            CustodySnapshot custody = inventory.Custody;
            // The shared raw planner splits the exact prefix; the acceptance identity names any child.
            var request = new CommandEnvelope(acceptance.AcceptanceId, acceptance.PrincipalId, acceptance.OriginSessionId,
                quote.SellerActorId, quote.WorldId, quote.BranchId,
                [
                    new(RevisionTargetKind.Branch, quote.BranchId, custody.Revision),
                    new(RevisionTargetKind.Entity, quote.LotId, quote.ExpectedLotRevision),
                    new(RevisionTargetKind.Entity, quote.SellerCustodianId, custody.GetStorage(quote.SellerCustodianId).Revision),
                    new(RevisionTargetKind.Entity, quote.BuyerCustodianId, custody.GetStorage(quote.BuyerCustodianId).Revision)
                ],
                new TransferLotIntent(quote.LotId, quote.SellerCustodianId, quote.BuyerCustodianId, acceptance.Quantity));
            CustodyTransferPlan plan = new CustodyTransferPlanner().Plan(custody, request);
            if (plan.MovedLotId != quote.LotId && occupied(plan.MovedLotId))
            {
                return Reject(SpotExchangeRejection.IdentityConflict);
            }

            return new(inventory.WithCustody(plan.Candidate), produced, plan.MovedLotId, SpotExchangeRejection.None);
        }
        catch (CustodyConflictException conflict)
        {
            return Reject(conflict.Conflict switch
            {
                CustodyConflict.RevisionConflict => SpotExchangeRejection.RevisionConflict,
                CustodyConflict.CapacityExceeded => SpotExchangeRejection.InsufficientCapacity,
                CustodyConflict.IdentityCollision => SpotExchangeRejection.IdentityConflict,
                _ => SpotExchangeRejection.StockUnavailable
            });
        }
    }

    private SpotMaterialOutcome MoveProduced(SpotQuote quote, SpotAcceptance acceptance)
    {
        TransformedLot? lot = produced.FirstOrDefault(value => value.LotId == quote.LotId);
        SpotExchangeRejection stock = CheckStock(lot?.Definition, lot?.CustodianId, lot?.Revision, lot?.Quantity,
            lot?.Protection, lot?.ReservedBy, quote, acceptance);
        if (stock != SpotExchangeRejection.None)
        {
            return Reject(stock);
        }

        bool split = acceptance.Quantity < lot!.Quantity;
        if (!Fits(quote.BuyerCustodianId, acceptance.Quantity) || split && inventory.Custody.Lots.Count + produced.Count >= ProductionSnapshot.MaximumLots)
        {
            return Reject(SpotExchangeRejection.InsufficientCapacity);
        }

        List<TransformedLot> lots = produced.Where(value => value.LotId != lot.LotId).ToList();
        EntityId movedId = lot.LotId;
        if (split)
        {
            movedId = EntityId.DeriveSpawned(acceptance.AcceptanceId, CustodyTransferPlanner.SplitChildOrdinal);
            if (occupied(movedId))
            {
                return Reject(SpotExchangeRejection.IdentityConflict);
            }

            ulong boundary = checked(lot.Output.Start + acceptance.Quantity);
            lots.Add(new TransformedLot(lot.LotId, lot.CustodianId, lot.Definition,
                new ProductionOutputInterval(lot.Output.BatchId, lot.Output.OutputIndex, boundary, lot.Output.End),
                checked(lot.Revision + 1)));
            lots.Add(new TransformedLot(movedId, quote.BuyerCustodianId, lot.Definition,
                new ProductionOutputInterval(lot.Output.BatchId, lot.Output.OutputIndex, lot.Output.Start, boundary), 0));
        }
        else
        {
            lots.Add(new TransformedLot(lot.LotId, quote.BuyerCustodianId, lot.Definition, lot.Output,
                checked(lot.Revision + 1)));
        }

        // Storage revisions advance as for any custody change; the raw lots are untouched.
        CustodySnapshot custody = inventory.Custody;
        var touched = new CustodySnapshot(custody.WorldId, custody.BranchId, checked(custody.Revision + 1),
            custody.Storages.Select(storage => storage.CustodianId == quote.SellerCustodianId
                || storage.CustodianId == quote.BuyerCustodianId
                    ? storage with { Revision = checked(storage.Revision + 1) } : storage), custody.Lots);
        return new(inventory.WithCustody(touched), lots, movedId, SpotExchangeRejection.None);
    }

    private static SpotExchangeRejection CheckStock(CountedItemDefinition? definition, EntityId? custodianId,
        ulong? revision, ulong? quantity, LotProtection? protection, EntityId? reservedBy, SpotQuote quote,
        SpotAcceptance acceptance)
    {
        if (definition is null || definition != quote.Definition || custodianId != quote.SellerCustodianId)
        {
            return SpotExchangeRejection.StockUnavailable;
        }

        if (revision != quote.ExpectedLotRevision)
        {
            return SpotExchangeRejection.RevisionConflict;
        }

        // A sale requires unreserved stock without any manual or equipped protection.
        if (reservedBy is not null || (protection!.Value & AnyProtection) != 0)
        {
            return SpotExchangeRejection.ProtectedStock;
        }

        return acceptance.Quantity > quantity ? SpotExchangeRejection.StockUnavailable : SpotExchangeRejection.None;
    }

    // Raw and produced contents share one storage budget; wide arithmetic keeps the comparison exact.
    private bool Fits(EntityId destinationId, ulong arriving)
    {
        CustodianStorage? destination = inventory.Custody.Storages.FirstOrDefault(value => value.CustodianId == destinationId);
        if (destination is null)
        {
            return false;
        }

        UInt128 stored = inventory.Custody.GetStoredQuantity(destinationId);
        stored += produced.Where(value => value.CustodianId == destinationId).Aggregate(0UL, (total, value) => checked(total + value.Quantity));
        return stored + arriving <= destination.UnitCapacity;
    }

}
