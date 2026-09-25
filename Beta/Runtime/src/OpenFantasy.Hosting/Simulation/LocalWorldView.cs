using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed record LocalActorView(EntityId ActorId, string DisplayName, SubunitPosition Feet,
    ulong PresenceRevision, ResidentControllerKind ControllerKind);
public sealed record LocalItemLotView(EntityId LotId, EntityId DefinitionId, ulong Revision, ulong Quantity,
    IngredientLotKind Provenance = IngredientLotKind.Source, bool AvailableForDelivery = true);
public sealed record LocalDialogueView(EntityId DialogueId, EntityId SpeakerActorId, string SpeakerName, string Text);

public sealed class LocalInventoryView
{
    internal LocalInventoryView(EntityId custodianId, ulong revision, IEnumerable<LocalItemLotView> lots)
    {
        CustodianId = custodianId;
        Revision = revision;
        Lots = Array.AsReadOnly(lots.OrderBy(lot => lot.LotId).ToArray());
        Quantity = Lots.Aggregate(0UL, (total, lot) => checked(total + lot.Quantity));
    }

    public EntityId CustodianId { get; }
    public ulong Revision { get; }
    public ulong Quantity { get; }
    public ReadOnlyCollection<LocalItemLotView> Lots { get; }
}

/// <summary>
/// Public projection of the admitted sight-complete local profile. This is not a global visibility
/// system; other actors' inventory, source claims, private commit counters and hashes are excluded.
/// </summary>
public sealed class LocalWorldView
{
    internal LocalWorldView(WorldSnapshot state, EntityId playerActorId)
    {
        WorldId = state.WorldId;
        BranchId = state.BranchId;
        PlayerActorId = playerActorId;
        Calendar = state.Calendar;
        HasReadingHold = state.HasReadingHold;
        IsResolvingConsequences = state.PendingExcavation is not null;
        Actors = Array.AsReadOnly(state.Residents.Select(resident =>
        {
            var presence = state.Space.GetActor(resident.ActorId);
            return new LocalActorView(resident.ActorId, resident.DisplayName, presence.Feet,
                presence.Revision, resident.ControllerKind);
        }).ToArray());
        EntityId storage = state.GetResident(playerActorId).InventoryCustodianId;
        PlayerInventory = new LocalInventoryView(storage, state.Inventory.Custody.GetStorage(storage).Revision,
            state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == storage)
                .Select(lot => new LocalItemLotView(lot.LotId, lot.Definition.DefinitionId, lot.Revision, lot.Quantity,
                    IngredientLotKind.Source, lot.Protection == LotProtection.None && lot.ReservedBy is null))
                .Concat(state.Production?.Lots.Where(lot => lot.CustodianId == storage).Select(lot =>
                    new LocalItemLotView(lot.LotId, lot.Definition.DefinitionId, lot.Revision, lot.Quantity, IngredientLotKind.Transformed)) ?? []));
        Crafting = state.Production is { } production ? new LocalCraftingView(production, state.Construction is { } construction
            ? construction.IsConstructionRecipe : null) : null;
        GatheringNodes = Array.AsReadOnly(state.Gathering is { } gathering
            ? gathering.Nodes.Where(node => node.VisibilityAt(state.AdvancingSteps) != GatherNodeVisibility.Absent)
                .Select(node => new LocalGatherNodeView(node.NodeId, node.Definition.DisplayName, node.Feet,
                    node.Revision, node.Definition.Kind, node.Definition.RequiredToolKind,
                    (uint)node.RemainingSwings, node.VisibilityAt(state.AdvancingSteps)))
                .OrderBy(node => node.NodeId).ToArray()
            : []);
        List<LocalGatherToolLotView> tools = [];
        if (state.Gathering is { } admittedGathering)
        {
            PlayerGatherEffort = (uint)admittedGathering.GetActorWork(playerActorId).Effort;
            foreach (var lot in state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == storage))
            {
                var tool = admittedGathering.Tools.FirstOrDefault(value => value.Definition == lot.Definition);
                if (tool is not null)
                {
                    tools.Add(new LocalGatherToolLotView(lot.LotId, lot.Revision, tool.Kind, tool.Power));
                }
            }
        }
        GatheringTools = Array.AsReadOnly(tools.OrderBy(tool => tool.LotId).ToArray());
        Worksite = state.Excavation is not null && state.ConstructionPhysical is not null
            ? new LocalWorksiteView(state, playerActorId) : null;
        Liquids = state.LiquidCells is not null ? new LocalLiquidView(state) : null;
        DialogueSession? dialogue = state.Dialogues.FirstOrDefault(value => value.ListenerActorId == playerActorId);
        if (dialogue is not null)
        {
            WorldResident speaker = state.GetResident(dialogue.SpeakerActorId);
            Dialogue = new LocalDialogueView(dialogue.DialogueId, speaker.ActorId, speaker.DisplayName,
                speaker.OpeningUtterance!.Text);
        }
        Quests = ProjectQuests(state, playerActorId, dialogue);
        Construction = Array.AsReadOnly(state.Construction?.Projects.Where(project => project.IsCompleted
                || state.Quests!.FindParticipation(playerActorId, project.QuestId) is not null)
            .Select(project => new LocalConstructionView(project.ProjectId, project.BuilderActorId,
                project.Definition.DisplayName, project.SiteFeet, project.WorkFeet, project.Progress.CompletedUnits,
                project.Definition.RequiredWork.Value, project.IsCompleted)).ToArray() ?? []);
        if (state.Progression is { } progression)
        {
            var actor = progression.GetActor(playerActorId);
            Progression = new LocalProgressionView(actor.Revision, actor.Profile.DisplayName, actor.Level,
                actor.Experience, actor.CurrentHp, actor.Stats.MaxHp, actor.CurrentMp, actor.Stats.MaxMp,
                progression.Currency.GetActorAccount(playerActorId).Balance, progression.Currency.Currency.UnitName);
        }
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId PlayerActorId { get; }
    public WorldInstant Calendar { get; }
    public bool HasReadingHold { get; }
    public bool IsResolvingConsequences { get; }
    public ReadOnlyCollection<LocalActorView> Actors { get; }
    public LocalInventoryView PlayerInventory { get; }
    public ReadOnlyCollection<LocalGatherNodeView> GatheringNodes { get; }
    public ReadOnlyCollection<LocalGatherToolLotView> GatheringTools { get; }
    public uint? PlayerGatherEffort { get; }
    public LocalWorksiteView? Worksite { get; }
    public LocalLiquidView? Liquids { get; }
    public LocalCraftingView? Crafting { get; }
    public LocalDialogueView? Dialogue { get; }
    public LocalProgressionView? Progression { get; }
    public ReadOnlyCollection<LocalQuestView> Quests { get; }
    public ReadOnlyCollection<LocalConstructionView> Construction { get; }

    private ReadOnlyCollection<LocalQuestView> ProjectQuests(WorldSnapshot world, EntityId actorId, DialogueSession? dialogue)
    {
        List<LocalQuestView> views = [];
        if (world.Quests is not { } quests || world.Progression is not { } progression)
        {
            return views.AsReadOnly();
        }
        var actor = progression.GetActor(actorId);
        foreach (DeliveryQuestDefinition definition in quests.Definitions)
        {
            ActorQuestState? participation = quests.FindParticipation(actorId, definition.DefinitionId);
            bool addressed = dialogue?.SpeakerActorId == definition.GiverActorId;
            if (!addressed && participation is null)
            {
                continue;
            }
            ulong eligible = PlayerInventory.Lots.Where(lot => lot.Provenance == IngredientLotKind.Source
                && lot.AvailableForDelivery && lot.DefinitionId == definition.Item.DefinitionId)
                .Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));
            var reserve = progression.GetAccount(definition.Reward.ReserveAccountId);
            bool funded = quests.OutstandingReward(reserve.AccountId) + definition.Reward.CurrencyAmount <= reserve.Balance;
            bool levelEligible = actor.Level >= definition.MinimumLevel;
            views.Add(new LocalQuestView(definition.DefinitionId, definition.Version, definition.Title,
                definition.Description, definition.GiverActorId, definition.Item.DefinitionId, definition.Quantity,
                eligible, participation?.Status, participation?.Revision ?? 0, definition.MinimumLevel,
                definition.Reward.Experience, definition.Reward.CurrencyAmount, progression.Currency.Currency.UnitName,
                addressed && participation is null && levelEligible && funded,
                addressed && participation?.Status == ActorQuestStatus.Accepted && levelEligible && eligible >= definition.Quantity));
        }
        return views.AsReadOnly();
    }
}
