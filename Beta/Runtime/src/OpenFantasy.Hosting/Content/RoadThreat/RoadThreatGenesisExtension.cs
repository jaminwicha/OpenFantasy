using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Content.RoadThreat;

/// <summary>
/// Adds the one Bandit Chief of the retained camp spawner to an unplayed cargo genesis, before paid exchange, markets
/// and the retreat law are admitted. Every owner receives its own record: presence and body in space, an empty store,
/// owner-only policy and host control in counted custody, source statistics and level in progression, an empty wallet
/// in the currency ledger, a gathering work slot, a construction exit, a lifecycle debt row and resource history.
/// <para>
/// The lifecycle receives one ability (the source attack) and one scenario whose initiator is the chief and whose
/// target is the player, with the chief's own attack reach as entry range; every other resident is a side-zero
/// bystander, and the chief joins each retained wolf scenario as a side-zero bystander. Nothing else changes: the
/// retained scenarios keep their participants, abilities and entry ranges; their versions change only because the
/// lifecycle owner requires every resident in every scenario. A played or differently interpreted root is refused.
/// </para>
/// </summary>
internal sealed class RoadThreatGenesisExtension
{
    internal const string IdentityProfile = "road-threat-world";
    internal const string ScenarioKey = "bandit_chief_road";
    internal const string AttackKey = "bandit_chief_attack";

    /// <summary>
    /// The authored stance tile. The source spawner chose a random walkable tile centre inside its rectangle; this
    /// binding fixes one tile inside that rectangle and the admitted corridor, one cell east of the lane crossing
    /// (x=346), so the walked lane passes within the chief's actual attack reach and a lane-side bypass does not.
    /// </summary>
    internal static readonly (long X, long Y) StanceTile = (347, 311);

    private readonly WorldSnapshot _basis;
    private readonly RoadThreatSources _sources;
    private readonly HashSet<EntityId> _newIds = [];

    internal RoadThreatGenesisExtension(WorldSnapshot cargoGenesis, RoadThreatSources sources)
    {
        ArgumentNullException.ThrowIfNull(cargoGenesis);
        ArgumentNullException.ThrowIfNull(sources);
        _basis = cargoGenesis;
        _sources = sources;
    }

    internal WorldSnapshot Build()
    {
        WorldSnapshot basis = _basis;
        if (basis.ProfileVersion != 26 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.Coordination is not null
            || basis.Dialogues.Count != 0 || basis.EncounterLifecycle is not { HasEncounter: false, EncounterCount: 0 })
        {
            throw new ArgumentException("The road threat extends only an unplayed fresh cargo genesis.");
        }
        CargoTileRect spawner = _sources.Spawner.Tiles;
        if (StanceTile.X < spawner.MinimumX || StanceTile.X >= spawner.MaximumX || StanceTile.Y < spawner.MinimumY
            || StanceTile.Y >= spawner.MaximumY || !_sources.Camp.Contains(StanceTile.X, StanceTile.Y))
        {
            throw new InvalidDataException("The authored stance lies outside the retained camp spawner.");
        }
        WorldResident player = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        EntityId wren = basis.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
        ActorPresence playerPresence = basis.Space.GetActor(player.ActorId);

        // Transform: the monster record carries no anatomy; the chief uses the world's admitted humanoid body.
        BodyDefinition body = basis.Space.GetActor(wren).Condition.Definition;
        EntityId actor = Instance("chief.actor");
        ActorFormBinding binding = ActorFormBinding.Corporeal(actor, Instance("chief.form"), Instance("chief.physical-body"), body);
        SubunitPosition feet = CargoCorridor.FeetAt(new CellCoordinate(StanceTile.X, StanceTile.Y,
            playerPresence.Feet.Z / SubunitPosition.SubunitsPerCell - 1));
        EntityId home = Register(CargoMerchantPresence.PlaceIdentity(basis, _sources.Camp));
        var presence = new ActorPresence(binding, new BodyConditionSnapshot(binding.FormInstanceId, body, []),
            basis.Space.Scope, feet, 0, home);
        if (!basis.Space.Regions.Any(region => region.Bounds.Contains(presence.Feet.Cell)))
        {
            throw new InvalidDataException("The chief's stance lies outside the admitted corridor regions.");
        }
        LocalSpatialSnapshot space = basis.Space.WithActors(basis.Space.Actors.Append(presence));

        EntityId storage = Instance("chief.storage");
        var comprehension = new ActorComprehensionProfile(actor, 0, player.Comprehension.RulesVersion, []);
        var resident = new WorldResident(actor, storage, _sources.Monster.Name, ResidentControllerKind.Host, feet, 0,
            comprehension, LocalExposureDuration.Zero, null);
        var control = new ActorControlBinding(Instance("chief.principal"), Instance("chief.session"), actor);
        CountedAuthorityState inventory = ExtendInventory(actor, storage, control);
        ProgressionSnapshot progression = ExtendProgression(actor);
        GatheringSnapshot gathering = basis.Gathering!;
        gathering = new(gathering.WorldId, gathering.BranchId, gathering.AdvancingStep, gathering.Nodes, gathering.Tools,
            gathering.Actors.Append(new GatherActorWork(actor, 0)));
        ConstructionSnapshot construction = ExtendConstruction(space, actor, feet);
        IEnumerable<WorldResident> residents = basis.Residents.Append(resident).ToArray();
        EncounterLifecycleSnapshot lifecycle = ExtendLifecycle(residents, player.ActorId, actor);
        ContentHash admission = AdmissionVersion(basis.ContentHash, _sources, feet, body.Version, lifecycle);
        var history = new WorldResourceHistory(admission, new ActorResourceHistory(progression, 0, []), []);
        (LiquidReservoirState reservoirs, LiquidCellState cells) = CargoWaterRebinding.Rebind(basis, inventory, space);
        return new WorldSnapshot(inventory, space, residents, admission, basis.Calendar, 0, 0, basis.Dialogues, gathering,
            basis.Production, progression, basis.Quests, construction, reservoirs: reservoirs,
            reservoirSeals: basis.ReservoirSeals, liquidCells: cells, excavation: basis.Excavation,
            pacing: basis.Pacing, encounterLifecycle: lifecycle, care: basis.Care, resourceHistory: history);
    }

