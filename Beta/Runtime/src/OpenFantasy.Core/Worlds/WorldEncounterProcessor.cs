using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Publishes coordinated timing, applicable exposure and earned passive-process intervals together.</summary>
internal static class WorldEncounterProcessor
{
    internal static WorldStepDecision Decide(WorldSnapshot world, WorldStepBatch batch, Func<EntityId, bool>? historicallyOccupied)
    {
        WorldPacingState pacing = world.Pacing ?? throw new InvalidOperationException("A tactical boundary requires its pacing owner.");
        EncounterTimingSnapshot basis = pacing.Encounter ?? throw new InvalidOperationException("No admitted encounter owns this boundary.");
        if (world.HasReadingHold || world.PendingExcavation is not null
            || batch.Kind is not WorldBatchKind.EncounterDecision and not WorldBatchKind.EncounterResolution and not WorldBatchKind.TacticalAdvance)
        {
            throw new InvalidOperationException("An active encounter accepts only its explicit timing boundaries.");
        }
        ulong revision = checked(world.Revision + 1);
        HashSet<EntityId> reserved = [];
        List<WorldCommittedIdentity> identities = [];
        void Reserve(EntityId identity, WorldCommittedIdentityKind kind)
        {
            if (world.IdentityIndex.Contains(identity) || historicallyOccupied?.Invoke(identity) == true || !reserved.Add(identity))
            {
                throw new InvalidOperationException("The selected encounter identity is already occupied.");
            }
            identities.Add(new WorldCommittedIdentity(identity, kind));
        }
        Reserve(batch.CauseId, WorldCommittedIdentityKind.CommitCause);
        EncounterTimingSnapshot timing = basis;
        List<WorldActionReceipt> receipts = [];
        if (batch.Kind == WorldBatchKind.EncounterDecision)
        {
            foreach (WorldActionCommand command in batch.Actions)
            {
                Reserve(command.CommandId, WorldCommittedIdentityKind.Command);
                WorldActionRejection rejection = ValidateChoice(world, basis, command);
                if (rejection == WorldActionRejection.None)
                {
                    timing = timing.QueueDecision(timing.Revision, new EncounterDecision(command.ActorId, command.CommandId,
                        EncounterDecisionKind.Wait, command.ContentHash));
                }
                WorldActionReceipt receipt = WorldActionReceipt.Create(command, revision, rejection);
                Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
                receipts.Add(receipt);
            }
        }
        else
        {
            EncounterTimingTransition transition = batch.Kind == WorldBatchKind.TacticalAdvance
                ? timing.TryAdvance(timing.Revision) : timing.TryConfirmNext(timing.Revision);
            if (!transition.IsAccepted)
            {
                throw new InvalidOperationException("The selected encounter boundary is held or unavailable.");
            }
            timing = transition.State;
        }
        WorldPacingState nextPacing = pacing.ObserveEncounter(timing);
        LocalExposureDuration duration = batch.Kind == WorldBatchKind.TacticalAdvance
            ? ExposurePacingV1.FromTacticalUnits(EncounterTimingSnapshot.UnitsPerTick) : LocalExposureDuration.Zero;
        ulong advancing = batch.Kind == WorldBatchKind.TacticalAdvance ? checked(world.AdvancingSteps + 1) : world.AdvancingSteps;
        var gathering = world.Gathering!;
        var liquids = world.LiquidCells!;
        if (nextPacing.ProcessIntervals != pacing.ProcessIntervals)
        {
            if (nextPacing.ProcessIntervals != checked(pacing.ProcessIntervals + 1))
            {
                throw new InvalidOperationException("A selected tactical tick cannot grant multiple legacy process intervals.");
            }
            gathering = gathering.AdvanceStep(nextPacing.ProcessIntervals);
            liquids = new LiquidCellPlanner().PlanAdvance(liquids, nextPacing.ProcessBoundary, false).ApplyTo(liquids);
        }
        WorldSnapshot candidate = world.WithState(world.Inventory, world.Space,
            world.Residents.Select(resident => resident.Experience(duration)), nextPacing.Calendar, revision, advancing,
            world.Dialogues, gathering, world.Production, world.Progression, world.Quests, world.Construction,
            world.Reservoirs, world.Transport, world.Excavation, world.ConstructionPhysical, liquids, nextPacing);
        WorldCanonicalFrame candidateFrame = WorldCanonicalFrame.Capture(candidate);
        return new WorldStepDecision(candidate, receipts, identities, candidateFrame: candidateFrame);
    }

    private static WorldActionRejection ValidateChoice(WorldSnapshot world, EncounterTimingSnapshot basis, WorldActionCommand command)
    {
        if (!world.Inventory.CanControl(new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId))
            return WorldActionRejection.NotAuthorized;
        if (world.Space.GetActor(command.ActorId).Revision != command.ExpectedActorRevision)
            return WorldActionRejection.RevisionConflict;
        if (command.Intent is not QueueEncounterWaitIntent intent || intent.EncounterId != basis.EncounterId)
            return WorldActionRejection.Unavailable;
        if (intent.ExpectedTimingRevision != basis.Revision) return WorldActionRejection.RevisionConflict;
        if (!basis.GetActor(command.ActorId).IsDue || basis.Decisions.Any(decision => decision.ActorId == command.ActorId))
            return WorldActionRejection.Unavailable;
        return WorldActionRejection.None;
    }
}
