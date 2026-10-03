using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.RoadThreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.SelectedQueries;

public sealed class SelectedInquiryTradingTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Hostile_opportunity_against_the_home_human_precedes_selected_trade_without_consuming_the_inquiry()
    {
        await using var fixture = await SelectedQueryTradingFixture.Open(keepGranteeNearHelper: true);
        var player = await fixture.Acquire();
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Source.Owner);
        var before = fixture.State;
        var entry = Assert.IsType<WorldActionCommand>(LocalRoadThreatController.SelectPlayerEntry(before, fixture.Source.Commands.NextId));
        Assert.Equal(fixture.Source.Other, entry.ActorId);
        Assert.Equal(fixture.Source.Journey.LaterScenario, Assert.IsType<EnterEncounterIntent>(entry.Intent).ScenarioId);
        var inquiry = new LocalTradeInquiry(fixture.Market.MarketId, fixture.Market.Commodity, LocalTradeDirection.Buy, 1);
        await player.SubmitSelectedTradeInquiryAsync(selected.Selection, inquiry);
        player.ResumeSelected(selected);
        await SessionFixture.Tick(fixture.Time);
        player.PauseSelected(selected.Selection);
        var after = fixture.State;
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
        Assert.Equal(EncounterLifecyclePhase.Confrontation, after.EncounterLifecycle!.Phase);
        Assert.Equal(fixture.Source.Journey.LaterScenario, after.EncounterLifecycle.ScenarioId);
        Assert.Equal(fixture.Source.Grantee,
            after.EncounterLifecycle.GetScenario(after.EncounterLifecycle.ScenarioId!.Value).TargetActorId);
        Assert.Equal(WorldControllerMode.Human, after.Controllers!.Get(fixture.Source.Grantee).Mode);
        Assert.Equal(before.Inventory.Custody.ContentHash, after.Inventory.Custody.ContentHash);
        AssertCurrencyUnchanged(before.Progression!.Currency, after.Progression!.Currency);
        Assert.Equal(before.Coordination!.PaidExchange!.ContentHash, after.Coordination!.PaidExchange!.ContentHash);
        Assert.Equal(LocalTradeInquiryState.Waiting, (await player.ReadSelectedTradeInquiryAsync(selected.Selection)).Value!.State);
        Assert.Equal(LocalTradeInquiryState.Cancelled, (await player.CancelSelectedTradeInquiryAsync(selected.Selection)).Value!.State);
        Assert.False(player.Ready);
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    [Fact]
    public async Task Merchant_answers_selected_owner_under_its_own_delivery_without_exposing_the_offer_as_history()
    {
        await using var fixture = await SelectedQueryTradingFixture.Open();
        var player = await fixture.Acquire();
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Source.Owner);
        var before = fixture.State;
        var inquiry = new LocalTradeInquiry(fixture.Market.MarketId, fixture.Market.Commodity, LocalTradeDirection.Buy, 1);
        var waiting = await player.SubmitSelectedTradeInquiryAsync(selected.Selection, inquiry);
        Assert.Equal(LocalTradeInquiryState.Waiting, waiting.Value.State);
        Assert.Same(before, fixture.State);
        Assert.Null(LocalRoadThreatController.SelectPlayerEntry(before, fixture.Source.Commands.NextId));
        player.ResumeSelected(selected);
        await SessionFixture.Tick(fixture.Time);
        player.PauseSelected(selected.Selection);
        var answer = await player.ReadSelectedTradeInquiryAsync(selected.Selection);
        answer.ValidateFor(player.ReadSelectedObservation(), LocalSelectedQueryOperation.ReadTradeInquiry);
        output.WriteLine($"Trading cutoff: revision {before.Revision}->{fixture.State.Revision}, steps {before.AdvancingSteps}->{fixture.State.AdvancingSteps}, phase {fixture.State.EncounterLifecycle!.Phase}, hold {fixture.State.HasReadingHold}, inquiry {answer.Value?.State}.");
        Assert.Equal(LocalTradeInquiryState.Offered, answer.Value!.State);
        var quoteId = answer.Value.QuoteId!.Value;
        var open = await player.InspectSelectedOpenPaidQuoteAsync(selected.Selection, quoteId);
        var quote = Assert.IsType<PaidQuoteView>(open.Value);
        Assert.Equal(PaidQuoteStatus.Open, quote.Status);
        Assert.Equal(fixture.Source.Owner, quote.CounterpartyActorId);
        Assert.Equal(fixture.Market.MerchantActorId, quote.IssuerActorId);
        Assert.Equal(fixture.Market.MarketId, quote.MarketId);
        Assert.Null((await player.InspectSelectedPaidQuoteHistoryAsync(selected.Selection, quoteId)).Value);
        Assert.Null(await player.InspectPaidQuoteAsync(quoteId)); // The grantee's permanent home is not a participant.
        Assert.Equal(before.Inventory.Custody.ContentHash, fixture.State.Inventory.Custody.ContentHash);
        AssertCurrencyUnchanged(before.Progression!.Currency, fixture.State.Progression!.Currency);
        Assert.Equal(before.AdvancingSteps + 1, fixture.State.AdvancingSteps);
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
        await fixture.Stop();
        using (var reopened = SqliteWorldAuthority.OpenExisting(fixture.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            var restored = reopened.ReadStateForHost();
            Assert.Equal(WorldSnapshotCodec.Encode(restored),
                WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(restored.Revision).State));
        }
        using var database = WorldDatabase.Open(fixture.Path, false);
        var issued = Assert.Single(database.ReadJournal().SelectMany(row => WorldStepCodec.Decode(row.AcceptedBatch).Actions),
            command => command.Intent is IssueMarketQuoteIntent intent && intent.Quote.QuoteId == quoteId);
        Assert.Equal(fixture.Market.MerchantActorId, issued.ActorId);
        Assert.Equal(fixture.Source.Delivery(issued.ActorId).PrincipalId, issued.PrincipalId);
        Assert.Equal(fixture.Source.Delivery(issued.ActorId).SessionId, issued.OriginSessionId);
        Assert.Null(issued.ControlWitness);
    }
    private static void AssertCurrencyUnchanged(CurrencyLedger before, CurrencyLedger after)
    {
        Assert.Equal((before.WorldId, before.BranchId, before.Revision, before.AdmittedSupply,
                before.Currency.DefinitionId, before.Currency.Version),
            (after.WorldId, after.BranchId, after.Revision, after.AdmittedSupply,
                after.Currency.DefinitionId, after.Currency.Version));
        Assert.Equal(before.Accounts.Select(account => (account.AccountId, account.Kind, account.ActorId, account.Balance, account.Revision)),
            after.Accounts.Select(account => (account.AccountId, account.Kind, account.ActorId, account.Balance, account.Revision)));
    }

}
