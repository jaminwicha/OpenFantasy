using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.Simulation;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerQueries;

public sealed class PlayerProposalQueryTests(MaterialProposalWorldFixture world) : IClassFixture<MaterialProposalWorldFixture>
{
    [Fact]
    public async Task Archived_proposal_preserves_terms_and_filters_foreign_subjects_without_changing_history()
    {
        using var live = world.Open();
        var own = live.Save(world.PinnedRawTerms(live.State, world.Helper, world.Player));
        var original = live.Book.FindOpen(own.ProposalId)!;
        Assert.Equal(WorldActionRejection.None, live.Submit(world.Player, new DeclineMaterialProposalIntent(own.ProposalId)));
        ulong closedAt = live.Book.FindOutcome(own.ProposalId)!.WorldSequence;
        EntityId other = live.State.Residents.First(value => value.ActorId != world.Player && value.ActorId != world.Helper).ActorId;
        var foreign = live.Save(world.PinnedRawTerms(live.State, world.Helper, other));
        Assert.Equal(WorldActionRejection.None, live.Submit(world.Helper, new WithdrawMaterialProposalIntent(foreign.ProposalId)));
        for (int cycle = 0; live.Authority.InspectForHost().Journal.MaterialCareArchiveCount == 0; cycle++)
        {
            Assert.True(cycle <= WorldMaterialProposalBook.MaximumOutcomes, "Real saved outcomes must cause an ordinary bounded archive cut.");
            var pressure = live.Save(world.PinnedRawTerms(live.State, world.Player, world.Helper));
            Assert.Equal(WorldActionRejection.None, live.Submit(world.Player, new WithdrawMaterialProposalIntent(pressure.ProposalId)));
        }
        Assert.Null(live.Book.FindOutcome(own.ProposalId));
        Assert.Null(live.Book.FindOutcome(foreign.ProposalId));
        live.Reopen();
        var before = live.State;
        var usage = live.Authority.InspectForHost().Journal;
        var delivery = world.DeliveryOf(before, world.Player);
        await using var host = new LocalWorldSession(live.Authority, world.Player, delivery, new ManualSessionTimeProvider());
        await using var connection = await QuerySessionConnection.Open(host, world.Player, delivery, sockets: true);
        var found = (await connection.Query(new(PlayerQueryKind.InspectMaterialProposal, subjectId: own.ProposalId))).MaterialProposal!;
        Assert.Equal((own.ProposalId, MaterialProposalStatus.Declined, closedAt, false),
            (found.ProposalId, found.Status, found.ClosedAtSequence!.Value, found.ViewerIsSender));
        Assert.Equal(original.Terms, found.Terms);
        Assert.Equal(original.GrantHash, found.GrantHash);
        Assert.Null((await connection.Query(new(PlayerQueryKind.InspectMaterialProposal, subjectId: foreign.ProposalId))).MaterialProposal);
        Assert.Null((await connection.Query(new(PlayerQueryKind.InspectMaterialProposal, subjectId: world.Player))).MaterialProposal);
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        Assert.Equal(usage, live.Authority.InspectForHost().Journal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_real_history_read_bounds_queries_keeps_controls_responsive_and_joins_on_close(bool closeWhileReading)
    {
        using var live = world.Open();
        var own = live.Save(world.PinnedRawTerms(live.State, world.Helper, world.Player));
        var before = live.State;
        var delivery = world.DeliveryOf(before, world.Player);
        await using var host = new LocalWorldSession(live.Authority, world.Player, delivery, new ManualSessionTimeProvider());
        await using var connection = await QuerySessionConnection.Open(host, world.Player, delivery);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task held = Task.Run(() => live.Authority.ReadForController(world.Player, delivery, state =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
                "The test must release the actual authority read lock within its original deadline.");
            return state.Revision;
        }), TestContext.Current.CancellationToken);
        ulong queryId = connection.NextId();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await connection.SendQuery(queryId, new(PlayerQueryKind.InspectMaterialProposal, subjectId: own.ProposalId));
            await SessionFixture.Until(() => connection.Channel.Player.ProposalInspection.Latest is { IsCompleted: false });
            ulong refused = connection.NextId();
            await connection.SendQuery(refused, new(PlayerQueryKind.ReadTradeInquiry));
            var busy = await connection.Receive();
            Assert.Equal(refused, busy.RequestId);
            Assert.Equal(PlayerRequestRefusal.Busy, PlayerGameplayCodec.ReadRefusal(busy));
            await connection.Send(PlayerGameplayCodec.Readiness(connection.NextId(), true));
            await connection.Send(PlayerGameplayCodec.Movement(connection.NextId(), 32, 0));
            await connection.Ping();
            Assert.True(connection.Channel.Player.Ready);
            Assert.Equal(32, connection.Channel.Player.MovementX);
            await connection.Send(PlayerGameplayCodec.Readiness(connection.NextId(), false));
            await connection.Ping();
            Assert.False(connection.Channel.Player.Ready);
            Assert.Equal(0, connection.Channel.Player.MovementX);
            if (closeWhileReading)
            {
                connection.Stop();
                Assert.False(connection.Completion.IsCompleted);
            }
        }
        finally
        {
            release.Set();
            await held;
        }
        if (closeWhileReading)
        {
            try { await connection.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
            catch (OperationCanceledException) { }
            Assert.True(connection.Channel.Player.ProposalInspection.Latest!.IsCompleted);
            Assert.False(connection.Channel.Player.Connected);
        }
        else
        {
            var response = await connection.Receive();
            Assert.Equal((PlayerMessageKind.QueryResult, queryId), (response.Kind, response.RequestId));
            Assert.Equal(own.ProposalId, PlayerQueryResultCodec.Decode(response.Payload).MaterialProposal!.ProposalId);
            Assert.Null((await connection.Query(new(PlayerQueryKind.ReadTradeInquiry))).Inquiry);
        }
        Assert.Equal(before.ContentHash, live.State.ContentHash);
    }
}
