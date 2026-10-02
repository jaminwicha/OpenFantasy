using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Server.Tests;

public sealed class RemotePlayerSessionTests
{
    [Fact]
    public async Task Automatic_status_refresh_observes_real_scheduled_merchant_answer_without_an_explicit_inquiry_read()
    {
        // This scheduling fixture uses the retained native-market composition, not the cargo production opener.
        await using var world = await RemoteScheduledMarketWorld.Open();
        await using var session = await RemotePlayerSession.OpenAsync(world.Protocol, ServerWorld.Cancellation);
        var inquiry = new LocalTradeInquiry(world.Stall.MarketId, world.Stall.Commodity, LocalTradeDirection.Buy, 1);
        ulong before = world.Authority.ReadStateForHost().AdvancingSteps;
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
        LocalTradeInquiryView waiting = await session.SubmitTradeInquiryAsync(inquiry);
        Assert.Equal(LocalTradeInquiryState.Waiting, waiting.State);
        Assert.Null(waiting.QuoteId);
        Assert.Equal(waiting, session.ReadTradeInquiry());
        Assert.Equal(before, world.Authority.ReadStateForHost().AdvancingSteps);
        int initialRefreshes = world.Tap.Sent.Count(message => message.Kind == PlayerMessageKind.Query
            && PlayerQueryCodec.Decode(message.Payload).Kind == PlayerQueryKind.ReadParticipantStatus);

        session.SetReady(true);
        // Readiness passes through the same real receiver; it permits the merchant's next normal cutoff.
        await world.Protocol.PingAsync().WaitAsync(TimeSpan.FromSeconds(3), ServerWorld.Cancellation);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (session.ReadTradeInquiry()?.State != LocalTradeInquiryState.Offered)
            await Task.Delay(TimeSpan.FromMilliseconds(20), deadline.Token);

        LocalTradeInquiryView offered = session.ReadTradeInquiry()!;
        Assert.Equal(waiting.Inquiry, offered.Inquiry);
        Assert.Equal(waiting.ExpiresAtStep, offered.ExpiresAtStep);
        Assert.NotNull(offered.QuoteId);
        var state = world.Authority.ReadStateForHost();
        Assert.True(state.AdvancingSteps > before);
        var committed = Assert.Single(state.Coordination!.PaidExchange!.Open,
            quote => quote.QuoteId == offered.QuoteId);
        Assert.Equal(world.Stall.MerchantActorId, committed.Quote.IssuerActorId);
        Assert.Equal(session.Welcome.ActorId, committed.Quote.CounterpartyActorId);
        Assert.Equal(world.Stall.Commodity, committed.Quote.Definition);
        Assert.True(committed.Quote.IssuedAtStep > before);
        Assert.Equal(world.Stall.MarketId, state.Coordination.Markets!.FindQuote(committed.QuoteId)!.MarketId);
        var sent = world.Tap.Sent.Where(message => message.Kind == PlayerMessageKind.Query)
            .Select(message => PlayerQueryCodec.Decode(message.Payload).Kind).ToArray();
        Assert.True(sent.Count(kind => kind == PlayerQueryKind.ReadParticipantStatus) > initialRefreshes);
        Assert.Single(sent, kind => kind == PlayerQueryKind.SubmitTradeInquiry);
        Assert.DoesNotContain(PlayerQueryKind.ReadTradeInquiry, sent);
        Assert.DoesNotContain(PlayerQueryKind.CancelTradeInquiry, sent);
    }

