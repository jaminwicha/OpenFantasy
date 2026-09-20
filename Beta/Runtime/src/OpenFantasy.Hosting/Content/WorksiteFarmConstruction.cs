using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>
/// Two demonstration assemblies with explicit geometric volume and conserved farm material mass.
/// These ratings and joinery-stock shapes are authored calibrations, not a density claim about timber.
/// </summary>
internal static class WorksiteFarmConstruction
{
    internal static ConstructionSnapshot Create(WorldSnapshot farm, DeliveryQuestDefinition stairQuest,
        DeliveryQuestDefinition deckQuest)
    {
        ConstructionDefinition previous = farm.Construction!.Definitions.Single();
        ConstructionDefinition stairs = CreateDefinition(previous, false);
        ConstructionDefinition deck = CreateDefinition(previous, true);
        EntityId builder = stairQuest.GiverActorId;
        WorldResident player = farm.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player);
        var playerAccess = new GroundAccessRequirement(player.ActorId, player.HomeFeet, WorksiteFarmLayout.PlayerParking);
        ConstructionProject[] projects =
        [
            new(Instance("stairs.project"), stairs, builder, builder, stairQuest.DefinitionId,
                stairQuest.DestinationCustodianId, stairQuest.MaterialReservationId,
                WorksiteFarmLayout.StairOrigin, WorksiteFarmLayout.StairWork,
                [new(builder, WorksiteFarmLayout.StairWork, farm.GetResident(builder).HomeFeet), playerAccess]),
            new(Instance("deck.project"), deck, builder, builder, deckQuest.DefinitionId,
                deckQuest.DestinationCustodianId, deckQuest.MaterialReservationId,
                WorksiteFarmLayout.RemovableDeckOrigin, WorksiteFarmLayout.RemovableDeckWork,
                [new(builder, WorksiteFarmLayout.RemovableDeckWork, farm.GetResident(builder).HomeFeet), playerAccess])
        ];
        return new(farm.WorldId, farm.BranchId, 0, [stairs, deck], projects);

        EntityId Instance(string role) => ConstructionFarmIdentityScope.InstanceId(farm, "worksite-farm", role);
    }

    private static ConstructionDefinition CreateDefinition(ConstructionDefinition original, bool removable)
    {
        string key = removable ? WorksiteFarmLayout.RemovableDeckDefinitionKey : WorksiteFarmLayout.StairDefinitionKey;
        MillimetreBox[] members = removable
            ? [new(0, 0, 0, 1000, 1000, 2000), new(-1125, 0, 2000, 1000, 2000, 2250)]
            : Enumerable.Range(0, 9).Select(index => new MillimetreBox(-4000 + index * 875, 0, 0,
                -4000 + (index + 1) * 875, 875, (index + 1) * 250)).Concat([
                    new MillimetreBox(2000, 875, 2000, 3875, 4000, 2250),
                    new MillimetreBox(2000, 3750, 0, 2250, 4000, 2000),
                    new MillimetreBox(3625, 3750, 0, 3875, 4000, 2000)]).ToArray();
        ItemPhysicalBinding wood = original.MaterialTransform.Bindings.Single(value => value.Physical.Key == "wood");
        ItemPhysicalBinding offcuts = original.MaterialTransform.Bindings.Single(value => value.Physical.Key == "wood_offcuts");
        ulong volume = members.Aggregate(0UL, (sum, member) => checked(sum + member.VolumeMicrolitres));
        var material = new MaterialDefinition(wood.Physical.Package, Definition("physical/" + key), key,
            new MassMilligrams(270_000), new VolumeMicrolitres(volume), new HardnessRating(1));
        var output = new ItemPhysicalBinding(new CountedItemDefinition(Definition("items/" + key), material.Version), material);
        var recipe = new CraftRecipeDefinition(Definition("recipes/" + key),
            new MaterialRecipeDefinition(wood.Physical.Package, Definition("physical-recipes/" + key), "assemble_" + key,
                new WorkUnits(1), null, new MaterialCatalog([wood.Physical, offcuts.Physical, material]),
                [new(RecipeMaterialRole.Input, wood.Physical.Pin(), 6), new(RecipeMaterialRole.Output, material.Pin(), 1),
                    new(RecipeMaterialRole.Waste, offcuts.Physical.Pin(), 3)]),
            [wood, offcuts, output], [new(wood.Item, 0, new MassMilligrams(270_000)), new(wood.Item, 1, new MassMilligrams(10_000))]);
        return new(Definition("construction/" + key), key,
            removable ? "Demonstration raised work deck" : "Demonstration stairs and fixed deck",
            recipe, 0, original.RequiredTool, members, original.RequiredWork, original.WorkRate);
    }

    private static EntityId Definition(string key) => ConstructionFarmIdentityScope.DefinitionId("worksite-farm", key);
}
