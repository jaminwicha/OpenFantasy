using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;

namespace OpenFantasy.Core.Worlds;

/// <summary>Atomically publishes queued choices, actual resources, readiness and applicable process time.</summary>
internal static class WorldEncounterActionProcessor
{
    internal static WorldStepDecision Decide(WorldSnapshot world, WorldStepBatch batch, Func<EntityId, bool>? historicallyOccupied)
    {
        if (WorldEncounterActions.IsOutcome(world) || world.HasReadingHold || world.PendingExcavation is not null
            || batch.Kind is not WorldBatchKind.EncounterDecision and not WorldBatchKind.EncounterResolution and not WorldBatchKind.TacticalAdvance)
        {
            throw new InvalidOperationException("This encounter requires an available explicit action boundary.");
        }
        var pacing = world.Pacing!;
        var basis = pacing.Encounter!;
        var timing = basis;
        var actions = world.EncounterActions!;
        var progression = world.Progression!;
        var resourceHistory = world.ResourceHistory;
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
        List<WorldActionReceipt> receipts = [];
        if (batch.Kind == WorldBatchKind.EncounterDecision)
        {
            var pending = actions.Pending.ToList();
            foreach (var command in batch.Actions)
            {
                Reserve(command.CommandId, WorldCommittedIdentityKind.Command);
                WorldActionRejection rejection = ValidateChoice(world, basis, command);
                if (rejection == WorldActionRejection.None)
                {
                    var kind = command.Intent is QueueEncounterAbilityIntent ? EncounterDecisionKind.Action : EncounterDecisionKind.Wait;
                    timing = timing.QueueDecision(timing.Revision, new(command.ActorId, command.CommandId, kind, command.ContentHash));
                    if (kind == EncounterDecisionKind.Action) pending.Add(command);
                }
                var receipt = WorldActionReceipt.Create(command, revision, rejection);
                Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
                receipts.Add(receipt);
            }
            if (pending.Count != actions.Pending.Count) actions = actions.WithPending(pending);
        }
        else if (batch.Kind == WorldBatchKind.EncounterResolution)
        {
            (timing, actions, progression, resourceHistory) = Resolve(world, revision);
        }
        else
        {
            if (WorldEncounterActions.DueActors(world).Count != 0)
            {
                throw new InvalidOperationException("A tactical tick cannot bypass a capable due actor.");
            }
            timing = new EncounterTimingSnapshot(basis.Scope, checked(basis.Revision + 1),
                checked(basis.ElapsedTacticalUnits + EncounterTimingSnapshot.UnitsPerTick),
                basis.Actors.Select(actor => actor.WithDelay(actor.RemainingDelay > EncounterTimingSnapshot.UnitsPerTick
                    ? actor.RemainingDelay - EncounterTimingSnapshot.UnitsPerTick : 0)), []);
        }
        var nextPacing = pacing.ObserveEncounter(timing);
        bool tick = batch.Kind == WorldBatchKind.TacticalAdvance;
        var duration = tick ? ExposurePacingV1.FromTacticalUnits(EncounterTimingSnapshot.UnitsPerTick) : LocalExposureDuration.Zero;
        var gathering = world.Gathering!;
        var liquids = world.LiquidCells!;
        if (nextPacing.ProcessIntervals != pacing.ProcessIntervals)
        {
            if (nextPacing.ProcessIntervals != checked(pacing.ProcessIntervals + 1))
            {
                throw new InvalidOperationException("A tactical tick cannot earn multiple process intervals.");
            }
            gathering = gathering.AdvanceStep(nextPacing.ProcessIntervals);
            liquids = new LiquidCellPlanner().PlanAdvance(liquids, nextPacing.ProcessBoundary, false).ApplyTo(liquids);
        }
        bool outcome = actions.Combatants.Where(actor => actor.Side != 0 && progression.GetActor(actor.ActorId).CurrentHp > 0)
            .Select(actor => actor.Side).Distinct().Count() <= 1;
        var lifecycle = world.EncounterLifecycle?.ObserveActive(timing, actions, tick, outcome);
        var candidate = world.WithState(world.Inventory, world.Space,
            world.Residents.Select(actor => actor.Experience(duration)), nextPacing.Calendar, revision,
            tick ? checked(world.AdvancingSteps + 1) : world.AdvancingSteps,
            world.Dialogues, gathering, world.Production, progression, world.Quests, world.Construction,
            world.Reservoirs, world.Transport, world.Excavation, world.ConstructionPhysical, liquids, nextPacing, lifecycle is null ? actions : null, lifecycle, resourceHistory: resourceHistory);
        WorldCanonicalFrame candidateFrame = WorldCanonicalFrame.Capture(candidate);
        return new WorldStepDecision(candidate, receipts, identities, candidateFrame: candidateFrame);
    }

    private static WorldActionRejection ValidateChoice(WorldSnapshot world, EncounterTimingSnapshot basis, WorldActionCommand command)
    {
        if (!world.Inventory.CanControl(new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId))
            return WorldActionRejection.NotAuthorized;
        if (world.Space.GetActor(command.ActorId).Revision != command.ExpectedActorRevision)
            return WorldActionRejection.RevisionConflict;
        if (!WorldEncounterActions.CanParticipate(world, command.ActorId) || !basis.GetActor(command.ActorId).IsDue
            || basis.Decisions.Any(decision => decision.ActorId == command.ActorId)) return WorldActionRejection.Unavailable;
        (EntityId encounter, ulong revision) = command.Intent switch
        {
            QueueEncounterWaitIntent wait => (wait.EncounterId, wait.ExpectedTimingRevision),
            QueueEncounterAbilityIntent ability => (ability.EncounterId, ability.ExpectedTimingRevision),
            _ => throw new InvalidOperationException("Unknown encounter choice.")
        };
        if (encounter != basis.EncounterId) return WorldActionRejection.Unavailable;
        if (revision != basis.Revision) return WorldActionRejection.RevisionConflict;
        return command.Intent is QueueEncounterAbilityIntent intent
            ? WorldEncounterActions.EvaluateAbility(world, command.ActorId, intent) : WorldActionRejection.None;
    }

