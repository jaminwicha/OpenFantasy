using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>Separates current selection from retained command attribution. Neither path authenticates a transport.</summary>
public static class WorldActorCommandAuthority
{
    public static WorldActorAuthorization? AuthorizeSelection(WorldSnapshot world, WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        var scope = RequiredCapabilities(command.Intent);
        var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
        if (command.WorldId != world.WorldId || command.BranchId != world.BranchId) return null;
        if (command.Intent is ControlPermissionIntent)
            return command.ControlWitness is null && world.Inventory.CanControl(delivery, command.ActorId)
                ? new(command.ActorId, delivery, command.CommandId, command.ContentHash, null, 0) : null;
        if (!IsCurrentWitness(world, command.ActorId, delivery, command.ControlWitness, scope)) return null;
        return new(command.ActorId, delivery, command.CommandId, command.ContentHash, command.ControlWitness, scope);
    }

    public static WorldActorAuthorization? AuthorizeMovement(WorldSnapshot world, WorldMovementInput sample)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sample);
        return !IsCurrentWitness(world, sample.ActorId, sample.Delivery, sample.ControlWitness, WorldControlCapabilities.Traversal)
            ? null : new(sample.ActorId, sample.Delivery, sample.ActorId, null, sample.ControlWitness, WorldControlCapabilities.Traversal);
    }

    public static WorldActorAuthorization? AuthorizeRetainedOffer(WorldSnapshot world, EntityId actorId,
        TrustedCommandDelivery delivery, WorldControlWitness? witness, EntityId commandId, WorldControlCapabilities required) =>
        IsCurrentWitness(world, actorId, delivery, witness, required) ? new(actorId, delivery, commandId, null, witness, required) : null;

    public static bool IsCurrentWitness(WorldSnapshot world, EntityId actorId, TrustedCommandDelivery delivery,
        WorldControlWitness? witness, WorldControlCapabilities required)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(delivery);
        return WorldActorControlDependencies.AllowsCurrent(world, actorId, delivery, witness, required);
    }

    internal static bool IsEligibleAssignment(WorldSnapshot world, WorldControlWitness witness)
    {
        return world.Controllers is not null && world.ControlPermissions is not null
            && IsEligibleAssignment(world.Controllers, world.ControlPermissions, witness);
    }

    internal static bool IsEligibleAssignment(WorldControllerState controllers, WorldControlPermissionState permissions, WorldControlWitness witness)
    {
        var owner = controllers.Assignments.FirstOrDefault(row => row.ActorId == witness.OwnerActorId);
        var grantee = controllers.Assignments.FirstOrDefault(row => row.ActorId == witness.GranteeActorId);
        var permission = permissions.Find(witness.OwnerActorId, witness.GranteeActorId);
        return IsEligibleAssignment(owner, grantee, permission, witness);
    }

    internal static bool IsEligibleAssignment(WorldControllerAssignment? owner, WorldControllerAssignment? grantee,
        WorldControlPermissionSlot? permission, WorldControlWitness witness) =>
        owner is { Mode: WorldControllerMode.Autonomous } && owner.Revision == witness.OwnerControlRevision
            && owner.DisconnectId == witness.DisconnectId && grantee is { Mode: WorldControllerMode.Human }
            && permission is { Status: WorldControlPermissionStatus.Accepted }
            && permission.Revision == witness.GrantRevision && permission.GrantCommandId == witness.GrantCommandId
            && permission.AcceptanceCommandId == witness.AcceptanceCommandId && permission.TermsHash == witness.TermsHash
            && permission.Terms!.Capabilities == witness.Capabilities && permission.AcceptedSequence < witness.AcquiredSequence;

    public static bool ValidateRecordedEvidence(CountedAuthorityState inventory, WorldActionCommand command) =>
        command.WorldId == inventory.WorldId && command.BranchId == inventory.BranchId
        && ValidateRecordedEvidence(inventory, command.ActorId, new(command.PrincipalId, command.OriginSessionId),
            command.ControlWitness, RequiredCapabilities(command.Intent));

    public static bool ValidateRecordedEvidence(CountedAuthorityState inventory, EntityId actorId,
        TrustedCommandDelivery delivery, WorldControlWitness? witness, WorldControlCapabilities required)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(delivery);
        if (witness is null) return inventory.CanControl(delivery, actorId);
        return ValidateRecordedBindings(inventory.WorldId, inventory.BranchId, actorId, delivery, witness, required,
            inventory.FindControl(actorId), inventory.FindControl(witness.GranteeActorId));
    }

    internal static bool ValidateRecordedBindings(EntityId worldId, EntityId branchId, EntityId actorId,
        TrustedCommandDelivery delivery, WorldControlWitness witness, WorldControlCapabilities required,
        ActorControlBinding? owner, ActorControlBinding? grantee)
    {
        if (required == 0 || (witness.Capabilities & required) != required || witness.OwnerActorId != actorId) return false;
        if (owner is null || grantee is null || grantee.PrincipalId != delivery.PrincipalId || grantee.SessionId != delivery.SessionId) return false;
        try
        {
            var terms = new WorldControlPermissionTerms(worldId, branchId, actorId,
                owner.PrincipalId, owner.SessionId, grantee.ActorId, grantee.PrincipalId, grantee.SessionId,
                witness.GrantCommandId, witness.Capabilities);
            return terms.ContentHash == witness.TermsHash;
        }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Only a command already owned by this exact pending combat or retreat may retain accepted authority.</summary>
    internal static WorldActorAuthorization? AuthorizeAccepted(WorldSnapshot world, WorldActionCommand command)
    {
        bool retained = world.EncounterActions?.Pending.Any(value => value.CommandId == command.CommandId && value.ContentHash == command.ContentHash) == true
            || world.Retreat?.Active is { } retreat && retreat.Command.CommandId == command.CommandId && retreat.Command.ContentHash == command.ContentHash
            || world.Retreat?.Withdrawal?.FindMember(command.ActorId) is { } member
                && member.Command.CommandId == command.CommandId && member.Command.ContentHash == command.ContentHash;
        if (!retained || !ValidateRecordedEvidence(world.Inventory, command)) return null;
        return new(command.ActorId, new(command.PrincipalId, command.OriginSessionId), command.CommandId,
            command.ContentHash, command.ControlWitness, RequiredCapabilities(command.Intent));
    }

    public static WorldControlCapabilities RequiredCapabilities(WorldActionIntent intent) => intent switch
    {
        ControlPermissionIntent => 0,
        PendingCareDispositionIntent or RevokeCarePermissionIntent => WorldControlCapabilities.CareDisposition,
        AcknowledgeCareIntent or AdministerRemedyIntent or UseRestorativeItemIntent => WorldControlCapabilities.Recovery,
        QueueEncounterRetreatIntent or CancelEncounterWithdrawalIntent => WorldControlCapabilities.Combat | WorldControlCapabilities.Traversal,
        QueueEncounterWaitIntent or QueueEncounterAbilityIntent or EnterEncounterIntent or ReleaseEncounterIntent
            or AcknowledgeEncounterOutcomeIntent => WorldControlCapabilities.Combat,
        SpeakToActorIntent or CloseDialogueIntent or DeliveryQuestIntent => WorldControlCapabilities.ConversationAndQuests,
        GiveItemIntent or MaterialProposalIntent or OfferMaterialIntent or AcceptMaterialIntent => WorldControlCapabilities.Inventory,
        MarketIntent or PaidExchangeIntent => WorldControlCapabilities.Trade,
        GatherIntent or InspectGatherSourceIntent or ExcavateIntent or CraftIntent or WorkConstructionIntent => WorldControlCapabilities.Production,
        _ => 0
    };
}
