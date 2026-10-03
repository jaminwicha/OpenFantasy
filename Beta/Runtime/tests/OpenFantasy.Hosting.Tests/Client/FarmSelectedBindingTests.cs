using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.ActorSelection;
using OpenFantasy.Hosting.Tests.Simulation.SelectedQueries;

namespace OpenFantasy.Hosting.Tests.Client.ActorSelection;

public sealed class FarmSelectedBindingTests
{
    [Fact]
    public async Task Captured_offer_keeps_real_delivery_and_rejects_an_actor_cycle_and_another_adapter()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var first = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var delivery = fixture.Delivery(fixture.Grantee);
        var client = new FarmClientSession(new SelectedPort(player), delivery, fixture.Third, new EntityId(0xdada01, 1), null);
        var target = first.ControlledView!.Actors.Single(actor => actor.ActorId == fixture.Third);
        var offer = client.CreateOffers(first, new FarmTarget(target.ActorId, target.DisplayName,
            FarmTargetKind.Person, target.Feet))[FarmSemanticAction.Speak];
        var command = offer.ToCommand(new EntityId(0xdada01, 2));
        Assert.Equal(fixture.Owner, command.ActorId);
        Assert.Equal(delivery.PrincipalId, command.PrincipalId);
        Assert.Equal(delivery.SessionId, command.OriginSessionId);
        Assert.Equal(acquired.Witness, command.ControlWitness);
        Assert.Equal(first.Selection, offer.Scope.Selection);
        Assert.Same(first.HomeView, player.ReadSelectedObservation().HomeView);

