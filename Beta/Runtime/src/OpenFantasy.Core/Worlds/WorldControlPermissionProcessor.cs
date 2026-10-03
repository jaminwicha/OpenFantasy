using OpenFantasy.Core.Authority;

namespace OpenFantasy.Core.Worlds;

/// <summary>One permission bookkeeping boundary, preserving every accepted gameplay obligation.</summary>
internal static class WorldControlPermissionProcessor
{
    internal static WorldStepDecision Decide(WorldSnapshot world, WorldStepBatch batch, Func<EntityId, bool>? historicallyOccupied)
    {
        var permissions = world.ControlPermissions ?? throw new InvalidOperationException("This world has no control permission interpretation.");
        var command = batch.Actions.Single();
        var reserved = new HashSet<EntityId>();
        List<WorldCommittedIdentity> identities = [];
        void Reserve(EntityId id, WorldCommittedIdentityKind kind)
        {
            if (world.IdentityIndex.Contains(id) || historicallyOccupied?.Invoke(id) == true || !reserved.Add(id))
                throw new InvalidOperationException("A permission boundary cannot reuse a committed identity.");
            identities.Add(new(id, kind));
        }
        Reserve(batch.CauseId, WorldCommittedIdentityKind.CommitCause);
        Reserve(command.CommandId, WorldCommittedIdentityKind.Command);
        ulong sequence = checked(world.Revision + 1);
        var rejection = Validate(world, command);
        if (rejection == WorldActionRejection.None)
        {
            (permissions, rejection) = Apply(world, permissions, command, sequence);
        }
        var candidate = world.WithControlPermissions(permissions, sequence);
        var receipt = WorldActionReceipt.Create(command, sequence, rejection);
        Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
        return new(candidate, [receipt], identities, candidateFrame: WorldCanonicalFrame.Capture(candidate));
    }

    private static WorldActionRejection Validate(WorldSnapshot world, WorldActionCommand command)
    {
        if (!world.Inventory.CanControl(new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId)
            || world.Controllers!.Assignments.All(row => row.ActorId != command.ActorId))
            return WorldActionRejection.NotAuthorized;
        if (world.Space.GetActor(command.ActorId).Revision != command.ExpectedActorRevision)
            return WorldActionRejection.RevisionConflict;
        if (command.Intent is not RevokeControlPermissionIntent && world.Controllers.Get(command.ActorId).Mode != WorldControllerMode.Human)
            return WorldActionRejection.Unavailable;
        return WorldActionRejection.None;
    }

    private static (WorldControlPermissionState, WorldActionRejection) Apply(WorldSnapshot world,
        WorldControlPermissionState permissions, WorldActionCommand command, ulong sequence)
    {
        EntityId owner, grantee; ulong expected;
        switch (command.Intent)
        {
            case OfferControlPermissionIntent offer: owner = command.ActorId; grantee = offer.GranteeActorId; expected = offer.ExpectedSlotRevision; break;
            case AcceptControlPermissionIntent accept: owner = accept.OwnerActorId; grantee = command.ActorId; expected = accept.ExpectedSlotRevision; break;
            case RevokeControlPermissionIntent revoke: owner = command.ActorId; grantee = revoke.GranteeActorId; expected = revoke.ExpectedSlotRevision; break;
            default: throw new InvalidOperationException("Unknown permission operation.");
        }
        if (owner == grantee || world.Controllers!.Assignments.All(row => row.ActorId != owner)
            || world.Controllers.Assignments.All(row => row.ActorId != grantee))
            return (permissions, WorldActionRejection.NotAuthorized);
        var old = permissions.Find(owner, grantee);
        if ((old?.Revision ?? 0) != expected) return (permissions, WorldActionRejection.RevisionConflict);
        ulong revision = checked(expected + 1);
        WorldControlPermissionSlot next;
        if (command.Intent is OfferControlPermissionIntent offerIntent)
        {
            var ownerBinding = world.Inventory.Controls.Single(row => row.ActorId == owner);
            var granteeBinding = world.Inventory.Controls.Single(row => row.ActorId == grantee);
            if (ownerBinding.PrincipalId == granteeBinding.PrincipalId || ownerBinding.SessionId == granteeBinding.SessionId)
                return (permissions, WorldActionRejection.NotAuthorized);
            var terms = new WorldControlPermissionTerms(world.WorldId, world.BranchId, owner,
                ownerBinding.PrincipalId, ownerBinding.SessionId, grantee, granteeBinding.PrincipalId,
                granteeBinding.SessionId, command.CommandId, offerIntent.Capabilities);
            next = new(owner, grantee, revision, sequence, terms, sequence);
        }
        else if (command.Intent is AcceptControlPermissionIntent acceptIntent)
        {
            if (old?.Status != WorldControlPermissionStatus.Offered || old.GrantCommandId != acceptIntent.GrantCommandId
                || old.TermsHash != acceptIntent.TermsHash)
                return (permissions, WorldActionRejection.Unavailable);
            next = new(owner, grantee, revision, sequence, old.Terms, old.OfferedSequence, command.CommandId, sequence);
        }
        else
        {
            if (old?.Terms is null) return (permissions, WorldActionRejection.Unavailable);
            next = new(owner, grantee, revision, sequence);
        }
        return (permissions.Replace(next), WorldActionRejection.None);
    }
}
