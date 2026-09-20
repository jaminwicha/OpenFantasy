using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Composes fresh worksite rights without paying quests, fabricating lots or advancing any actor.</summary>
internal sealed class WorksiteFarmBuilder
{
    private readonly WorldSnapshot _farm;
    private readonly WorldResident _player;
    private readonly WorldResident _builder;

    internal WorksiteFarmBuilder(WorldSnapshot farm)
    {
        _farm = farm;
        _player = farm.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        _builder = farm.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host);
    }

    internal WorldSnapshot Build()
    {
        DeliveryQuestDefinition originalQuest = _farm.Quests!.Definitions.Single();
        DeliveryQuestDefinition stairQuest = CreateStairQuest(originalQuest);
        DeliveryQuestDefinition deckQuest = CreateDeckQuest(originalQuest);
        var quests = new QuestSnapshot(_farm.WorldId, _farm.BranchId, 0, [stairQuest, deckQuest], []);
        ConstructionSnapshot construction = WorksiteFarmConstruction.Create(_farm, stairQuest, deckQuest);
        ConstructionDefinition original = _farm.Construction!.Definitions.Single();
        var production = new ProductionSnapshot(_farm.WorldId, _farm.BranchId, 0,
            _farm.Production!.Recipes.Where(value => value.DefinitionId != original.MaterialTransform.DefinitionId)
                .Concat(construction.Definitions.Select(value => value.MaterialTransform)), [], []);
        IReadOnlyList<TerrainRegionSnapshot> regions = WorksiteFarmTerrain.Create(_farm);
        var space = new LocalSpatialSnapshot(_farm.Space.Scope, regions, _farm.Space.Actors);
        ExcavationSite excavation = CreateExcavation(space);
        CountedAuthorityState inventory = CreateInventory(deckQuest, excavation);
        ProgressionSnapshot progression = CreateProgression(deckQuest);
        var authored = new WorldSnapshot(inventory, space, _farm.Residents, _farm.AdmissionVersion,
            _farm.Calendar, 0, 0, gathering: _farm.Gathering, production: production,
            progression: progression, quests: quests, construction: construction);
        ContentHash admission = ContentHash.Compute(Encoding.UTF8.GetBytes("worksite-farm.admission.v1")
            .Concat(authored.ContentHash.ToByteArray()).ToArray());
        var basis = new WorldSnapshot(inventory, space, _farm.Residents, admission, _farm.Calendar, 0, 0,
            gathering: _farm.Gathering, production: production, progression: progression,
            quests: quests, construction: construction);
        WorldSnapshot physical = WorldConstructionPhysicalAdmission.CreateGenesis(WorldSteppedSupportAdmission.CreateGenesis(
            WorldConstructedSupportAdmission.CreateGenesis(WorldExcavationAdmission.CreateGenesis(basis, new([excavation])))));
        WorldSnapshot structural = WorldStructuralPolicyAdmission.CreateGenesis(physical, WorksiteFarmPolicy.Create(physical));
        WorldSnapshot settling = WorldStructuralSettlementAdmission.CreateGenesis(structural,
            new StructuralSettlementPolicy(256, 256, 256, 9800));
        WorldSnapshot continuation = WorldConsequenceAdmission.CreateGenesis(settling, new ConsequenceExecutionPolicy(16));
        return WorldEventSettlementAdmission.CreateGenesis(WorldCoupledSettlementAdmission.CreateGenesis(continuation,
            new CoupledSettlementPolicy(2)));
    }

    private DeliveryQuestDefinition CreateStairQuest(DeliveryQuestDefinition original) =>
        new(Instance("stairs.quest"), "worksite_stairway_stock", "Stairway supplies",
            "Bring six wood for the demonstration stairway and fixed work deck.",
            original.GiverActorId, original.MinimumLevel, original.Item, original.Quantity,
            original.DestinationCustodianId, original.MaterialReservationId, original.Reward);

    private DeliveryQuestDefinition CreateDeckQuest(DeliveryQuestDefinition original)
    {
        var reward = new ActorRewardDefinition(Instance("deck.reward"), "worksite_deck_reward", original.Reward.Experience,
            original.Reward.Currency, original.Reward.ReserveAccountId, original.Reward.CurrencyAmount);
        return new(Instance("deck.quest"), WorksiteFarmLayout.DeckQuestKey, "Raised work deck",
            "Bring six wood for the raised work deck. The oak can supply the additional stock.",
            _builder.ActorId, original.MinimumLevel, original.Item, 6,
            Instance("deck.custody"), Instance("deck.reservation"), reward);
    }

    private ExcavationSite CreateExcavation(LocalSpatialSnapshot space)
    {
        TerrainCell cell = space.Regions.Single(region => region.Bounds.Contains(WorksiteFarmLayout.SupportCell))
            .GetCell(WorksiteFarmLayout.SupportCell);
        CountedItemDefinition tool = _farm.Gathering!.Tools.Single(value => value.Kind == GatherToolKind.Axe).Definition;
        var physical = new MaterialDefinition(new PackageId("openfantasy.worksite-farm"), Definition("physical/substrate"),
            "worksite_substrate", new MassMilligrams(2_500_000_000), new VolumeMicrolitres(ExcavationSite.CellVolumeMicrolitres),
            new HardnessRating(4));
        return new(Instance("excavation.site"), WorksiteFarmLayout.SupportCell, cell.SourceId!.Value,
            Instance("excavation.source"), _player.ActorId, new ItemPhysicalBinding(cell.Material!, physical), tool,
            3, 0, WorksiteFarmLayout.ExcavationStance);
    }

    private CountedAuthorityState CreateInventory(DeliveryQuestDefinition quest, ExcavationSite excavation)
    {
        CountedAuthorityState old = _farm.Inventory;
        var custody = new CustodySnapshot(_farm.WorldId, _farm.BranchId, 0,
            old.Custody.Storages.Append(new CustodianStorage(quest.DestinationCustodianId, quest.Quantity, 0)), old.Custody.Lots);
        return new(custody, checked(old.AdmittedUnitCapacity + excavation.Quantity), old.Actors, old.Controls,
            old.Policies.Append(new CustodianAccessPolicy(quest.DestinationCustodianId, _builder.ActorId,
                CustodyDepositRule.AnyControlledActor)),
            old.Sources.Append(new AdmittedCountedSource(excavation.CountedSourceId, excavation.Material.Item, excavation.Quantity)),
            old.ReservationIds.Append(quest.MaterialReservationId));
    }

    private ProgressionSnapshot CreateProgression(DeliveryQuestDefinition quest)
    {
        ProgressionSnapshot old = _farm.Progression!;
        CurrencyLedger currency = old.Currency;
        var funded = new CurrencyLedger(_farm.WorldId, _farm.BranchId, 0, currency.Currency,
            checked(currency.AdmittedSupply + quest.Reward.CurrencyAmount),
            currency.Accounts.Select(account => account.AccountId == quest.Reward.ReserveAccountId
                ? new CurrencyAccount(account.AccountId, account.Kind, account.ActorId,
                    checked(account.Balance + quest.Reward.CurrencyAmount), 0) : account));
        return new(_farm.WorldId, _farm.BranchId, 0, old.Profiles, old.Curves, old.Actors, funded);
    }

    private EntityId Instance(string role) => ConstructionFarmIdentityScope.InstanceId(_farm, "worksite-farm", role);
    private static EntityId Definition(string key) => ConstructionFarmIdentityScope.DefinitionId("worksite-farm", key);
}
