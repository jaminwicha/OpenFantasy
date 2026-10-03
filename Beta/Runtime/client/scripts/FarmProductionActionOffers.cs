using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>One field-production offer path for home and permitted operated characters.</summary>
internal static class FarmProductionActionOffers
{
    internal static void Add(IDictionary<FarmSemanticAction, FarmActionOffer> offers,
        FarmProductionPresentation view, FarmActionScope scope, FarmTarget? target,
        EntityId woodDefinition, EntityId? berryDefinition)
    {
        if (view.GatherEffort.HasValue)
            offers[FarmSemanticAction.Gather] = Gather(view, scope, target);
        if (view.HasCrafting)
            offers[FarmSemanticAction.Craft] = Craft(view, scope, woodDefinition, berryDefinition);
        WorksiteActionOffers.Add(offers, view.Worksite, view.Feet, scope, target, view.CanTraverse, view.ContextBlocker);
        ConstructionActionOffers.Add(offers, view.ConstructionWork, scope, target);
    }

    private static FarmActionOffer Gather(FarmProductionPresentation view, FarmActionScope scope, FarmTarget? selected)
    {
        LocalGatherNodeView? node = selected is null ? view.NearestGatherNode()
            : view.GatheringNodes.FirstOrDefault(candidate => candidate.NodeId == selected.Id);
        string cost = $"{GatherRulesV1.EffortPerSwing} effort";
        string detail = $"You have {view.GatherEffort} effort.";
        if (node is null && selected is { } target)
        {
            string reason = target.Kind == FarmTargetKind.Person ? "Choose a plant to gather, or clear the selection."
                : "The selected plant is no longer visible. Choose another target or clear the selection.";
            return new(FarmSemanticAction.Gather, scope, "Gather", target.Name, target.Id, cost, "", null,
                view.ContextBlocker ?? reason, detailText: detail);
        }
        if (node is null)
            return new(FarmSemanticAction.Gather, scope, "Gather", "No plant nearby", null, cost, "", null,
                view.ContextBlocker ?? "These plants need time to recover.", detailText: detail);
        string label = node.RequiredTool == GatherToolKind.Axe ? "Chop oak" : "Pick berries";
        LocalGatherToolLotView? tool = view.GatheringTools.FirstOrDefault(value => value.Kind == node.RequiredTool);
        if (tool is null)
            return new(FarmSemanticAction.Gather, scope, label, node.DisplayName, node.NodeId, cost, "", null,
                view.ContextBlocker ?? "You need the right tool in your pack.", detailText: detail);
        string? blocker = view.ContextBlocker ?? (node.RemainingSwings == 0 || node.Visibility != GatherNodeVisibility.Ready
            ? "This plant needs time to recover."
            : !view.CanReach(node) ? $"Move closer to the {node.DisplayName}."
            : view.GatherEffort < GatherRulesV1.EffortPerSwing ? "You are too tired to gather; rest a moment." : null);
        return new(FarmSemanticAction.Gather, scope, label, node.DisplayName, node.NodeId, cost,
            "You gathered supplies.", new GatherIntent(node.NodeId, node.Revision, tool.LotId, tool.Revision), blocker,
            detailText: detail);
    }

    private static FarmActionOffer Craft(FarmProductionPresentation view, FarmActionScope scope,
        EntityId woodDefinition, EntityId? berryDefinition)
    {
        var recipe = view.DefaultRecipe;
        if (recipe is null)
            return new(FarmSemanticAction.Craft, scope, "Pack rations", view.PackName, null, "Unknown recipe", "", null,
                view.ContextBlocker ?? view.CraftSelection.BlockingReason ?? FarmCrafting.MissingRecipe);
        string cost = string.Join(", ", recipe.Terms.Where(term => term.Role == RecipeMaterialRole.Input)
            .Select(term => $"{term.Quantity} {MaterialName(term.DefinitionId, woodDefinition, berryDefinition)}"));
        var selection = view.CraftSelection;
        bool rations = recipe.Key == "craft_rations";
        return new(FarmSemanticAction.Craft, scope, rations ? "Pack rations" : "Craft " + recipe.Key,
            view.PackName, null, cost,
            rations ? "You packed two rations and kept the wood offcuts." : "The selected recipe was crafted.",
            selection.Request is null ? null : new CraftIntent(selection.Request), view.ContextBlocker ?? selection.BlockingReason);
    }

    private static string MaterialName(EntityId definition, EntityId wood, EntityId? berries) =>
        definition == wood ? "wood" : definition == berries ? "berries" : "material";
}
