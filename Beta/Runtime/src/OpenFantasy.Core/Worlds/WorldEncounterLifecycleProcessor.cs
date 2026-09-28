using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Claims, stages and releases supported encounters without elapsed-time or resource grants.</summary>
internal static class WorldEncounterLifecycleProcessor
{
    internal static WorldStepDecision Decide(WorldSnapshot world, WorldStepBatch batch, Func<EntityId, bool>? historicallyOccupied)
    {
        var lifecycle = world.EncounterLifecycle ?? throw new InvalidOperationException("Lifecycle ownership is required.");
        var pacing = world.Pacing!;
        ulong revision = checked(world.Revision + 1);
        HashSet<EntityId> reserved = [];
        List<WorldCommittedIdentity> identities = [];
        List<WorldActionReceipt> receipts = [];
        bool Occupied(EntityId identity) => world.IdentityIndex.Contains(identity)
            || reserved.Contains(identity) || historicallyOccupied?.Invoke(identity) == true;
        bool OccupiedEpisode(EntityId identity) => world.IdentityIndex.ContainsEntity(identity)
            || reserved.Contains(identity) || historicallyOccupied?.Invoke(identity) == true;
        void Reserve(EntityId identity, WorldCommittedIdentityKind kind)
        {
            if (Occupied(identity)) throw new InvalidOperationException("A lifecycle identity is already occupied.");
            reserved.Add(identity);
            identities.Add(new(identity, kind));
        }
        Reserve(batch.CauseId, WorldCommittedIdentityKind.CommitCause);
        if (batch.Actions.Count == 0)
        {
            lifecycle = lifecycle.AdvanceStaging();
        }
        else
        {
            var command = batch.Actions[0];
            Reserve(command.CommandId, WorldCommittedIdentityKind.Command);
            var rejection = ValidateControl(world, command);
            if (rejection == WorldActionRejection.None)
            {
                rejection = command.Intent switch
                {
                    EnterEncounterIntent enter => EvaluateEntry(world, command.ActorId, enter, OccupiedEpisode),
                    ReleaseEncounterIntent release => EvaluateRelease(world, command.ActorId, release),
                    _ => throw new InvalidOperationException("Unsupported lifecycle command.")
                };
            }
            if (rejection != WorldActionRejection.None)
            {
                lifecycle = lifecycle.RetainBoundary();
            }
            else if (command.Intent is EnterEncounterIntent enter)
            {
                // Only this accepted entry claims its exact reserved episode identity. Generic
                // causes, commands and spawned objects cannot occupy any future episode in the family.
                if (OccupiedEpisode(enter.EncounterId)) throw new InvalidOperationException("The episode identity is occupied.");
                reserved.Add(enter.EncounterId);
                identities.Add(new(enter.EncounterId, WorldCommittedIdentityKind.SpawnedEntity));
                lifecycle = lifecycle.Begin(enter.EncounterId, enter.ScenarioId);
                var timing = new EncounterTimingSnapshot(lifecycle.Actions!.Scope, 0, 0,
                    world.Residents.Select(resident => new EncounterActorTiming(resident.ActorId,
                        WorldEncounterLifecycleAdmission.NormalizedSpeed(world, resident.ActorId),
                        lifecycle.GetDebt(resident.ActorId).RemainingDelay,
                        resident.ControllerKind == ResidentControllerKind.Player
                            ? EncounterActorControl.RequiredHuman : EncounterActorControl.Automatic)), []);
                pacing = pacing.StartEncounter(timing);
            }
            else
            {
                lifecycle = lifecycle.Release();
                pacing = pacing.EndEncounter();
            }
            var receipt = WorldActionReceipt.Create(command, revision, rejection);
            Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
            receipts.Add(receipt);
        }
        // Reconstruct the whole root: actual clearance, support, construction access, nominal wet
        // envelopes and conserved material remain mandatory at both entry and release boundaries.
        var candidate = world.WithState(world.Inventory, world.Space, world.Residents, world.Calendar,
            revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production,
            world.Progression, world.Quests, world.Construction, world.Reservoirs, world.Transport,
            world.Excavation, world.ConstructionPhysical, world.LiquidCells, pacing, encounterLifecycle: lifecycle);
        WorldCanonicalFrame candidateFrame = WorldCanonicalFrame.Capture(candidate);
        return new(candidate, receipts, identities, candidateFrame: candidateFrame);
    }

    private static WorldActionRejection ValidateControl(WorldSnapshot world, WorldActionCommand command)
    {
        if (!world.Inventory.CanControl(new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId))
            return WorldActionRejection.NotAuthorized;
        if (world.Space.GetActor(command.ActorId).Revision != command.ExpectedActorRevision)
            return WorldActionRejection.RevisionConflict;
        return WorldActorAvailability.CanCooperate(world, command.ActorId)
            ? WorldActionRejection.None : WorldActionRejection.Unavailable;
    }

    private static WorldActionRejection EvaluateEntry(WorldSnapshot world, EntityId actorId,
        EnterEncounterIntent intent, Func<EntityId, bool> occupied)
    {
        var lifecycle = world.EncounterLifecycle!;
        if (lifecycle.Phase != EncounterLifecyclePhase.Exploration || world.HasReadingHold
            || !lifecycle.Scenarios.Any(value => value.DefinitionId == intent.ScenarioId) || occupied(intent.EncounterId))
            return WorldActionRejection.Unavailable;
        var scenario = lifecycle.GetScenario(intent.ScenarioId);
        if (intent.EncounterId != EncounterEpisodeIdentity.Derive(lifecycle, intent.ScenarioId))
            return WorldActionRejection.Unavailable;
        if (scenario.InitiatingActorId != actorId) return WorldActionRejection.NotAuthorized;
        if (intent.ExpectedEncounterCount != lifecycle.EncounterCount
            || world.Space.GetActor(scenario.TargetActorId).Revision != intent.ExpectedTargetRevision)
            return WorldActionRejection.RevisionConflict;
        foreach (var participant in scenario.Combatants.Where(value => value.Side != 0))
        {
            if (!WorldActorAvailability.CanCooperate(world, participant.ActorId)) return WorldActionRejection.Unavailable;
            if (participant.ActorId != actorId && world.Space.QueryInteraction(actorId, participant.ActorId,
                    scenario.EntryRangeSubunits) != SpatialInteraction.Reachable)
                return WorldActionRejection.OutOfReach;
        }
        return WorldActionRejection.None;
    }

    private static WorldActionRejection EvaluateRelease(WorldSnapshot world, EntityId actorId, ReleaseEncounterIntent intent)
    {
        var lifecycle = world.EncounterLifecycle!;
        if (lifecycle.Phase != EncounterLifecyclePhase.Outcome || lifecycle.Actions!.Scope.EncounterId != intent.EncounterId)
            return WorldActionRejection.Unavailable;
        if (lifecycle.GetScenario(lifecycle.ScenarioId!.Value).InitiatingActorId != actorId)
            return WorldActionRejection.NotAuthorized;
        if (lifecycle.Actions.ContentHash != intent.ExpectedActionHash) return WorldActionRejection.RevisionConflict;
        return WorldActionRejection.None;
    }
}
