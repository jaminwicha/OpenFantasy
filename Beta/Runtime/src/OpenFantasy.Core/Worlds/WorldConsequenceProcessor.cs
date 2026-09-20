using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Worlds;

/// <summary>Owns the accepted command and reserved clock interval across bounded geometric continuations.</summary>
internal static class WorldConsequenceProcessor
{
    internal static WorldStepDecision? TryBegin(WorldSnapshot state, WorldStepBatch batch, Func<EntityId, bool>? historical)
    {
        if (state.ConsequencePolicy is null || batch.Kind != WorldBatchKind.AdvancingStep
            || !batch.Actions.Any(action => action.Intent is ExcavateIntent)) { return null; }
        if (batch.Actions.Count != 1 || batch.Movement.Count != 0)
        {
            throw new InvalidOperationException("Deferred excavation must be selected as one isolated action before acceptance.");
        }
        var command = batch.Actions[0];
        if (new ExcavationPlanner().ValidateRequest(state, state.Inventory, state.Space, state.Gathering!,
                state.Excavation!, command, true, false) != WorldActionRejection.None) { return null; }
        // Even a refused calculation owes one selected interval. Prove that terminal fallback
        // can be represented before accepting durable work (clock/exposure overflow and root bound).
        ulong nextStep = checked(state.AdvancingSteps + 1);
        var timeOnly = state.WithState(state.Inventory, state.Space,
            state.Residents.Select(resident => resident.ExperienceStep()),
            state.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep),
            checked(state.Revision + 1), nextStep, state.Dialogues, state.Gathering!.AdvanceStep(nextStep),
            state.Production, state.Progression, state.Quests, state.Construction, state.Reservoirs,
            state.Transport, state.Excavation, state.ConstructionPhysical);
        _ = WorldSnapshotCodec.Encode(timeOnly);
        var pending = PendingExcavation.Begin(state, command, batch.CauseId);
        if (pending is null) { return null; }
        bool Occupied(EntityId id) => state.IdentityIndex.Contains(id) || historical?.Invoke(id) == true;
        if (pending.ResultReservations.Append(pending.ReservedSourceId).Append(command.CommandId)
            .Append(batch.CauseId).Any(Occupied))
        {
            throw new InvalidOperationException("The pending command cannot exclusively reserve its publication identities.");
        }
        var candidate = state.WithPending(pending, checked(state.Revision + 1));
        return new(candidate, [], [new(batch.CauseId, WorldCommittedIdentityKind.CommitCause),
            new(command.CommandId, WorldCommittedIdentityKind.Command)]);
    }

    internal static WorldStepDecision Continue(WorldStepProcessor processor, WorldSnapshot state, WorldStepBatch batch,
        Func<EntityId, bool>? historical)
    {
        var pending = state.PendingExcavation ?? throw new InvalidOperationException("There is no accepted work to continue.");
        if (batch.Kind != WorldBatchKind.ConsequenceContinuation || batch.Actions.Count != 0 || batch.Movement.Count != 0
            || state.IdentityIndex.Contains(batch.CauseId) || historical?.Invoke(batch.CauseId) == true)
        {
            throw new InvalidOperationException("The continuation must own a fresh cause and no additional controls.");
        }
        var next = pending.Advance();
        if (!next.Resolution.IsComplete)
        {
            return new(state.WithPending(next, checked(state.Revision + 1)), [],
                [new(batch.CauseId, WorldCommittedIdentityKind.CommitCause)]);
        }
        // Remove only this job's temporary reservations. Its command/cause remain in durable history.
        var stable = state.WithPending(null, state.Revision);
        var selected = new WorldStepBatch(batch.CauseId, state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [pending.Command], state.ProfileVersion);
        var final = processor.DecideImmediate(stable, selected, historical, next.Resolution, pending.Command);
        var receipt = final.Receipts.Single();
        if (receipt.Rejection is not WorldActionRejection.None and not WorldActionRejection.Unavailable
            || !pending.ResultReservations.Contains(receipt.ResultId)
            || receipt.CommandHash != pending.Command.ContentHash)
        {
            throw new InvalidOperationException("Terminal publication escaped the command's reserved outcome contract.");
        }
        return final;
    }
}
