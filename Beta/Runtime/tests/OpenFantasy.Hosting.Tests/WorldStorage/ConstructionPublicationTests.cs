using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConstructionPublicationTests
{
    private const int MetadataPages = 512;

    [Fact]
    public void Carried_gift_and_intervened_work_share_one_original_history_and_recover_exactly()
    {
        using var files = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        var genesis = ConstructionFarmAdmission.CreateGenesis(fixture.QuestGenesis());
        var funded = fixture.CreateSource(files.Path, completeQuest: true, genesis: genesis);
        byte[] partial;
        ulong partialRevision;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var player = funded.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            var world = fixture.Act(authority, funded, player, new CloseDialogueIntent(funded.Dialogues.Single().DialogueId));
            var feet = world.Space.GetActor(player).Feet;
            var inventory = AuthorityEncodingV1.Encode(world.Inventory);
            for (int i = 0; i < 8; i++)
                world = authority.Commit(fixture.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, player), player, -32, 0)])).Candidate;
            Assert.NotEqual(feet.Cell, world.Space.GetActor(player).Feet.Cell);
            Assert.Equal(inventory, AuthorityEncodingV1.Encode(world.Inventory));
            for (int i = 0; i < 8; i++)
                world = authority.Commit(fixture.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, player), player, 32, 0)])).Candidate;
            Assert.Equal(feet, world.Space.GetActor(player).Feet);
            var builder = Project(world).BuilderActorId;
            var bag = world.GetResident(player).InventoryCustodianId;
            var wood = world.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == world.Quests!.Definitions.Single().Item);
            Assert.Equal(2UL, wood.Quantity);
            world = fixture.Act(authority, world, player, new GiveItemIntent(builder, world.Space.GetActor(builder).Revision,
                wood.LotId, wood.Revision, bag, world.Inventory.Custody.GetStorage(bag).Revision, 1));
            Assert.Equal(1UL, world.Inventory.Custody.GetLot(wood.LotId).Quantity);
            var given = Assert.Single(world.Inventory.Custody.Lots, value => value.CustodianId == world.GetResident(builder).InventoryCustodianId
                && value.Definition == wood.Definition);
            Assert.Equal(1UL, given.Quantity);
            Assert.Equal(wood.Source.SourceId, given.Source.SourceId);
            world = ReachWork(authority, fixture, world);
            world = Advance(authority, fixture, world, 20);
            // An ordinary actor intervention invalidates the retained complete geometry population.
            world = authority.Commit(fixture.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, player), player, -32, 0)])).Candidate;
            Assert.Equal(20UL, Project(world).Progress.CompletedUnits);
            world = Advance(authority, fixture, world, 40);
            partial = WorldSnapshotCodec.Encode(world);
            partialRevision = world.Revision;
            Assert.Empty(world.Space.Structures);
            Assert.Equal(partial, WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(partialRevision).State));
        }
        byte[] complete;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis)))
        {
            var world = authority.ReadStateForHost();
            Assert.Equal(partial, WorldSnapshotCodec.Encode(world));
            world = Advance(authority, fixture, world, 200);
            AssertCompleted(world);
            complete = WorldSnapshotCodec.Encode(world);
            Assert.Equal(partial, WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(partialRevision).State));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(complete, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    [Theory]
    [InlineData(1, (int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData(1, (int)AuthorityCommitBoundary.AfterCommit)]
    [InlineData(200, (int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData(200, (int)AuthorityCommitBoundary.AfterCommit)]
    public void Interrupted_work_publishes_exactly_one_whole_transition(int target, int boundaryValue)
    {
        using var files = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        var genesis = ConstructionFarmAdmission.CreateGenesis(fixture.QuestGenesis());
        var funded = fixture.CreateSource(files.Path, completeQuest: true, genesis: genesis);
        byte[] before;
        WorldStepDecision expected;
        WorldStepBatch batch;
        EntityId project;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var world = StartWork(authority, fixture, funded);
            world = Advance(authority, fixture, world, (ulong)target - 1);
            project = Project(world).ProjectId;
            before = WorldSnapshotCodec.Encode(world);
            batch = NextBatch(fixture, world);
            expected = new WorldStepProcessor().Decide(world, batch, authority.IsIdentityOccupiedForHost);
            Assert.Throws<IOException>(() => authority.CommitConstructionLocality(batch, project, MetadataPages, at =>
            {
                if ((int)at == boundaryValue) throw new IOException("interrupt locality publication");
            }));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        if (boundaryValue == (int)AuthorityCommitBoundary.BeforeCommit)
        {
            Assert.Equal(before, WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
            Assert.Null(recovered.FindReceipt(batch.Actions.Single(), ConstructionUpgradeFixture.Delivery(recovered.ReadStateForHost(), batch.Actions.Single().ActorId)));
            recovered.CommitConstructionLocality(batch, project, MetadataPages);
        }
        var durable = recovered.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(durable));
        Assert.Throws<AuthorityDispatchException>(() => recovered.CommitConstructionLocality(batch, project, MetadataPages));
        var receipt = recovered.FindReceipt(batch.Actions.Single(), ConstructionUpgradeFixture.Delivery(durable, batch.Actions.Single().ActorId));
        Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single()), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(receipt)));
        Assert.Equal(WorldSnapshotCodec.Encode(durable), WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(durable.Revision).State));
        if (target == 200) AssertCompleted(durable);
    }

    [Fact]
    public void History_and_changed_metadata_budget_refuse_without_poisoning_or_earning_work()
    {
        using var files = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        var genesis = ConstructionFarmAdmission.CreateGenesis(fixture.QuestGenesis());
        var funded = fixture.CreateSource(files.Path, completeQuest: true, genesis: genesis);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        var world = StartWork(authority, fixture, funded);
        var first = NextBatch(fixture, world);
        world = authority.CommitConstructionLocality(first, Project(world).ProjectId, MetadataPages).Candidate;
        // Ordinary commit clears the locality cache; old cause survives only in durable identity history.
        world = authority.Commit(fixture.Batch(world, [], [])).Candidate;
        var fresh = NextBatch(fixture, world);
        var duplicate = new WorldStepBatch(first.CauseId, world.WorldId, world.BranchId, world.Revision,
            world.AdvancingSteps, fresh.Kind, [], fresh.Actions, world.ProfileVersion);
        byte[] before = WorldSnapshotCodec.Encode(world);
        Assert.Throws<AuthorityDispatchException>(() => authority.CommitConstructionLocality(duplicate, Project(world).ProjectId, MetadataPages));
        Assert.Throws<AuthorityDispatchException>(() => authority.CommitConstructionLocality(fresh, Project(world).ProjectId, 0));
        Assert.Equal(before, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        world = authority.CommitConstructionLocality(fresh, Project(world).ProjectId, MetadataPages).Candidate;
        Assert.Equal(2UL, Project(world).Progress.CompletedUnits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Current_expedition_interpretations_refuse_without_losing_facets(bool withdrawal)
    {
        using var files = new StorageFiles();
        var genesis = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan"]);
        if (withdrawal) genesis = WorldPartyWithdrawalAdmission.CreateFresh(genesis);
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var batch = new ConstructionUpgradeFixture().Batch(genesis, [], []);
        var before = WorldSnapshotCodec.Encode(genesis);
        Assert.Throws<AuthorityDispatchException>(() => authority.CommitConstructionLocality(batch, Project(genesis).ProjectId, MetadataPages));
        Assert.Equal(before, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        Assert.NotNull(authority.ReadStateForHost().Care);
        Assert.NotNull(authority.ReadStateForHost().EffectiveControl);
        Assert.NotNull(authority.ReadStateForHost().Coordination);
        Assert.NotNull(authority.ReadStateForHost().EncounterLifecycle);
    }

    private static WorldSnapshot StartWork(SqliteWorldAuthority authority, ConstructionUpgradeFixture fixture, WorldSnapshot world)
    {
        var player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        world = fixture.Act(authority, world, player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
        return ReachWork(authority, fixture, world);
    }

    private static WorldSnapshot ReachWork(SqliteWorldAuthority authority, ConstructionUpgradeFixture fixture, WorldSnapshot world)
    {
        for (int i = 0; i < 512; i++)
        {
            var batch = NextBatch(fixture, world);
            if (batch.Actions.Any(value => value.Intent is WorkConstructionIntent))
            {
                Assert.Empty(batch.Movement);
                return world;
            }
            world = authority.Commit(batch).Candidate;
        }
        throw new InvalidOperationException("Funded builder failed to reach work within the fixture bound.");
    }

    private static WorldSnapshot Advance(SqliteWorldAuthority authority, ConstructionUpgradeFixture fixture, WorldSnapshot world, ulong target)
    {
        for (int selected = 0; Project(world).Progress.CompletedUnits < target; selected++)
        {
            Assert.True(selected < 512, "Construction must reach the requested work within its fixture bound.");
            var batch = NextBatch(fixture, world);
            var expected = new WorldStepProcessor().Decide(world, batch, authority.IsIdentityOccupiedForHost);
            var actual = authority.CommitConstructionLocality(batch, Project(world).ProjectId, MetadataPages);
            Assert.Equal(WorldActionRejection.None, Assert.Single(actual.Receipts).Rejection);
            Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(actual.Candidate));
            Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts), WorldReceiptCodecV1.EncodeBatch(actual.Receipts));
            Assert.Equal(expected.Identities, actual.Identities);
            world = actual.Candidate;
        }
        return world;
    }

    private static WorldStepBatch NextBatch(ConstructionUpgradeFixture fixture, WorldSnapshot world)
    {
        var controller = new LocalNpcController();
        return fixture.Batch(world, controller.SelectActions(world, fixture.NextIdentity), controller.SelectMovement(world));
    }

    private static ConstructionProject Project(WorldSnapshot world) => world.Construction!.Projects.Single();

    private static void AssertCompleted(WorldSnapshot world)
    {
        Assert.True(Project(world).IsCompleted);
        Assert.Single(world.Space.Structures);
        Assert.Single(world.Production!.Batches);
        Assert.Equal(1UL, Assert.Single(world.Production.Lots, value => value.ReservedBy == Project(world).ReservationId).Quantity);
        Assert.Equal(3UL, Assert.Single(world.Production.Lots, value => value.ReservedBy is null).Quantity);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, value => value.ReservedBy == Project(world).ReservationId);
    }
}
