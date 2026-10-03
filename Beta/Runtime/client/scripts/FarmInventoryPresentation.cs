using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

internal sealed record FarmInventoryScope(EntityId WorldId, EntityId BranchId, EntityId ActorId,
    LocalActorSelection? Selection);

/// <summary>Permitted pack facts, not a substitute full-world observation or another inventory owner.</summary>
internal sealed class FarmInventoryPresentation
{
    private FarmInventoryPresentation(LocalWorldView view, LocalActorSelection? selection)
    {
        Scope = new(view.WorldId, view.BranchId, view.PlayerActorId, selection);
        Calendar = view.Calendar;
        Inventory = view.PlayerInventory;
        MaterialProposals = view.MaterialProposals;
        Actors = view.Actors;
        GatheringTools = view.GatheringTools;
        Restoratives = view.Restorative?.SelfUseOffers.ToArray() ?? [];
        RationsRecipe = FarmCrafting.RationsRecipe(view);
        CanReadHistory = true;
        ContextBlocker = view.HasReadingHold ? "Finish the current conversation before changing gifts."
            : view.IsResolvingConsequences ? "Wait for the current world changes to finish."
            : view.Care?.PendingAcknowledgmentId is not null ? "The accepted recovery treatment must finish first."
            : view.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration
                ? "Gifts are available during ordinary exploration."
            : view.EncounterLifecycle is { PlayerCanAct: false } ? "You cannot act until your current recovery ends."
            : null;
    }

    private FarmInventoryPresentation(LocalSelectedPlayerObservation observation, LocalControlledActorView view)
    {
        Scope = new(view.WorldId, view.BranchId, view.ActorId, observation.Selection);
        Calendar = view.Calendar;
        Inventory = view.Inventory ?? throw new InvalidOperationException("Inventory permission is not available for this character.");
        MaterialProposals = view.MaterialProposals;
        Actors = view.Actors;
        GatheringTools = view.Production?.GatheringTools.ToArray() ?? [];
        Restoratives = view.Recovery?.SelfUseOffers.ToArray() ?? [];
        RationsRecipe = view.Production?.Crafting?.Recipes.SingleOrDefault(recipe => recipe.Key == "craft_rations");
        CanReadHistory = (view.Capabilities & WorldControlCapabilities.PrivateHistory) != 0;
        // Phase and holds are shared world facts. Never borrow the home actor's readiness or care state.
        ContextBlocker = view.HasReadingHold ? "Finish the current conversation before changing gifts."
            : view.IsResolvingConsequences ? "Wait for the current world changes to finish."
            : observation.HomeView.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration
                ? "Gifts are available during ordinary exploration."
            : view.Actors.Single(actor => actor.ActorId == view.ActorId).IsIncapacitated
                ? "This character cannot act while incapacitated."
            : view.Recovery?.Care?.PendingAcknowledgmentId is not null
                ? "The accepted recovery treatment must finish first."
            : view.Combat?.Lifecycle is { PlayerCanAct: false }
                ? "This character cannot act until current recovery ends." : null;
    }

    internal FarmInventoryScope Scope { get; }
    internal EntityId WorldId => Scope.WorldId;
    internal EntityId BranchId => Scope.BranchId;
    internal EntityId ActorId => Scope.ActorId;
    internal LocalActorSelection? Selection => Scope.Selection;
    internal WorldInstant Calendar { get; }
    internal LocalInventoryView Inventory { get; }
    internal LocalMaterialProposalView? MaterialProposals { get; }
    internal IReadOnlyList<LocalActorView> Actors { get; }
    internal IReadOnlyList<LocalGatherToolLotView> GatheringTools { get; }
    internal IReadOnlyList<LocalRestorativeUseView> Restoratives { get; }
    internal LocalCraftRecipeView? RationsRecipe { get; }
    internal bool CanReadHistory { get; }
    internal string? ContextBlocker { get; }
    internal bool IsSelected => Selection is { ControlWitness: not null };
    internal string ActorName => Name(ActorId);
    internal string AvailabilityNote => IsSelected
        ? "Confirmation checks current readiness, reach, and stock."
        : "Confirmation checks current availability.";

    internal static FarmInventoryPresentation From(LocalWorldView view) => new(view, null);

    internal static FarmInventoryPresentation From(LocalSelectedPlayerObservation observation) =>
        observation.ControlledView is { } controlled ? new(observation, controlled)
            : new(observation.HomeView, observation.Selection);

    internal string Name(EntityId? actor) => actor is null ? "Choose a person"
        : Actors.FirstOrDefault(value => value.ActorId == actor)?.DisplayName ?? $"Person {actor}";

    internal string ItemName(EntityId definition, EntityId? woodDefinition = null, EntityId? berryDefinition = null)
    {
        if (definition == woodDefinition) return "Wood";
        if (definition == berryDefinition) return "Berries";
        if (Restoratives.FirstOrDefault(value => value.ItemDefinitionId == definition) is { } restorative)
            return restorative.ItemName;
        if (GatheringTools.FirstOrDefault(tool => Inventory.Lots.Any(lot =>
            lot.LotId == tool.LotId && lot.DefinitionId == definition)) is { } tool)
            return tool.Kind == GatherToolKind.Axe ? "Axe" : "Sickle";
        return RationsRecipe?.Terms.FirstOrDefault(term => term.DefinitionId == definition)?.Role switch
        {
            RecipeMaterialRole.Output => "Rations",
            RecipeMaterialRole.Waste => "Wood offcuts",
            _ => $"Unfamiliar item ({definition})"
        };
    }
}
