using OpenFantasy.Client;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.SelectedQueries;

namespace OpenFantasy.Hosting.Tests.Client.SelectedTrading;

/// <summary>The owner earns the admitted quest reward before disconnecting and granting operated trade access.</summary>
internal static class SelectedMarketFunding
{
    internal static async Task EarnQuestReward(SelectedQueryTradingFixture fixture)
    {
        var actor = fixture.Source.Owner;
        ulong before = fixture.State.Progression!.Currency.GetActorAccount(actor).Balance;
        var quest = fixture.State.Quests!.Definitions.Single(value => value.GiverActorId == fixture.Source.Other);
        await using var owner = await fixture.Host.ConnectPlayerAsync(actor, fixture.Source.Delivery(actor));
        await SessionFixture.Tick(fixture.Time);
        owner.ResumeSelected(owner.ReadSelectedObservation());
        var offers = new FarmActionOffers(fixture.Source.Delivery(actor), quest.GiverActorId,
            quest.Item.DefinitionId, fixture.Market.Commodity.DefinitionId);
        foreach (var action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give,
            FarmSemanticAction.Give, FarmSemanticAction.CloseDialogue })
        {
            var observation = owner.ReadSelectedObservation();
            var offer = offers.Create(observation.HomeView)[action];
            Assert.True(offer.CanExecute, offer.BlockingReason);
            var command = offer.ToCommand(fixture.Source.Commands.NextId());
            var pending = owner.SubmitSelectedAction(observation.Selection, command);
            await SessionFixture.Tick(fixture.Time);
            await SessionFixture.Until(() => pending.IsCompleted);
            var receipt = await pending;
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(command.CommandId, receipt.CommandId);
        }
        owner.PauseSelected(owner.ReadSelectedObservation().Selection);
        Assert.Equal(checked(before + quest.Reward.CurrencyAmount), fixture.State.Progression!.Currency.GetActorAccount(actor).Balance);
        Assert.False(owner.ReadView().HasReadingHold);
    }
}
