using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Joins actual construction qualification, material retirement and physical completion in explicit V5.</summary>
public sealed class MaterialConstructionPlanner
{
    public MaterialConstructionPlan Plan(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot production,
        MaterialConstructionState state, LocalSpatialSnapshot space, WorldActionCommand command,
        bool isAdvancingStep, bool held, Func<EntityId, bool> occupied)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(occupied);
        if (production.Continuation is not { SupportsConstruction: true } || production.MaterialHistory is null)
            throw new ArgumentException("Construction continuation requires explicit V5 material capability.", nameof(production));
        MaterialConstructionPlan Reject(WorldActionRejection rejection) => new(inventory, production, state, space, rejection, []);
        bool CurrentOccupied(EntityId identity) => occupied(identity)
            || state.Physical?.Members.Any(value => value.ElementId == identity) == true
            || state.Construction.Projects.Any(value => value.ProjectId == identity || value.Installed?.StructureId == identity);
        try
        {
            if (!state.CanAdvance(world, command.ActorId)) return Reject(WorldActionRejection.RevisionConflict);
            if (CurrentOccupied(command.CommandId) || world.IdentityIndex.Contains(command.CommandId)
                || inventory.ContainsIdentity(command.CommandId) || production.ContainsIdentity(command.CommandId)
                || state.Physical?.Members.Any(value => value.ElementId == command.CommandId) == true
                || state.Construction.Projects.Any(value => value.ProjectId == command.CommandId || value.Installed?.StructureId == command.CommandId))
                return Reject(WorldActionRejection.Unavailable);
            state.Construction.ValidateForMaterialContinuation(world, inventory, production, space, state.Physical);
            ConstructionPlan plan = new ConstructionPlanner().PlanContinued(world, inventory, production,
                state.Construction, space, command, isAdvancingStep, held, CurrentOccupied);
            if (!plan.Succeeded) return Reject(plan.Rejection);
            ConstructionPhysicalSnapshot? physical = state.Physical?.ReconcileCompletions(plan.CandidateConstruction);
            List<EntityId> created = plan.CreatedIds.ToList();
            if (physical is not null)
            {
                var old = state.Physical!.Members.Select(value => value.ElementId).ToHashSet();
                foreach (EntityId id in physical.Members.Select(value => value.ElementId).Where(value => !old.Contains(value)))
                {
                    if (id == command.CommandId || created.Contains(id) || CurrentOccupied(id) || world.IdentityIndex.Contains(id)
                        || inventory.ContainsIdentity(id) || plan.CandidateProduction.ContainsIdentity(id))
                        return Reject(WorldActionRejection.Unavailable);
                    created.Add(id);
                }
            }
            plan.CandidateConstruction.ValidateForMaterialContinuation(world, plan.CandidateInventory,
                plan.CandidateProduction, plan.CandidateSpace, physical);
            MaterialConstructionState next = state.Advance(world, command.ActorId, plan.CandidateConstruction, physical);
            return new(plan.CandidateInventory, plan.CandidateProduction, next, plan.CandidateSpace,
                WorldActionRejection.None, created, plan.Evidence);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or ProductionFormatException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}
