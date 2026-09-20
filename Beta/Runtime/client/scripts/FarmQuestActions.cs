using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Builds an explicit owned request from the addressed public offer; rewards remain authority-owned.</summary>
internal static class FarmQuestActions
{
    /// <summary>Keep an accepted task first, then a new offer, then completed records in stable identity order.</summary>
    internal static LocalQuestView? AddressedQuest(LocalWorldView view) => view.Quests
        .Where(value => value.GiverActorId == view.Dialogue?.SpeakerActorId)
        .OrderBy(value => value.Status == ActorQuestStatus.Completed ? 2 : value.Status is null ? 1 : 0)
        .ThenBy(value => value.DefinitionId).FirstOrDefault();

    internal static DeliveryQuestIntent Request(LocalWorldView view, LocalQuestView? selected = null)
    {
        LocalQuestView quest = selected ?? AddressedQuest(view)
            ?? throw new InvalidOperationException("Speak with Wren about available tasks first.");
        LocalProgressionView actor = view.Progression
            ?? throw new InvalidOperationException("This save does not have quest progression.");
        ulong giverRevision = view.Actors.Single(value => value.ActorId == quest.GiverActorId).PresenceRevision;
        if (quest.Status is null && quest.CanAccept)
        {
            return new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
                giverRevision, quest.ParticipationRevision, actor.Revision);
        }
        if (!quest.CanComplete)
        {
            throw new InvalidOperationException(quest.Status == ActorQuestStatus.Completed
                ? $"You have already completed {quest.Title}." : $"You need {quest.RequiredQuantity} available wood in your pack.");
        }
        List<SourceLotSelection> selections = [];
        ulong remaining = quest.RequiredQuantity;
        foreach (LocalItemLotView lot in view.PlayerInventory.Lots.Where(value =>
            value.DefinitionId == quest.ItemDefinitionId && value.Provenance == IngredientLotKind.Source && value.AvailableForDelivery))
        {
            if (remaining == 0)
            {
                break;
            }
            ulong quantity = Math.Min(remaining, lot.Quantity);
            selections.Add(new SourceLotSelection(lot.LotId, lot.Revision, quantity));
            remaining -= quantity;
        }
        if (remaining != 0)
        {
            throw new InvalidOperationException("The needed materials are no longer available.");
        }
        return new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
            giverRevision, quest.ParticipationRevision, actor.Revision, view.PlayerInventory.Revision, selections);
    }
}
