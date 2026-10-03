using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

/// <summary>Checks public metadata spelling and disclosed definition families, without granting an action.</summary>
internal static class MaterialDescriptionValidation
{
    internal static void Validate(LocalWorldView view) => Validate(view.MaterialDescriptions, view.PlayerInventory,
        view.MaterialProposals, view.Crafting, view.GatheringTools);

    internal static void Validate(LocalControlledActorView view) => Validate(view.MaterialDescriptions, view.Inventory,
        view.MaterialProposals, view.Production?.Crafting, view.Production?.GatheringTools);

    private static void Validate(LocalMaterialDescriptions? descriptions, LocalInventoryView? inventory,
        LocalMaterialProposalView? proposals, LocalCraftingView? crafting, IEnumerable<LocalGatherToolLotView>? tools)
    {
        if (descriptions is null) return;
        var definitions = inventory?.Lots.Select(lot => lot.DefinitionId).ToHashSet() ?? new HashSet<EntityId>();
        if (crafting is not null) definitions.UnionWith(crafting.Recipes.SelectMany(recipe => recipe.Terms).Select(term => term.DefinitionId));
        if (proposals is not null) definitions.UnionWith(proposals.Offers.Where(offer => offer.Status == MaterialProposalStatus.Open)
            .Select(offer => offer.Terms.Definition.DefinitionId));
        var toolKinds = tools?.Select(tool => tool.Kind).ToHashSet() ?? [];
        foreach (var description in descriptions.Items)
        {
            if (!FarmMaterialDescriptionCatalog.Matches(description))
                throw new FormatException("A public item descriptor differs from its exact admitted content pin.");
            // Tools disclose their functional family but not an item-to-lot association. The host
            // joined the exact lot pin; this family check never turns the public name into a lot claim.
            bool toolFamily = description.Key switch
            {
                "items/copper_axe" => toolKinds.Contains(GatherToolKind.Axe),
                "items/sickle" => toolKinds.Contains(GatherToolKind.Sickle),
                _ => false
            };
            if (!definitions.Contains(description.DefinitionId) && !toolFamily)
                throw new FormatException("A public item descriptor is unrelated to the disclosed inventory, offers or production.");
        }
    }
}
