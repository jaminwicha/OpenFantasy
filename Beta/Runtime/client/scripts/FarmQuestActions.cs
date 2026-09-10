using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Builds an explicit owned request from the addressed public offer; rewards remain authority-owned.</summary>
internal static class FarmQuestActions
{
    internal static DeliveryQuestIntent Request(LocalWorldView view)
    {
        LocalQuestView quest = view.Quests.SingleOrDefault(value => value.GiverActorId == view.Dialogue?.SpeakerActorId)
            ?? throw new InvalidOperationException("Speak with Wren about the orchard first.");
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
                ? "You have already delivered the orchard supplies." : "You need six available wood in your pack.");
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
