using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Finite work commitments and exclusive installed transformed material in one retained locality.</summary>
public sealed class ConstructionSnapshot
{
    public const int MaximumDefinitions = 16;
    public const int MaximumProjects = 32;

    public ConstructionSnapshot(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<ConstructionDefinition> definitions, IEnumerable<ConstructionProject> projects)
    {
        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Definitions = Copy(definitions, MaximumDefinitions, value => value.DefinitionId);
        Projects = Copy(projects, MaximumProjects, value => value.ProjectId);
        if (worldId == branchId || Definitions.Count == 0 || Projects.Count == 0
            || Definitions.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count() != Definitions.Count
            || Definitions.Select(value => value.MaterialTransform.DefinitionId).Distinct().Count() != Definitions.Count
            || Projects.Select(value => value.ReservationId).Distinct().Count() != Projects.Count
            || Projects.Select(value => value.ProjectCustodianId).Distinct().Count() != Projects.Count
            || Projects.Aggregate(0UL, (sum, value) => checked(sum + value.Revision)) != revision)
        {
            throw new ArgumentException("Construction requires unique finite definitions, exclusive project stock and retained work revisions.");
        }
        foreach (ConstructionProject project in Projects)
        {
            if (!Definitions.Any(value => value.DefinitionId == project.Definition.DefinitionId && value.Version == project.Definition.Version))
            {
                throw new ArgumentException("Each project must bind an exact admitted construction definition.");
            }
        }
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<ConstructionDefinition> Definitions { get; }
    public ReadOnlyCollection<ConstructionProject> Projects { get; }
    public ContentHash ContentHash => ContentHash.Compute(ConstructionCodecV1.Encode(this));
    public ConstructionProject? FindProject(EntityId id) => Projects.FirstOrDefault(value => value.ProjectId == id);
    public bool IsConstructionRecipe(EntityId recipeId) => Definitions.Any(value => value.MaterialTransform.DefinitionId == recipeId);
    public ReadOnlyCollection<SpatialStructure> GetStructures(bool supportsActors = false) => Array.AsReadOnly(Projects.Where(value => value.Installed is not null)
        .Select(value => value.Definition.CreateStructure(value.Installed!.StructureId, value.SiteFeet, supportsActors)).OrderBy(value => value.StructureId).ToArray());

    internal ConstructionSnapshot WithProject(ConstructionProject project) => new(WorldId, BranchId,
        checked(Revision + 1), Definitions, Projects.Where(value => value.ProjectId != project.ProjectId).Append(project));

    public void ValidateForWorld(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Production?.IsContinued == true)
            throw new ArgumentException("Complete-world construction retains its original inline production interpretation.", nameof(world));
        ValidateComponents(world, world.Inventory, world.Production, world.Space, world.ConstructionPhysical);
    }

    /// <summary>Validates exact standalone V5 material, work and physical components without admitting a continued world.</summary>
    public void ValidateForMaterialContinuation(WorldSnapshot world, CountedAuthorityState inventory,
        ProductionSnapshot production, LocalSpatialSnapshot space, ConstructionPhysicalSnapshot? physical = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(space);
        if (production.Continuation is not { SupportsConstruction: true })
            throw new ArgumentException("Component construction admission requires the explicit V5 interpretation.", nameof(production));
        ConstructionSourceFrame.ValidateSpace(world.Space, space);
        if (!Definitions.Select(value => value.MaterialTransform.DefinitionId).Order().SequenceEqual(production.Continuation.ConstructionRecipeIds))
            throw new ArgumentException("Construction component admission retains the exact originally captured construction recipe catalog.");
        if ((world.ConstructionPhysical is null) != (physical is null))
            throw new ArgumentException("Component admission must preserve the actual source world's physical interpretation.", nameof(physical));
        production.ValidateForAuthority(inventory);
        if (Projects.Any(project => inventory.ContainsIdentity(project.ProjectId) || production.ContainsIdentity(project.ProjectId))
            || physical?.Members.Any(member => inventory.ContainsIdentity(member.ElementId)
                || production.ContainsIdentity(member.ElementId) || Projects.Any(project => project.ProjectId == member.ElementId
                    || project.Installed?.StructureId == member.ElementId)) == true)
            throw new ArgumentException("Construction project and member identities cannot alias material or one another.");
        ValidateComponents(world, inventory, production, space, physical);
    }

