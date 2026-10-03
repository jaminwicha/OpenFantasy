using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Qualifies one present builder's work, retaining or committing the complete physical candidate atomically.</summary>
public sealed class ConstructionPlanner
{
    public ConstructionPlan Plan(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot production,
        ConstructionSnapshot construction, LocalSpatialSnapshot space, WorldActionCommand command,
        bool isAdvancingStep, bool held, Func<EntityId, bool>? occupied = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(command);
        if (production.IsContinued && production.Continuation?.SupportsCompleteWorld != true)
        {
            throw new ArgumentException("Construction requires qualified archived producer and ingredient witnesses.", nameof(production));
        }
        return PlanQualified(world, inventory, production, construction, space, command, isAdvancingStep, held, occupied, false);
    }

    internal ConstructionPlan PlanContinued(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot production,
        ConstructionSnapshot construction, LocalSpatialSnapshot space, WorldActionCommand command,
        bool isAdvancingStep, bool held, Func<EntityId, bool> occupied) =>
        PlanQualified(world, inventory, production, construction, space, command, isAdvancingStep, held, occupied, true);

    private static ConstructionPlan PlanQualified(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot production,
        ConstructionSnapshot construction, LocalSpatialSnapshot space, WorldActionCommand command,
        bool isAdvancingStep, bool held, Func<EntityId, bool>? occupied, bool continued)
    {
        ConstructionPlan Reject(WorldActionRejection rejection) => new(inventory, production, construction, space, rejection, []);
        var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
        if (command.WorldId != world.WorldId || command.BranchId != world.BranchId
            || inventory.WorldId != world.WorldId || inventory.BranchId != world.BranchId
            || construction.WorldId != world.WorldId || construction.BranchId != world.BranchId
            || production.WorldId != world.WorldId || production.BranchId != world.BranchId
            || space.Scope.WorldId != world.WorldId || space.Scope.BranchId != world.BranchId
            || WorldActorCommandAuthority.AuthorizeSelection(world, command) is null)
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (command.Intent is not WorkConstructionIntent intent)
        {
            throw new ArgumentException("This planner interprets construction work only.", nameof(command));
        }
        ConstructionProject? project = construction.FindProject(intent.ProjectId);
        if (project is null || project.BuilderActorId != command.ActorId
            || project.OwnerActorId != command.ActorId
            || !inventory.Policies.Any(value => value.CustodianId == project.ProjectCustodianId && value.OwnerActorId == command.ActorId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (!isAdvancingStep || held || world.HasReadingHold || project.IsCompleted || world.Progression is null || world.Quests is null)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        ActorPresence actor = space.GetActor(command.ActorId);
        if (actor.Revision != command.ExpectedActorRevision || project.Revision != intent.ExpectedProjectRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        if (actor.Binding.Kind != ActorFormKind.Corporeal || !actor.Condition.SupportsFunction(BodyFunction.Manipulation)
            || !actor.Condition.CanLocomote(LocomotionMode.Ground) || world.Progression.GetActor(command.ActorId).CurrentHp == 0
            || !world.Quests.Participations.Any(value => value.QuestId == project.QuestId && value.Status == ActorQuestStatus.Completed))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        if (actor.Feet != project.WorkFeet
            || space.QueryPointInteraction(actor.ActorId, project.SiteFeet, ConstructionDefinition.WorkReachSubunits) != SpatialInteraction.Reachable)
        {
            return Reject(WorldActionRejection.OutOfReach);
        }
        CountedSourceLot? tool = inventory.Custody.Lots.FirstOrDefault(value => value.LotId == intent.ToolLotId);
        EntityId primaryInventory = world.GetResident(command.ActorId).InventoryCustodianId;
        if (tool is null || tool.CustodianId != primaryInventory || tool.Definition != project.Definition.RequiredTool.Item
            || tool.Quantity != 1 || tool.ReservedBy is not null || tool.Protection != LotProtection.None)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        CustodianStorage storage = inventory.Custody.GetStorage(project.ProjectCustodianId);
        if (tool.Revision != intent.ExpectedToolLotRevision || storage.Revision != intent.ExpectedProjectStorageRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        try
        {
            CraftRecipeDefinition recipe = project.Definition.MaterialTransform;
            CountedItemDefinition input = recipe.ItemFor(recipe.Inputs.Single());
            CountedSourceLot[] stock = inventory.Custody.Lots.Where(value => value.ReservedBy == project.ReservationId).ToArray();
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
            StructurePlacementResult placement = space.PrepareStructurePlacement(
                project.Definition.CreateStructure(structureId, project.SiteFeet,
                    space.GroundSupport != GroundSupportMode.TerrainOnly), access);
            if (placement.Status != StructurePlacementStatus.Prepared)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            ExposureProgress progress = ExposureProgress.Restore(project.Progress, project.Definition.WorkRate)
                .Advance(LocalExposureDuration.FromMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep));
            if (progress.CompletedUnits < project.Definition.RequiredWork.Value)
            {
                return new ConstructionPlan(inventory, production, construction.WithProject(project.WithProgress(progress, null)),
                    space, WorldActionRejection.None, []);
            }
            if (production.Batches.Count == ProductionSnapshot.MaximumBatches
                || continued && production.MaterialHistory!.Operations.Count == MaterialOperationHistory.MaximumOperations)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            EntityId[] created = Enumerable.Range(0, recipe.Outputs.Count + 2)
                .Select(index => EntityId.DeriveSpawned(command.CommandId, (ulong)index)).ToArray();
            if (created.Distinct().Count() != created.Length || created.Any(id => id == command.CommandId
                    || world.IdentityIndex.Contains(id) || inventory.ContainsIdentity(id) || production.ContainsIdentity(id)
                    || occupied?.Invoke(id) == true))
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            UInt128 produced = recipe.Outputs.Aggregate((UInt128)0, (sum, value) => sum + value.Count);
            UInt128 after = (UInt128)production.GetCombinedStoredQuantity(inventory, storage.CustodianId)
                - recipe.Inputs[0].Count + produced;
            if (after > storage.UnitCapacity)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            var selections = stock.Select(value => new IngredientRequest(IngredientLotKind.Source, value.LotId, value.Revision, value.Quantity)).ToArray();
            var request = new CraftRequest(recipe.DefinitionId, recipe.Version, production.Revision,
                storage.CustodianId, storage.Revision, selections);
            var retired = stock.Select(value => new RetiredIngredient(value.LotId, value.Revision, value.Definition, value.Source)).ToArray();
            EntityId guardLot = created[checked((int)project.Definition.InstalledOutputIndex + 1)];
            var installed = new InstalledStructure(structureId, guardLot, 0, created[0], command.CommandId);
            ConstructionProject completedProject = project.WithProgress(progress, installed);
            ConstructionSnapshot completed = construction.WithProject(completedProject);
            ConstructionCompletionEvidence? evidence = null;
            CraftPlan material = MaterialTransformCommitter.Prepare(inventory, production, actor.ActorId, command.CommandId,
                request, recipe, retired, created[..^1], new InstalledOutputReservation(project.Definition.InstalledOutputIndex, project.ReservationId),
                continued ? (batch, candidateInventory) => evidence = new ConstructionCompletionEvidence(world, space, command,
                    inventory, candidateInventory, production, construction, completed, completedProject, batch) : null);
            if (material.Rejection != WorldActionRejection.None)
            {
                return Reject(material.Rejection);
            }
            return new ConstructionPlan(material.CandidateInventory, material.CandidateProduction, completed,
                placement.Candidate!, WorldActionRejection.None, created, evidence);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}