    /// <summary>The executed source attack: power, reach in subunits and the readiness ratio against the standard action.</summary>
    internal static EncounterAbilityDefinition Attack(RoadThreatSources sources) => new(
        ConstructionFarmIdentityScope.DefinitionId(IdentityProfile, "abilities/bandit-chief-attack"), AttackKey,
        "Bandit Chief attack", sources.Monster.PayloadHash, checked((uint)sources.Monster.AttackPower),
        sources.AttackRangeSubunits, checked((uint)sources.Monster.AttackAtbCost), RoadThreatSources.StandardAtbCost, 0, 1);

    private CountedAuthorityState ExtendInventory(EntityId actor, EntityId storage, ActorControlBinding control)
    {
        CountedAuthorityState original = _basis.Inventory;
        // An empty owner-only store, as for the retained wolves: no stock, drop or loot is admitted.
        var custody = new CustodySnapshot(original.WorldId, original.BranchId, original.Custody.Revision,
            original.Custody.Storages.Append(new CustodianStorage(storage, 1, 0)), original.Custody.Lots);
        return new CountedAuthorityState(custody, original.AdmittedUnitCapacity, original.Actors.Append(actor),
            original.Controls.Append(control),
            original.Policies.Append(new CustodianAccessPolicy(storage, actor, CustodyDepositRule.OwnerOnly)),
            original.Sources, original.ReservationIds);
    }

