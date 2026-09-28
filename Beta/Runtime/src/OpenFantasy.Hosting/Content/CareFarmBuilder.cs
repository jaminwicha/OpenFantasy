using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Composes finite fresh custody and nearby helper placement before geography binds the complete world.</summary>
internal sealed class CareFarmBuilder(WorldSnapshot farm, CareFarmSources sources)
{
    private readonly HashSet<EntityId> _newIdentities = [];

    internal WorldSnapshot Build()
    {
        if (farm.Revision != 0 || farm.AdvancingSteps != 0 || farm.Production is not { Revision: 0 } production
            || production.Batches.Count != 0 || production.Lots.Count != 0 || production.SupportsConsumption)
        {
            throw new ArgumentException("Care supplies require fresh unearned farm composition.");
        }
        WorldResident player = farm.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        EntityId helperId = farm.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
        WorldResident helper = farm.GetResident(helperId);
        if (helper.ControllerKind != ResidentControllerKind.Host
            || !farm.Inventory.Controls.Any(value => value.ActorId == helperId))
        {
            throw new InvalidDataException("The care helper must retain an actual host control binding.");
        }
        CountedItemDefinition berries = farm.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Berry)
            .Definition.YieldDefinition;
        ItemPhysicalBinding berryBinding = production.Recipes.SelectMany(value => value.Bindings)
            .First(value => value.Item == berries);
        var potion = new CountedItemDefinition(Definition("items/potion"), sources.PotionVersion);
        CraftRecipeDefinition recipe = CreateRecipe(berryBinding, potion);
        var inventory = AddStock(helper.InventoryCustodianId, berries, potion);
        var completeProduction = new ProductionSnapshot(farm.WorldId, farm.BranchId, 0,
            production.Recipes.Append(recipe), [], []);
        completeProduction.ValidateForAuthority(inventory);
        LocalSpatialSnapshot space = PlaceHelper(player, helper);
        var residents = farm.Residents.Select(value => value.ActorId == helperId
            ? new WorldResident(value.ActorId, value.InventoryCustodianId, value.DisplayName, value.ControllerKind,
                space.GetActor(helperId).Feet, 0, value.Comprehension, value.ExperiencedTime, value.OpeningUtterance)
            : value).ToArray();
        WorldSnapshot Root(ContentHash admission) => new(inventory, space, residents, admission, farm.Calendar, 0, 0,
            gathering: farm.Gathering, production: completeProduction, progression: farm.Progression,
            quests: farm.Quests, construction: farm.Construction);
        WorldSnapshot authored = Root(farm.AdmissionVersion);
        ContentHash version = ContentHash.Compute(Encoding.ASCII.GetBytes("care-farm.admission.v1")
            .Concat(farm.AdmissionVersion.ToByteArray()).Concat(sources.Version.ToByteArray())
            .Concat(authored.ContentHash.ToByteArray()).ToArray());
        return Root(version);
    }

    private CraftRecipeDefinition CreateRecipe(ItemPhysicalBinding berries, CountedItemDefinition potion)
    {
        var package = new PackageId("openfantasy.care-farm");
        // The original recipe has no physical quantities. Its explicit calibration retains all
        // three admitted berry units' mass and volume in one dose, with no invented vessel stock.
        var physicalPotion = new MaterialDefinition(package, Definition("physical/potion"), "potion",
            berries.Physical.MassPerUnit.Multiply(sources.BerryCount),
            berries.Physical.VolumePerUnit.Multiply(sources.BerryCount), new HardnessRating(0));
        var physical = new MaterialRecipeDefinition(package, Definition("physical-recipes/craft-potion"),
            CareJourneyAdmission.RecipeKey, new WorkUnits(1), sources.RecipeVersion,
            new MaterialCatalog([berries.Physical, physicalPotion]),
            [new(RecipeMaterialRole.Input, berries.Physical.Pin(), sources.BerryCount),
                new(RecipeMaterialRole.Output, physicalPotion.Pin(), sources.OutputCount)]);
        return new CraftRecipeDefinition(Definition("recipes/craft-potion"), physical,
            [berries, new(potion, physicalPotion)], [new(berries.Item, 0, physicalPotion.MassPerUnit)]);
    }

    private CountedAuthorityState AddStock(EntityId storage, CountedItemDefinition berries, CountedItemDefinition potion)
    {
        ulong addedCapacity = CareJourneyAdmission.InitialBerryQuantity + CareJourneyAdmission.InitialPotionQuantity;
        EntityId berrySource = Instance("supplied-berries.source"), potionSource = Instance("supplied-potion.source");
        CountedSourceLot[] added =
        [
            new(Instance("supplied-berries.lot"), storage, berries, new(berrySource, 0, CareJourneyAdmission.InitialBerryQuantity), 0),
            new(Instance("supplied-potion.lot"), storage, potion, new(potionSource, 0, CareJourneyAdmission.InitialPotionQuantity), 0)
        ];
        var old = farm.Inventory;
        var custody = new CustodySnapshot(farm.WorldId, farm.BranchId, 0,
            old.Custody.Storages.Select(value => value.CustodianId == storage
                ? new CustodianStorage(storage, checked(value.UnitCapacity + addedCapacity), value.Revision) : value),
            old.Custody.Lots.Concat(added));
        return new(custody, checked(old.AdmittedUnitCapacity + addedCapacity), old.Actors, old.Controls, old.Policies,
            old.Sources.Concat([new(berrySource, berries, CareJourneyAdmission.InitialBerryQuantity),
                new(potionSource, potion, CareJourneyAdmission.InitialPotionQuantity)]), old.ReservationIds);
    }

    private LocalSpatialSnapshot PlaceHelper(WorldResident player, WorldResident helper)
    {
        ActorPresence prior = farm.Space.GetActor(helper.ActorId);
        SubunitPosition feet = farm.Space.GetActor(player.ActorId).Feet.Offset(0, -256, 0);
        if (farm.Space.QueryOccupancy(helper.ActorId, feet) != SpatialOccupancy.Clear)
        {
            throw new InvalidDataException("The fresh care helper requires clear supported nearby ground.");
        }
        var presence = new ActorPresence(prior.Binding, prior.Condition, prior.Scope, feet, 0, prior.HomePlaceId);
        LocalSpatialSnapshot space = farm.Space.WithActors(farm.Space.Actors.Select(value => value.ActorId == helper.ActorId ? presence : value));
        if (space.QueryInteraction(helper.ActorId, player.ActorId, checked((long)CareJourneyAdmission.RemedyRangeSubunits))
            != SpatialInteraction.Reachable)
        {
            throw new InvalidDataException("The fresh care helper must be in actual remedy reach.");
        }
        return space;
    }

    private EntityId Definition(string key) => Register(CareJourneyAdmission.Definition(key));
    private EntityId Instance(string role) => Register(CareJourneyAdmission.Instance(farm, role));
    private EntityId Register(EntityId identity)
    {
        if (farm.IdentityIndex.Contains(identity) || !_newIdentities.Add(identity))
        {
            throw new InvalidDataException("A care supply identity collides with retained content or another new role.");
        }
        return identity;
    }
}
