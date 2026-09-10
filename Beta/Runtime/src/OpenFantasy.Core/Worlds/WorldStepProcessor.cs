using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Replays a host-selected batch without IO or wall-clock access. Authentication and selection at
/// the fixed host cutoff precede this boundary; encoded delivery claims do not authenticate callers.
/// </summary>
public sealed class WorldStepProcessor
{
    private readonly WorldActionResolver _actions = new();
    private readonly GatherPlanner _gathering = new();
    private readonly WorldCraftResolver _crafting = new();

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
        List<DialogueSession> dialogues = state.Dialogues.ToList();
        List<WorldActionReceipt> receipts = [];
        foreach (WorldActionCommand action in batch.Actions)
        {
            WorldActionResolution resolution;
            IReadOnlyList<EntityId> craftCreated = [];
            if (action.Intent is GatherIntent)
            {
                if (gathering is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret gathering actions.");
                }
                GatherPlan? plan = dialogues.Count == 0 ? _gathering.Plan(state, inventory, gathering, action) : null;
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
                CraftPlan plan = _crafting.Resolve(state, inventory, production, action, dialogues.Count != 0, Occupied);
                resolution = new WorldActionResolution(plan.CandidateInventory, dialogues, plan.Rejection, null);
                production = plan.CandidateProduction;
                craftCreated = plan.CreatedIds;
            }
            else
            {
                resolution = _actions.Resolve(state, inventory, dialogues, action, Occupied);
                if (!FitsProduction(production, resolution.Inventory))
                {
                    resolution = new WorldActionResolution(inventory, dialogues, WorldActionRejection.Unavailable, null);
                }
            }
            var receipt = WorldActionReceipt.Create(action, sequence, resolution.Rejection);
            Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
            if (resolution.SpawnedId is { } spawned)
            {
                Reserve(spawned, WorldCommittedIdentityKind.SpawnedEntity);
            }
            foreach (EntityId created in craftCreated)
            {
                Reserve(created, WorldCommittedIdentityKind.SpawnedEntity);
            }
            inventory = resolution.Inventory;
            dialogues = resolution.Dialogues;
            receipts.Add(receipt);
        }

        LocalSpatialSnapshot space = state.Space;
        WorldInstant calendar = state.Calendar;
        ulong advancingSteps = state.AdvancingSteps;
        IEnumerable<WorldResident> residents = state.Residents;
        if (batch.Kind == WorldBatchKind.AdvancingStep)
        {
            // The already selected step contributes once, including when an action opens a hold.
            calendar = calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep);
            advancingSteps = checked(advancingSteps + 1);
            gathering = gathering?.AdvanceStep(advancingSteps);
            residents = state.Residents.Select(resident => resident.ExperienceStep());
            if (dialogues.Count == 0)
            {
                foreach (WorldMovementInput input in batch.Movement)
                {
                    if (!inventory.CanControl(input.Delivery, input.ActorId))
                    {
                        continue;
                    }
                    ActorPresence actor = space.GetActor(input.ActorId);
                    SpatialMoveResult move = space.PrepareMove(input.ActorId, actor.Revision, input.DeltaX, input.DeltaY);
                    if (move.Status == SpatialMoveStatus.Prepared)
                    {
                        space = new LocalSpatialSnapshot(space.Scope, space.Regions,
                            space.Actors.Select(other => other.ActorId == input.ActorId ? move.Candidate! : other));
                    }
                }
            }
        }
        WorldSnapshot candidate = state.WithState(inventory, space, residents, calendar, sequence,
            advancingSteps, dialogues, gathering, production);
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
