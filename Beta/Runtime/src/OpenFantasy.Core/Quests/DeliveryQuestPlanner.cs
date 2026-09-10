using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Quests;

/// <summary>
/// Adjudicates a quest in an addressed conversation. Delivery, reserved materials, paid currency,
/// progression and the once-only completion are returned together or remain entirely unchanged.
/// </summary>
public sealed class DeliveryQuestPlanner
{
    private readonly SourceDeliveryPlanner _delivery = new();

    public DeliveryQuestPlan Plan(WorldSnapshot world, CountedAuthorityState inventory,
        ProductionSnapshot production, ProgressionSnapshot progression, QuestSnapshot quests,
        WorldActionCommand command, IReadOnlyList<DialogueSession> dialogues,
        Func<EntityId, bool>? historicallyOccupied = null, LocalSpatialSnapshot? currentSpace = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(dialogues);
        LocalSpatialSnapshot space = currentSpace ?? world.Space;
        DeliveryQuestPlan Reject(WorldActionRejection rejection) => new(inventory, progression, quests, rejection, []);
        var authenticated = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
        if (space.Scope != world.Space.Scope || command.WorldId != world.WorldId || command.BranchId != world.BranchId
            || inventory.WorldId != world.WorldId || inventory.BranchId != world.BranchId
            || !inventory.CanControl(authenticated, command.ActorId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (command.Intent is not DeliveryQuestIntent intent)
        {
            throw new ArgumentException("This planner only interprets delivery quest actions.", nameof(command));
        }
        DeliveryQuestDefinition? definition = quests.FindDefinition(intent.QuestId);
        if (definition is null || definition.Version != intent.QuestVersion
            || definition.GiverActorId != intent.GiverActorId || definition.GiverActorId == command.ActorId)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        ActorPresence actor = space.GetActor(command.ActorId);
        ActorPresence? giver = space.Actors.FirstOrDefault(value => value.ActorId == definition.GiverActorId);
        if (actor.Revision != command.ExpectedActorRevision || giver?.Revision != intent.ExpectedGiverRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        if (space.QueryInteraction(command.ActorId, definition.GiverActorId) != SpatialInteraction.Reachable)
        {
            return Reject(WorldActionRejection.OutOfReach);
        }
        if (!dialogues.Any(value => value.ListenerActorId == command.ActorId
            && value.SpeakerActorId == definition.GiverActorId && world.Dialogues.Contains(value)))
        {
            return Reject(WorldActionRejection.UnableToCommunicate);
        }
        try
        {
            quests.ValidateForAuthority(inventory, progression);
            ActorProgressionState currentActor = progression.GetActor(command.ActorId);
            ActorQuestState? participation = quests.FindParticipation(command.ActorId, definition.DefinitionId);
            if (currentActor.Revision != intent.ExpectedProgressionRevision
                || (participation?.Revision ?? 0) != intent.ExpectedParticipationRevision)
            {
                return Reject(WorldActionRejection.RevisionConflict);
            }
            if (currentActor.Level < definition.MinimumLevel)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            if (intent is AcceptDeliveryQuestIntent)
            {
                if (participation is not null)
                {
                    return Reject(WorldActionRejection.Unavailable);
                }
                var accepted = new ActorQuestState(command.ActorId, definition.DefinitionId, command.CommandId, world.Calendar);
                QuestSnapshot candidate = quests.WithParticipation(accepted);
                // Acceptance itself creates a funded obligation; another actor cannot overbook this reserve.
                candidate.ValidateForAuthority(inventory, progression);
                return new DeliveryQuestPlan(inventory, progression, candidate, WorldActionRejection.None, []);
            }
            if (intent is not CompleteDeliveryQuestIntent completion || participation?.Status != ActorQuestStatus.Accepted
                || !actor.Condition.SupportsFunction(BodyFunction.Manipulation)
                || !giver!.Condition.SupportsFunction(BodyFunction.Manipulation))
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            var request = new SourceDeliveryRequest(world.GetResident(command.ActorId).InventoryCustodianId,
                completion.ExpectedInventoryRevision, definition.DestinationCustodianId, definition.Item,
                definition.Quantity, definition.MaterialReservationId, completion.Selections);
            SourceDeliveryPlan material = _delivery.Plan(inventory, production, authenticated, command.ActorId,
                command.CommandId, request, historicallyOccupied);
            if (!material.Succeeded)
            {
                return Reject(material.Rejection);
            }
            ActorRewardPlan reward = ActorRewardPlanner.Plan(progression, command.ActorId, currentActor.Revision, definition.Reward);
            if (!reward.Succeeded)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            QuestSnapshot completed = quests.WithParticipation(participation.Complete(command.CommandId, world.Calendar));
            completed.ValidateForAuthority(material.Candidate, reward.Candidate);
            return new DeliveryQuestPlan(material.Candidate, reward.Candidate, completed,
                WorldActionRejection.None, material.CreatedIds);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}
