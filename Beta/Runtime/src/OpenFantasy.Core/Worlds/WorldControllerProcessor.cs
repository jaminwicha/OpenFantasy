namespace OpenFantasy.Core.Worlds;

/// <summary>Applies an external host decision without earning time or changing an accepted gameplay action.</summary>
internal static class WorldControllerProcessor
{
    internal static WorldStepDecision Decide(WorldSnapshot state, WorldStepBatch batch,
        Func<EntityId, bool>? historicallyOccupied)
    {
        if (state.Controllers is not { } controllers || batch.ControllerEvent is not { } input)
            throw new InvalidOperationException("A controller boundary requires its admitted owner and external decision.");
        if (state.IdentityIndex.Contains(batch.CauseId) || historicallyOccupied?.Invoke(batch.CauseId) == true
            || controllers.Assignments.Any(row => row.DisconnectId == batch.CauseId))
            throw new InvalidOperationException("The controller boundary cause is already occupied.");
        if (input.Kind == WorldControllerEventKind.Reconnected && !WorldControllerBoundaries.CanRestoreHuman(state, input.ActorId))
            throw new InvalidOperationException("Control can return only at a safe gameplay boundary.");
        WorldControllerState next = controllers.Apply(input, batch.CauseId);
        WorldSnapshot candidate = state.WithControllers(next, checked(state.Revision + 1));
        return new WorldStepDecision(candidate, [], [new(batch.CauseId, WorldCommittedIdentityKind.CommitCause)],
            candidateFrame: WorldCanonicalFrame.Capture(candidate));
    }
}

