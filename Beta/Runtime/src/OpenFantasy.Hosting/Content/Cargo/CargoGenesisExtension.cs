using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>
/// Extends an unplayed fresh ordinary-care genesis with the corridor terrain and the two retained merchants. Every
/// owner keeps its own record: presence and terrain in space, store, policy, control and opening stock in counted
/// custody, purse in the currency ledger, stat state in progression, work slots in gathering, exits in construction
/// and a non-combatant readiness debt in the encounter lifecycle. Nothing is copied from Wren except the shared
/// humanoid anatomy and NPC stat profile, both named explicitly. A played or differently interpreted root is refused.
/// </summary>
internal sealed class CargoGenesisExtension
{
    private const string IdentityProfile = "cargo-world";
    internal const string RationsRecipeKey = "craft_rations";
    private readonly WorldSnapshot _basis;
    private readonly CargoCircuitSources _sources;
    private readonly CargoMerchantProvision _originProvision;
    private readonly CargoMerchantProvision _destinationProvision;
    private readonly HashSet<EntityId> _newIds = [];

    internal CargoGenesisExtension(WorldSnapshot ordinaryCareGenesis, CargoCircuitSources sources)
        : this(ordinaryCareGenesis, sources, CargoCircuitPolicy.BrasshollowProvision, CargoCircuitPolicy.GearfordProvision)
    {
    }

    /// <summary>The same extension with explicit provisions; each must name its own retained merchant.</summary>
    internal CargoGenesisExtension(WorldSnapshot ordinaryCareGenesis, CargoCircuitSources sources,
        CargoMerchantProvision originProvision, CargoMerchantProvision destinationProvision)
    {
        ArgumentNullException.ThrowIfNull(ordinaryCareGenesis);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(originProvision);
        ArgumentNullException.ThrowIfNull(destinationProvision);
        _basis = ordinaryCareGenesis;
        _sources = sources;
        _originProvision = originProvision;
        _destinationProvision = destinationProvision;
    }

    internal WorldSnapshot Build()
    {
        WorldSnapshot basis = _basis;
        if (basis.ProfileVersion != 26 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.Coordination is not null
            || basis.Dialogues.Count != 0 || basis.EncounterLifecycle is not { HasEncounter: false })
        {
            throw new ArgumentException("The cargo circuit extends only an unplayed fresh ordinary-care genesis.");
        }
        WorldResident player = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        EntityId wren = basis.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
        ActorPresence playerPresence = basis.Space.GetActor(player.ActorId);
        TerrainRegionSnapshot farm = basis.Space.Regions.Single(region => region.Bounds.Contains(playerPresence.Feet.Cell));
        CargoCorridor corridor = CargoCorridor.CreateCircuit(farm.Bounds, _sources);
        CountedItemDefinition rations = RationDefinition(basis);
        Merchant[] merchants =
        [
            CreateMerchant(_sources.Origin, _originProvision, wren, player, playerPresence.Feet.Z, rations),
            CreateMerchant(_sources.Destination, _destinationProvision, wren, player, playerPresence.Feet.Z, rations)
        ];
        LocalSpatialSnapshot space = new(basis.Space.Scope, corridor.GenerateTerrain(basis.Space.Regions),
            basis.Space.Actors.Concat(merchants.Select(merchant => merchant.Presence)), basis.Space.Structures,
            basis.Space.GroundSupport, basis.Space.StructureSupport);
        CountedAuthorityState inventory = ExtendInventory(merchants);
        ProgressionSnapshot progression = ExtendProgression(merchants);
        GatheringSnapshot gathering = basis.Gathering!;
        gathering = new(gathering.WorldId, gathering.BranchId, gathering.AdvancingStep, gathering.Nodes, gathering.Tools,
            gathering.Actors.Concat(merchants.Select(merchant => new GatherActorWork(merchant.ActorId, 0))));
        ConstructionSnapshot construction = ExtendConstruction(space, merchants);
        IEnumerable<WorldResident> residents = basis.Residents.Concat(merchants.Select(merchant => merchant.Resident));
        EncounterLifecycleSnapshot lifecycle = ExtendLifecycle(residents, merchants);
        ContentHash admission = AdmissionVersion(_basis.ContentHash, corridor.AddedRegions,
            merchants.Select(merchant => (merchant.Source, merchant.Provision)), _sources.SharedGood,
            CargoCorridorOverlays.VersionOf(corridor.Overlays));
        var history = new WorldResourceHistory(admission, new ActorResourceHistory(progression, 0, []), []);
        (LiquidReservoirState reservoirs, LiquidCellState cells) = RebindWater(inventory, space);
        return new WorldSnapshot(inventory, space, residents, admission, basis.Calendar, 0, 0, basis.Dialogues, gathering,
            basis.Production, progression, basis.Quests, construction, reservoirs: reservoirs,
            reservoirSeals: basis.ReservoirSeals, liquidCells: cells, excavation: basis.Excavation,
            pacing: basis.Pacing, encounterLifecycle: lifecycle, care: basis.Care, resourceHistory: history);
    }

