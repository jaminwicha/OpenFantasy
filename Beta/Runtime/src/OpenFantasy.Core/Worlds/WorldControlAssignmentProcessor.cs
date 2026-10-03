namespace OpenFantasy.Core.Worlds;

internal static class WorldControlAssignmentProcessor
{
    internal static WorldStepDecision Decide(WorldSnapshot state, WorldStepBatch batch, Func<EntityId, bool>? historicallyOccupied)
    {
        var controls = state.EffectiveControl ?? throw new InvalidOperationException("This world has no effective control interpretation.");
        var input = batch.ControlAssignmentEvent ?? throw new InvalidOperationException("An assignment boundary requires its host event.");
        if (state.IdentityIndex.Contains(batch.CauseId) || historicallyOccupied?.Invoke(batch.CauseId) == true)
            throw new InvalidOperationException("The assignment event cause is occupied.");
        var previous = controls.Find(input.ActorId);
        if ((previous?.Revision ?? 0) != input.ExpectedAssignmentRevision)
            throw new InvalidOperationException("Stale effective assignment revision.");
        ulong sequence = checked(state.Revision + 1);
        if (input.Witness is { } witness)
        {
            var delivery = state.Inventory.Controls.FirstOrDefault(binding => binding.ActorId == witness.GranteeActorId);
            if (previous?.Witness is not null || witness.AssignmentId != batch.CauseId || witness.AcquiredSequence != sequence
                || delivery is null || !WorldActorCommandAuthority.ValidateRecordedEvidence(state.Inventory, input.ActorId,
                    new(delivery.PrincipalId, delivery.SessionId), witness, witness.Capabilities)
                || !WorldActorCommandAuthority.IsEligibleAssignment(state, witness)
                || !WorldControllerBoundaries.CanRestoreHuman(state, input.ActorId)
                || controls.Slots.Any(slot => slot.Witness?.GranteeActorId == witness.GranteeActorId))
                throw new InvalidOperationException("Acquisition requires the exact expired episode, accepted grant, available grantee and safe boundary.");
        }
        else if (previous?.Witness is null) throw new InvalidOperationException("No active assignment can be released.");
        var next = controls.Replace(new(input.ActorId, checked(input.ExpectedAssignmentRevision + 1), sequence, input.Witness));
        var candidate = state.WithEffectiveControl(next, sequence);
        return new(candidate, [], [new(batch.CauseId, WorldCommittedIdentityKind.CommitCause)], candidateFrame: WorldCanonicalFrame.Capture(candidate));
    }
}
