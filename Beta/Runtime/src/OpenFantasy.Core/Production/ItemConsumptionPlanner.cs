using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>Retires an exact raw or produced prefix into an explicit administered-material claim.</summary>
public sealed class ItemConsumptionPlanner
{
    public ItemConsumptionPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        TrustedCommandDelivery delivery, EntityId actorId, EntityId commandId, ConsumeItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(request);
        ItemConsumptionPlan Reject(WorldActionRejection reason) => new(inventory, production, reason);
        if (!inventory.CanControl(delivery, actorId)) return Reject(WorldActionRejection.NotAuthorized);
        CustodianAccessPolicy? policy = inventory.Policies.FirstOrDefault(value => value.CustodianId == request.CustodianId);
        if (policy is null || policy.OwnerActorId != actorId)
            return Reject(WorldActionRejection.NotAuthorized);
        // A malformed composition is a programmer/archive error, not an untrusted command refusal.
        production.ValidateForAuthority(inventory);
        if (!production.SupportsConsumption || !inventory.Actors.Contains(request.RecipientActorId)
            || production.ConsumedItems.Count == ProductionSnapshot.MaximumConsumedItems)
            return Reject(WorldActionRejection.Unavailable);
        CustodianStorage storage = inventory.Custody.GetStorage(request.CustodianId);
        if (storage.Revision != request.ExpectedStorageRevision || production.Revision != request.ExpectedProductionRevision)
            return Reject(WorldActionRejection.RevisionConflict);
        RetiredIngredient? selected = MaterialLotSelection.Select(inventory, production, request.CustodianId, request.Ingredient);
        if (selected is null || selected.Definition != request.ExpectedDefinition)
            return Reject(WorldActionRejection.Unavailable);
        EntityId consumptionId = EntityId.DeriveSpawned(commandId, 0);
        if (commandId == consumptionId || inventory.ContainsIdentity(commandId) || production.ContainsIdentity(commandId)
            || inventory.ContainsIdentity(consumptionId) || production.ContainsIdentity(consumptionId))
            return Reject(WorldActionRejection.Unavailable);
        try
        {
            return Prepare(inventory, production, actorId, commandId, consumptionId, request, selected);
        }
        catch (OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        catch (ProductionFormatException error) when (error.Failure == ProductionFormatFailure.LimitExceeded)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }

    private static ItemConsumptionPlan Prepare(CountedAuthorityState inventory, ProductionSnapshot production,
        EntityId actorId, EntityId commandId, EntityId consumptionId, ConsumeItemRequest request, RetiredIngredient selected)
    {
        var raw = new List<CountedSourceLot>();
        foreach (CountedSourceLot lot in inventory.Custody.Lots)
        {
            if (selected.Kind != IngredientLotKind.Source || lot.LotId != selected.LotId) raw.Add(lot);
            else if (selected.Quantity < lot.Quantity)
                raw.Add(new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition,
                    new SourceInterval(lot.Source.SourceId, selected.End, lot.Source.End), checked(lot.Revision + 1),
                    lot.Protection, lot.ReservedBy));
        }
        var derived = new List<TransformedLot>();
        foreach (TransformedLot lot in production.Lots)
        {
            if (selected.Kind != IngredientLotKind.Transformed || lot.LotId != selected.LotId) derived.Add(lot);
            else if (selected.Quantity < lot.Quantity)
                derived.Add(new TransformedLot(lot.LotId, lot.CustodianId, lot.Definition,
                    new ProductionOutputInterval(lot.Output.BatchId, lot.Output.OutputIndex, selected.End, lot.Output.End),
                    checked(lot.Revision + 1), lot.Protection, lot.ReservedBy));
        }
        var claim = new ConsumedItemClaim(consumptionId, commandId, actorId, request.RecipientActorId,
            request.CustodianId, selected);
        var custody = new CustodySnapshot(inventory.WorldId, inventory.BranchId, checked(inventory.Custody.Revision + 1),
            inventory.Custody.Storages.Select(storage => storage.CustodianId == request.CustodianId
                ? storage with { Revision = checked(storage.Revision + 1) } : storage), raw);
        CountedAuthorityState candidateInventory = inventory.WithCustody(custody);
        var candidateProduction = new ProductionSnapshot(production.WorldId, production.BranchId,
            checked(production.Revision + 1), production.Recipes, production.Batches, derived,
            [.. production.ConsumedItems, claim]);
        candidateProduction.ValidateForAuthority(candidateInventory);
        _ = ProductionCodecV2.Encode(candidateProduction);
        return new(candidateInventory, candidateProduction, WorldActionRejection.None, claim);
    }
}