    /// <summary>
    /// The water owners pin the exact counted admission and terrain, so custody-only reconciliation cannot carry added
    /// residents or sources. The retained dry capacity domain is therefore prepared again, unchanged, against the
    /// extended inventory: the same network, terrain mapping, owner, material, terminals, closed budget identity and
    /// reservoir geometry, with a zero-length dry forcing interval that is cancelled exactly as the farm's own
    /// admission does. The placed cells (quantities, portions, profile, boundary and seals) are kept and rebound to
    /// those reservoirs and to the extended regions.
    /// <para>
    /// The closed budget identity is read from the retained capacity state, not re-derived from a profile name. The
    /// re-preparation is lossless only for the fresh dry genesis, so that assumption is checked, not trusted: the
    /// same preparation against the retained pins must reproduce the retained capacity and reservoir hashes exactly.
    /// Any other retained water state (pending runoff, received totals, a second closed budget or lived reservoir
    /// state) is refused rather than reset.
    /// </para>
    /// </summary>
    private (LiquidReservoirState Reservoirs, LiquidCellState Cells) RebindWater(CountedAuthorityState inventory,
        LocalSpatialSnapshot space)
    {
        LiquidReservoirState retained = _basis.Reservoirs!;
        WatershedRunoffCapacityState retainedCapacity = retained.Capacity;
        if (retainedCapacity.Pending.Count != 0 || retainedCapacity.Closed.Count != 1)
        {
            throw new InvalidDataException("The cargo extension supports only the fresh dry water genesis with one closed budget.");
        }
        EntityId budget = retainedCapacity.Closed[0];
        if (PrepareDry(retainedCapacity.Inventory, retainedCapacity.Production, budget).ContentHash != retainedCapacity.ContentHash
            || LiquidReservoirState.Create(retainedCapacity, retained.Geometries).ContentHash != retained.ContentHash)
        {
            throw new InvalidDataException("The retained water owners carry state beyond the fresh dry genesis; it is not reset.");
        }
        WatershedRunoffCapacityState capacity = PrepareDry(inventory, _basis.Production, budget);
        LiquidReservoirState reservoirs = LiquidReservoirState.Create(capacity, retained.Geometries);
        LiquidCellState cells = _basis.LiquidCells!;
        cells = cells.Topology is not null
            ? cells.RebindTerrain(reservoirs, TerrainLiquidTopology.Create(reservoirs, space.Regions, _basis.ReservoirSeals!))
            : cells.Rebind(reservoirs);
        return (reservoirs, cells);
    }

