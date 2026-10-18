using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.PlayerRoster;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Two fresh finite contracts; all deliveries, travel and construction work are actual commands.</summary>
internal sealed class CoordinatedConstructionJourney
{
    private readonly ConstructionUpgradeFixture _commands = new();
    private CareWorldSession _session;

    internal CoordinatedConstructionJourney(bool coordinated, bool hostBuilders = false, bool constructionOnly = false,
        bool withBystander = false)
    {
        var source = RestorativeJourneyAdmission.CreateEmbedded("coordinated-concurrent-construction", "Traveler");
        Assert.Equal((ushort)26, source.ProfileVersion);
        if (withBystander)
        {
            Assert.True(constructionOnly);
            source = new PlayerRosterGenesisExtension(source, new PlayerRosterDefinition(["Traveler", "Observer"])).Build();
        }
        Player = source.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player
            && value.DisplayName == "Traveler").ActorId;
        var originalQuest = Assert.Single(source.Quests!.Definitions);
        Helper = originalQuest.GiverActorId;
        var originalProject = Assert.Single(source.Construction!.Projects);
        var definition = originalProject.Definition;
        var playerBag = source.GetResident(Player).InventoryCustodianId;
        var helperBag = source.GetResident(Helper).InventoryCustodianId;

        var addedQuest = new DeliveryQuestDefinition(Id(1), "second_builder_stock", "A second sapling guard",
            "Deliver six wood units for the traveler's own guard.", Player, 1, originalQuest.Item,
            originalQuest.Quantity, Id(2), Id(3), new ActorRewardDefinition(Id(4), "second_builder_reward", 0,
                originalQuest.Reward.Currency, originalQuest.Reward.ReserveAccountId, originalQuest.Reward.CurrencyAmount));
        var quests = new QuestSnapshot(source.WorldId, source.BranchId, 0,
            source.Quests.Definitions.Append(addedQuest), []);
        // The second guard sits in the adjacent open farm column. Both original 200-unit
        // construction geometry and all original access witnesses remain admitted.
        var workFeet = originalProject.WorkFeet.Offset(256, 0, 0);
        var addedProject = new ConstructionProject(Id(5), definition, Player, Player, addedQuest.DefinitionId,
            addedQuest.DestinationCustodianId, addedQuest.MaterialReservationId,
            originalProject.SiteFeet.Offset(256, 0, 0), workFeet,
            originalProject.RequiredAccess.Append(new GroundAccessRequirement(Player, workFeet,
                originalProject.RequiredAccess.First(value => value.ActorId == Player).To)),
            stockLocation: originalProject.StockLocation);
        var construction = new ConstructionSnapshot(source.WorldId, source.BranchId, 0,
            source.Construction.Definitions, source.Construction.Projects.Append(addedProject));

        var old = source.Inventory;
        var custody = new CustodySnapshot(source.WorldId, source.BranchId, 0,
            old.Custody.Storages.Select(storage => storage.CustodianId == playerBag
                ? new CustodianStorage(playerBag, checked(storage.UnitCapacity + 1), 0)
                : storage.CustodianId == helperBag
                    ? new CustodianStorage(helperBag, checked(storage.UnitCapacity + originalQuest.Quantity), 0)
                    : storage).Append(new CustodianStorage(addedProject.ProjectCustodianId, originalQuest.Quantity, 0)),
            old.Custody.Lots.Concat([
                new CountedSourceLot(Id(6), helperBag, originalQuest.Item, new SourceInterval(Id(7), 0, originalQuest.Quantity), 0),
                new CountedSourceLot(Id(8), playerBag, definition.RequiredTool.Item, new SourceInterval(Id(9), 0, 1), 0)]));
        var inventory = new CountedAuthorityState(custody, checked(old.AdmittedUnitCapacity + originalQuest.Quantity + 1),
            old.Actors, old.Controls,
            old.Policies.Append(new CustodianAccessPolicy(addedProject.ProjectCustodianId, Player, CustodyDepositRule.AnyControlledActor)),
            old.Sources.Concat([new AdmittedCountedSource(Id(7), originalQuest.Item, originalQuest.Quantity),
                new AdmittedCountedSource(Id(9), definition.RequiredTool.Item, 1)]),
            old.ReservationIds.Append(addedProject.ReservationId));
        var originalProgression = source.Progression!;
        var originalCurrency = originalProgression.Currency;
        var currency = new CurrencyLedger(source.WorldId, source.BranchId, 0, originalCurrency.Currency,
            checked(originalCurrency.AdmittedSupply + addedQuest.Reward.CurrencyAmount),
            originalCurrency.Accounts.Select(account => account.AccountId == addedQuest.Reward.ReserveAccountId
                ? new CurrencyAccount(account.AccountId, account.Kind, account.ActorId,
                    checked(account.Balance + addedQuest.Reward.CurrencyAmount), 0) : account));
        var progression = new ProgressionSnapshot(source.WorldId, source.BranchId, 0,
            originalProgression.Profiles, originalProgression.Curves, originalProgression.Actors, currency);
        var utterance = source.GetResident(Helper).OpeningUtterance!;
        var residents = source.Residents.Select(resident => resident.ActorId != Player ? resident : new WorldResident(
            resident.ActorId, resident.InventoryCustodianId, resident.DisplayName,
            hostBuilders ? ResidentControllerKind.Host : resident.ControllerKind,
            resident.HomeFeet, resident.WanderRadiusCells, resident.Comprehension, resident.ExperiencedTime,
            new ResidentUtterance(Id(10), utterance.Channel, utterance.ChannelVersion, "Please bring wood for my guard.")));
        var production = new ProductionSnapshot(source.WorldId, source.BranchId, 0, source.Production!.Recipes, [], []);
        var admission = ContentHash.Compute(source.ContentHash.ToByteArray().Concat(construction.ContentHash.ToByteArray())
            .Concat(quests.ContentHash.ToByteArray()).Concat(inventory.ContentHash.ToByteArray()).ToArray());
        if (hostBuilders)
        {
            admission = ContentHash.Compute(admission.ToByteArray().Concat("two-host-builders-v1"u8.ToArray()).ToArray());
        }
        var basis = new WorldSnapshot(inventory, source.Space, residents, admission, source.Calendar, 0, 0,
            gathering: source.Gathering, production: production, progression: progression, quests: quests, construction: construction);
        if (constructionOnly)
        {
            Assert.False(coordinated);
            Assert.Equal((ushort)5, basis.ProfileVersion);
            Genesis = basis;
            _session = new(Genesis);
            return;
        }

