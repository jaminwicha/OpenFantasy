using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Projects names from the same root and only from the facets the observer can already inspect.</summary>
internal static class LocalMaterialDescriptionProjection
{
    internal static LocalMaterialDescriptions? Project(WorldSnapshot state, LocalWorldView view) =>
        Project(state, view.PlayerInventory, view.MaterialProposals, view.Crafting, view.GatheringTools);

    internal static LocalMaterialDescriptions? Project(WorldSnapshot state, LocalControlledActorView view) =>
        Project(state, view.Inventory, view.MaterialProposals, view.Production?.Crafting, view.Production?.GatheringTools);

    private static LocalMaterialDescriptions? Project(WorldSnapshot state, LocalInventoryView? inventory,
        LocalMaterialProposalView? proposals, LocalCraftingView? crafting, IEnumerable<LocalGatherToolLotView>? tools)
    {
        var pins = new HashSet<CountedItemDefinition>();
        var lots = inventory?.Lots.Select(lot => lot.LotId).ToHashSet() ?? [];
        if (tools is not null) lots.UnionWith(tools.Select(tool => tool.LotId));
        foreach (var lot in state.Inventory.Custody.Lots.Where(lot => lots.Contains(lot.LotId))) pins.Add(lot.Definition);
        if (state.Production is { } production)
        {
            foreach (var lot in production.Lots.Where(lot => lots.Contains(lot.LotId))) pins.Add(lot.Definition);
            foreach (var recipe in production.Recipes.Where(recipe => crafting?.Recipes.Any(visible =>
                visible.RecipeId == recipe.DefinitionId && visible.Version == recipe.Version) == true))
                foreach (var binding in recipe.Bindings) pins.Add(binding.Item);
        }
        foreach (var offer in proposals?.Offers.Where(offer => offer.Status == MaterialProposalStatus.Open) ?? [])
            pins.Add(offer.Terms.Definition);
        var descriptions = pins.Select(FarmMaterialDescriptionCatalog.Find).OfType<LocalMaterialDescription>().ToArray();
        return descriptions.Length == 0 ? null : new(descriptions);
    }
}