    private void ValidateComponents(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot? production,
        LocalSpatialSnapshot space, ConstructionPhysicalSnapshot? physical)
    {
        if (world.WorldId != WorldId || world.BranchId != BranchId || Revision > world.Revision
            || production is null || inventory.WorldId != WorldId || inventory.BranchId != BranchId
            || production.WorldId != WorldId || production.BranchId != BranchId
            || space.Scope.WorldId != WorldId || space.Scope.BranchId != BranchId
            || physical is not null && (physical.ConstructionHash != ContentHash || physical.Revision > world.Revision)
            || world.Quests is null || world.Progression is null)
        {
            throw new ArgumentException("Construction requires the retained production and quest world in one exact scope.");
        }
        foreach (var builderProjects in Projects.GroupBy(value => value.BuilderActorId))
        {
            UInt128 workSteps = builderProjects.Aggregate((UInt128)0, (sum, value) => sum + value.Revision);
            UInt128 experiencedWork = builderProjects.Aggregate((UInt128)0,
                (sum, value) => checked(sum + value.Progress.Elapsed.TotalMilliseconds));
            if (workSteps > world.AdvancingSteps
                || experiencedWork > world.GetResident(builderProjects.Key).ExperiencedTime.TotalMilliseconds)
            {
                throw new ArgumentException("A builder's retained work must fit both advancing steps and actual experienced time.");
            }
        }
        foreach (ConstructionDefinition definition in Definitions)
        {
            if (!production.Recipes.Any(value => value.DefinitionId == definition.MaterialTransform.DefinitionId
                    && value.Version == definition.MaterialTransform.Version)
                || !inventory.Sources.Any(value => value.Definition == definition.RequiredTool.Item))
            {
                throw new ArgumentException("Construction requires exact admitted transform and finite tool definition pins.");
            }
        }
        HashSet<EntityId> installedLots = [];
        HashSet<EntityId> installedBatches = [];
        foreach (ConstructionProject project in Projects)
        {
            DeliveryQuestDefinition? quest = world.Quests.FindDefinition(project.QuestId);
            if (quest is null || quest.GiverActorId != project.OwnerActorId
                || quest.DestinationCustodianId != project.ProjectCustodianId || quest.MaterialReservationId != project.ReservationId
                || !inventory.Policies.Any(value => value.CustodianId == project.ProjectCustodianId && value.OwnerActorId == project.BuilderActorId)
                || !inventory.Actors.Contains(project.BuilderActorId)
                || world.GetResident(project.BuilderActorId).InventoryCustodianId == project.ProjectCustodianId
                || project.RequiredAccess.Any(value => !inventory.Actors.Contains(value.ActorId))
                || space.Actors.Any(actor => actor.Feet.Z == project.SiteFeet.Z
                    && !project.RequiredAccess.Any(value => value.ActorId == actor.ActorId))
                || project.Definition.MaterialTransform.Inputs.Count != 1
                || project.Definition.MaterialTransform.Inputs[0].Count != quest.Quantity
                || project.Definition.MaterialTransform.ItemFor(project.Definition.MaterialTransform.Inputs[0]) != quest.Item)
            {
                throw new ArgumentException("Construction must retain its exact giver-carried quest stock and admitted access witnesses.");
            }
            if (project.Revision != 0 && !world.Quests.Participations.Any(value =>
                    value.QuestId == project.QuestId && value.Status == ActorQuestStatus.Completed))
            {
                throw new ArgumentException("Construction work cannot precede its actual supply agreement completion.");
            }
            if (project.Installed is not { } installed)
            {
                continue;
            }
            TransformedLot? lot = production.Lots.FirstOrDefault(value => value.LotId == installed.GuardLotId);
            ProductionBatch? batch = production.Batches.FirstOrDefault(value => value.BatchId == installed.ProductionBatchId)
                ?? production.Continuation?.ConstructionBatches.FirstOrDefault(value => value.BatchId == installed.ProductionBatchId);
            if (batch is not null && !production.Batches.Any(value => value.BatchId == batch.BatchId))
                ConstructionBatchArchive.ValidateWitness(batch, production.Continuation!.Basis);
            var recipe = project.Definition.MaterialTransform;
            if (batch is not null) ConstructionBatchArchive.ValidateProject(project, production, batch);
            if (!installedLots.Add(installed.GuardLotId) || !installedBatches.Add(installed.ProductionBatchId)
                || installed.StructureId != EntityId.DeriveSpawned(installed.CompletionCommandId, (ulong)recipe.Outputs.Count + 1)
                || lot is null || lot.Revision != installed.GuardLotRevision || lot.Quantity != 1
                || lot.CustodianId != project.ProjectCustodianId || lot.ReservedBy != project.ReservationId
                || lot.Protection != LotProtection.None || lot.Output.BatchId != installed.ProductionBatchId
                || lot.Output.OutputIndex != project.Definition.InstalledOutputIndex || lot.Output.Start != 0 || lot.Output.End != 1
                || batch is null || batch.CommandId != installed.CompletionCommandId || batch.ActorId != project.BuilderActorId
                || batch.CustodianId != project.ProjectCustodianId || batch.RecipeId != recipe.DefinitionId || batch.RecipeVersion != recipe.Version
                || batch.Ingredients.Any(value => value.Kind != IngredientLotKind.Source)
                || inventory.Custody.Lots.Any(value => value.ReservedBy == project.ReservationId))
            {
                throw new ArgumentException("An installed structure must exclusively retain its exact completed material batch and live output.");
            }
        }
        if (production.Batches.Concat(production.Continuation?.ConstructionBatches.AsEnumerable() ?? Array.Empty<ProductionBatch>()).Any(value => IsConstructionRecipe(value.RecipeId) && !installedBatches.Contains(value.BatchId)))
        {
            throw new ArgumentException("Construction-only material batches require one fully worked, installed project.");
        }
        if (production.Continuation is { SupportsConstruction: true } context
            && context.Basis.BatchWitnesses.Any(value => context.ConstructionRecipeIds.Contains(value.RecipeId)
                && (!installedBatches.Contains(value.BatchId) || !context.ConstructionBatches.Any(batch => batch.BatchId == value.BatchId))))
            throw new ArgumentException("Every imported construction recipe producer requires one installed project and its complete original batch.");
        physical?.ValidateStableGeometry(space);
        var expected = physical?.GetStructures()
            ?? GetStructures(space.GroundSupport != GroundSupportMode.TerrainOnly);
        if (space.Structures.Count != expected.Count || expected.Where((value, index) =>
                value.StructureId != space.Structures[index].StructureId
                || !value.Boxes.SequenceEqual(space.Structures[index].Boxes)
                || !value.BearingSurfaces.SequenceEqual(space.Structures[index].BearingSurfaces)).Any())
        {
            throw new ArgumentException("Authoritative spatial structures must exactly match the installed physical definitions.");
        }
    }

    private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> source, int maximum, Func<T, EntityId> key) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        SortedDictionary<EntityId, T> result = [];
        foreach (T item in source)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (result.Count == maximum || !result.TryAdd(key(item), item))
            {
                throw new ArgumentException("The construction collection is excessive or has duplicate identities.");
            }
        }
        return Array.AsReadOnly(result.Values.ToArray());
    }
}