        // Rebind the exact fresh reservoir and liquid distribution to the extended finite custody.
        var oldCapacity = source.Reservoirs!.Capacity;
        Assert.Empty(oldCapacity.Pending);
        var closed = Assert.Single(oldCapacity.Closed);
        var domain = oldCapacity.Domain;
        var request = new WatershedRunoffRequest(domain.Network, domain.Mapping, inventory, production, closed,
            domain.OwnerActorId, source.Calendar, source.Calendar.AddMilliseconds(1), domain.Material,
            domain.Network.Basins.Select(basin => WatershedRunoffInput.Dry(basin.BasinId)), domain.Terminals);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request,
            TestContext.Current.CancellationToken));
        capacity = capacity.PlanCancel(closed).ApplyTo(capacity);
        var reservoirs = LiquidReservoirState.Create(capacity, source.Reservoirs.Geometries);
        var topology = TerrainLiquidTopology.Create(reservoirs, source.Space.Regions, source.ReservoirSeals!);
        var cells = LiquidCellState.Create(reservoirs, source.LiquidCells!.Profile, source.Calendar,
            source.LiquidCells.Portions, topology);
        var wet = WorldTerrainLiquidAdmission.CreateGenesis(basis, reservoirs, source.ReservoirSeals!, source.Excavation!, cells);
        var lifecycle = WorldEncounterLifecycleAdmission.CreateFresh(WorldPacingAdmission.CreateFresh(wet), source.EncounterLifecycle!);
        var care = new CareSnapshot(source.WorldId, source.BranchId, 0, source.Care!.Remedies, source.Care.Agreements, [], []);
        var ordinary = WorldRestorativeAdmission.CreateFresh(WorldCareAdmission.CreateFresh(lifecycle, care), source.Care.Ordinary!);
        Genesis = coordinated ? WorldCoordinatedAdmission.CreateFresh(ordinary) : ordinary;
        Assert.Equal(source.Gathering!.ContentHash, Genesis.Gathering!.ContentHash);
        Assert.Equal(source.Excavation!.ContentHash, Genesis.Excavation!.ContentHash);
        Assert.Equal(source.Care.ContentHash, Genesis.Care!.ContentHash);
        Assert.Equal(source.LiquidCells.Portions, Genesis.LiquidCells!.Portions);
        Assert.Equal(source.Space.Actors, Genesis.Space.Actors);
        Assert.Equal(source.Space.Regions.Select(region => region.ComputeContentHash()),
            Genesis.Space.Regions.Select(region => region.ComputeContentHash()));
        Assert.Equal(0UL, Genesis.Revision);
        Assert.Equal(0UL, Genesis.Construction!.Revision);
        Assert.Empty(Genesis.Quests!.Participations);
        Assert.Empty(Genesis.Production!.Batches);
        Assert.Empty(Genesis.ResourceHistory!.History.Batches);
        _session = new(Genesis);
    }

    internal WorldSnapshot Genesis { get; }
    internal EntityId Player { get; }
    internal EntityId Helper { get; }
    internal WorldSnapshot World => _session.ReadStateForHost();
    internal IEnumerable<EntityId> OccupiedIdentities => _session.OccupiedIdentities;
    internal IReadOnlyList<WorldStepBatch> Batches => _session.Batches;
    internal EntityId NextIdentity() => _commands.NextIdentity();
    internal WorldStepBatch Batch(IEnumerable<WorldActionCommand> commands) => _commands.Batch(World, commands, []);
    internal WorldStepDecision Commit(WorldStepBatch batch) => _session.Commit(batch);

    internal void SupplyAndPositionBuilders()
    {
        Walk(Helper, World.Space.GetActor(Player).Feet.Offset(0, -256, 0));
        foreach (var quest in World.Quests!.Definitions)
        {
            var actor = quest.GiverActorId == Helper ? Player : Helper;
            Apply(Command(actor, new SpeakToActorIntent(quest.GiverActorId, World.Space.GetActor(quest.GiverActorId).Revision)));
            Apply(Command(actor, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
                World.Space.GetActor(quest.GiverActorId).Revision, 0, World.Progression!.GetActor(actor).Revision)));
            var before = World;
            var bag = before.GetResident(actor).InventoryCustodianId;
            var wood = before.Inventory.Custody.Lots.Single(lot => lot.CustodianId == bag && lot.Definition == quest.Item);
            Apply(Command(actor, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
                before.Space.GetActor(quest.GiverActorId).Revision, 1, before.Progression!.GetActor(actor).Revision,
                before.Inventory.Custody.GetStorage(bag).Revision, [new(wood.LotId, wood.Revision, quest.Quantity)])));
            Assert.Equal(ActorQuestStatus.Completed, World.Quests!.FindParticipation(actor, quest.DefinitionId)!.Status);
            Assert.Equal(before.Progression.Currency.GetActorAccount(actor).Balance + quest.Reward.CurrencyAmount,
                World.Progression!.Currency.GetActorAccount(actor).Balance);
            Apply(Command(actor, new CloseDialogueIntent(Assert.Single(World.Dialogues).DialogueId)));
        }
        // Put the farther builder in place first, leaving the nearer work anchor free during travel.
        Walk(Player, World.Construction!.Projects.Single(project => project.BuilderActorId == Player).WorkFeet);
        Walk(Helper, World.Construction!.Projects.Single(project => project.BuilderActorId == Helper).WorkFeet);
        Assert.Empty(World.Dialogues);
        Assert.All(World.Construction.Projects, project => Assert.Equal(0UL, project.Revision));
    }

    internal WorldActionCommand[] WorkCommands() => World.Construction!.Projects.Select(project =>
    {
        var tool = World.Inventory.Custody.Lots.Single(lot =>
            lot.CustodianId == World.GetResident(project.BuilderActorId).InventoryCustodianId
            && lot.Definition == project.Definition.RequiredTool.Item);
        return Command(project.BuilderActorId, new WorkConstructionIntent(project.ProjectId, project.Revision,
            World.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, tool.LotId, tool.Revision));
    }).OrderBy(command => command.ActorId).ToArray();

    internal WorldStepDecision Commit(params WorldActionCommand[] commands) =>
        _session.Commit(_commands.Batch(World, commands, []));

    internal WorldMaterialCareArchiveSegment CutAndReopen()
    {
        var before = World;
        var identities = _session.OccupiedIdentities.ToArray();
        var segment = WorldMaterialCareArchiveSegment.Seal(before);
        Assert.Equal(WorldSnapshotCodec.Encode(before), segment.Encode());
        var retrieved = WorldMaterialCareArchiveSegment.Decode(segment.Reference, segment.Encode(), before.Coordination!.Anchor);
        var bytes = WorldSnapshotCodec.Encode(retrieved.Continued);
        var restored = WorldSnapshotCodec.DecodeCoordinated(bytes, retrieved.Anchor);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(restored));
        _session = new(restored, identities);
        Assert.Equal(identities.Order(), _session.OccupiedIdentities.Order());
        return retrieved;
    }

    internal WorldActionCommand Command(EntityId actor, WorldActionIntent intent)
    {
        var delivery = ConstructionUpgradeFixture.Delivery(World, actor);
        return new(_commands.NextIdentity(), delivery.PrincipalId, delivery.SessionId, actor,
            World.WorldId, World.BranchId, World.Space.GetActor(actor).Revision, intent);
    }

    private void Apply(WorldActionCommand command)
    {
        var decision = Commit(command);
        var receipt = Assert.Single(decision.Receipts);
        Assert.Equal(command.CommandId, receipt.CommandId);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
    }

    internal void Walk(EntityId actor, SubunitPosition target)
    {
        for (int step = 0; World.Space.GetActor(actor).Feet != target; step++)
        {
            Assert.True(step < 160, $"The real builder route to {target} exceeded its finite fixture budget.");
            var world = World;
            var feet = world.Space.GetActor(actor).Feet;
            var route = world.Space.QueryGroundRoute(actor, target);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            Assert.True(dx != 0 || dy != 0);
            var decision = _session.Commit(_commands.Batch(world, [],
                [new WorldMovementInput(ConstructionUpgradeFixture.Delivery(world, actor), actor, dx, dy)]));
            Assert.Empty(decision.Receipts);
            Assert.Equal(feet.Offset(dx, dy, 0), World.Space.GetActor(actor).Feet);
        }
    }

    private static EntityId Id(ulong value) => new(0xc00dc05700000000, value);
}
