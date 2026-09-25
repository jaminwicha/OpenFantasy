using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>Shared exact-prefix selection; access, purpose, identity and publication belong to callers.</summary>
internal static class MaterialLotSelection
{
    internal static RetiredIngredient? Select(CountedAuthorityState inventory, ProductionSnapshot production,
        EntityId custodianId, IngredientRequest selected)
    {
        if (selected.Kind == IngredientLotKind.Source)
        {
            CountedSourceLot? lot = inventory.Custody.Lots.FirstOrDefault(value => value.LotId == selected.LotId);
            if (lot is null || lot.CustodianId != custodianId || lot.Revision != selected.ExpectedRevision
                || lot.Protection != LotProtection.None || lot.ReservedBy is not null || selected.Quantity > lot.Quantity)
                return null;
            return new RetiredIngredient(lot.LotId, lot.Revision, lot.Definition,
                new SourceInterval(lot.Source.SourceId, lot.Source.Start, checked(lot.Source.Start + selected.Quantity)));
        }
        TransformedLot? derived = production.Lots.FirstOrDefault(value => value.LotId == selected.LotId);
        if (derived is null || derived.CustodianId != custodianId || derived.Revision != selected.ExpectedRevision
            || derived.Protection != LotProtection.None || derived.ReservedBy is not null || selected.Quantity > derived.Quantity)
            return null;
        return new RetiredIngredient(derived.LotId, derived.Revision, derived.Definition,
            new ProductionOutputInterval(derived.Output.BatchId, derived.Output.OutputIndex,
                derived.Output.Start, checked(derived.Output.Start + selected.Quantity)));
    }
}