    private static (EncounterTimingSnapshot Timing, EncounterActionSnapshot Actions, ProgressionSnapshot Progression, WorldResourceHistory? Resources)
        Resolve(WorldSnapshot world, ulong sequence)
    {
        var basis = world.Pacing!.Encounter!;
        var actions = world.EncounterActions!;
        var due = WorldEncounterActions.DueActors(world);
        if (WorldEncounterActions.HasRequiredDecisions(world) || due.Count == 0)
            throw new InvalidOperationException("An encounter confirmation requires complete human choices and a due actor.");
        var actor = due[0];
        var choice = basis.Decisions.SingleOrDefault(decision => decision.ActorId == actor.ActorId)
            ?? throw new InvalidOperationException("The first due actor has no admitted choice.");
        var progression = world.Progression!;
        var resourceHistory = world.ResourceHistory;
        var random = actions.Random;
        ulong delay = actor.StandardDelay;
        EntityId? targetId = null;
        EntityId? abilityId = null;
        uint damage = 0, beforeHp = 0, afterHp = 0, mana = 0, variation = 0;
        var status = EncounterActionResolutionStatus.Wait;
        List<ActorResourceChange> resourceChanges = [];
        if (choice.Kind == EncounterDecisionKind.Action)
        {
            var command = actions.Pending.Single(value => value.CommandId == choice.DecisionId);
            var intent = (QueueEncounterAbilityIntent)command.Intent;
            var ability = actions.GetAbility(intent.AbilityDefinitionId);
            targetId = intent.TargetActorId;
            abilityId = ability.DefinitionId;
            delay = ability.Delay(actor.NormalizedSpeed);
            status = EncounterActionResolutionStatus.Invalidated;
            if (WorldEncounterActions.EvaluateAbility(world, actor.ActorId, intent) == WorldActionRejection.None)
            {
                var source = progression.GetActor(actor.ActorId);
                var target = progression.GetActor(intent.TargetActorId);
                var stream = actions.CreateRandom();
                variation = checked(900U + (uint)stream.NextUInt64(201));
                random = stream.CaptureState();
                beforeHp = (uint)target.CurrentHp;
                var damageEffect = target.TakeDamage(EncounterActionRules.ComputePhysicalDamage(source.Stats.Attack,
                    target.Stats.Defense, ability.PowerTenths, variation));
                damage = checked((uint)-damageEffect.Health.Delta);
                afterHp = (uint)damageEffect.Candidate.CurrentHp;
                mana = ability.ManaCost;
                var magicEffect = source.SpendMagic(mana);
                if (resourceHistory is not null)
                {
                    var cause = new ResourceChangeCause(command.CommandId, ability.DefinitionId, ability.Version);
                    resourceChanges.Add(ActorResourceChange.FromTransition(0, cause, damageEffect));
                    if (mana != 0) resourceChanges.Add(ActorResourceChange.FromTransition(1, cause, magicEffect));
                }
                var changed = progression.Actors.Select(value => value.ActorId == target.ActorId
                    ? damageEffect.Candidate
                    : value.ActorId == source.ActorId && mana != 0
                        ? magicEffect.Candidate : value);
                progression = new ProgressionSnapshot(progression.WorldId, progression.BranchId, checked(progression.Revision + 1),
                    progression.Profiles, progression.Curves, changed, progression.Currency);
                status = EncounterActionResolutionStatus.Damaged;
            }
        }
        bool outcome = actions.Combatants.Where(value => value.Side != 0 && progression.GetActor(value.ActorId).CurrentHp > 0)
            .Select(value => value.Side).Distinct().Count() <= 1;
        var cancelled = basis.Decisions.Where(value => value.ActorId != actor.ActorId
            && (outcome || progression.GetActor(value.ActorId).CurrentHp == 0)).Select(value => value.DecisionId).ToHashSet();
        var timing = new EncounterTimingSnapshot(basis.Scope, checked(basis.Revision + 1), basis.ElapsedTacticalUnits,
            basis.Actors.Select(value => value.ActorId == actor.ActorId ? value.WithDelay(delay) : value),
            basis.Decisions.Where(value => value.DecisionId != choice.DecisionId && !cancelled.Contains(value.DecisionId)));
        var resolution = new EncounterActionResolution(sequence, timing.Revision, actor.ActorId, choice.DecisionId,
            targetId, abilityId, status, damage, beforeHp, afterHp, mana, delay, variation, cancelled);
        var nextActions = actions.WithResolution(actions.Pending.Where(value => value.CommandId != choice.DecisionId
            && !cancelled.Contains(value.CommandId)), random, resolution);
        if (resourceHistory is not null && resourceChanges.Count != 0)
        {
            var request = actions.Pending.Single(value => value.CommandId == choice.DecisionId);
            resourceHistory = resourceHistory.Append(new(sequence, 0, choice.DecisionId, resourceChanges),
                new WorldEncounterResourceEvidence(world.EncounterLifecycle!.ScenarioId!.Value, request, resolution));
        }
        return (timing, nextActions, progression, resourceHistory);
    }
}