        var another = new FarmClientSession(new SelectedPort(player), delivery, fixture.Third, new EntityId(0xdada01, 1), null);
        Assert.Throws<InvalidOperationException>(() => { _ = another.SubmitOffer(offer); });
        var home = await player.SelectActorAsync(first.Selection, fixture.Grantee);
        var second = await player.SelectActorAsync(home.Selection, fixture.Owner);
        Assert.False(offer.Scope.BelongsTo(delivery, second));
        var before = fixture.State.ContentHash;
        Assert.Throws<InvalidOperationException>(() => { _ = client.SubmitOffer(offer); });
        Assert.Equal(before, fixture.State.ContentHash);
        Assert.False(second.IsReady);
    }

    [Fact]
    public async Task Trade_only_capture_omits_other_facets_and_prepares_one_exact_paid_command()
    {
        await using var fixture = await SelectedQueryTradingFixture.Open();
        var player = await fixture.Acquire();
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Source.Owner);
        var client = new FarmClientSession(new SelectedPort(player), fixture.Source.Delivery(fixture.Source.Grantee),
            fixture.Market.MerchantActorId, new EntityId(0xdada02, 1), null);
        Assert.Null(observed.ControlledView!.Inventory);
        Assert.Null(observed.ControlledView.Combat);
        Assert.Null(observed.ControlledView.Production);
        Assert.Empty(client.CreateOffers(observed));
        await player.SubmitSelectedTradeInquiryAsync(observed.Selection,
            new LocalTradeInquiry(fixture.Market.MarketId, fixture.Market.Commodity, LocalTradeDirection.Buy, 1));
        player.ResumeSelected(observed);
        await SessionFixture.Tick(fixture.Time);
        player.PauseSelected(observed.Selection);
        var quoted = player.ReadSelectedObservation();
        var quote = Assert.Single(quoted.ControlledView!.Trade!.Quotes);
        var before = fixture.State.ContentHash;
        var review = client.ReviewSelectedQuote(quoted, quote, 1);
        var command = review.ToCommand(review.CommandId!.Value);
        var acceptance = Assert.IsType<AcceptSpotQuoteIntent>(command.Intent);
        Assert.Equal(quote.QuoteId, acceptance.Acceptance.QuoteId);
        Assert.Equal(quote.QuoteHash, acceptance.Acceptance.QuoteHash);
        Assert.Equal(quoted.Selection.ControlWitness, command.ControlWitness);
        Assert.Equal(fixture.Source.Owner, command.ActorId);
        Assert.Equal(fixture.Source.Delivery(fixture.Source.Grantee).PrincipalId, command.PrincipalId);
        Assert.Equal(before, fixture.State.ContentHash);
        var home = await player.SelectActorAsync(quoted.Selection, fixture.Source.Grantee);
        Assert.Throws<InvalidOperationException>(() => { _ = client.SubmitOffer(review); });
        Assert.Null(home.ControlledView);
    }

    private sealed class SelectedPort(LocalPlayerConnection player) : IPlayerSession, ISelectedPlayerSession
    {
        public bool SupportsControlPermissions => true;
        public bool SupportsActorSelection => true;
        public Task Completion => player.HostCompletion;
        public LocalWorldView ReadView() => player.ReadView();
        public LocalWorldRunState ReadRunState() => player.ReadRunState();
        public PlayerControlStatus ReadControlStatus() => player.ReadControlStatus();
        public void SetReady(bool ready) => throw new InvalidOperationException("Unbound readiness is forbidden by this fixture.");
        public void SetMovement(long dx, long dy) => throw new InvalidOperationException("Unbound movement is forbidden by this fixture.");
        public Task<WorldActionReceipt> SubmitAction(WorldActionCommand command) => throw new InvalidOperationException("Unbound action is forbidden by this fixture.");
        public Task<LocalTradeInquiryView> SubmitTradeInquiryAsync(LocalTradeInquiry inquiry) => throw new InvalidOperationException();
        public Task<LocalTradeInquiryView?> CancelTradeInquiryAsync() => throw new InvalidOperationException();
        public LocalTradeInquiryView? ReadTradeInquiry() => player.ReadTradeInquiry();
        public Task<PaidQuoteView?> InspectPaidQuoteAsync(EntityId id) => player.InspectPaidQuoteAsync(id);
        public Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId id) => player.InspectMaterialProposalAsync(id);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public LocalSelectedPlayerObservation ReadSelectedObservation() => player.ReadSelectedObservation();
        public Task<LocalControlPermissionBoard> ReadControlPermissionsAsync() => player.ReadControlPermissionsAsync();
        public Task<WorldActionReceipt> SubmitControlPermissionAsync(WorldActionCommand command) => player.SubmitControlPermissionAsync(command);
        public Task<WorldControlWitness> AcquireControlAsync(LocalControlAcquisition observed) => player.AcquireControlAsync(observed);
        public Task ReleaseControlAsync(WorldControlWitness observed) => player.ReleaseControlAsync(observed);
        public Task<LocalSelectedPlayerObservation> SelectActorAsync(LocalActorSelection observed, EntityId actorId) => player.SelectActorAsync(observed, actorId);
        public void ResumeSelected(LocalSelectedPlayerObservation observed) => player.ResumeSelected(observed);
        public void PauseSelected(LocalActorSelection observed) => player.PauseSelected(observed);
        public void SetSelectedMovement(LocalActorSelection observed, long dx, long dy) => player.SetSelectedMovement(observed, dx, dy);
        public Task<WorldActionReceipt> SubmitSelectedAction(LocalActorSelection observed, WorldActionCommand command) => player.SubmitSelectedAction(observed, command);
        public Task<LocalSelectedQueryResult<LocalTradeInquiryView>> SubmitSelectedTradeInquiryAsync(LocalActorSelection observed, LocalTradeInquiry inquiry) => player.SubmitSelectedTradeInquiryAsync(observed, inquiry);
        public Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> CancelSelectedTradeInquiryAsync(LocalActorSelection observed) => player.CancelSelectedTradeInquiryAsync(observed);
        public Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> ReadSelectedTradeInquiryAsync(LocalActorSelection observed) => player.ReadSelectedTradeInquiryAsync(observed);
        public Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedOpenPaidQuoteAsync(LocalActorSelection observed, EntityId id) => player.InspectSelectedOpenPaidQuoteAsync(observed, id);
        public Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedOpenMaterialProposalAsync(LocalActorSelection observed, EntityId id) => player.InspectSelectedOpenMaterialProposalAsync(observed, id);
        public Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedPaidQuoteHistoryAsync(LocalActorSelection observed, EntityId id) => player.InspectSelectedPaidQuoteHistoryAsync(observed, id);
        public Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedMaterialProposalHistoryAsync(LocalActorSelection observed, EntityId id) => player.InspectSelectedMaterialProposalHistoryAsync(observed, id);
    }
}
