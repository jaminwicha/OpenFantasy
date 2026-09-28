using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Construction;

/// <summary>Bounded original direct-source fabrication records; never recursive producer ancestry.</summary>
internal static class ConstructionBatchArchive
{
    internal static ReadOnlyCollection<ProductionBatch> Capture(ProductionSnapshot production,
        ProductionContinuationBasis basis, ConstructionSnapshot? construction, bool enabled, IReadOnlyCollection<EntityId> constructionRecipes)
    {
        if (!enabled) return Array.AsReadOnly(Array.Empty<ProductionBatch>());
        var actual = production.Batches.Concat(production.Continuation?.ConstructionBatches.AsEnumerable() ?? Array.Empty<ProductionBatch>())
            .ToDictionary(value => value.BatchId);
        var ids = construction is null
            ? (production.Continuation?.ConstructionBatches.Select(value => value.BatchId) ?? [])
                .Concat(production.MaterialHistory?.Operations.OfType<MaterialConstructionOperation>()
                    .Select(value => value.Evidence.Batch.BatchId) ?? []).ToHashSet()
            : construction.Projects.Where(value => value.Installed is not null)
                .Select(value => value.Installed!.ProductionBatchId).ToHashSet();
        if (ids.Count > ConstructionSnapshot.MaximumProjects)
            throw new ArgumentException("Construction archives retain at most one original batch per bounded project.");
        if (production.Batches.Any(value => constructionRecipes.Contains(value.RecipeId) && !ids.Contains(value.BatchId))
            || basis.BatchWitnesses.Any(value => constructionRecipes.Contains(value.RecipeId) && !ids.Contains(value.BatchId)))
            throw new ArgumentException("Every active or imported construction recipe producer needs its exact installed project archive.");
        List<ProductionBatch> records = [];
        foreach (EntityId id in ids.Order())
        {
            if (!actual.TryGetValue(id, out ProductionBatch? batch) || batch is null)
                throw new ArgumentException("Construction requires the actual original complete fabrication batch.");
            ValidateWitness(batch, basis);
            records.Add(batch);
        }
        if (construction is not null)
        {
            foreach (ConstructionProject project in construction.Projects.Where(value => value.Installed is not null))
                ValidateProject(project, production, records.Single(value => value.BatchId == project.Installed!.ProductionBatchId));
            if (production.Batches.Any(value => construction.IsConstructionRecipe(value.RecipeId) && !ids.Contains(value.BatchId))
                || production.Continuation?.ConstructionBatches.Any(value => !ids.Contains(value.BatchId)) == true
                || production.MaterialHistory?.Operations.OfType<MaterialConstructionOperation>()
                    .Any(value => !ids.Contains(value.Evidence.Batch.BatchId)) == true)
                throw new ArgumentException("Every retained construction producer belongs to one installed project.");
        }
        return records.AsReadOnly();
    }

    internal static void ValidateWitness(ProductionBatch batch, ProductionContinuationBasis basis)
    {
        ProductionBatchWitness? witness = basis.BatchWitnesses.FirstOrDefault(value => value.BatchId == batch.BatchId);
        if (witness is null || witness.CanonicalBatchHash != ContentHash.Compute(ProductionCodecV1.EncodeBatch(batch))
            || batch.Ingredients.Count is 0 or > MaterialRecipeDefinition.MaximumTermCount
            || batch.Ingredients.Any(value => value.Kind != IngredientLotKind.Source))
            throw new ArgumentException("Archived construction must match its complete original source-only batch witness.");
    }

    internal static void ValidateProject(ConstructionProject project, ProductionSnapshot production, ProductionBatch batch)
    {
        InstalledStructure installed = project.Installed!;
        CraftRecipeDefinition recipe = project.Definition.MaterialTransform;
        TransformedLot? lot = production.Lots.FirstOrDefault(value => value.LotId == installed.GuardLotId);
        if (batch.BatchId != installed.ProductionBatchId || batch.CommandId != installed.CompletionCommandId
            || batch.ActorId != project.BuilderActorId || batch.CustodianId != project.ProjectCustodianId
            || batch.RecipeId != recipe.DefinitionId || batch.RecipeVersion != recipe.Version
            || batch.Ingredients.Any(value => value.Kind != IngredientLotKind.Source)
            || lot is null || lot.Revision != installed.GuardLotRevision || lot.Quantity != 1
            || lot.ReservedBy != project.ReservationId || lot.CustodianId != project.ProjectCustodianId
            || lot.Protection != Custody.LotProtection.None || lot.Output.BatchId != batch.BatchId
            || lot.Output.OutputIndex != project.Definition.InstalledOutputIndex || lot.Output.Start != 0 || lot.Output.End != 1
            || installed.StructureId != EntityId.DeriveSpawned(batch.CommandId, (ulong)recipe.Outputs.Count + 1))
            throw new ArgumentException("Installed construction requires its exact original batch and exclusive reserved output.");
        ProductionClaimValidation.ValidateIngredientAmounts(recipe, batch.Ingredients);
    }
}
