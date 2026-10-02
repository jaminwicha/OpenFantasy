using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.HistoryQueries;

public sealed class WorldPaidQuoteReplaySelectionTests
{
    [Fact]
    public void Same_quote_terms_cannot_substitute_a_different_terminal_cause_or_sequence()
    {
        using var world = new ScheduledTradingWorld();
        var purchase = world.BuyOneFromWrenDirectly();
        WorldSnapshot publishing = world.State;
        WorldPaidQuoteOutcome filled = publishing.Coordination!.PaidExchange!.FindOutcome(purchase.Quote.QuoteId)!;
        var selection = new WorldPaidQuoteReplaySelection(purchase.Quote.QuoteId);
        selection.Observe(publishing);
        var changedCause = new WorldPaidQuoteOutcome(filled.Quote, WorldPaidQuoteOutcomeKind.Expired,
            filled.WorldSequence, null);
        var changedSequence = new WorldPaidQuoteOutcome(filled.Quote, WorldPaidQuoteOutcomeKind.Filled,
            filled.WorldSequence + 1, filled.Settlement);
        Assert.Equal(filled.Quote.ContentHash, changedCause.Quote.ContentHash);
        Assert.Equal(filled.Quote.ContentHash, changedSequence.Quote.ContentHash);
        Assert.Throws<InvalidDataException>(() => selection.ResolveArchived(changedCause));
        Assert.Throws<InvalidDataException>(() => selection.ResolveArchived(changedSequence));
        Assert.Same(publishing, selection.ResolveArchived(filled));
        Assert.Null(selection.ResolveArchived(null));
    }

    [Fact]
    public void A_later_root_carrying_the_same_outcome_cannot_replace_its_original_publication()
    {
        using var world = new ScheduledTradingWorld();
        var purchase = world.BuyOneFromWrenDirectly();
        WorldSnapshot publishing = world.State;
        WorldPaidQuoteOutcome filled = publishing.Coordination!.PaidExchange!.FindOutcome(purchase.Quote.QuoteId)!;
        WorldSnapshot later = world.Authority.Commit(new WorldStepBatch(world.Fixture.NextId(), publishing.WorldId,
            publishing.BranchId, publishing.Revision, publishing.AdvancingSteps, WorldBatchKind.AdvancingStep,
            [], [], publishing.ProfileVersion)).Candidate;
        Assert.NotNull(later.Coordination!.PaidExchange!.FindOutcome(purchase.Quote.QuoteId));
        var missingOriginal = new WorldPaidQuoteReplaySelection(purchase.Quote.QuoteId);
        missingOriginal.Observe(later);
        Assert.Throws<InvalidDataException>(() => missingOriginal.ResolveArchived(filled));
        var complete = new WorldPaidQuoteReplaySelection(purchase.Quote.QuoteId);
        complete.Observe(publishing);
        complete.Observe(later);
        Assert.Same(publishing, complete.ResolveArchived(filled));
    }
}
