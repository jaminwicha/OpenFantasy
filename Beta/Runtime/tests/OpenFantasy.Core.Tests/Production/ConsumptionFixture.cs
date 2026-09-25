using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

/// <summary>Exact supplied material dispositions, following real crafting; no care or healing admission is implied.</summary>
internal static class ConsumptionFixture
{
    internal static (CountedAuthorityState Inventory, ProductionSnapshot Production) ConsumedRaw()
    {
        var initial = Create();
        CountedSourceLot original = initial.Inventory.Custody.GetLot(Id(70));
        var claim = new ConsumedItemClaim(EntityId.DeriveSpawned(Id(9001), 0), Id(9001), Id(10), Id(11), Id(60),
            new RetiredIngredient(original.LotId, original.Revision, original.Definition,
                new SourceInterval(original.Source.SourceId, 0, 1)));
        var remainder = new CountedSourceLot(original.LotId, original.CustodianId, original.Definition,
            new SourceInterval(original.Source.SourceId, 1, original.Source.End), 1);
        CountedAuthorityState inventory = WithRawLots(initial.Inventory,
            initial.Inventory.Custody.Lots.Select(lot => lot.LotId == original.LotId ? remainder : lot));
        var production = new ProductionSnapshot(initial.Production.WorldId, initial.Production.BranchId, 1,
            initial.Production.Recipes, [], [], [claim]);
        production.ValidateForAuthority(inventory);
        return (inventory, production);
    }

    internal static (CountedAuthorityState Inventory, ProductionSnapshot Production) ConsumedProduct()
    {
        var initial = Create(chained: true);
        CraftPlan crafted = Craft(initial.Inventory, initial.Production);
        ProductionSnapshot basis = crafted.CandidateProduction;
        TransformedLot original = basis.Lots.Single(lot => lot.Definition == Rations);
        var claim = new ConsumedItemClaim(EntityId.DeriveSpawned(Id(9001), 0), Id(9001), Id(10), Id(11), Id(60),
            new RetiredIngredient(original.LotId, original.Revision, original.Definition,
                new ProductionOutputInterval(original.Output.BatchId, original.Output.OutputIndex, 0, 1)));
        var remainder = new TransformedLot(original.LotId, original.CustodianId, original.Definition,
            new ProductionOutputInterval(original.Output.BatchId, original.Output.OutputIndex, 1, 2), 1);
        var production = new ProductionSnapshot(basis.WorldId, basis.BranchId, 2, basis.Recipes, basis.Batches,
            basis.Lots.Select(lot => lot.LotId == original.LotId ? remainder : lot), [claim]);
        production.ValidateForAuthority(crafted.CandidateInventory);
        return (crafted.CandidateInventory, production);
    }

    internal static CountedAuthorityState WithRawLots(CountedAuthorityState basis, IEnumerable<CountedSourceLot> lots)
    {
        var custody = new CustodySnapshot(basis.WorldId, basis.BranchId, checked(basis.Custody.Revision + 1),
            basis.Custody.Storages.Select(storage => storage.CustodianId == Id(60)
                ? storage with { Revision = checked(storage.Revision + 1) } : storage), lots);
        return new(custody, basis.AdmittedUnitCapacity, basis.Actors, basis.Controls, basis.Policies,
            basis.Sources, basis.ReservationIds);
    }

    internal static ProductionSnapshot WithClaims(ProductionSnapshot basis, IEnumerable<ConsumedItemClaim> claims,
        IEnumerable<TransformedLot>? lots = null) => new(basis.WorldId, basis.BranchId, basis.Revision,
            basis.Recipes, basis.Batches, lots ?? basis.Lots, claims);

    internal static ConsumedItemClaim Change(ConsumedItemClaim claim, EntityId? id = null, EntityId? command = null,
        EntityId? actor = null, EntityId? recipient = null, EntityId? custodian = null, RetiredIngredient? ingredient = null) =>
        new(id ?? EntityId.DeriveSpawned(command ?? claim.CommandId, 0), command ?? claim.CommandId, actor ?? claim.ActorId,
            recipient ?? claim.RecipientActorId, custodian ?? claim.CustodianId, ingredient ?? claim.Ingredient);
}
