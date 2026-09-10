using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Resolves addressed actions at the step's starting positions, before movement selection.</summary>
internal sealed class WorldActionResolver
{
    private readonly CustodyTransferPlanner _transfers = new();

    internal WorldActionResolution Resolve(WorldSnapshot world, CountedAuthorityState inventory,
        List<DialogueSession> dialogues, WorldActionCommand action, Func<EntityId, bool> occupied)
    {
        WorldActionResolution Reject(WorldActionRejection rejection) => new(inventory, dialogues, rejection, null);
        var delivery = new TrustedCommandDelivery(action.PrincipalId, action.OriginSessionId);
        if (!inventory.CanControl(delivery, action.ActorId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        ActorPresence actor = world.Space.GetActor(action.ActorId);
        if (actor.Revision != action.ExpectedActorRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        if (action.Intent is CloseDialogueIntent close)
        {
            DialogueSession? session = dialogues.FirstOrDefault(value => value.DialogueId == close.DialogueId
                && value.ListenerActorId == action.ActorId);
            return session is null ? Reject(WorldActionRejection.Unavailable)
                : new WorldActionResolution(inventory, dialogues.Where(value => value != session).ToList(),
                    WorldActionRejection.None, null);
        }

        (EntityId targetId, ulong expectedTargetRevision) = action.Intent switch
        {
            SpeakToActorIntent speak => (speak.TargetActorId, speak.ExpectedTargetRevision),
            GiveItemIntent give => (give.TargetActorId, give.ExpectedTargetRevision),
            _ => throw new InvalidOperationException("The action kind is not supported by this profile.")
        };
        ActorPresence? target = world.Space.Actors.FirstOrDefault(value => value.ActorId == targetId);
        if (target is null || targetId == action.ActorId)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        if (target.Revision != expectedTargetRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        if (world.Space.QueryInteraction(action.ActorId, targetId) != SpatialInteraction.Reachable)
        {
            return Reject(WorldActionRejection.OutOfReach);
        }

        if (action.Intent is SpeakToActorIntent)
        {
            WorldResident speaker = world.GetResident(targetId);
            WorldResident listener = world.GetResident(action.ActorId);
            if (dialogues.Count == 4 || dialogues.Any(value => value.ListenerActorId == action.ActorId)
                || speaker.OpeningUtterance is not { } utterance)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            var requirement = new ComprehensionRequirement(utterance.Channel, utterance.ChannelVersion,
                ComprehensionBand.Conversational);
            if (utterance.Channel.Mode != CommunicationMode.Spoken
                || !target.Condition.SupportsFunction(BodyFunction.Vocalization)
                || !actor.Condition.SupportsFunction(BodyFunction.Hearing)
                || !speaker.Comprehension.Query(speaker.ActorId, speaker.Comprehension.RulesVersion, requirement).MeetsBandRequirement
                || !listener.Comprehension.Query(listener.ActorId, listener.Comprehension.RulesVersion, requirement).MeetsBandRequirement)
            {
                return Reject(WorldActionRejection.UnableToCommunicate);
            }
            EntityId dialogueId = EntityId.DeriveSpawned(action.CommandId, 0);
            if (occupied(dialogueId))
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            List<DialogueSession> opened = [.. dialogues, new DialogueSession(dialogueId, action.ActorId,
                targetId, utterance.DefinitionId, action.CommandId, world.Calendar)];
            return new WorldActionResolution(inventory, opened, WorldActionRejection.None, dialogueId);
        }

        var handover = (GiveItemIntent)action.Intent;
        EntityId destinationId = world.GetResident(targetId).InventoryCustodianId;
        if (!inventory.AllowsTransfer(action.ActorId, handover.SourceCustodianId, destinationId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (!actor.Condition.SupportsFunction(BodyFunction.Manipulation)
            || !target.Condition.SupportsFunction(BodyFunction.Manipulation)
            || handover.LotId == handover.SourceCustodianId || handover.LotId == destinationId)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        // Private recipient storage is read only after current control, reach and ownership gates.
        CustodianStorage destination = inventory.Custody.GetStorage(destinationId);
        var transfer = new CommandEnvelope(action.CommandId, action.PrincipalId, action.OriginSessionId,
            action.ActorId, world.WorldId, world.BranchId,
            [new ExpectedRevision(RevisionTargetKind.Branch, world.BranchId, inventory.Custody.Revision),
             new ExpectedRevision(RevisionTargetKind.Entity, handover.LotId, handover.ExpectedLotRevision),
             new ExpectedRevision(RevisionTargetKind.Entity, handover.SourceCustodianId, handover.ExpectedSourceRevision),
             new ExpectedRevision(RevisionTargetKind.Entity, destinationId, destination.Revision)],
            new TransferLotIntent(handover.LotId, handover.SourceCustodianId, destinationId, handover.Quantity));
        try
        {
            if (inventory.Custody.Lots.Count == CustodySnapshot.MaximumLots
                && handover.Quantity < inventory.Custody.GetLot(handover.LotId).Quantity)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            CustodyTransferPlan plan = _transfers.Plan(inventory.Custody, transfer);
            EntityId? spawned = plan.MovedLotId != handover.LotId ? plan.MovedLotId : null;
            if (spawned is { } child && occupied(child))
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            return new WorldActionResolution(inventory.WithCustody(plan.Candidate), dialogues,
                WorldActionRejection.None, spawned);
        }
        catch (CustodyConflictException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        catch (OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}

internal sealed record WorldActionResolution(CountedAuthorityState Inventory, List<DialogueSession> Dialogues,
    WorldActionRejection Rejection, EntityId? SpawnedId);
