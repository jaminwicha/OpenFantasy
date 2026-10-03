using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Only available ingredients of the admitted crafting recipes, with exact current request pins.</summary>
public sealed class LocalControlledCraftPreparation
{
    internal LocalControlledCraftPreparation(WorldSnapshot state, LocalWorldView view)
    {
        Recipes = Array.AsReadOnly(view.Crafting!.Recipes.ToArray());
        ProductionRevision = view.Crafting.Revision;
        CustodianId = view.PlayerInventory.CustodianId;
        StorageRevision = view.PlayerInventory.Revision;
        var inputs = Recipes.SelectMany(recipe => recipe.Terms).Where(term => term.Role == RecipeMaterialRole.Input)
            .Select(term => term.DefinitionId).ToHashSet();
        var available = state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == CustodianId
                && lot.ReservedBy is null && lot.Protection == LotProtection.None).Select(lot => lot.LotId)
            .Concat(state.Production!.Lots.Where(lot => lot.CustodianId == CustodianId
                && lot.ReservedBy is null && lot.Protection == LotProtection.None).Select(lot => lot.LotId)).ToHashSet();
        Ingredients = Array.AsReadOnly(view.PlayerInventory.Lots.Where(lot => lot.AvailableForDelivery
            && inputs.Contains(lot.DefinitionId) && available.Contains(lot.LotId)).ToArray());
    }

    public ReadOnlyCollection<LocalCraftRecipeView> Recipes { get; }
    public ulong ProductionRevision { get; }
    public EntityId CustodianId { get; }
    public ulong StorageRevision { get; }
    public ReadOnlyCollection<LocalItemLotView> Ingredients { get; }

    public CraftRequest? Prepare(EntityId recipeId)
    {
        var recipe = Recipes.SingleOrDefault(value => value.RecipeId == recipeId);
        if (recipe is null) return null;
        // Minimize selections so small fragments cannot hide an otherwise representable request.
        List<IngredientRequest> selected = [];
        foreach (var term in recipe.Terms.Where(term => term.Role == RecipeMaterialRole.Input))
        {
            ulong remaining = term.Quantity;
            foreach (var lot in Ingredients.Where(lot => lot.DefinitionId == term.DefinitionId)
                .OrderByDescending(lot => lot.Quantity).ThenBy(lot => lot.LotId))
            {
                ulong quantity = Math.Min(remaining, lot.Quantity);
                if (quantity == 0) continue;
                if (selected.Count == MaterialRecipeDefinition.MaximumTermCount) return null;
                selected.Add(new(lot.Provenance, lot.LotId, lot.Revision, quantity));
                remaining -= quantity;
                if (remaining == 0) break;
            }
            if (remaining != 0) return null;
        }
        return selected.Count == 0 ? null
            : new(recipe.RecipeId, recipe.Version, ProductionRevision, CustodianId, StorageRevision, selected);
    }
}

/// <summary>Quest concurrency and eligible delivery materials, without personal XP, level, wallet or unrelated lots.</summary>
public sealed class LocalControlledQuestPreparation
{
    private readonly IReadOnlyDictionary<EntityId, ulong> _giverRevisions;

    internal LocalControlledQuestPreparation(LocalWorldView view)
    {
        ProgressionRevision = view.Progression!.Revision;
        InventoryRevision = view.PlayerInventory.Revision;
        Quests = Array.AsReadOnly(view.Quests.Where(quest => quest.Status != ActorQuestStatus.Completed).ToArray());
        var definitions = Quests.Select(quest => quest.ItemDefinitionId).ToHashSet();
        DeliveryLots = Array.AsReadOnly(view.PlayerInventory.Lots.Where(lot => lot.AvailableForDelivery
            && lot.Provenance == IngredientLotKind.Source && definitions.Contains(lot.DefinitionId)).ToArray());
        _giverRevisions = new ReadOnlyDictionary<EntityId, ulong>(view.Actors
            .Where(actor => Quests.Any(quest => quest.GiverActorId == actor.ActorId))
            .ToDictionary(actor => actor.ActorId, actor => actor.PresenceRevision));
    }

    public ulong ProgressionRevision { get; }
    public ulong InventoryRevision { get; }
    public ReadOnlyCollection<LocalQuestView> Quests { get; }
    public ReadOnlyCollection<LocalItemLotView> DeliveryLots { get; }

    public DeliveryQuestIntent? Prepare(EntityId questId)
    {
        var quest = Quests.SingleOrDefault(value => value.DefinitionId == questId);
        if (quest is null || !_giverRevisions.TryGetValue(quest.GiverActorId, out ulong giverRevision)) return null;
        if (quest.Status is null && quest.CanAccept)
            return new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
                giverRevision, quest.ParticipationRevision, ProgressionRevision);
        if (!quest.CanComplete) return null;
        List<SourceLotSelection> selected = [];
        ulong remaining = quest.RequiredQuantity;
        foreach (var lot in DeliveryLots.Where(lot => lot.DefinitionId == quest.ItemDefinitionId)
            .OrderByDescending(lot => lot.Quantity).ThenBy(lot => lot.LotId))
        {
            ulong quantity = Math.Min(remaining, lot.Quantity);
            if (quantity == 0) continue;
            if (selected.Count == SourceDeliveryRequest.MaximumSelections) return null;
            selected.Add(new(lot.LotId, lot.Revision, quantity));
            remaining -= quantity;
            if (remaining == 0) break;
        }
        return remaining != 0 || selected.Count == 0 ? null
            : new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
                giverRevision, quest.ParticipationRevision, ProgressionRevision, InventoryRevision, selected);
    }
}

/// <summary>Exact work requests for the current builder's visible unfinished project and owned matching tools.</summary>
public sealed class LocalControlledConstructionPreparation
{
    private LocalControlledConstructionPreparation(EntityId projectId, IEnumerable<WorkConstructionIntent> choices)
    {
        ProjectId = projectId;
        Choices = Array.AsReadOnly(choices.ToArray());
    }

    public EntityId ProjectId { get; }
    public ReadOnlyCollection<WorkConstructionIntent> Choices { get; }
    public WorkConstructionIntent? Prepare(EntityId toolLotId) => Choices.SingleOrDefault(choice => choice.ToolLotId == toolLotId);

    internal static ReadOnlyCollection<LocalControlledConstructionPreparation> Project(WorldSnapshot state, LocalWorldView view)
    {
        var visible = view.Construction.Where(project => !project.IsCompleted && project.BuilderActorId == view.PlayerActorId)
            .Select(project => project.ProjectId).ToHashSet();
        return Array.AsReadOnly(state.Construction!.Projects.Where(project => visible.Contains(project.ProjectId))
            .Select(project => new LocalControlledConstructionPreparation(project.ProjectId,
                state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == view.PlayerInventory.CustodianId
                    && lot.Definition == project.Definition.RequiredTool.Item && lot.ReservedBy is null
                    && lot.Protection == LotProtection.None).OrderBy(lot => lot.LotId)
                    .Select(lot => new WorkConstructionIntent(project.ProjectId, project.Revision,
                        state.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, lot.LotId, lot.Revision))))
            .ToArray());
    }
}