    /// <summary>The retained domain prepared with a zero-length dry interval and cancelled, as the farm admission does.</summary>
    private WatershedRunoffCapacityState PrepareDry(CountedAuthorityState inventory, ProductionSnapshot? production, EntityId budget)
    {
        WatershedRunoffDomain domain = _basis.Reservoirs!.Capacity.Domain;
        var request = new WatershedRunoffRequest(domain.Network, domain.Mapping, inventory, production, budget,
            domain.OwnerActorId, _basis.Calendar, _basis.Calendar.AddMilliseconds(1), domain.Material,
            domain.Terminals.Select(terminal => WatershedRunoffInput.Dry(terminal.BasinId)), domain.Terminals);
        WatershedRunoffCapacityState capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request));
        return capacity.PlanCancel(budget).ApplyTo(capacity);
    }

    /// <summary>The farm's own produced rations definition, the shared good both retained shop lists name.</summary>
    internal static CountedItemDefinition RationDefinition(WorldSnapshot world)
    {
        CraftRecipeDefinition recipe = world.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == RationsRecipeKey);
        return recipe.ItemFor(recipe.Outputs[0]);
    }

    private Merchant CreateMerchant(CargoSourceMerchant source, CargoMerchantProvision provision, EntityId bodySource,
        WorldResident player, long feetZ, CountedItemDefinition rations)
    {
        if (provision.MerchantSourceId != source.SourceId)
        {
            throw new InvalidDataException("A provision names a different retained merchant.");
        }
        CargoMerchantPresence presence = CargoMerchantPresence.AtSourceTile(_basis, source, bodySource, feetZ);
        EntityId actor = Register(presence.ActorId);
        Register(presence.Presence.Binding.FormInstanceId);
        Register(presence.Presence.Binding.PhysicalBodyId!.Value);
        Register(presence.Presence.HomePlaceId);
        EntityId storage = Instance(source.SourceId + ".storage");
        var comprehension = new ActorComprehensionProfile(actor, 0, player.Comprehension.RulesVersion, []);
        // Transform: the source wander radius is not admitted; each merchant keeps its own counter tile.
        var resident = new WorldResident(actor, storage, source.Name, ResidentControllerKind.Host, presence.Presence.Feet, 0,
            comprehension, LocalExposureDuration.Zero, null);
        CountedSourceLot? lot = null;
        AdmittedCountedSource? opening = null;
        if (provision.OpeningUnits > 0)
        {
            EntityId sourceId = Instance(source.SourceId + ".opening-stock-source");
            opening = new AdmittedCountedSource(sourceId, rations, provision.OpeningUnits);
            lot = new CountedSourceLot(Instance(source.SourceId + ".opening-stock-lot"), storage, rations,
                new SourceInterval(sourceId, 0, provision.OpeningUnits), 0);
        }
        return new Merchant(source, provision, presence.Presence, resident,
            new CustodianStorage(storage, provision.StorageCapacity, 0),
            new ActorControlBinding(Instance(source.SourceId + ".principal"), Instance(source.SourceId + ".session"), actor),
            lot, opening, Instance(source.SourceId + ".wallet"));
    }

    private CountedAuthorityState ExtendInventory(IReadOnlyList<Merchant> merchants)
    {
        CountedAuthorityState original = _basis.Inventory;
        // Each merchant's store follows Wren's admitted store deposit rule, so paid settlement can deliver into it.
        WorldResident wren = _basis.GetResident(_basis.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single());
        CustodyDepositRule rule = original.Policies.Single(value => value.CustodianId == wren.InventoryCustodianId).DepositRule;
        ulong addedUnits = merchants.Aggregate(0UL, (sum, merchant) => checked(sum + (merchant.Opening?.UnitCapacity ?? 0)));
        var custody = new CustodySnapshot(original.WorldId, original.BranchId, original.Custody.Revision,
            original.Custody.Storages.Concat(merchants.Select(merchant => merchant.Storage)),
            original.Custody.Lots.Concat(merchants.Where(merchant => merchant.Lot is not null).Select(merchant => merchant.Lot!)));
        return new CountedAuthorityState(custody, checked(original.AdmittedUnitCapacity + addedUnits),
            original.Actors.Concat(merchants.Select(merchant => merchant.ActorId)),
            original.Controls.Concat(merchants.Select(merchant => merchant.Control)),
            original.Policies.Concat(merchants.Select(merchant =>
                new CustodianAccessPolicy(merchant.Storage.CustodianId, merchant.ActorId, rule))),
            original.Sources.Concat(merchants.Where(merchant => merchant.Opening is not null).Select(merchant => merchant.Opening!)),
            original.ReservationIds);
    }

    private ProgressionSnapshot ExtendProgression(IReadOnlyList<Merchant> merchants)
    {
        ProgressionSnapshot original = _basis.Progression!;
        // Transform: the retained merchant records carry no stats; both share Wren's admitted NPC stat profile and curve.
        ActorProgressionState shared = original.GetActor(_basis.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single());
        CurrencyLedger currency = original.Currency;
        ulong purses = merchants.Aggregate(0UL, (sum, merchant) => checked(sum + merchant.Provision.PurseGil));
        currency = new(currency.WorldId, currency.BranchId, currency.Revision, currency.Currency,
            checked(currency.AdmittedSupply + purses), currency.Accounts.Concat(merchants.Select(merchant =>
                new CurrencyAccount(merchant.AccountId, CurrencyAccountKind.Actor, merchant.ActorId, merchant.Provision.PurseGil, 0))));
        return new ProgressionSnapshot(original.WorldId, original.BranchId, original.Revision, original.Profiles, original.Curves,
            original.Actors.Concat(merchants.Select(merchant => new ActorProgressionState(merchant.ActorId, 0, shared.Profile,
                shared.Curve, shared.Level, 0, shared.Stats.MaxHp, shared.Stats.MaxMp))), currency);
    }

    private ConstructionSnapshot ExtendConstruction(LocalSpatialSnapshot space, IReadOnlyList<Merchant> merchants)
    {
        // Construction's complete actor gate requires a real grounded exit for each added occupant.
        List<GroundAccessRequirement> access = [];
        foreach (Merchant merchant in merchants)
        {
            SubunitPosition exit = merchant.Presence.Feet.Offset(0, -SubunitPosition.SubunitsPerCell, 0);
            if (space.QueryGroundRoute(merchant.ActorId, exit).Status != GroundRouteStatus.Reachable)
            {
                throw new InvalidDataException("A merchant requires a real grounded exit from its retained tile.");
            }
            access.Add(new GroundAccessRequirement(merchant.ActorId, merchant.Presence.Feet, exit));
        }
        ConstructionSnapshot original = _basis.Construction!;
        var projects = original.Projects.Select(project => new ConstructionProject(project.ProjectId, project.Definition,
            project.OwnerActorId, project.BuilderActorId, project.QuestId, project.ProjectCustodianId, project.ReservationId,
            project.SiteFeet, project.WorkFeet, project.RequiredAccess.Concat(access), project.Revision, project.Progress,
            project.Installed, project.StockLocation));
        return new(original.WorldId, original.BranchId, original.Revision, original.Definitions, projects);
    }

    private EncounterLifecycleSnapshot ExtendLifecycle(IEnumerable<WorldResident> residents, IReadOnlyList<Merchant> merchants)
    {
        // The merchants join every retained scenario as non-combatants (side zero, no abilities); no hostility is granted.
        EncounterLifecycleSnapshot original = _basis.EncounterLifecycle!;
        IEnumerable<EncounterScenarioDefinition> scenarios = original.Scenarios.Select(scenario => new EncounterScenarioDefinition(
            scenario.DefinitionId, scenario.Key, scenario.DisplayName, scenario.InitiatingActorId, scenario.TargetActorId,
            scenario.EntryRangeSubunits, scenario.Combatants.Concat(merchants.Select(merchant =>
                new EncounterCombatant(merchant.ActorId, 0, []))))).ToArray();
        return EncounterLifecycleSnapshot.BeginFresh(_basis.WorldId, _basis.BranchId, original.Seed, original.Abilities, scenarios,
            residents.Select(resident => resident.ActorId).Order());
    }

    /// <summary>
    /// The admission identity in canonical frames: the basis hash, every added region as explicit signed 64-bit
    /// minimum and extent fields, each merchant's source identity, record, place and provision hashes, the shared
    /// good and the declared overlay version. No culture-dependent or compiler-generated text is hashed.
    /// </summary>
    internal static ContentHash AdmissionVersion(ContentHash basis, IEnumerable<TerrainRegionBounds> added,
        IEnumerable<(CargoSourceMerchant Source, CargoMerchantProvision Provision)> merchants, CargoSourceGood good,
        ContentHash overlays)
    {
        List<byte[]> fields = [basis.ToByteArray()];
        foreach (TerrainRegionBounds bounds in added)
        {
            fields.AddRange([CargoCanonicalFrame.Signed64(bounds.Minimum.X), CargoCanonicalFrame.Signed64(bounds.Minimum.Y),
                CargoCanonicalFrame.Signed64(bounds.Minimum.Z), CargoCanonicalFrame.Signed64(bounds.Width),
                CargoCanonicalFrame.Signed64(bounds.Depth), CargoCanonicalFrame.Signed64(bounds.Height)]);
        }
        foreach ((CargoSourceMerchant source, CargoMerchantProvision provision) in merchants)
        {
            fields.AddRange([CargoCanonicalFrame.Text(source.SourceId), source.PayloadHash.ToByteArray(),
                source.Place.PayloadHash.ToByteArray(), provision.Version.ToByteArray()]);
        }
        fields.Add(good.PayloadHash.ToByteArray());
        fields.Add(overlays.ToByteArray());
        return CargoCanonicalFrame.Hash("cargo-world.genesis-extension.v2", fields);
    }

    private EntityId Instance(string role) => Register(ConstructionFarmIdentityScope.InstanceId(_basis, IdentityProfile, role));

    private EntityId Register(EntityId identity)
    {
        if (_basis.IdentityIndex.Contains(identity) || !_newIds.Add(identity))
        {
            throw new InvalidDataException("A cargo-world identity aliases retained authority or another new role.");
        }
        return identity;
    }

    private sealed record Merchant(CargoSourceMerchant Source, CargoMerchantProvision Provision, ActorPresence Presence,
        WorldResident Resident, CustodianStorage Storage, ActorControlBinding Control, CountedSourceLot? Lot,
        AdmittedCountedSource? Opening, EntityId AccountId)
    {
        public EntityId ActorId => Presence.ActorId;
    }
}
