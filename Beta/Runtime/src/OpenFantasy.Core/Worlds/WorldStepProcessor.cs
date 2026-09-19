using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Replays a host-selected batch without IO or wall-clock access. Authentication and selection at
/// the fixed host cutoff precede this boundary; encoded delivery claims do not authenticate callers.
/// </summary>
public sealed class WorldStepProcessor
{
    private readonly WorldActionResolver _actions = new();
    private readonly ExcavationPlanner _excavation = new();
    private readonly GatherPlanner _gathering = new();
    private readonly WorldCraftResolver _crafting = new();
    private readonly DeliveryQuestPlanner _quests = new();
    private readonly ConstructionPlanner _construction = new();
    private readonly WorldRunoffResolver _runoff = new();
    private readonly WorldTransportResolver _transport = new();

    public WorldStepDecision Decide(WorldSnapshot state, WorldStepBatch batch,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.ProfileVersion != state.ProfileVersion || batch.WorldId != state.WorldId || batch.BranchId != state.BranchId
            || batch.ExpectedRevision != state.Revision || batch.ExpectedAdvancingSteps != state.AdvancingSteps
            || (batch.Kind == WorldBatchKind.HeldInteraction) != state.HasReadingHold)
        {
            throw new InvalidOperationException("The selected batch does not match the current world, revision or hold.");
        }

        ulong sequence = checked(state.Revision + 1);
        HashSet<EntityId> reserved = [];
        List<WorldCommittedIdentity> identities = [];
        bool Occupied(EntityId identity) => reserved.Contains(identity) || state.IdentityIndex.Contains(identity)
            || historicallyOccupied?.Invoke(identity) == true;
        void Reserve(EntityId identity, WorldCommittedIdentityKind kind)
        {
            if (Occupied(identity))
            {
                throw new InvalidOperationException("A committed identity is already occupied.");
            }
            reserved.Add(identity);
            identities.Add(new WorldCommittedIdentity(identity, kind));
        }

        Reserve(batch.CauseId, WorldCommittedIdentityKind.CommitCause);
        foreach (WorldActionCommand action in batch.Actions)
        {
            Reserve(action.CommandId, WorldCommittedIdentityKind.Command);
        }
        CountedAuthorityState inventory = state.Inventory;
        GatheringSnapshot? gathering = state.Gathering;
        ProductionSnapshot? production = state.Production;
        ProgressionSnapshot? progression = state.Progression;
        QuestSnapshot? quests = state.Quests;
        ConstructionSnapshot? construction = state.Construction;
        LocalSpatialSnapshot space = state.Space;
        LiquidReservoirState? reservoirs = state.Reservoirs;
        ReservoirTransportState? transport = state.Transport;
        ExcavationSnapshot? excavation = state.Excavation;
        HashSet<EntityId> worked = [];
        List<DialogueSession> dialogues = state.Dialogues.ToList();
        List<WorldActionReceipt> receipts = [];
        foreach (WorldActionCommand action in batch.Actions)
        {
            // Material validation covers the entire action, including its non-inventory side effects.
            var previousExcavation = excavation;
            var previousGathering = gathering;
            var previousProduction = production;
            var previousProgression = progression;
            var previousQuests = quests;
            var previousConstruction = construction;
            var previousSpace = space;
            var previousReservoirs = reservoirs;
            var previousTransport = transport;
            bool performedWork = false;
            WorldActionResolution resolution;
            IReadOnlyList<EntityId> createdIds = [];
            if (action.Intent is ClimbIntent)
            {
                if (gathering is null || state.ProfileVersion != 11)
                {
                    throw new InvalidOperationException("This saved profile does not interpret assisted climbing.");
                }
                var plan = WorldClimbResolver.Resolve(inventory, space, gathering, action,
                    batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0);
                space = plan.Space;
                gathering = plan.Gathering;
                resolution = new(inventory, dialogues, plan.Rejection, null);
                performedWork = plan.Rejection == WorldActionRejection.None;
            }
            else if (action.Intent is ExcavateIntent)
            {
                if (excavation is null || gathering is null || production is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret excavation.");
                }
                var plan = _excavation.Plan(state, inventory, space, gathering, production, excavation, action,
                    batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0, Occupied);
                resolution = new(plan.Inventory, dialogues, plan.Rejection, plan.SpawnedId);
                excavation = plan.Excavation;
                gathering = plan.Gathering;
                space = plan.Space;
                performedWork = plan.Succeeded;
            }
            else if (action.Intent is ScheduleRunoffTransportIntent)
            {
                if (transport is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret scheduled transport.");
                }
                var plan = _transport.Resolve(transport, space, action,
                    batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0, Occupied);
                transport = plan.Candidate;
                createdIds = plan.CreatedIds;
                resolution = new WorldActionResolution(inventory, dialogues, plan.Rejection, null);
            }
            else if (action.Intent is RunoffCommitmentIntent)
            {
                if (reservoirs is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret runoff commitments.");
                }
                var plan = _runoff.Resolve(state, reservoirs, space, action,
                    batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0);
                reservoirs = plan.Candidate;
                if (plan.Rejection == WorldActionRejection.None && transport is not null)
                {
                    transport = action.Intent is CancelRunoffIntent cancel
                        ? transport.PlanCancel(cancel.ReservationId).ApplyTo(transport)
                        : transport.PlanRebind(reservoirs).ApplyTo(transport);
                    reservoirs = transport.Reservoirs;
                }
                resolution = new WorldActionResolution(inventory, dialogues, plan.Rejection, null);
            }
            else if (action.Intent is WorkConstructionIntent)
            {
                if (construction is null || production is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret construction work.");
                }
                ConstructionPlan plan = _construction.Plan(state, inventory, production, construction, space,
                    action, batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0, Occupied);
                resolution = new WorldActionResolution(plan.CandidateInventory, dialogues, plan.Rejection, null);
                production = plan.CandidateProduction;
                construction = plan.CandidateConstruction;
                space = plan.CandidateSpace;
                createdIds = plan.CreatedIds;
                if (plan.Succeeded)
                {
                    performedWork = true;
                }
            }
            else if (action.Intent is DeliveryQuestIntent)
            {
                if (quests is null || progression is null || production is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret quest actions.");
                }
                DeliveryQuestPlan plan = _quests.Plan(state, inventory, production, progression, quests,
                    action, dialogues, identity => identity != action.CommandId && Occupied(identity), space);
                resolution = new WorldActionResolution(plan.CandidateInventory, dialogues, plan.Rejection, null);
                progression = plan.CandidateProgression;
                quests = plan.CandidateQuests;
                createdIds = plan.CreatedIds;
            }
            else if (action.Intent is GatherIntent)
            {
                if (gathering is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret gathering actions.");
                }
                GatherPlan? plan = dialogues.Count == 0 ? _gathering.Plan(state, inventory, gathering, action, space) : null;
                if (plan is null || plan.SpawnedId is { } generated && Occupied(generated)
                    || !FitsProduction(production, plan.CandidateInventory))
                {
                    resolution = new WorldActionResolution(inventory, dialogues, WorldActionRejection.Unavailable, null);
                }
                else
                {
                    resolution = new WorldActionResolution(plan.CandidateInventory, dialogues, plan.Rejection, plan.SpawnedId);
                    gathering = plan.CandidateGathering;
                }
            }
            else if (action.Intent is CraftIntent)
            {
                if (production is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret crafting actions.");
                }
                CraftPlan plan = _crafting.Resolve(state, inventory, production, action, dialogues.Count != 0, Occupied, space);
                resolution = new WorldActionResolution(plan.CandidateInventory, dialogues, plan.Rejection, null);
                production = plan.CandidateProduction;
                createdIds = plan.CreatedIds;
            }
            else
            {
                resolution = _actions.Resolve(state, inventory, dialogues, action, Occupied, space);
                if (!FitsProduction(production, resolution.Inventory))
                {
                    resolution = new WorldActionResolution(inventory, dialogues, WorldActionRejection.Unavailable, null);
                }
            }
            if (!WorldReservoirBinding.TryReconcile(reservoirs, resolution.Inventory, production, out var reconciled)
                || !WorldTransportBinding.TryReconcile(transport, reconciled, out var reconciledTransport))
            {
                excavation = previousExcavation;
                gathering = previousGathering;
                production = previousProduction;
                progression = previousProgression;
                quests = previousQuests;
                construction = previousConstruction;
                space = previousSpace;
                reservoirs = previousReservoirs;
                transport = previousTransport;
                performedWork = false;
                createdIds = [];
                resolution = new WorldActionResolution(inventory, dialogues, WorldActionRejection.Unavailable, null);
            }
            else
            {
                reservoirs = reconciled;
                transport = reconciledTransport;
            }
            if (performedWork)
            {
                worked.Add(action.ActorId);
            }
            var receipt = WorldActionReceipt.Create(action, sequence, resolution.Rejection);
            Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
            if (resolution.SpawnedId is { } spawned)
            {
                Reserve(spawned, WorldCommittedIdentityKind.SpawnedEntity);
            }
            foreach (EntityId created in createdIds)
            {
                Reserve(created, WorldCommittedIdentityKind.SpawnedEntity);
            }
            inventory = resolution.Inventory;
            dialogues = resolution.Dialogues;
            receipts.Add(receipt);
        }

