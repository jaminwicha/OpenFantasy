using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Fresh company members are composed before physical owners, encounters and care bind the world.</summary>
internal sealed class ConsentedCompanyFixture
{
    internal EntityId First => Id(1);
    internal EntityId Second => Id(101);

    internal WorldSnapshot Create()
    {
        var files = FarmContentFiles.Embedded();
        var supplies = CareFarmSources.Load(files);
        var journey = EncounterJourneyAdmission.Create(files, null, EncounterJourneyWorldFixture.SeedText,
            EncounterJourneyWorldFixture.PlayerName, farm =>
            {
                var stocked = new CareFarmBuilder(farm, supplies, splitStock: true).Build();
                return AddMember(AddMember(stocked, 1, 256, "Traveling herbalist"), 101, 512, "Traveling scribe");
            });
        EntityId patient = journey.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId helper = journey.Quests!.Definitions.Single().GiverActorId;
        var potion = new CountedItemDefinition(CareJourneyAdmission.Definition("items/potion"), supplies.PotionVersion);
        var remedy = new RestorativeRemedyDefinition(Id(300), potion, supplies.RestoredHp, CareJourneyAdmission.RemedyRangeSubunits);
        var care = WorldCareAdmission.CreateFresh(journey, new CareSnapshot(journey.WorldId, journey.BranchId, 0,
            [remedy], [new(Id(301), patient, helper, remedy.DefinitionId, remedy.Version)], [], []));
        var form = care.Space.GetActor(patient).Binding;
        var supported = new RestorativeFormBinding(form.DefinitionId, form.DefinitionVersion);
        var self = new RestorativeUseDefinition(Id(302), remedy.DefinitionId, remedy.Version,
            RestorativeDeliveryMode.SelfUse, [supported]);
        var assist = new RestorativeUseDefinition(Id(303), remedy.DefinitionId, remedy.Version,
            RestorativeDeliveryMode.LivingAssistance, [supported]);
        var permission = new CarePermission(Id(304), patient, helper, assist.DefinitionId, assist.Version);
        return WorldRestorativeAdmission.CreateFresh(care, new OrdinaryCareSnapshot([self, assist], [permission], []));
    }

    private static WorldSnapshot AddMember(WorldSnapshot farm, ulong firstIdentity, long offsetY, string name)
    {
        Assert.Equal(0UL, farm.Revision);
        Assert.Equal(0UL, farm.AdvancingSteps);
        EntityId actor = Id(firstIdentity);
        EntityId storage = Id(firstIdentity + 1);
        EntityId helper = farm.Quests!.Definitions.Single().GiverActorId;
        WorldResident exemplar = farm.GetResident(helper);
        ActorPresence player = farm.Space.GetActor(farm.Residents.Single(value =>
            value.ControllerKind == ResidentControllerKind.Player).ActorId);
        BodyDefinition body = farm.Space.GetActor(helper).Condition.Definition;
        var binding = ActorFormBinding.Corporeal(actor, Id(firstIdentity + 2), Id(firstIdentity + 3), body);
        var feet = player.Feet.Offset(0, offsetY, 0);
        var presence = new ActorPresence(binding, new BodyConditionSnapshot(binding.FormInstanceId, body, []),
            farm.Space.Scope, feet, 0, player.HomePlaceId);
        var space = farm.Space.WithActors(farm.Space.Actors.Append(presence));
        var exit = feet.Offset(256, 0, 0);
        Assert.Equal(GroundRouteStatus.Reachable, space.QueryGroundRoute(actor, exit).Status);
        var resident = new WorldResident(actor, storage, name, ResidentControllerKind.Host, feet, 0,
            new ActorComprehensionProfile(actor, 0, exemplar.Comprehension.RulesVersion, []), LocalExposureDuration.Zero);
        var control = new ActorControlBinding(Id(firstIdentity + 4), Id(firstIdentity + 5), actor);
        var definition = farm.Inventory.Custody.Lots.First(lot => lot.CustodianId == exemplar.InventoryCustodianId
            && lot.Quantity > 1).Definition;
        var source = new AdmittedCountedSource(Id(firstIdentity + 6), definition, 3);
        var lot = new CountedSourceLot(Id(firstIdentity + 7), storage, definition, new(source.SourceId, 0, 3), 0);
        var custody = new CustodySnapshot(farm.WorldId, farm.BranchId, 0,
            farm.Inventory.Custody.Storages.Append(new CustodianStorage(storage, 12, 0)),
            farm.Inventory.Custody.Lots.Append(lot));
        var inventory = new CountedAuthorityState(custody, checked(farm.Inventory.AdmittedUnitCapacity + 3),
            farm.Inventory.Actors.Append(actor), farm.Inventory.Controls.Append(control),
            farm.Inventory.Policies.Append(new CustodianAccessPolicy(storage, actor, CustodyDepositRule.OwnerOnly)),
            farm.Inventory.Sources.Append(source), farm.Inventory.ReservationIds);
        var gathering = farm.Gathering!;
        gathering = new GatheringSnapshot(farm.WorldId, farm.BranchId, 0, gathering.Nodes, gathering.Tools,
            gathering.Actors.Append(new GatherActorWork(actor, 0)));
        var originalProgression = farm.Progression!;
        var profile = originalProgression.GetActor(helper);
        var currency = originalProgression.Currency;
        currency = new CurrencyLedger(farm.WorldId, farm.BranchId, 0, currency.Currency, currency.AdmittedSupply,
            currency.Accounts.Append(new CurrencyAccount(Id(firstIdentity + 8), CurrencyAccountKind.Actor, actor, 0, 0)));
        var progression = new ProgressionSnapshot(farm.WorldId, farm.BranchId, 0,
            originalProgression.Profiles, originalProgression.Curves,
            originalProgression.Actors.Append(new ActorProgressionState(actor, 0, profile.Profile, profile.Curve,
                1, 0, profile.Profile.Calculate(1).MaxHp, profile.Profile.Calculate(1).MaxMp)), currency);
        var construction = farm.Construction!;
        var projects = construction.Projects.Select(project => new ConstructionProject(project.ProjectId,
            project.Definition, project.OwnerActorId, project.BuilderActorId, project.QuestId, project.ProjectCustodianId,
            project.ReservationId, project.SiteFeet, project.WorkFeet,
            project.RequiredAccess.Append(new GroundAccessRequirement(actor, feet, exit)),
            project.Revision, project.Progress, project.Installed, project.StockLocation));
        construction = new ConstructionSnapshot(farm.WorldId, farm.BranchId, 0, construction.Definitions, projects);
        var admission = ContentHash.Compute(farm.ContentHash.ToByteArray()
            .Concat(inventory.ContentHash.ToByteArray()).Concat(progression.ContentHash.ToByteArray())
            .Concat(construction.ContentHash.ToByteArray()).Concat(binding.FormInstanceId.ToByteArray()).ToArray());
        return new WorldSnapshot(inventory, space, farm.Residents.Append(resident), admission, farm.Calendar, 0, 0,
            gathering: gathering, production: farm.Production, progression: progression, quests: farm.Quests,
            construction: construction);
    }

    private static EntityId Id(ulong value) => new(0xc028c04a00000000, value);
}
