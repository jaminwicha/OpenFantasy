using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Production;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// The real source-backed journey plus finite authored restorative stock and prior care permission.
/// Wren approaches by ordinary movement; the player loses HP only to actual source-backed bites.
/// The Potion record and recipe counts are retained source content; physical calibration is explicit.
/// </summary>
internal sealed class CareWorldFixture
{
    private readonly EncounterJourneyWorldFixture _journey = new();
    private readonly RestorativeSupplyFixture _supply = new();
    private ulong _identity = 10_000;

    internal CareWorldFixture(bool craftedDose = false, ulong? rewardExperience = null, uint? strikeMana = null, bool invalidNormalizedSpeedOnReward = false)
    {
        CraftedDose = craftedDose;
        WorldSnapshot source = _journey.Genesis;
        HelperStorage = source.GetResident(Helper).InventoryCustodianId;
        Assert.DoesNotContain(source.Inventory.Custody.Lots, lot => lot.CustodianId == HelperStorage
            && (lot.Definition == _supply.Potion || lot.Definition == _supply.Berries));
        var added = new List<CountedSourceLot>
        {
            new(BerryLot, HelperStorage, _supply.Berries, new(BerrySource, 0, 6), 0)
        };
        var sources = source.Inventory.Sources.Append(new AdmittedCountedSource(BerrySource, _supply.Berries, 6));
        if (!craftedDose)
        {
            added.Add(new(PotionLot, HelperStorage, _supply.Potion, new(PotionSource, 0, 2), 0));
            sources = sources.Append(new(PotionSource, _supply.Potion, 2));
        }
        var inventory = new CountedAuthorityState(new(source.WorldId, source.BranchId, 0,
            source.Inventory.Custody.Storages, source.Inventory.Custody.Lots.Concat(added)),
            checked(source.Inventory.AdmittedUnitCapacity + (craftedDose ? 6UL : 8UL)), source.Inventory.Actors,
            source.Inventory.Controls, source.Inventory.Policies, sources, source.Inventory.ReservationIds);
        var production = new ProductionSnapshot(source.WorldId, source.BranchId, 0,
            source.Production!.Recipes.Append(_supply.Recipe), [], []);
        var admission = ContentHash.Compute(source.ContentHash.ToByteArray().Concat(_supply.Recipe.Version.ToByteArray()).ToArray());
        var progression = source.Progression!;
        if (invalidNormalizedSpeedOnReward)
        {
            // Legal fresh content whose next level exceeds the encounter interpreter's speed range.
            // This forces rejection at COMPLETE root admission, after the quest/resource planners succeed.
            var original = progression.GetActor(Player).Profile;
            var stats = original.BaseStats;
            var profile = new ActorStatProfileDefinition(original.DefinitionId, original.Key, original.DisplayName,
                original.Kind, new(stats.MaxHp, stats.MaxMp, stats.Attack, stats.Magic, stats.Defense, (int)(uint.MaxValue / 10)),
                original.Growth with { Speed = 1 }, original.HeritageModifiers with { Speed = 0 });
            progression = new ProgressionSnapshot(progression.WorldId, progression.BranchId, 0,
                progression.Profiles.Select(value => value.DefinitionId == profile.DefinitionId ? profile : value),
                progression.Curves, progression.Actors.Select(actor => actor.Profile.DefinitionId == profile.DefinitionId
                    ? new ActorProgressionState(actor.ActorId, 0, profile, actor.Curve, actor.Level, actor.Experience,
                        actor.CurrentHp, actor.CurrentMp) : actor), progression.Currency);
        }
        var quests = source.Quests!;
        if (rewardExperience is { } experience)
        {
            // Explicit test calibration of the authored reward, preserving real funding and delivery.
            var quest = Assert.Single(quests.Definitions);
            var reward = quest.Reward;
            var calibrated = new ActorRewardDefinition(reward.DefinitionId, reward.Key, experience,
                reward.Currency, reward.ReserveAccountId, reward.CurrencyAmount);
            var definition = new DeliveryQuestDefinition(quest.DefinitionId, quest.Key, quest.Title, quest.Description,
                quest.GiverActorId, quest.MinimumLevel, quest.Item, quest.Quantity, quest.DestinationCustodianId,
                quest.MaterialReservationId, calibrated);
            quests = new QuestSnapshot(quests.WorldId, quests.BranchId, 0, [definition], []);
        }
        var basis = new WorldSnapshot(inventory, source.Space, source.Residents, admission, source.Calendar, 0, 0,
            gathering: source.Gathering, production: production, progression: progression,
            quests: quests, construction: source.Construction);

        // Recreate the same fresh, closed dry request against complete initial custody. This adds
        // no runoff, water entitlement, terrain edit or earned production/construction history.
        var oldCapacity = source.Reservoirs!.Capacity;
        Assert.Empty(oldCapacity.Pending);
        EntityId closed = Assert.Single(oldCapacity.Closed);
        var domain = oldCapacity.Domain;
        var request = new WatershedRunoffRequest(domain.Network, domain.Mapping, inventory, production, closed,
            domain.OwnerActorId, basis.Calendar, basis.Calendar.AddMilliseconds(1), domain.Material,
            domain.Network.Basins.Select(basin => WatershedRunoffInput.Dry(basin.BasinId)), domain.Terminals);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request,
            TestContext.Current.CancellationToken));
        capacity = capacity.PlanCancel(closed).ApplyTo(capacity);
        var reservoirs = LiquidReservoirState.Create(capacity, source.Reservoirs.Geometries);
        var topology = TerrainLiquidTopology.Create(reservoirs, source.Space.Regions, source.ReservoirSeals!);
        var cells = LiquidCellState.Create(reservoirs, source.LiquidCells!.Profile, source.Calendar,
            source.LiquidCells.Portions, topology);
        var wet = WorldTerrainLiquidAdmission.CreateGenesis(basis, reservoirs, source.ReservoirSeals!, source.Excavation!, cells);
        var catalog = source.EncounterLifecycle!;
        if (strikeMana is { } mana)
        {
            // Explicit cost variant exercises the actual paired caster/target publication path.
            var abilities = catalog.Abilities.Select(ability => ability.DefinitionId == _journey.Strike
                ? new EncounterAbilityDefinition(ability.DefinitionId, ability.Key, ability.DisplayName, ability.SourceVersion,
                    ability.PowerTenths, ability.RangeSubunits, ability.ReadinessCostNumerator,
                    ability.ReadinessCostDenominator, mana, ability.MinimumLevel, ability.RequiredFunction) : ability);
            catalog = EncounterLifecycleSnapshot.BeginFresh(catalog.WorldId, catalog.BranchId, catalog.Seed,
                abilities, catalog.Scenarios, catalog.Debts.Select(value => value.ActorId));
        }
        var lifecycle = WorldEncounterLifecycleAdmission.CreateFresh(WorldPacingAdmission.CreateFresh(wet), catalog);
        uint restored = _supply.PotionPayload.GetProperty("healHp").GetUInt32();
        Assert.Equal(40U, restored);
        var remedy = new RestorativeRemedyDefinition(Remedy, _supply.Potion, restored, 256);
        var care = new CareSnapshot(source.WorldId, source.BranchId, 0, [remedy],
            [new(Agreement, Player, Helper, Remedy, remedy.Version)], [], []);
        Genesis = WorldCareAdmission.CreateFresh(lifecycle, care);
        Assert.Equal((ushort)25, Genesis.ProfileVersion);
        Assert.Equal(source.Space.Regions.Select(region => region.ComputeContentHash()),
            Genesis.Space.Regions.Select(region => region.ComputeContentHash()));
        Assert.Equal(source.LiquidCells.Portions, Genesis.LiquidCells!.Portions);
        Assert.Equal(progression.ContentHash, Genesis.Progression!.ContentHash);
    }

    internal WorldSnapshot Genesis { get; }
    internal bool CraftedDose { get; }
    internal EntityId Player => _journey.Player;
    internal EntityId Helper => _journey.Wren;
    internal EntityId Wolf => _journey.WolfFirst;
    internal EntityId HelperStorage { get; }
    internal EntityId Agreement => Id(1);
    internal EntityId Remedy => Id(2);
    internal EntityId PotionLot => Id(3);
    internal EntityId PotionSource => Id(4);
    internal EntityId BerryLot => Id(5);
    internal EntityId BerrySource => Id(6);
    internal CountedItemDefinition Potion => _supply.Potion;
    internal WorldSnapshot? BeforeDefeat { get; private set; }

    internal WorldSnapshot PrepareDefeat(CareWorldSession authority)
    {
        MoveHelper(authority);
        var state = authority.ReadStateForHost();
        if (CraftedDose)
        {
            var storage = state.Inventory.Custody.GetStorage(HelperStorage);
            var berry = state.Inventory.Custody.GetLot(BerryLot);
            var craft = Command(state, Helper, new CraftIntent(new(_supply.Recipe.DefinitionId, _supply.Recipe.Version,
                state.Production!.Revision, HelperStorage, storage.Revision,
                [new(IngredientLotKind.Source, berry.LotId, berry.Revision, 3)])));
            state = Commit(authority, Batch(state, WorldBatchKind.AdvancingStep, [craft])).Candidate;
            Assert.Equal(1UL, Assert.Single(state.Production!.Lots, lot => lot.Definition == Potion).Quantity);
        }
        state = Commit(authority, Batch(state, WorldBatchKind.EncounterLifecycle,
            [_journey.Enter(state, _journey.FirstScenario)])).Candidate;
        for (int stage = 0; stage < 2; stage++)
            state = Commit(authority, Batch(state, WorldBatchKind.EncounterLifecycle)).Candidate;
        for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
        {
            Assert.True(boundary < 1_000, "Actual Wait/bite defeat exceeded the finite fixture boundary bound.");
            var prior = state;
            state = NextCombatBoundary(authority, playerAttacks: false);
            if (state.Progression!.GetActor(Player).CurrentHp == 0) BeforeDefeat = prior;
        }
        Assert.Equal(0, state.Progression!.GetActor(Player).CurrentHp);
        Assert.Equal(Genesis.Progression!.GetActor(Wolf).CurrentHp, state.Progression.GetActor(Wolf).CurrentHp);
        Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(Helper, Player, 256));
        Assert.NotNull(BeforeDefeat);
        return state;
    }

    internal WorldSnapshot PrepareVictory(CareWorldSession authority)
    {
        MoveHelper(authority);
        var state = authority.ReadStateForHost();
        state = Commit(authority, Batch(state, WorldBatchKind.EncounterLifecycle,
            [_journey.Enter(state, _journey.FirstScenario)])).Candidate;
        for (int stage = 0; stage < 2; stage++) state = Commit(authority, Batch(state, WorldBatchKind.EncounterLifecycle)).Candidate;
        for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
        {
            Assert.True(boundary < 1000, "Actual victory must fit the inherited encounter bound.");
            state = NextCombatBoundary(authority, playerAttacks: true);
        }
        Assert.True(state.Progression!.GetActor(Player).CurrentHp > 0);
        Assert.Equal(0, state.Progression.GetActor(Wolf).CurrentHp);
        return Commit(authority, Batch(state, WorldBatchKind.EncounterLifecycle, [_journey.Release(state)])).Candidate;
    }

    internal WorldSnapshot CompleteQuest(CareWorldSession authority)
    {
        var command = PrepareQuestCompletion(authority);
        var state = authority.ReadStateForHost();
        return Commit(authority, Batch(state, WorldBatchKind.HeldInteraction, [command])).Candidate;
    }

    internal WorldActionCommand PrepareQuestCompletion(CareWorldSession authority)
    {
        MoveHelper(authority);
        var state = authority.ReadStateForHost();
        for (int step = 0; !WorldActorAvailability.CanAct(state, Player); step++)
        {
            Assert.True(step < 32, "Post-combat action debt must recover through ordinary exploration steps.");
            state = Commit(authority, Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
        }
        state = Act(authority, state, new SpeakToActorIntent(Helper, state.Space.GetActor(Helper).Revision));
        var quest = Assert.Single(state.Quests!.Definitions);
        state = Act(authority, state, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
            state.Space.GetActor(Helper).Revision, 0, state.Progression!.GetActor(Player).Revision));
        var bag = state.GetResident(Player).InventoryCustodianId;
        var wood = state.Inventory.Custody.Lots.Single(lot => lot.CustodianId == bag && lot.Definition == quest.Item);
        return Command(state, Player, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
            state.Space.GetActor(Helper).Revision, 1, state.Progression!.GetActor(Player).Revision,
            state.Inventory.Custody.GetStorage(bag).Revision, [new(wood.LotId, wood.Revision, quest.Quantity)]));
    }

    private WorldSnapshot Act(CareWorldSession session, WorldSnapshot state, WorldActionIntent intent) =>
        Commit(session, Batch(state, state.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            [Command(state, Player, intent)])).Candidate;

    internal WorldSnapshot NextCombatBoundary(CareWorldSession authority, bool playerAttacks)
    {
        var state = authority.ReadStateForHost();
        var timing = state.Pacing!.Encounter!;
        var due = WorldEncounterActions.DueActors(state);
        var choices = due.Where(actor => !timing.Decisions.Any(choice => choice.ActorId == actor.ActorId))
            .Select(actor => actor.ActorId == Player && !playerAttacks ? _journey.Wait(state, Player)
                : _journey.Attack(state, actor.ActorId, actor.ActorId == Player ? Wolf : Player)).ToArray();
        var kind = choices.Length != 0 ? WorldBatchKind.EncounterDecision
            : due.Count != 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance;
        return Commit(authority, Batch(state, kind, choices)).Candidate;
    }

    internal WorldActionCommand Acknowledge(WorldSnapshot state) => Command(state, Player,
        new AcknowledgeCareIntent(Agreement, state.EncounterActions!.Scope.EncounterId,
            state.EncounterLifecycle!.Revision, state.EncounterActions.ContentHash));

    internal WorldActionCommand Administer(WorldSnapshot state) => Command(state, Helper,
        WorldCareActions.PrepareRemedy(state) ?? throw new InvalidOperationException("Actual helper and dose must be available."));

    internal WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(NextId(), control.PrincipalId, control.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, intent);
    }

    internal TrustedCommandDelivery DeliveryFor(EntityId actor) => _journey.DeliveryFor(actor);
    internal WorldStepBatch Batch(WorldSnapshot state, WorldBatchKind kind, IEnumerable<WorldActionCommand>? actions = null)
        => new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps, kind, [], actions ?? [], state.ProfileVersion);

    internal static WorldStepDecision Commit(CareWorldSession authority, WorldStepBatch batch)
    {
        var result = authority.Commit(batch);
        Assert.All(result.Receipts, receipt => Assert.Equal(CommandReceiptStatus.Applied, receipt.Status));
        return result;
    }

    private void MoveHelper(CareWorldSession authority)
    {
        var state = authority.ReadStateForHost();
        SubunitPosition goal = state.Space.GetActor(Player).Feet.Offset(0, -256, 0);
        for (int step = 0; state.Space.GetActor(Helper).Feet != goal; step++)
        {
            Assert.True(step < 256, "The neutral helper must approach through actual clear ground within the fixture route bound.");
            var route = state.Space.QueryGroundRoute(Helper, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            var feet = state.Space.GetActor(Helper).Feet;
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            state = Commit(authority, new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new(DeliveryFor(Helper), Helper, dx, dy)], [], state.ProfileVersion)).Candidate;
            Assert.Equal(feet.Offset(dx, dy, 0), state.Space.GetActor(Helper).Feet);
        }
    }

    private EntityId NextId()
    {
        EntityId identity = Id(_identity++);
        if (Genesis.IdentityIndex.Contains(identity)) throw new InvalidOperationException("Care fixture command aliases admitted content.");
        return identity;
    }

    private static EntityId Id(ulong value) => new(0xca25000000000000, value);
}