    [Fact]
    public async Task Production_host_supplies_real_metadata_and_queries_preserve_paused_world_and_inquiry_identity()
    {
        await using var world = await RemoteSessionWorld.Open();
        await using var session = await RemotePlayerSession.OpenAsync(await world.ConnectProtocol(), ServerWorld.Cancellation);
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
        Assert.Equal(NativeCargoWorld.Stalls(world.Genesis), session.Presentation.CargoStalls);
        Assert.Equal(world.Genesis.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single(), session.Presentation.WrenActorId);
        Assert.True(session.Presentation.IsRegional);
        Assert.NotNull(session.ReadView().Regional);
        var source = world.Genesis.Inventory.Sources.First();
        var request = new LocalTradeInquiry(session.Presentation.CargoStalls[0].MarketId,
            new CountedItemDefinition(source.Definition.DefinitionId, source.Definition.Version), LocalTradeDirection.Buy, 1);
        var waiting = await session.SubmitTradeInquiryAsync(request);
        Assert.Equal(LocalTradeInquiryState.Waiting, waiting.State);
        Assert.Same(waiting, session.ReadTradeInquiry());
        var cancelled = await session.CancelTradeInquiryAsync();
        Assert.Equal(waiting.Inquiry, cancelled!.Inquiry);
        Assert.Equal(waiting.ExpiresAtStep, cancelled.ExpiresAtStep);
        Assert.Equal(LocalTradeInquiryState.Cancelled, cancelled.State);
        await Task.Delay(RemotePlayerSession.RefreshInterval * 2, ServerWorld.Cancellation);
        Assert.Equal(cancelled, session.ReadTradeInquiry());
        Assert.Equal(world.Genesis.Calendar, session.ReadView().Calendar);
        await session.DisposeAsync();
        Assert.False(world.Host.Session.Completion.IsCompleted);
    }

    [Fact]
    public async Task Foreground_inspection_waits_once_behind_refresh_and_a_second_request_is_busy()
    {
        await using var world = await RemoteSessionWorld.Open();
        await using var session = await RemotePlayerSession.OpenAsync(await world.ConnectProtocol(), ServerWorld.Cancellation);
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Tap!.BeforeReceive = async (message, token) =>
        {
            if (message.Kind != PlayerMessageKind.QueryResult
                || PlayerQueryResultCodec.Decode(message.Payload).Kind != PlayerQueryKind.ReadParticipantStatus) return;
            blocked.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        try
        {
            await blocked.Task.WaitAsync(TimeSpan.FromSeconds(3), ServerWorld.Cancellation);
            Task<PaidQuoteView?> inspection = session.InspectPaidQuoteAsync(new(880, 1));
            var refusal = await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => session.InspectMaterialProposalAsync(new(880, 2)));
            Assert.Equal(PlayerRequestRefusal.Busy, refusal.Refusal);
            Assert.False(inspection.IsCompleted);
            release.TrySetResult();
            Assert.Null(await inspection.WaitAsync(TimeSpan.FromSeconds(3), ServerWorld.Cancellation));
            var queries = world.Tap.Sent.Where(value => value.Kind == PlayerMessageKind.Query)
                .Select(value => PlayerQueryCodec.Decode(value.Payload).Kind).ToArray();
            int explicitIndex = Array.IndexOf(queries, PlayerQueryKind.InspectPaidQuote);
            Assert.True(explicitIndex >= 3);
            Assert.Equal(PlayerQueryKind.ReadParticipantStatus, queries[explicitIndex - 1]);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task Disposal_joins_refresh_and_releases_a_queued_user_query_without_closing_host()
    {
        await using var world = await RemoteSessionWorld.Open();
        await using var session = await RemotePlayerSession.OpenAsync(await world.ConnectProtocol(), ServerWorld.Cancellation);
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Tap!.BeforeReceive = async (_, token) =>
        {
            blocked.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        await blocked.Task.WaitAsync(TimeSpan.FromSeconds(3), ServerWorld.Cancellation);
        Task<PaidQuoteView?> query = session.InspectPaidQuoteAsync(new(881, 1));
        await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3), ServerWorld.Cancellation);
        Assert.True(session.Completion.IsCompleted);
        Assert.True(query.IsCompleted);
        Assert.NotNull(await Record.ExceptionAsync(() => query));
        Assert.Equal(LocalWorldRunState.Unavailable, session.ReadRunState());
        Assert.False(world.Host.Session.Completion.IsCompleted);
    }

    [Fact]
    public async Task Host_without_presentation_is_refused_before_native_readiness()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        PlayerProtocolClient protocol = await fixture.Connect();
        var refusal = await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => RemotePlayerSession.OpenAsync(protocol, ServerWorld.Cancellation));
        Assert.Equal(PlayerRequestRefusal.NotAdmitted, refusal.Refusal);
        Assert.True(protocol.Completion.IsCompleted);
        Assert.Equal(0UL, fixture.World.Authority.ReadStateForHost().AdvancingSteps);
    }
}
