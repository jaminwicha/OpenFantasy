using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Fresh finite care supplies and prior permission in the source-backed encounter journey.</summary>
public static class CareJourneyAdmission
{
    public const string RecipeKey = "craft_potion";
    public const ulong RemedyRangeSubunits = 256;
    public const ulong InitialPotionQuantity = 2;
    public const ulong InitialBerryQuantity = 6;

    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        Create(FarmContentFiles.Embedded(), null, seedText, playerName);

    public static WorldSnapshot Create(string contentRoot, string seedText, string playerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        return Create(new FarmContentFiles(contentRoot), contentRoot, seedText, playerName);
    }

    internal static WorldSnapshot CreateOrdinaryBasis(FarmContentFiles files, string? contentRoot, string seedText, string playerName) =>
        Create(files, contentRoot, seedText, playerName, splitStock: true);

    private static WorldSnapshot Create(FarmContentFiles files, string? contentRoot, string seedText, string playerName, bool splitStock = false)
    {
        CareFarmSources sources = CareFarmSources.Load(files);
        WorldSnapshot journey = EncounterJourneyAdmission.Create(files, contentRoot, seedText, playerName,
            farm => new CareFarmBuilder(farm, sources, splitStock).Build());
        WorldResident patient = journey.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        EntityId helper = journey.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
        EntityId remedyId = Definition("remedies/potion");
        EntityId agreementId = Instance(journey, "care.agreement");
        if (remedyId == agreementId || journey.IdentityIndex.Contains(remedyId) || journey.IdentityIndex.Contains(agreementId))
        {
            throw new InvalidDataException("Care permission identities collide with admitted content.");
        }
        var item = new CountedItemDefinition(Definition("items/potion"), sources.PotionVersion);
        var remedy = new RestorativeRemedyDefinition(remedyId, item, sources.RestoredHp, RemedyRangeSubunits);
        var care = new CareSnapshot(journey.WorldId, journey.BranchId, 0, [remedy],
            [new(agreementId, patient.ActorId, helper, remedyId, remedy.Version)], [], []);
        return WorldCareAdmission.CreateFresh(journey, care);
    }

    internal static EntityId Definition(string key) => ConstructionFarmIdentityScope.DefinitionId("care-farm", key);
    internal static EntityId Instance(WorldSnapshot world, string role) => ConstructionFarmIdentityScope.InstanceId(world, "care-farm", role);
}