        WorldInstant calendar = state.Calendar;
        ulong advancingSteps = state.AdvancingSteps;
        IEnumerable<WorldResident> residents = state.Residents;
        if (batch.Kind == WorldBatchKind.AdvancingStep)
        {
            // The already selected step contributes once, including when an action opens a hold.
            calendar = calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep);
            advancingSteps = checked(advancingSteps + 1);
            if (transport is not null)
            {
                var stages = transport.PlanAdvance(calendar, dialogues.Count != 0, Occupied);
                transport = stages.ApplyTo(transport);
                reservoirs = transport.Reservoirs;
                inventory = reservoirs.Capacity.Inventory;
                foreach (var identity in stages.CreatedIds)
                {
                    Reserve(identity, WorldCommittedIdentityKind.SpawnedEntity);
                }
            }
            gathering = gathering?.AdvanceStep(advancingSteps);
            residents = state.Residents.Select(resident => resident.ExperienceStep());
            if (dialogues.Count == 0)
            {
                foreach (WorldMovementInput input in batch.Movement)
                {
                    if (worked.Contains(input.ActorId) || !inventory.CanControl(input.Delivery, input.ActorId))
                    {
                        continue;
                    }
                    ActorPresence actor = space.GetActor(input.ActorId);
                    SpatialMoveResult move = space.PrepareMove(input.ActorId, actor.Revision, input.DeltaX, input.DeltaY);
                    if (move.Status == SpatialMoveStatus.Prepared)
                    {
                        space = space.WithActors(space.Actors.Select(other => other.ActorId == input.ActorId ? move.Candidate! : other));
                    }
                }
            }
        }
        WorldSnapshot candidate = state.WithState(inventory, space, residents, calendar, sequence,
            advancingSteps, dialogues, gathering, production, progression, quests, construction, reservoirs, transport, excavation);
        return new WorldStepDecision(candidate, receipts, identities);
    }

    private static bool FitsProduction(ProductionSnapshot? production, CountedAuthorityState inventory)
    {
        try
        {
            production?.ValidateForAuthority(inventory);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            // A material candidate can exceed combined capacity although its raw subset still fits.
            return false;
        }
    }
}
