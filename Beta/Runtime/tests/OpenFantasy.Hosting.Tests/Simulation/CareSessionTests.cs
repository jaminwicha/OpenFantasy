using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class CareSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Patient_acknowledgment_precedes_a_distinct_ready_helper_tick_and_pending_reopen(bool crafted)
    {
        using var files = new StorageFiles();
        var fixture = new CareWorldFixture(crafted);
        WorldSnapshot defeated;
        using (var owner = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            defeated = fixture.PrepareDefeat(new(owner));
        }
        var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        var time = new ManualSessionTimeProvider();
        WorldActionCommand acknowledgment;
        WorldActionReceipt receipt;
        WorldSnapshot acknowledged;
        ulong earlierReceipts = authority.InspectForHost().Journal.CommandOutcomeCount;
        await using (var session = new LocalWorldSession(authority, fixture.Player, fixture.DeliveryFor(fixture.Player), time))
        {
            var care = Assert.IsType<LocalCareView>(session.ReadView().Care);
            Assert.Equal(LocalCareStatus.Available, care.Status);
            var offer = Assert.Single(care.Offers);
            Assert.True(offer.CanAcknowledge);
            Assert.Equal(fixture.Helper, offer.HelperActorId);
            Assert.Equal(WorldActionRejection.None, offer.Rejection);
            acknowledgment = fixture.Command(defeated, fixture.Player, offer.Intent);
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(acknowledgment));
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(defeated.ContentHash, authority.ReadStateForHost().ContentHash);

            session.SetReady(true);
            session.SetMovement(32, 0);
            Task<WorldActionReceipt> original = session.SubmitAction(acknowledgment);
            Task<WorldActionReceipt> duplicate = session.SubmitAction(acknowledgment);
            await Tick(time);
            await Until(() => original.IsCompleted && duplicate.IsCompleted);
            receipt = await original;
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await duplicate));
            acknowledged = authority.ReadStateForHost();
            Assert.Equal(defeated.Revision + 1, acknowledged.Revision);
            Assert.Equal(earlierReceipts + 1, authority.InspectForHost().Journal.CommandOutcomeCount);
            Assert.Equal(acknowledgment.CommandId, acknowledged.Care!.Pending!.CommandId);
            Assert.Empty(acknowledged.Care.Treatments);
            Assert.Empty(acknowledged.Production!.ConsumedItems);
            Assert.Equal(defeated.Progression!.ContentHash, acknowledged.Progression!.ContentHash);
            Assert.Equal(defeated.Inventory.ContentHash, acknowledged.Inventory.ContentHash);
            AssertNoTimeOrMovement(defeated, acknowledged);
            Assert.Equal(LocalCareStatus.Pending, Assert.IsType<LocalCareView>(session.ReadView().Care).Status);
            Assert.Empty(session.ReadView().Care!.Offers);

            session.SetReady(false);
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(acknowledged.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await session.SubmitAction(acknowledgment)));
        }

        var restored = SqliteWorldAuthority.OpenExisting(files.Path);
        var clock = new ManualSessionTimeProvider();
        WorldSnapshot treated;
        WorldActionCommand helperCommand;
        await using (var resumed = new LocalWorldSession(restored, fixture.Player, fixture.DeliveryFor(fixture.Player), clock))
        {
            Assert.Equal(acknowledged.ContentHash, restored.ReadStateForHost().ContentHash);
            Assert.Equal(LocalCareStatus.Pending, Assert.IsType<LocalCareView>(resumed.ReadView().Care).Status);
            await Tick(clock, TimeSpan.FromHours(1));
            Assert.Equal(acknowledged.ContentHash, restored.ReadStateForHost().ContentHash);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await resumed.SubmitAction(acknowledgment)));
            helperCommand = Assert.IsType<WorldActionCommand>(new LocalCareController().SelectTreatment(acknowledged,
                restored.IsIdentityOccupiedForHost));
            Assert.Equal(fixture.Helper, helperCommand.ActorId);
            Assert.Equal(fixture.DeliveryFor(fixture.Helper).PrincipalId, helperCommand.PrincipalId);
            Assert.Equal(fixture.DeliveryFor(fixture.Helper).SessionId, helperCommand.OriginSessionId);
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => resumed.SubmitAction(helperCommand));
            var patientImpersonation = fixture.Command(acknowledged, fixture.Player, helperCommand.Intent);
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => resumed.SubmitAction(patientImpersonation));
            Assert.Equal(acknowledged.ContentHash, restored.ReadStateForHost().ContentHash);

            resumed.SetReady(true);
            resumed.SetMovement(32, 0);
            await Tick(clock);
            treated = restored.ReadStateForHost();
            Assert.Equal(acknowledged.Revision + 1, treated.Revision);
            Assert.Equal(earlierReceipts + 2, restored.InspectForHost().Journal.CommandOutcomeCount);
            Assert.Null(treated.Care!.Pending);
            Assert.Equal(helperCommand.CommandId, Assert.Single(treated.Care.Treatments).CommandId);
            Assert.Equal(40, treated.Progression!.GetActor(fixture.Player).CurrentHp);
            var dose = Assert.Single(treated.Production!.ConsumedItems);
            Assert.Equal(helperCommand.CommandId, dose.CommandId);
            Assert.Equal(crafted ? IngredientLotKind.Transformed : IngredientLotKind.Source, dose.Ingredient.Kind);
            Assert.Equal(1UL, dose.Ingredient.Quantity);
            Assert.True(treated.Pacing!.Encounter!.GetActor(fixture.Helper).RemainingDelay > 0);
            Assert.Equal(EncounterLifecyclePhase.Active, treated.EncounterLifecycle!.Phase);
            AssertNoTimeOrMovement(acknowledged, treated);
            Assert.Equal(0U, treated.EncounterActions!.LastResolution!.TargetHpAfter);
            Assert.Null(resumed.ReadView().Care);
            Assert.Equal(CommandReceiptStatus.Applied, Assert.IsType<WorldActionReceipt>(
                restored.FindReceipt(helperCommand, fixture.DeliveryFor(fixture.Helper))).Status);
            resumed.SetReady(false);
            await Tick(clock, TimeSpan.FromHours(1));
            Assert.Equal(treated.ContentHash, restored.ReadStateForHost().ContentHash);
            Assert.False(resumed.Completion.IsCompleted);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(treated), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Single(reopened.ReadStateForHost().Production!.ConsumedItems);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.IsType<WorldActionReceipt>(
            reopened.FindReceipt(helperCommand, fixture.DeliveryFor(fixture.Helper))).Status);
    }

    [Fact]
    public async Task Stale_acknowledgment_receives_a_durable_rejection_without_healing_or_replacing_player_control()
    {
        using var files = new StorageFiles();
        var fixture = new CareWorldFixture();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var defeated = fixture.PrepareDefeat(new(authority));
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, fixture.DeliveryFor(fixture.Player), time);
        session.SetReady(true);
        var offered = Assert.Single(Assert.IsType<LocalCareView>(session.ReadView().Care).Offers).Intent;
        var command = fixture.Command(defeated, fixture.Player, new AcknowledgeCareIntent(offered.AgreementId,
            offered.EncounterId, offered.ExpectedLifecycleRevision + 1, offered.ExpectedActionHash));
        var foreign = new WorldActionCommand(command.CommandId, command.PrincipalId, new EntityId(0xabcdef, 9),
            command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision, command.Intent);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(foreign));
        var helper = fixture.Command(defeated, fixture.Helper, offered);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(helper));
        var submitted = session.SubmitAction(command);
        await Tick(time);
        await Until(() => submitted.IsCompleted);
        var receipt = await submitted;
        Assert.Equal(CommandReceiptStatus.Rejected, receipt.Status);
        Assert.Equal(WorldActionRejection.RevisionConflict, receipt.Rejection);
        var rejected = authority.ReadStateForHost();
        Assert.Equal(defeated.Revision + 1, rejected.Revision);
        Assert.Null(rejected.Care!.Pending);
        Assert.Empty(rejected.Care.Treatments);
        Assert.Empty(rejected.Production!.ConsumedItems);
        Assert.Equal(defeated.Progression!.ContentHash, rejected.Progression!.ContentHash);
        AssertNoTimeOrMovement(defeated, rejected);
        Assert.Equal(LocalCareStatus.Available, Assert.IsType<LocalCareView>(session.ReadView().Care).Status);
        session.SetReady(false);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await session.SubmitAction(command)));
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(rejected.ContentHash, authority.ReadStateForHost().ContentHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Occupied_helper_selection_or_rejected_real_attempt_never_retries_with_new_identities(bool collideConsumption)
    {
        using var files = new StorageFiles();
        var fixture = new CareWorldFixture();
        WorldSnapshot pending;
        EntityId selectedId;
        using (var owner = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var defeated = fixture.PrepareDefeat(new(owner));
            var intended = fixture.Acknowledge(defeated);
            var preview = owner.Preview(fixture.Batch(defeated, WorldBatchKind.Care, [intended]));
            selectedId = LocalCareController.SelectionId(preview.Candidate);
            var intent = (AcknowledgeCareIntent)intended.Intent;
            EntityId reservedId = collideConsumption ? EntityId.DeriveSpawned(selectedId, 0) : selectedId;
            var reserve = new WorldActionCommand(reservedId, intended.PrincipalId, intended.OriginSessionId,
                intended.ActorId, intended.WorldId, intended.BranchId, intended.ExpectedActorRevision,
                new AcknowledgeCareIntent(intent.AgreementId, intent.EncounterId,
                    intent.ExpectedLifecycleRevision + 1, intent.ExpectedActionHash));
            var refused = owner.Commit(fixture.Batch(defeated, WorldBatchKind.Care, [reserve]));
            Assert.Equal(WorldActionRejection.RevisionConflict, Assert.Single(refused.Receipts).Rejection);
            var current = refused.Candidate;
            var actual = new WorldActionCommand(intended.CommandId, intended.PrincipalId, intended.OriginSessionId,
                intended.ActorId, intended.WorldId, intended.BranchId, current.Space.GetActor(fixture.Player).Revision,
                new AcknowledgeCareIntent(intent.AgreementId, intent.EncounterId,
                    current.EncounterLifecycle!.Revision, current.EncounterActions!.ContentHash));
            var admitted = owner.Commit(fixture.Batch(current, WorldBatchKind.Care, [actual]));
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(admitted.Receipts).Status);
            pending = admitted.Candidate;
            Assert.Equal(selectedId, LocalCareController.SelectionId(pending));
            Assert.Equal(!collideConsumption, owner.IsIdentityOccupiedForHost(selectedId));
            if (!collideConsumption) Assert.Null(new LocalCareController().SelectTreatment(pending, owner.IsIdentityOccupiedForHost));
        }
        var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        var time = new ManualSessionTimeProvider();
        ulong outcomes = authority.InspectForHost().Journal.CommandOutcomeCount;
        await using (var session = new LocalWorldSession(authority, fixture.Player, fixture.DeliveryFor(fixture.Player), time))
        {
            var care = Assert.IsType<LocalCareView>(session.ReadView().Care);
            Assert.Equal(collideConsumption ? LocalCareStatus.Pending : LocalCareStatus.NeedsAttention, care.Status);
            Assert.Equal(pending.Care!.Pending!.AcknowledgmentId, care.PendingAcknowledgmentId);
            Assert.Empty(care.Offers);
            session.SetReady(true);
            if (collideConsumption)
            {
                var selected = Assert.IsType<WorldActionCommand>(new LocalCareController().SelectTreatment(pending,
                    authority.IsIdentityOccupiedForHost));
                await Tick(time);
                var rejected = authority.ReadStateForHost();
                Assert.Equal(pending.Revision + 1, rejected.Revision);
                Assert.Equal(pending.Care.ContentHash, rejected.Care!.ContentHash);
                Assert.Equal(pending.Production!.ContentHash, rejected.Production!.ContentHash);
                Assert.Equal(pending.Inventory.ContentHash, rejected.Inventory.ContentHash);
                Assert.Equal(pending.Progression!.ContentHash, rejected.Progression!.ContentHash);
                AssertNoTimeOrMovement(pending, rejected);
                Assert.Equal(WorldActionRejection.Unavailable, Assert.IsType<WorldActionReceipt>(
                    authority.FindReceipt(selected, fixture.DeliveryFor(fixture.Helper))).Rejection);
                pending = rejected;
                outcomes++;
            }
            for (int attempt = 0; attempt < 3; attempt++)
            {
                await Tick(time, attempt == 2 ? TimeSpan.FromHours(1) : null);
                Assert.Equal(pending.ContentHash, authority.ReadStateForHost().ContentHash);
                Assert.Equal(outcomes, authority.InspectForHost().Journal.CommandOutcomeCount);
                Assert.Equal(LocalCareStatus.NeedsAttention, session.ReadView().Care!.Status);
            }
            Assert.Empty(authority.ReadStateForHost().Production!.ConsumedItems);
            Assert.False(session.Completion.IsCompleted);
        }
        var restored = SqliteWorldAuthority.OpenExisting(files.Path);
        var resumedClock = new ManualSessionTimeProvider();
        await using var resumed = new LocalWorldSession(restored, fixture.Player, fixture.DeliveryFor(fixture.Player), resumedClock);
        Assert.Equal(LocalCareStatus.NeedsAttention, resumed.ReadView().Care!.Status);
        resumed.SetReady(true);
        await Tick(resumedClock, TimeSpan.FromHours(1));
        Assert.Equal(pending.ContentHash, restored.ReadStateForHost().ContentHash);
        Assert.Equal(outcomes, restored.InspectForHost().Journal.CommandOutcomeCount);
    }

    [Fact]
    public async Task Human_controlled_helper_is_not_impersonated_by_the_local_host_even_with_core_care_permission()
    {
        using var files = new StorageFiles();
        var fixture = new CareWorldFixture();
        var genesis = WithPlayerHelper(fixture.Genesis, fixture.Helper);
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var defeated = fixture.PrepareDefeat(new(authority));
        var offer = Assert.Single(Assert.IsType<LocalCareView>(new LocalWorldView(defeated, fixture.Player).Care).Offers);
        Assert.False(offer.CanAcknowledge);
        Assert.Equal(WorldActionRejection.Unavailable, offer.Rejection);
        Assert.Equal(WorldActionRejection.None, WorldCareActions.EvaluateAcknowledgment(defeated, fixture.Player, offer.Intent));
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, fixture.DeliveryFor(fixture.Player), time);
        session.SetReady(true);
        var acknowledgment = fixture.Command(defeated, fixture.Player, offer.Intent);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(acknowledgment));
        Assert.Equal(LocalCareStatus.Unavailable, session.ReadView().Care!.Status);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(defeated.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Empty(authority.ReadStateForHost().Production!.ConsumedItems);
        Assert.False(session.Completion.IsCompleted);
    }

    [Fact]
    public async Task Care_enabled_exploration_preserves_its_real_excavation_action_route()
    {
        using var files = new StorageFiles();
        var fixture = new CareWorldFixture();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var current = fixture.Genesis;
        var site = current.Excavation!.Sites.Single(value => value.OwnerActorId == fixture.Player);
        // The retained breach fixture reaches the dry north bank before moving east along it.
        // Validate each real local connector; asking the bounded BFS for the whole distant route
        // exceeds its authored search envelope and is not an excavation eligibility test.
        var start = current.Space.GetActor(fixture.Player).Feet;
        int movementSteps = 0;
        foreach (var waypoint in new[] { new SubunitPosition(start.X, site.Stance.Y, start.Z), site.Stance })
        {
            while (current.Space.GetActor(fixture.Player).Feet != waypoint)
            {
                Assert.True(movementSteps++ < 512, "The admitted worksite must be reached by a finite actual ground route.");
                var feet = current.Space.GetActor(fixture.Player).Feet;
                long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
                long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
                var destination = feet.Offset(dx, dy, 0);
                var route = current.Space.QueryGroundRoute(fixture.Player, destination);
                Assert.Equal(GroundRouteStatus.Reachable, route.Status);
                Assert.Equal(destination, Assert.IsType<SubunitPosition>(route.NextWaypoint));
                var sample = fixture.Batch(current, WorldBatchKind.AdvancingStep);
                current = authority.Commit(new WorldStepBatch(sample.CauseId, current.WorldId, current.BranchId,
                    current.Revision, current.AdvancingSteps, WorldBatchKind.AdvancingStep,
                    [new(fixture.DeliveryFor(fixture.Player), fixture.Player, dx, dy)], [], current.ProfileVersion)).Candidate;
                Assert.Equal(destination, current.Space.GetActor(fixture.Player).Feet);
            }
        }
        for (int step = 0; current.Gathering!.GetActorWork(fixture.Player).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 128, "Excavation effort must be earned through actual exploration boundaries.");
            current = authority.Commit(fixture.Batch(current, WorldBatchKind.AdvancingStep)).Candidate;
        }
        var clock = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, fixture.DeliveryFor(fixture.Player), clock);
        var view = session.ReadView().Worksite!;
        var target = view.ExcavationTargets.Single(value => value.SiteId == site.SiteId);
        var tool = view.Tools.Single(value => value.DefinitionId == site.RequiredTool.DefinitionId);
        var command = fixture.Command(current, fixture.Player, new ExcavateIntent(target.SiteId,
            target.SiteRevision, target.TerrainRevision, tool.LotId, tool.Revision, target.DestinationRevision));
        session.SetReady(true);
        var submitted = session.SubmitAction(command);
        await Tick(clock);
        await Until(() => submitted.IsCompleted);
        Assert.Equal(CommandReceiptStatus.Applied, (await submitted).Status);
        var worked = authority.ReadStateForHost();
        Assert.Equal((ushort)25, worked.ProfileVersion);
        Assert.Equal(site.CompletedWork + 1, worked.Excavation!.Sites.Single(value => value.SiteId == site.SiteId).CompletedWork);
        Assert.Equal(current.AdvancingSteps + 1, worked.AdvancingSteps);
        Assert.Equal(current.Progression!.ContentHash, worked.Progression!.ContentHash);
        Assert.Equal(current.Care!.ContentHash, worked.Care!.ContentHash);
        Assert.Null(session.ReadView().Care);
    }

    private static void AssertNoTimeOrMovement(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
        Assert.Equal(before.EncounterLifecycle!.ExplorationSteps, after.EncounterLifecycle!.ExplorationSteps);
        Assert.Equal(before.EncounterLifecycle.TacticalTicks, after.EncounterLifecycle.TacticalTicks);
        Assert.Equal(before.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)),
            after.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)));
        Assert.Equal(before.Residents.Select(value => (value.ActorId, value.ExperiencedTime)),
            after.Residents.Select(value => (value.ActorId, value.ExperiencedTime)));
    }

    private static WorldSnapshot WithPlayerHelper(WorldSnapshot world, EntityId helper) => new(
        world.Inventory, world.Space, world.Residents.Select(resident => resident.ActorId != helper ? resident
            : new WorldResident(resident.ActorId, resident.InventoryCustodianId, resident.DisplayName,
                ResidentControllerKind.Player, resident.HomeFeet, resident.WanderRadiusCells,
                resident.Comprehension, resident.ExperiencedTime, resident.OpeningUtterance)),
        world.AdmissionVersion, world.Calendar, 0, 0, gathering: world.Gathering, production: world.Production,
        progression: world.Progression, quests: world.Quests, construction: world.Construction,
        reservoirs: world.Reservoirs, excavation: world.Excavation, reservoirSeals: world.ReservoirSeals,
        liquidCells: world.LiquidCells, pacing: world.Pacing, encounterLifecycle: world.EncounterLifecycle,
        care: world.Care, resourceHistory: world.ResourceHistory);
}
