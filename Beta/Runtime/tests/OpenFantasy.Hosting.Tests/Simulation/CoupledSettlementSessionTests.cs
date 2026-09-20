using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class CoupledSettlementSessionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Coupled_profiles_queue_excavation_and_climbing_for_normal_durable_adjudication(bool climbing, bool eventOrdered)
    {
        var fixture = new CoupledSettlementWorldFixture(eventOrdered: eventOrdered);
        WorldSnapshot genesis = fixture.Genesis;
        using var files = new SessionFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var delivery = ConstructionUpgradeFixture.Delivery(genesis, fixture.Player);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, delivery, time);
        WorldActionCommand excavation = fixture.NextExcavation(genesis).Actions.Single();
        Assert.Equal(0U, genesis.Gathering!.GetActorWork(fixture.Player).Effort);
        // The stale excavation pin and unfunded climb deliberately refuse in the domain after the
        // session admits the action family. Genesis has not yet earned labor or traversal.
        var command = new WorldActionCommand(excavation.CommandId, excavation.PrincipalId, excavation.OriginSessionId,
            excavation.ActorId, excavation.WorldId, excavation.BranchId, checked(excavation.ExpectedActorRevision + 1),
            climbing ? new ClimbIntent(32, 0, 32) : excavation.Intent);
        session.SetReady(true);
        Task<WorldActionReceipt> first = session.SubmitAction(command);
        Task<WorldActionReceipt> duplicate = session.SubmitAction(command);
        Assert.False(first.IsCompleted);
        Assert.False(duplicate.IsCompleted);
        await Tick(time);
        WorldActionReceipt receipt = await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(receipt.ResultId, (await duplicate).ResultId);
        Assert.Equal(CommandReceiptStatus.Rejected, receipt.Status);
        Assert.Equal(climbing ? WorldActionRejection.Unavailable : WorldActionRejection.RevisionConflict, receipt.Rejection);
        session.SetReady(false);
        WorldSnapshot after = authority.ReadStateForHost();
        Assert.Equal((ushort)(eventOrdered ? 18 : 17), after.ProfileVersion);
        Assert.Equal(genesis.DescentMode, after.DescentMode);
        Assert.Equal(1UL, after.Revision);
        Assert.Equal(1UL, after.AdvancingSteps);
        Assert.Equal(genesis.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.Null(after.PendingExcavation);
        Assert.Equal(genesis.Excavation!.ContentHash, after.Excavation!.ContentHash);
        Assert.Equal(genesis.ConstructionPhysical!.ContentHash, after.ConstructionPhysical!.ContentHash);
        Assert.Equal(genesis.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(genesis.CoupledSettlementPolicy!.ContentHash, after.CoupledSettlementPolicy!.ContentHash);
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
        Assert.Equal(receipt.ResultId, (await session.SubmitAction(command)).ResultId);
        await session.StopAsync();

        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
            Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(command, delivery))));
    }
}
