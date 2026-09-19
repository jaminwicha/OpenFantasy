using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
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
        if (world.WorldId != WorldId || world.BranchId != BranchId || Revision > world.Revision
            || world.Production is null || world.Quests is null || world.Progression is null)
        {
            throw new ArgumentException("Construction requires the retained production and quest world in one exact scope.");
        }
        ProductionSnapshot production = world.Production;
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
                || !world.Inventory.Sources.Any(value => value.Definition == definition.RequiredTool.Item))
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
                || !world.Inventory.Policies.Any(value => value.CustodianId == project.ProjectCustodianId && value.OwnerActorId == project.BuilderActorId)
                || !world.Inventory.Actors.Contains(project.BuilderActorId)
                || world.GetResident(project.BuilderActorId).InventoryCustodianId == project.ProjectCustodianId
                || project.RequiredAccess.Any(value => !world.Inventory.Actors.Contains(value.ActorId))
                || world.Space.Actors.Any(actor => actor.Feet.Z == project.SiteFeet.Z
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
            ProductionBatch? batch = production.Batches.FirstOrDefault(value => value.BatchId == installed.ProductionBatchId);
            var recipe = project.Definition.MaterialTransform;
            if (!installedLots.Add(installed.GuardLotId) || !installedBatches.Add(installed.ProductionBatchId)
                || installed.StructureId != EntityId.DeriveSpawned(installed.CompletionCommandId, (ulong)recipe.Outputs.Count + 1)
                || lot is null || lot.Revision != installed.GuardLotRevision || lot.Quantity != 1
                || lot.CustodianId != project.ProjectCustodianId || lot.ReservedBy != project.ReservationId
                || lot.Protection != LotProtection.None || lot.Output.BatchId != installed.ProductionBatchId
                || lot.Output.OutputIndex != project.Definition.InstalledOutputIndex || lot.Output.Start != 0 || lot.Output.End != 1
                || batch is null || batch.CommandId != installed.CompletionCommandId || batch.ActorId != project.BuilderActorId
                || batch.CustodianId != project.ProjectCustodianId || batch.RecipeId != recipe.DefinitionId || batch.RecipeVersion != recipe.Version
                || batch.Ingredients.Any(value => value.Kind != IngredientLotKind.Source)
                || world.Inventory.Custody.Lots.Any(value => value.ReservedBy == project.ReservationId))
            {
                throw new ArgumentException("An installed structure must exclusively retain its exact completed material batch and live output.");
            }
        }
        if (production.Batches.Any(value => IsConstructionRecipe(value.RecipeId) && !installedBatches.Contains(value.BatchId)))
        {
            throw new ArgumentException("Construction-only material batches require one fully worked, installed project.");
        }
        var expected = GetStructures(world.Space.GroundSupport == GroundSupportMode.TerrainAndConstruction);
        if (world.Space.Structures.Count != expected.Count || expected.Where((value, index) =>
                value.StructureId != world.Space.Structures[index].StructureId
                || !value.Boxes.SequenceEqual(world.Space.Structures[index].Boxes)
                || !value.BearingSurfaces.SequenceEqual(world.Space.Structures[index].BearingSurfaces)).Any())
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