    private ProgressionSnapshot ExtendProgression(EntityId actor)
    {
        ProgressionSnapshot original = _basis.Progression!;
        RoadThreatSourceMonster monster = _sources.Monster;
        EntityId definitionId = Register(ConstructionFarmIdentityScope.DefinitionId(IdentityProfile, "profiles/bandit-chief"));
        // Source statistics are the level-7 values themselves; no growth or heritage is applied on top of them.
        var profile = new ActorStatProfileDefinition(definitionId, monster.SourceId, monster.Name,
            ActorStatProfileKind.Unassigned, monster.Stats, ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        var state = new ActorProgressionState(actor, 0, profile, original.Curves.Single(), monster.Level, 0,
            monster.Stats.MaxHp, monster.Stats.MaxMp);
        CurrencyLedger currency = original.Currency;
        // The source gil range is a retained victory reward, not a purse: the wallet starts empty.
        currency = new(currency.WorldId, currency.BranchId, currency.Revision, currency.Currency, currency.AdmittedSupply,
            currency.Accounts.Append(new CurrencyAccount(Instance("chief.wallet"), CurrencyAccountKind.Actor, actor, 0, 0)));
        return new ProgressionSnapshot(original.WorldId, original.BranchId, original.Revision, original.Profiles.Append(profile),
            original.Curves, original.Actors.Append(state), currency);
    }

    private ConstructionSnapshot ExtendConstruction(LocalSpatialSnapshot space, EntityId actor, SubunitPosition feet)
    {
        // Construction's complete actor gate requires a real grounded exit for the added occupant.
        SubunitPosition exit = feet.Offset(0, -SubunitPosition.SubunitsPerCell, 0);
        if (space.QueryGroundRoute(actor, exit).Status != GroundRouteStatus.Reachable)
        {
            throw new InvalidDataException("The chief requires a real grounded exit from its authored stance.");
        }
        ConstructionSnapshot original = _basis.Construction!;
        var projects = original.Projects.Select(project => new ConstructionProject(project.ProjectId, project.Definition,
            project.OwnerActorId, project.BuilderActorId, project.QuestId, project.ProjectCustodianId, project.ReservationId,
            project.SiteFeet, project.WorkFeet, project.RequiredAccess.Append(new GroundAccessRequirement(actor, feet, exit)),
            project.Revision, project.Progress, project.Installed, project.StockLocation));
        return new(original.WorldId, original.BranchId, original.Revision, original.Definitions, projects);
    }

    private EncounterLifecycleSnapshot ExtendLifecycle(IEnumerable<WorldResident> residents, EntityId player, EntityId chief)
    {
        EncounterLifecycleSnapshot original = _basis.EncounterLifecycle!;
        EncounterAbilityDefinition attack = Attack(_sources);
        Register(attack.DefinitionId);
        EncounterAbilityDefinition strike = original.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey);
        // The chief is a non-combatant in every retained wolf scenario; nothing else about them changes.
        List<EncounterScenarioDefinition> scenarios = original.Scenarios.Select(scenario => new EncounterScenarioDefinition(
            scenario.DefinitionId, scenario.Key, scenario.DisplayName, scenario.InitiatingActorId, scenario.TargetActorId,
            scenario.EntryRangeSubunits, scenario.Combatants.Append(new EncounterCombatant(chief, 0, [])))).ToList();
        EntityId scenarioId = Instance("scenario.bandit-chief");
        // The chief initiates against the player at its own attack reach; every other resident is a bystander.
        scenarios.Add(new EncounterScenarioDefinition(scenarioId, ScenarioKey, "Bandit Chief at the camp crossing", chief,
            player, attack.RangeSubunits, residents.Select(resident => resident.ActorId == player
                ? new EncounterCombatant(player, 1, [strike.DefinitionId])
                : resident.ActorId == chief ? new EncounterCombatant(chief, 2, [attack.DefinitionId])
                : new EncounterCombatant(resident.ActorId, 0, []))));
        return EncounterLifecycleSnapshot.BeginFresh(_basis.WorldId, _basis.BranchId, original.Seed,
            original.Abilities.Append(attack), scenarios, residents.Select(resident => resident.ActorId).Order());
    }

    /// <summary>
    /// The admission identity in canonical frames: the cargo basis hash, the source binding version, the authored
    /// stance as signed 64-bit subunit fields, the shared body definition and the complete extended lifecycle catalog.
    /// </summary>
    private static ContentHash AdmissionVersion(ContentHash basis, RoadThreatSources sources, SubunitPosition feet,
        ContentHash body, EncounterLifecycleSnapshot lifecycle) => CargoCanonicalFrame.Hash("road-threat-world.genesis-extension.v1",
        [
            basis.ToByteArray(), sources.Version.ToByteArray(), CargoCanonicalFrame.Signed64(feet.X),
            CargoCanonicalFrame.Signed64(feet.Y), CargoCanonicalFrame.Signed64(feet.Z), body.ToByteArray(),
            lifecycle.ContentHash.ToByteArray()
        ]);

    private EntityId Instance(string role) => Register(ConstructionFarmIdentityScope.InstanceId(_basis, IdentityProfile, role));

    private EntityId Register(EntityId identity)
    {
        if (_basis.IdentityIndex.Contains(identity) || !_newIds.Add(identity))
        {
            throw new InvalidDataException("A road-threat identity aliases retained authority or another new role.");
        }
        return identity;
    }
}
