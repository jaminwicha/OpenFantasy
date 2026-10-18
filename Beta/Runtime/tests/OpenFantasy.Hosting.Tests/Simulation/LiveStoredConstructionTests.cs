using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed partial class CombinedStoredDueTests
{
    [Fact]
    public void Live_cutoff_fixture_has_no_npc_movement_and_a_lawful_player_step()
    {
        Assert.Empty(new LocalNpcController().SelectMovement(Fixture.Value.World));
        Assert.Equal(2, Select(Fixture.Value.World).Count);
        _ = LawfulObserverMove(Fixture.Value.World);
        var fixture = Fixture.Value;
        var originalView = new LocalWorldView(fixture.World, fixture.Observer);
        Assert.DoesNotContain(originalView.GatheringNodes, node => node.NodeId == fixture.NodeId);
        var after = new WorldStepProcessor().Decide(fixture.World, Batch(fixture.World, Select(fixture.World))).Candidate;
        Assert.Contains(new LocalWorldView(after, fixture.Observer).GatheringNodes, node => node.NodeId == fixture.NodeId);
    }

    [Fact]
    public async Task Live_read_slices_keep_one_old_view_until_the_complete_work_and_due_boundary()
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("live-coherent-view");
        var authority = CreateCombinedAuthority(files.Path);
        var time = new ManualSessionTimeProvider();
        await using var session = LiveConstructionSession(authority, time, 1);
        var originalView = session.ReadView();
        int owners = LiveOwnerCount();
        int reads = authority.OwnerArchiveReads, terrainReads = authority.TerrainArchiveReads;
        session.SetReady(true);
        for (int slice = 1; slice < owners; slice++)
        {
            await Tick(time);
            Assert.Same(originalView, session.ReadView());
            Assert.Equal(reads + slice, authority.OwnerArchiveReads);
            Assert.Equal(terrainReads, authority.TerrainArchiveReads);
        }
        await Tick(time);
        Assert.NotSame(originalView, session.ReadView());
        Assert.Equal(fixture.World.Calendar.AddMilliseconds(1800), session.ReadView().Calendar);
        Assert.DoesNotContain(originalView.GatheringNodes, node => node.NodeId == fixture.NodeId);
        Assert.Equal(checked((uint)fixture.World.Gathering!.GetNode(fixture.NodeId).Definition.MaxSwings),
            session.ReadView().GatheringNodes.Single(node => node.NodeId == fixture.NodeId).RemainingSwings);
        AssertCombinedBoundary(fixture.World, authority.ReadStateForHost(), fixture);
        Assert.True(authority.TerrainArchiveReads > terrainReads); // Coherent postcommit projection still restores the world.
        session.SetReady(false);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(fixture.World.AdvancingSteps + 1, authority.ReadStateForHost().AdvancingSteps);
        await session.StopAsync();
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        AssertCombinedBoundary(fixture.World, reopened.ReadStateForHost(), fixture);
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Genesis), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(0).State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pause_or_stop_between_slices_cancels_unpublished_work_without_earning_time(bool stop)
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("live-cancel-" + stop);
        var authority = CreateCombinedAuthority(files.Path);
        var time = new ManualSessionTimeProvider();
        await using var session = LiveConstructionSession(authority, time, 1);
        session.SetReady(true);
        await Tick(time);
        await Tick(time);
        if (stop)
        {
            await session.StopAsync();
            using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
            Assert.Equal(WorldSnapshotCodec.Encode(fixture.World), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            return;
        }
        session.SetReady(false);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(fixture.World.Calendar, session.ReadView().Calendar);
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.World), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        session.SetReady(true);
        for (int slice = 1; slice < LiveOwnerCount(); slice++)
        {
            await Tick(time);
            Assert.Equal(fixture.World.Calendar, session.ReadView().Calendar);
        }
        await Tick(time);
        AssertCombinedBoundary(fixture.World, authority.ReadStateForHost(), fixture);
    }

    [Fact]
    public async Task A_dialogue_request_queued_during_preparation_waits_for_the_next_cutoff_and_keeps_its_receipt()
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("live-queued-request");
        var authority = CreateCombinedAuthority(files.Path);
        var time = new ManualSessionTimeProvider();
        await using var session = LiveConstructionSession(authority, time, 1);
        session.SetReady(true);
        await Tick(time);
        var world = fixture.World;
        var delivery = ConstructionUpgradeFixture.Delivery(world, fixture.Observer);
        var speaker = world.Residents.First(resident => resident.ControllerKind == ResidentControllerKind.Host
            && resident.OpeningUtterance is not null);
        var command = new WorldActionCommand(NextIdentity(), delivery.PrincipalId, delivery.SessionId, fixture.Observer,
            world.WorldId, world.BranchId, world.Space.GetActor(fixture.Observer).Revision,
            new SpeakToActorIntent(speaker.ActorId, world.Space.GetActor(speaker.ActorId).Revision));
        var pending = session.SubmitAction(command);
        for (int slice = 1; slice < LiveOwnerCount(); slice++)
        {
            await Tick(time);
            Assert.False(pending.IsCompleted);
        }
        AssertCombinedBoundary(world, authority.ReadStateForHost(), fixture);
        await Tick(time);
        var receipt = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(command.CommandId, receipt.CommandId);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
            WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, delivery))));
        Assert.Equal(world.AdvancingSteps + 2, authority.ReadStateForHost().AdvancingSteps);
        Assert.Equal(receipt.ResultId, (await session.SubmitAction(command)).ResultId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Movement_uses_the_ordinary_cutoff_with_or_without_sliced_work_enabled(bool sliced)
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("live-movement-" + sliced);
        var authority = CreateCombinedAuthority(files.Path);
        var time = new ManualSessionTimeProvider();
        await using var session = LiveConstructionSession(authority, time, sliced ? 1 : null);
        var movement = LawfulObserverMove(fixture.World);
        var feet = fixture.World.Space.GetActor(fixture.Observer).Feet;
        session.SetReady(true);
        session.SetMovement(movement.Dx, movement.Dy);
        await Tick(time);
        Assert.Equal(fixture.World.AdvancingSteps + 1, authority.ReadStateForHost().AdvancingSteps);
        Assert.Equal(feet.Offset(movement.Dx, movement.Dy, 0), authority.ReadStateForHost().Space.GetActor(fixture.Observer).Feet);
        AssertCombinedBoundary(fixture.World, authority.ReadStateForHost(), fixture);
    }

    [Fact]
    public async Task External_world_change_invalidates_the_live_selection_before_a_fresh_later_cutoff()
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("live-intervention");
        var authority = CreateCombinedAuthority(files.Path);
        var time = new ManualSessionTimeProvider();
        await using var session = LiveConstructionSession(authority, time, 1);
        session.SetReady(true);
        await Tick(time);
        var changed = authority.Commit(Batch(fixture.World, [])).Candidate;
        await Tick(time);
        Assert.Equal(changed.Calendar, session.ReadView().Calendar);
        Assert.Equal(WorldSnapshotCodec.Encode(changed), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        for (int slice = 0; slice < LiveOwnerCount(); slice++) await Tick(time);
        AssertCombinedBoundary(changed, authority.ReadStateForHost(), fixture);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Sliced_session_requires_a_positive_budget_and_explicit_typed_storage(int budget, bool typed)
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("live-option-" + typed);
        using var authority = typed ? SqliteWorldAuthority.CreateWithTypedOwners(files.Path, fixture.Genesis)
            : SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        Assert.ThrowsAny<ArgumentException>(() => LiveConstructionSession(authority, new ManualSessionTimeProvider(), budget));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Genesis), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    private LocalWorldSession LiveConstructionSession(SqliteWorldAuthority authority, ManualSessionTimeProvider time, int? budget) =>
        new(authority, Fixture.Value.Observer, ConstructionUpgradeFixture.Delivery(Fixture.Value.World, Fixture.Value.Observer),
            time, storedConstructionOwnersPerSlice: budget);

    private int LiveOwnerCount() => StoredConstructionInterval.Capture(Fixture.Value.World, Select(Fixture.Value.World)).RequiredOwnerCount;

    private (long Dx, long Dy) LawfulObserverMove(WorldSnapshot world)
    {
        var actor = Fixture.Value.Observer;
        var feet = world.Space.GetActor(actor).Feet;
        var delivery = ConstructionUpgradeFixture.Delivery(world, actor);
        foreach (var move in new[] { (Dx: -32L, Dy: 0L), (Dx: 32L, Dy: 0L), (Dx: 0L, Dy: -32L), (Dx: 0L, Dy: 32L) })
        {
            var batch = new WorldStepBatch(NextIdentity(), world.WorldId, world.BranchId, world.Revision,
                world.AdvancingSteps, WorldBatchKind.AdvancingStep, [new(delivery, actor, move.Dx, move.Dy)], [], world.ProfileVersion);
            if (new WorldStepProcessor().Decide(world, batch).Candidate.Space.GetActor(actor).Feet == feet.Offset(move.Dx, move.Dy, 0))
                return move;
        }
        throw new InvalidOperationException("The actual observer needs a lawful one-sample movement for this live fixture.");
    }
}
