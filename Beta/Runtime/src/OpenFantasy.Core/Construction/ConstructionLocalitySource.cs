using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Live typed owners of a terrain-free construction continuation, under its admitted legacy law.</summary>
internal sealed class ConstructionLocalitySource(ConstructionLocalityState state) : ConstructionDecisionSource
{
    internal override EntityId WorldId => state.Inventory.WorldId;
    internal override EntityId BranchId => state.Inventory.BranchId;
    internal override bool HasReadingHold => state.Dialogues.Count != 0;
    internal override bool HasProgressionAndQuests => true;
    internal override bool Authorizes(WorldActionCommand command) => command.ControlWitness is null
        && state.Inventory.CanControl(new(command.PrincipalId, command.OriginSessionId), command.ActorId);
    internal override int CurrentHp(EntityId actorId) => state.Progression.GetActor(actorId).CurrentHp;
    internal override EntityId InventoryCustodian(EntityId actorId) =>
        state.InventoryCustodian(actorId);
    internal override bool QuestCompleted(EntityId questId) => state.Quests.Participations.Any(value =>
        value.QuestId == questId && value.Status == OpenFantasy.Core.Quests.ActorQuestStatus.Completed);
    internal override bool ContainsIdentity(EntityId identity) => state.ContainsIdentity(identity);
}
