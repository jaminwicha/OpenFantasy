using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Construction;

/// <summary>
/// One ephemeral decision over actual construction dependencies. Qualification does not award a clock,
/// publish geometry or authenticate a selectively loaded cohort; the owner must still admit the step.
/// </summary>
internal sealed record ConstructionWorkQualification(WorldActionRejection Rejection, QualifiedConstructionWork? Work)
{
    internal static ConstructionWorkQualification Evaluate(ConstructionDecisionSource world,
        CountedAuthorityState inventory, ProductionSnapshot production, ConstructionSnapshot construction,
        IConstructionGeometry space, WorldActionCommand command, bool isAdvancingStep, bool held) =>
        Evaluate(world, new CompleteConstructionWorkOwners(inventory, production, construction),
            space, command, isAdvancingStep, held);

    internal static ConstructionWorkQualification Evaluate(ConstructionDecisionSource world,
        IConstructionWorkOwners owners, IConstructionGeometry space, WorldActionCommand command,
        bool isAdvancingStep, bool held)
    {
        ConstructionWorkQualification Reject(WorldActionRejection rejection) => new(rejection, null);
        if (command.WorldId != world.WorldId || command.BranchId != world.BranchId
            || !owners.BelongsTo(world.WorldId, world.BranchId)
            || space.Scope.WorldId != world.WorldId || space.Scope.BranchId != world.BranchId
            || !world.Authorizes(command))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (command.Intent is not WorkConstructionIntent intent)
        {
            throw new ArgumentException("This planner interprets construction work only.", nameof(command));
        }
        ConstructionProject? project = owners.FindProject(intent.ProjectId);
        if (project is null || project.BuilderActorId != command.ActorId
            || project.OwnerActorId != command.ActorId
            || !owners.OwnsCustodian(project.ProjectCustodianId, command.ActorId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (!isAdvancingStep || held || world.HasReadingHold || project.IsCompleted || !world.HasProgressionAndQuests)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        ActorPresence actor = space.GetActor(command.ActorId);
        if (actor.Revision != command.ExpectedActorRevision || project.Revision != intent.ExpectedProjectRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        if (actor.Binding.Kind != ActorFormKind.Corporeal || !actor.Condition.SupportsFunction(BodyFunction.Manipulation)
            || !actor.Condition.CanLocomote(LocomotionMode.Ground) || world.CurrentHp(command.ActorId) == 0
            || !world.QuestCompleted(project.QuestId))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        if (actor.Feet != project.WorkFeet
            || space.QueryPointInteraction(actor.ActorId, project.SiteFeet, ConstructionDefinition.WorkReachSubunits) != SpatialInteraction.Reachable)
        {
            return Reject(WorldActionRejection.OutOfReach);
        }
        CountedSourceLot? tool = owners.FindLot(intent.ToolLotId);
        EntityId primaryInventory = world.InventoryCustodian(command.ActorId);
        if (tool is null || tool.CustodianId != primaryInventory || tool.Definition != project.Definition.RequiredTool.Item
            || tool.Quantity != 1 || tool.ReservedBy is not null || tool.Protection != LotProtection.None)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        CustodianStorage storage = owners.GetStorage(project.ProjectCustodianId);
        if (tool.Revision != intent.ExpectedToolLotRevision || storage.Revision != intent.ExpectedProjectStorageRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        try
        {
            CraftRecipeDefinition recipe = project.Definition.MaterialTransform;
            CountedItemDefinition input = recipe.ItemFor(recipe.Inputs.Single());
            CountedSourceLot[] stock = owners.ReservedStock(project.ReservationId);
            if (stock.Length is 0 or > MaterialRecipeDefinition.MaximumTermCount || stock.Any(value =>
                    value.CustodianId != project.ProjectCustodianId || value.Definition != input || value.Protection != LotProtection.None)
                || stock.Aggregate(0UL, (sum, value) => checked(sum + value.Quantity)) != recipe.Inputs[0].Count)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            ulong structureOrdinal = checked((ulong)recipe.Outputs.Count + 1);
            EntityId structureId = EntityId.DeriveSpawned(command.CommandId, structureOrdinal);
            var access = project.RequiredAccess.Concat(project.RequiredAccess.Select(value =>
                new GroundAccessRequirement(value.ActorId, space.GetActor(value.ActorId).Feet, value.To))).Distinct().ToArray();
            SpatialStructure proposed = project.Definition.CreateStructure(structureId, project.SiteFeet,
                space.GroundSupport != GroundSupportMode.TerrainOnly);
            StructurePlacementResult placement = space.PrepareStructurePlacement(proposed, access);
            if (placement.Status != StructurePlacementStatus.Prepared)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            ExposureProgress progress = ExposureProgress.Restore(project.Progress, project.Definition.WorkRate)
                .Advance(LocalExposureDuration.FromMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep));
            return new(WorldActionRejection.None, new(project, actor, tool, storage,
                Array.AsReadOnly(stock), Array.AsReadOnly(access), recipe, progress, proposed, placement));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}

/// <summary>
/// Named dependencies and the proposed work result for this evaluation only. Full owners, identity
/// history, clocks, freshness and publication remain with the caller; no subset admission is implied.
/// </summary>
internal sealed record QualifiedConstructionWork(ConstructionProject Project, ActorPresence Actor,
    CountedSourceLot Tool, CustodianStorage Storage, ReadOnlyCollection<CountedSourceLot> ReservedStock,
    ReadOnlyCollection<GroundAccessRequirement> RequiredAccess, CraftRecipeDefinition Recipe,
    ExposureProgress Progress, SpatialStructure Structure, StructurePlacementResult Placement);
