using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Prepares one finite kit and a source-stock construction commitment without starting work.</summary>
internal sealed class ConstructionFarmBuilder
{
    private readonly WorldSnapshot _world;
    private readonly ConstructionFarmDefinition _metadata;
    private readonly ConstructionFarmIdentityScope _identities;
    private readonly WorldResident _wren;

    internal ConstructionFarmBuilder(WorldSnapshot world, ConstructionFarmDefinition metadata)
    {
        _world = world;
        _metadata = metadata;
        _identities = new ConstructionFarmIdentityScope(world);
        _wren = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host);
    }

    internal ConstructionFarmComponents Build(bool fresh)
    {
        JsonElement toolMetadata = _metadata.Root.GetProperty("tool");
        var toolItem = new CountedItemDefinition(_identities.Definition(toolMetadata.GetProperty("definitionKey").GetString()!),
            FarmCanonicalJson.Hash(toolMetadata));
        var tool = new ConstructionToolDefinition(_identities.Definition(toolMetadata.GetProperty("qualificationKey").GetString()!),
            toolItem, (ConstructionToolOperation)toolMetadata.GetProperty("operation").GetInt32());
        CraftRecipeDefinition transform = CreateMaterialTransform();
        ConstructionDefinition definition = CreateDefinition(transform, tool);
        ConstructionProject project = CreateProject(definition);
        var construction = new ConstructionSnapshot(_world.WorldId, _world.BranchId, 0, [definition], [project]);
        var source = new AdmittedCountedSource(_identities.Instance(toolMetadata.GetProperty("sourceRole").GetString()!),
            toolItem, toolMetadata.GetProperty("sourceCapacity").GetUInt64());
        var toolAdmission = new ConstructionToolAdmission(_wren.ActorId, _wren.InventoryCustodianId, source,
            _identities.Instance(toolMetadata.GetProperty("lotRole").GetString()!));
        var admission = new WorldConstructionAdmission(ConstructionFarmAdmission.AdmissionVersion, construction, [toolAdmission]);
        CountedAuthorityState inventory = fresh ? admission.CreateGenesisInventory(_world.Inventory) : AddTool(toolAdmission);
        ProductionSnapshot current = _world.Production!;
        var production = new ProductionSnapshot(_world.WorldId, _world.BranchId, current.Revision,
            current.Recipes.Append(transform), current.Batches, current.Lots);
        production.ValidateForAuthority(inventory);
        return new ConstructionFarmComponents(inventory, production, admission);
    }

    private CraftRecipeDefinition CreateMaterialTransform()
    {
        CraftRecipeDefinition retained = _world.Production!.Recipes[0];
        ItemPhysicalBinding wood = retained.Bindings.Single(value => value.Physical.Key == "wood");
        ItemPhysicalBinding offcut = retained.Bindings.Single(value => value.Physical.Key == "wood_offcuts");
        JsonElement itemMetadata = _metadata.Root.GetProperty("installedItem");
        var item = new CountedItemDefinition(_identities.Definition(itemMetadata.GetProperty("definitionKey").GetString()!),
            FarmCanonicalJson.Hash(itemMetadata));
        JsonElement materialMetadata = _metadata.Root.GetProperty("installedMaterial");
        var package = new PackageId(_metadata.Root.GetProperty("physicalPackage").GetString()!);
        var material = new MaterialDefinition(package, _identities.Definition(materialMetadata.GetProperty("definitionKey").GetString()!),
            materialMetadata.GetProperty("key").GetString()!, new MassMilligrams(materialMetadata.GetProperty("massMilligrams").GetUInt64()),
            new VolumeMicrolitres(materialMetadata.GetProperty("volumeMicrolitres").GetUInt64()),
            new HardnessRating(materialMetadata.GetProperty("hardness").GetUInt32()));
        JsonElement recipeMetadata = _metadata.Root.GetProperty("materialTransform");
        var physical = new MaterialRecipeDefinition(package,
            _identities.Definition(recipeMetadata.GetProperty("physicalDefinitionKey").GetString()!), recipeMetadata.GetProperty("key").GetString()!,
            new WorkUnits(recipeMetadata.GetProperty("requiredWork").GetUInt64()), FarmCanonicalJson.Hash(recipeMetadata),
            new MaterialCatalog([wood.Physical, offcut.Physical, material]),
            [new RecipeTerm(RecipeMaterialRole.Input, wood.Physical.Pin(), recipeMetadata.GetProperty("inputWoodCount").GetUInt64()),
             new RecipeTerm(RecipeMaterialRole.Output, material.Pin(), recipeMetadata.GetProperty("installedOutputCount").GetUInt64()),
             new RecipeTerm(RecipeMaterialRole.Waste, offcut.Physical.Pin(), recipeMetadata.GetProperty("offcutOutputCount").GetUInt64())]);
        var allocations = recipeMetadata.GetProperty("allocations").EnumerateArray().Select(value =>
            new OutputMaterialAllocation(wood.Item, value.GetProperty("outputIndex").GetUInt32(),
                new MassMilligrams(value.GetProperty("massPerOutputUnitMilligrams").GetUInt64())));
        return new CraftRecipeDefinition(_identities.Definition(recipeMetadata.GetProperty("definitionKey").GetString()!),
            physical, [wood, offcut, new ItemPhysicalBinding(item, material)], allocations);
    }

    private ConstructionDefinition CreateDefinition(CraftRecipeDefinition transform, ConstructionToolDefinition tool)
    {
        JsonElement metadata = _metadata.Root.GetProperty("construction");
        var rate = new ExposureRate(_identities.Definition(metadata.GetProperty("workUnitKey").GetString()!),
            FarmCanonicalJson.Hash(metadata), 1, metadata.GetProperty("millisecondsPerWorkUnit").GetUInt64());
        var members = metadata.GetProperty("membersMillimetres").EnumerateArray().Select(value =>
        {
            long[] coordinates = value.EnumerateArray().Select(element => element.GetInt64()).ToArray();
            return new MillimetreBox(coordinates[0], coordinates[1], coordinates[2], coordinates[3], coordinates[4], coordinates[5]);
        });
        return new ConstructionDefinition(_identities.Definition(metadata.GetProperty("definitionKey").GetString()!),
            metadata.GetProperty("key").GetString()!, metadata.GetProperty("displayName").GetString()!, transform, 0, tool,
            members, new WorkUnits(metadata.GetProperty("requiredWork").GetUInt64()), rate,
            (ConstructionStockInterpretation)metadata.GetProperty("stockInterpretation").GetInt32());
    }

    private ConstructionProject CreateProject(ConstructionDefinition definition)
    {
        JsonElement metadata = _metadata.Root.GetProperty("construction");
        DeliveryQuestDefinition quest = _world.Quests!.Definitions[0];
        SubunitPosition site = Position(metadata.GetProperty("siteSubunits"));
        SubunitPosition work = Position(metadata.GetProperty("workSubunits"));
        SubunitPosition[] exits = metadata.GetProperty("exitCells").EnumerateArray().Select(value =>
            new SubunitPosition(value[0].GetInt64() * 256 + 128, value[1].GetInt64() * 256 + 128, site.Z)).ToArray();
        var access = exits.Select(exit => new GroundAccessRequirement(_wren.ActorId, work, exit)).ToList();
        WorldResident player = _world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        JsonElement gates = metadata.GetProperty("gateCells");
        for (int index = 0; index < exits.Length; index++)
        {
            var inside = new SubunitPosition(gates[index][0].GetInt64() * 256 + 128, gates[index][1].GetInt64() * 256 + 128, site.Z);
            access.Add(new GroundAccessRequirement(player.ActorId, inside, exits[index]));
        }
        return new ConstructionProject(_identities.Instance(metadata.GetProperty("projectRole").GetString()!), definition,
            _wren.ActorId, _wren.ActorId, quest.DefinitionId, quest.DestinationCustodianId, quest.MaterialReservationId,
            site, work, access, stockLocation: (ConstructionStockLocation)metadata.GetProperty("stockLocation").GetInt32());
    }

    private CountedAuthorityState AddTool(ConstructionToolAdmission tool)
    {
        CountedAuthorityState current = _world.Inventory;
        CustodianStorage previous = current.Custody.GetStorage(tool.CustodianId);
        var bag = new CustodianStorage(previous.CustodianId, checked(previous.UnitCapacity + tool.StorageCapacityIncrease),
            checked(previous.Revision + 1));
        var lot = new CountedSourceLot(tool.LotId, tool.CustodianId, tool.Source.Definition,
            new SourceInterval(tool.Source.SourceId, 0, 1), 0);
        var custody = new CustodySnapshot(_world.WorldId, _world.BranchId,
            checked(current.Custody.Revision + 1),
            current.Custody.Storages.Select(value => value.CustodianId == bag.CustodianId ? bag : value),
            current.Custody.Lots.Append(lot));
        return new CountedAuthorityState(custody, checked(current.AdmittedUnitCapacity + 1), current.Actors,
            current.Controls, current.Policies, current.Sources.Append(tool.Source), current.ReservationIds);
    }

    private static SubunitPosition Position(JsonElement value) => new(value[0].GetInt64(), value[1].GetInt64(), value[2].GetInt64());
}
