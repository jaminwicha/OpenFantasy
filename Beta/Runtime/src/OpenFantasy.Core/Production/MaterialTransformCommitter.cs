using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>Reservation of a newly created installed output; it grants no right to consume input stock.</summary>
internal sealed record InstalledOutputReservation(uint OutputIndex, EntityId ReservationId);

/// <summary>Shared private commit preparation after an ordinary or project owner has qualified exact input claims.</summary>
internal static class MaterialTransformCommitter
{
    internal static CraftPlan Prepare(CountedAuthorityState inventory, ProductionSnapshot production, EntityId actorId,
        EntityId commandId, CraftRequest request, CraftRecipeDefinition recipe, IReadOnlyList<RetiredIngredient> retired,
        EntityId[] created, InstalledOutputReservation? installation = null,
        Func<ProductionBatch, CountedAuthorityState, ConstructionCompletionEvidence>? constructionEvidence = null)
    {
        var selected = retired.ToDictionary(value => value.LotId);
        List<CountedSourceLot> rawLots = [];
        foreach (CountedSourceLot lot in inventory.Custody.Lots)
        {
            if (!selected.TryGetValue(lot.LotId, out RetiredIngredient? used))
            {
                rawLots.Add(lot);
            }
            else if (used.Quantity < lot.Quantity)
            {
                rawLots.Add(new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition,
                    new SourceInterval(lot.Source.SourceId, used.End, lot.Source.End), checked(lot.Revision + 1),
                    lot.Protection, lot.ReservedBy));
            }
        }
        List<TransformedLot> derivedLots = [];
        foreach (TransformedLot lot in production.Lots)
        {
            if (!selected.TryGetValue(lot.LotId, out RetiredIngredient? used))
            {
                derivedLots.Add(lot);
            }
            else if (used.Quantity < lot.Quantity)
            {
                derivedLots.Add(new TransformedLot(lot.LotId, lot.CustodianId, lot.Definition,
                    new ProductionOutputInterval(lot.Output.BatchId, lot.Output.OutputIndex, used.End, lot.Output.End),
                    checked(lot.Revision + 1), lot.Protection, lot.ReservedBy));
            }
        }
        if (rawLots.Count + derivedLots.Count + recipe.Outputs.Count > ProductionSnapshot.MaximumLots)
        {
            return new CraftPlan(inventory, production, WorldActionRejection.Unavailable, []);
        }
        var batch = new ProductionBatch(created[0], commandId, actorId, request.CustodianId,
            recipe.DefinitionId, recipe.Version, retired);
        for (int index = 0; index < recipe.Outputs.Count; index++)
        {
            RecipeTerm output = recipe.Outputs[index];
            derivedLots.Add(new TransformedLot(created[index + 1], request.CustodianId, recipe.ItemFor(output),
                new ProductionOutputInterval(batch.BatchId, (uint)index, 0, output.Count), 0,
                LotProtection.None, installation?.OutputIndex == (uint)index ? installation.ReservationId : null));
        }
        var custody = new CustodySnapshot(inventory.WorldId, inventory.BranchId, checked(inventory.Custody.Revision + 1),
            inventory.Custody.Storages.Select(storage => storage.CustodianId == request.CustodianId
                ? storage with { Revision = checked(storage.Revision + 1) } : storage), rawLots);
        CountedAuthorityState candidateInventory = inventory.WithCustody(custody);
        MaterialOperation? operation = null;
        if (production.MaterialHistory is not null)
        {
            if (installation is not null)
            {
                if (production.Continuation?.SupportsConstruction != true || constructionEvidence is null)
                    throw new ArgumentException("Reserved completion requires explicit V5 and genuine construction qualification.");
                operation = new MaterialConstructionOperation(checked(production.Revision + 1), constructionEvidence(batch, candidateInventory));
            }
            else
            {
                if (constructionEvidence is not null) throw new ArgumentException("Construction evidence requires its installed output reservation.");
                operation = new MaterialCraftOperation(checked(production.Revision + 1), batch);
            }
        }
        var candidateProduction = new ProductionSnapshot(production.WorldId, production.BranchId, checked(production.Revision + 1),
            production.Recipes, [.. production.Batches, batch], derivedLots,
            production.SupportsConsumption ? production.ConsumedItems : null, production.Continuation,
            production.MaterialHistory?.Append(production.Continuation!, operation!));
        candidateProduction.ValidateForAuthority(candidateInventory);
        _ = candidateProduction.ContentHash;
        return new CraftPlan(candidateInventory, candidateProduction, WorldActionRejection.None, created);
    }
}
