using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>One construction decision implementation over admitted nonspatial facts and qualified geometry.</summary>
internal static class ConstructionWorkKernel
{
    internal static ConstructionWorkResult Plan(ConstructionDecisionSource world, CountedAuthorityState inventory, ProductionSnapshot production,
        ConstructionSnapshot construction, IConstructionGeometry space, WorldActionCommand command,
        bool isAdvancingStep, bool held, Func<EntityId, bool>? occupied, bool continued,
        Func<ConstructionCompletionRequest, ConstructionCompletionEvidence>? completionEvidence)
    {
        ConstructionWorkResult Reject(WorldActionRejection rejection) => new(inventory, production, construction, rejection, []);
        var qualification = ConstructionWorkQualification.Evaluate(world, inventory, production, construction,
            space, command, isAdvancingStep, held);
        if (qualification.Work is not { } work) return Reject(qualification.Rejection);

        var project = work.Project;
        var actor = work.Actor;
        var storage = work.Storage;
        var stock = work.ReservedStock;
        var recipe = work.Recipe;
        var progress = work.Progress;
        var proposed = work.Structure;
        var structureId = proposed.StructureId;
        var placement = work.Placement;
        try
        {
            if (progress.CompletedUnits < project.Definition.RequiredWork.Value)
            {
                return new ConstructionWorkResult(inventory, production, construction.WithProject(project.WithProgress(progress, null)),
                    WorldActionRejection.None, []);
            }
            if (production.Batches.Count == ProductionSnapshot.MaximumBatches
                || continued && production.MaterialHistory!.Operations.Count == MaterialOperationHistory.MaximumOperations)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            EntityId[] created = Enumerable.Range(0, recipe.Outputs.Count + 2)
                .Select(index => EntityId.DeriveSpawned(command.CommandId, (ulong)index)).ToArray();
            if (created.Distinct().Count() != created.Length || created.Any(id => id == command.CommandId
                    || world.ContainsIdentity(id) || inventory.ContainsIdentity(id) || production.ContainsIdentity(id)
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
                continued ? (batch, candidateInventory) => evidence = completionEvidence!(
                    new(candidateInventory, completed, completedProject, batch)) : null);
            if (material.Rejection != WorldActionRejection.None)
            {
                return Reject(material.Rejection);
            }
            return new ConstructionWorkResult(material.CandidateInventory, material.CandidateProduction, completed,
                WorldActionRejection.None, created, proposed, placement.Candidate, evidence);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}
