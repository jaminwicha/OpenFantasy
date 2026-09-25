using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Adds one authored living quadruped without granting stock, currency, labor or past exposure.</summary>
internal sealed class EncounterFarmWolf(WorldSnapshot farm, EncounterFarmSources source,
    string instanceRole = "wolf", long offsetX = 256)
{
    private readonly HashSet<EntityId> _newIds = [];

    internal WorldSnapshot AddToFreshFarm()
    {
        WorldResident player = farm.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        source.ValidatePlayer(farm.Progression!.GetActor(player.ActorId));
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceRole);
        if (offsetX is not (-256 or 256)) throw new ArgumentOutOfRangeException(nameof(offsetX));
        EntityId actor = Instance("actor");
        BodyDefinition body = CreateBody();
        ActorFormBinding binding = ActorFormBinding.Corporeal(actor, Instance("form"), Instance("physical-body"), body);
        var condition = new BodyConditionSnapshot(binding.FormInstanceId, body, []);
        ActorPresence playerPresence = farm.Space.GetActor(player.ActorId);
        var feet = new SubunitPosition(checked(playerPresence.Feet.X + offsetX), playerPresence.Feet.Y, playerPresence.Feet.Z);
        var presence = new ActorPresence(binding, condition, farm.Space.Scope, feet, 0, playerPresence.HomePlaceId);
        LocalSpatialSnapshot space = farm.Space.WithActors(farm.Space.Actors.Append(presence));
        if (space.QueryInteraction(player.ActorId, actor, 448) != SpatialInteraction.Reachable
            || space.QueryInteraction(actor, player.ActorId, 352) != SpatialInteraction.Reachable)
        {
            throw new InvalidDataException("The authored wolf stance must be clear, grounded and within both actual melee sightlines.");
        }
        EntityId storage = Instance("storage");
        // The world shares one comprehension law; an empty record grants no language knowledge.
        var comprehension = new ActorComprehensionProfile(actor, 0, player.Comprehension.RulesVersion, []);
        var resident = new WorldResident(actor, storage, source.Wolf.GetProperty("name").GetString()!,
            ResidentControllerKind.Host, feet, 0, comprehension, LocalExposureDuration.Zero, null);
        var control = new ActorControlBinding(Instance("principal"), Instance("session"), actor);
        var custody = new CustodySnapshot(farm.WorldId, farm.BranchId, farm.Inventory.Custody.Revision,
            farm.Inventory.Custody.Storages.Append(new CustodianStorage(storage, 1, 0)), farm.Inventory.Custody.Lots);
        var inventory = new CountedAuthorityState(custody, farm.Inventory.AdmittedUnitCapacity,
            farm.Inventory.Actors.Append(actor), farm.Inventory.Controls.Append(control),
            farm.Inventory.Policies.Append(new CustodianAccessPolicy(storage, actor, CustodyDepositRule.OwnerOnly)),
            farm.Inventory.Sources, farm.Inventory.ReservationIds);
        GatheringSnapshot gathering = farm.Gathering!;
        gathering = new(gathering.WorldId, gathering.BranchId, gathering.AdvancingStep, gathering.Nodes, gathering.Tools,
            gathering.Actors.Append(new GatherActorWork(actor, 0)));
        ProgressionSnapshot progression = AddProgression(actor);
        ConstructionSnapshot construction = AddConstructionAccess(space, actor, feet);
        ContentHash admission = ContentHash.Compute(Encoding.UTF8.GetBytes("encounter-farm.quadruped-admission.v1")
            .Concat(farm.ContentHash.ToByteArray()).Concat(source.Version.ToByteArray()).Concat(body.Version.ToByteArray()).ToArray());
        return new WorldSnapshot(inventory, space, farm.Residents.Append(resident), admission, farm.Calendar, 0, 0,
            gathering: gathering, production: farm.Production, progression: progression, quests: farm.Quests,
            construction: construction);
    }

    private ConstructionSnapshot AddConstructionAccess(LocalSpatialSnapshot space, EntityId actor, SubunitPosition feet)
    {
        // Construction's complete actor gate requires an explicit exit for the added occupant.
        // Existing witnesses and every job/material field remain; this is fresh admission only.
        SubunitPosition exit = feet.Offset(0, -256, 0);
        if (space.QueryGroundRoute(actor, exit).Status != GroundRouteStatus.Reachable)
        {
            throw new InvalidDataException("The wolf requires a real grounded exit from its authored starting stance.");
        }
        ConstructionSnapshot original = farm.Construction!;
        var projects = original.Projects.Select(project => new ConstructionProject(project.ProjectId, project.Definition,
            project.OwnerActorId, project.BuilderActorId, project.QuestId, project.ProjectCustodianId, project.ReservationId,
            project.SiteFeet, project.WorkFeet, project.RequiredAccess.Append(new GroundAccessRequirement(actor, feet, exit)),
            project.Revision, project.Progress, project.Installed, project.StockLocation));
        return new(original.WorldId, original.BranchId, original.Revision, original.Definitions, projects);
    }

    private ProgressionSnapshot AddProgression(EntityId actor)
    {
        ProgressionSnapshot original = farm.Progression!;
        ActorStatValues stats = EncounterFarmSources.Stats(source.Wolf.GetProperty("stats"));
        EntityId definitionId = DefinitionIdentity("profiles/bramble-wolf");
        var candidate = new ActorStatProfileDefinition(definitionId, "bramble_wolf", "Bramble Wolf",
            ActorStatProfileKind.Unassigned, stats, ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        ActorStatProfileDefinition? retained = original.Profiles.SingleOrDefault(value => value.DefinitionId == definitionId);
        if (retained is not null && retained.Version != candidate.Version)
        {
            throw new InvalidDataException("The retained wolf statistic definition differs from its source-backed calibration.");
        }
        if (retained is null) Register(definitionId);
        ActorStatProfileDefinition profile = retained ?? candidate;
        var state = new ActorProgressionState(actor, 0, profile, original.Curves.Single(),
            source.Wolf.GetProperty("level").GetInt32(), 0, stats.MaxHp, stats.MaxMp);
        CurrencyLedger currency = original.Currency;
        currency = new(currency.WorldId, currency.BranchId, currency.Revision, currency.Currency, currency.AdmittedSupply,
            currency.Accounts.Append(new CurrencyAccount(Instance("wallet"), CurrencyAccountKind.Actor, actor, 0, 0)));
        return new(original.WorldId, original.BranchId, original.Revision,
            retained is null ? original.Profiles.Append(profile) : original.Profiles,
            original.Curves, original.Actors.Append(state), currency);
    }

    private BodyDefinition CreateBody()
    {
        // Newly authored anatomy calibration, not source sprite dimensions: four legs/paws,
        // independent eyes/ears and a jaw/teeth weapon. There are no hands or manipulation grants.
        List<BodyPartDefinition> parts =
        [
            Part("torso", null, new(192, 112, 112)),
            Part("head", "torso", new(64, 72, 64)),
            Part("jaw", "head", new(48, 56, 24), BodyFunction.Vocalization),
            Part("teeth", "jaw", new(40, 40, 12), BodyFunction.NaturalWeapon),
            Part("tail", "torso", new(80, 20, 20)),
            Part("left-eye", "head", new(12, 12, 12), BodyFunction.Sight),
            Part("right-eye", "head", new(12, 12, 12), BodyFunction.Sight),
            Part("left-ear", "head", new(24, 16, 32), BodyFunction.Hearing),
            Part("right-ear", "head", new(24, 16, 32), BodyFunction.Hearing)
        ];
        foreach (string limb in new[] { "front-left", "front-right", "hind-left", "hind-right" })
        {
            parts.Add(Part(limb + "-leg", "torso", new(24, 24, 96)));
            parts.Add(Part(limb + "-paw", limb + "-leg", new(32, 40, 24), BodyFunction.GroundContact));
        }
        EntityId definitionId = DefinitionIdentity("bodies/bramble-wolf-quadruped");
        var candidate = new BodyDefinition(definitionId, BodyNature.Corporeal,
            new BodyDimensions(192, 128, 192), parts, [new LocomotionRequirement(LocomotionMode.Ground, 3)]);
        BodyDefinition? retained = farm.Space.Actors.Select(value => value.Condition.Definition)
            .FirstOrDefault(value => value.DefinitionId == definitionId);
        if (retained is not null)
        {
            if (retained.Version != candidate.Version)
            {
                throw new InvalidDataException("The retained wolf anatomy differs from its admitted quadruped calibration.");
            }
            return retained;
        }
        Register(definitionId);
        return candidate;
    }

    private static BodyPartDefinition Part(string slot, string? parent, BodyDimensions dimensions,
        params BodyFunction[] functions) => new(new BodySlotKey(slot), parent is null ? null : new BodySlotKey(parent), dimensions, functions);

    private EntityId Instance(string role) => Register(ConstructionFarmIdentityScope.InstanceId(farm, "encounter-farm", instanceRole + "." + role));
    private static EntityId DefinitionIdentity(string key) => ConstructionFarmIdentityScope.DefinitionId("encounter-farm", key);

    private EntityId Register(EntityId identity)
    {
        if (farm.IdentityIndex.Contains(identity) || !_newIds.Add(identity))
        {
            throw new InvalidDataException("An encounter actor identity aliases retained authority or another new role.");
        }
        return identity;
    }
}
