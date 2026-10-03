using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Operated production facts, independent of permission to inspect the entire inventory.</summary>
internal sealed class FarmProductionPresentation
{
    private FarmProductionPresentation(LocalWorldView view)
    {
        ActorId = view.PlayerActorId;
        Feet = view.Actors.Single(actor => actor.ActorId == ActorId).Feet;
        GatheringNodes = view.GatheringNodes;
        GatheringTools = view.GatheringTools;
        GatherEffort = view.PlayerGatherEffort;
        Worksite = view.Worksite;
        HasCrafting = view.Crafting is not null;
        DefaultRecipe = FarmCrafting.RationsRecipe(view);
        CraftSelection = FarmCrafting.SelectRations(view);
        CanTraverse = true;
        PackName = "Your pack";
    }

    private FarmProductionPresentation(LocalSelectedPlayerObservation observation, LocalControlledActorView view)
    {
        var production = view.Production
            ?? throw new InvalidOperationException("Production permission is not available for this character.");
        ActorId = view.ActorId;
        var actor = view.Actors.Single(value => value.ActorId == ActorId);
        Feet = actor.Feet;
        GatheringNodes = production.GatheringNodes;
        GatheringTools = production.GatheringTools;
        GatherEffort = production.GatherEffort;
        Worksite = production.Worksite;
        HasCrafting = production.Crafting is not null;
        var recipes = production.CraftPreparation?.Recipes;
        DefaultRecipe = recipes?.SingleOrDefault(recipe => recipe.Key == "craft_rations")
            ?? (recipes is { Count: 1 } ? recipes[0] : null);
        var request = DefaultRecipe is null ? null : production.CraftPreparation!.Prepare(DefaultRecipe.RecipeId);
        CraftSelection = new(request, request is not null ? null
            : DefaultRecipe is null ? "Choose a specific known recipe before crafting."
            : DefaultRecipe.Key == "craft_rations" ? FarmCrafting.MissingIngredients
            : "The required ingredients are not currently available.");
        CanTraverse = (view.Capabilities & WorldControlCapabilities.Traversal) != 0;
        PackName = actor.DisplayName + "'s pack";
        // Shared world conditions may be read from home. Personal readiness must belong to the operated actor.
        ContextBlocker = view.HasReadingHold ? "Finish the current conversation before working."
            : view.IsResolvingConsequences ? "Wait for the accepted world change to finish."
            : observation.HomeView.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration
                ? "Production is available during ordinary exploration."
            : actor.IsIncapacitated ? "This character cannot work while incapacitated."
            : view.Recovery?.Care?.PendingAcknowledgmentId is not null
                ? "The accepted recovery treatment must finish first."
            : view.Combat?.Lifecycle is { PlayerCanAct: false }
                ? "This character cannot work until current recovery ends." : null;
    }

    internal EntityId ActorId { get; }
    internal SubunitPosition Feet { get; }
    internal IReadOnlyList<LocalGatherNodeView> GatheringNodes { get; }
    internal IReadOnlyList<LocalGatherToolLotView> GatheringTools { get; }
    internal uint? GatherEffort { get; }
    internal LocalWorksiteView? Worksite { get; }
    internal bool HasCrafting { get; }
    internal LocalCraftRecipeView? DefaultRecipe { get; }
    internal FarmCraftSelection CraftSelection { get; }
    internal bool CanTraverse { get; }
    internal string PackName { get; }
    internal string? ContextBlocker { get; }

    internal static FarmProductionPresentation From(LocalWorldView view) => new(view);
    internal static FarmProductionPresentation From(LocalSelectedPlayerObservation observation) =>
        observation.ControlledView is { } controlled ? new(observation, controlled) : new(observation.HomeView);

    internal LocalGatherNodeView? NearestGatherNode() => GatheringNodes.Where(node => node.RemainingSwings > 0)
        .OrderBy(node => DistanceSquared(node.Feet, Feet)).ThenBy(node => node.NodeId).FirstOrDefault();

    internal bool CanReach(LocalGatherNodeView node) =>
        Feet.Z == node.Feet.Z && DistanceSquared(node.Feet, Feet) <= 448L * 448;

    private static Int128 DistanceSquared(SubunitPosition left, SubunitPosition right)
    {
        Int128 dx = (Int128)left.X - right.X;
        Int128 dy = (Int128)left.Y - right.Y;
        return dx * dx + dy * dy;
    }
}
