using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.HistoryQueries;

public sealed class ArchivedQuoteReplayTests(ArchivedQuoteReplayFixture fixture)
    : IClassFixture<ArchivedQuoteReplayFixture>
{
    [Theory]
    [InlineData(WorldPaidQuoteOutcomeKind.Filled)]
    [InlineData(WorldPaidQuoteOutcomeKind.Withdrawn)]
    [InlineData(WorldPaidQuoteOutcomeKind.Expired)]
    [InlineData(WorldPaidQuoteOutcomeKind.Superseded)]
    public void Archived_outcome_retains_its_actual_publishing_root_and_verified_later_head(WorldPaidQuoteOutcomeKind kind)
    {
        ArchivedQuoteReplayFixture.ExpectedQuote expected = fixture.Expected(kind);
        using var files = fixture.Copy();
        byte[] original = File.ReadAllBytes(files.Path);
        RecordedWorldPaidQuoteOutcome recorded;
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            recorded = new WorldJournalRecovery().ReadPaidQuoteOutcome(database, expected.Outcome.Quote.QuoteId);
            WorldPaidQuoteOutcome outcome = Assert.IsType<WorldPaidQuoteOutcome>(recorded.Outcome);
            WorldSnapshot publishing = Assert.IsType<WorldSnapshot>(recorded.PublishingState);
            Assert.Equal(expected.Outcome.Encode(), outcome.Encode());
            Assert.Equal(WorldSnapshotCodec.Encode(expected.State), WorldSnapshotCodec.Encode(publishing));
            Assert.Equal(expected.State.ContentHash, publishing.ContentHash);
            Assert.Equal(outcome.WorldSequence, publishing.Revision);
            Assert.True(publishing.Revision < fixture.ArchiveHead.Revision);
            Assert.Equal(fixture.Head.Revision, recorded.SourceHeadRevision);
            Assert.Equal(fixture.Head.ContentHash, recorded.SourceHeadHash);
            Assert.Equal(outcome.Encode(), publishing.Coordination!.PaidExchange!.FindOutcome(outcome.Quote.QuoteId)!.Encode());
            if (kind == WorldPaidQuoteOutcomeKind.Expired)
            {
                Assert.Null(outcome.CommandId);
                Assert.Null(outcome.CommandOrdinal);
            }
        }
        // The detached result remains exact after the replay lease has ended.
        Assert.Equal(expected.State.ContentHash, recorded.PublishingState!.ContentHash);
        Assert.Equal(original, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Archived_market_provenance_and_participant_projection_use_the_actual_fill_root()
    {
        ArchivedQuoteReplayFixture.ExpectedQuote expected = fixture.Expected(WorldPaidQuoteOutcomeKind.Filled);
        EntityId quoteId = expected.Outcome.Quote.QuoteId;
        MarketQuoteRecord market = Assert.IsType<MarketQuoteRecord>(expected.State.Coordination!.Markets!.FindQuote(quoteId));
        Assert.Null(fixture.Head.Coordination!.Markets!.FindQuote(quoteId));
        using var files = fixture.Copy();
        byte[] original = File.ReadAllBytes(files.Path);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var journal = authority.InspectForHost().Journal;
            foreach (EntityId participant in new[] { fixture.Player, fixture.Issuer })
            {
                PaidQuoteView view = authority.ReadRecordedPaidQuoteForController(quoteId, participant,
                    fixture.Delivery(participant), (state, outcome) =>
                    {
                        Assert.Equal(WorldSnapshotCodec.Encode(expected.State), WorldSnapshotCodec.Encode(state));
                        Assert.Equal(expected.Outcome.Encode(), Assert.IsType<WorldPaidQuoteOutcome>(outcome).Encode());
                        Assert.Equal(market.Encode(), state.Coordination!.Markets!.FindQuote(quoteId)!.Encode());
                        return Assert.IsType<PaidQuoteView>(PaidQuoteProjection.ProjectRecorded(state, outcome, quoteId, participant));
                    });
                Assert.Equal(PaidQuoteStatus.Filled, view.Status);
                Assert.Equal(fixture.Market, view.MarketId);
                Assert.Equal(expected.Outcome.Quote.Quote.ContentHash, view.QuoteHash);
                Assert.Equal(expected.State.Revision, view.ClosedAtSequence);
                Assert.Equal(1UL, view.FilledQuantity);
                Assert.Equal(participant == fixture.Issuer, view.ViewerIsIssuer);
                Assert.Null(view.ObservedIssuerRevision);
                ScheduledTradingWorld.AssertCarriesNoDelivery(view, fixture.Delivery(fixture.Outsider),
                    fixture.Delivery(participant == fixture.Player ? fixture.Issuer : fixture.Player));
            }
            Assert.Equal(fixture.Head.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(journal, authority.InspectForHost().Journal);
        }
        Assert.Equal(original, File.ReadAllBytes(files.Path));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Unknown_live_outcome_and_open_quote_return_no_archived_outcome_or_publishing_root(int subject)
    {
        EntityId quoteId = subject switch
        {
            0 => fixture.UnknownId,
            1 => fixture.LiveOutcomeId,
            _ => fixture.OpenQuoteId
        };
        using var files = fixture.Copy();
        byte[] original = File.ReadAllBytes(files.Path);
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            RecordedWorldPaidQuoteOutcome recorded = new WorldJournalRecovery().ReadPaidQuoteOutcome(database, quoteId);
            Assert.Null(recorded.Outcome);
            Assert.Null(recorded.PublishingState);
            Assert.Equal(fixture.Head.Revision, recorded.SourceHeadRevision);
            Assert.Equal(fixture.Head.ContentHash, recorded.SourceHeadHash);
        }
        Assert.Equal(original, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Authenticated_outsider_and_unknown_identity_disclose_only_the_current_root_and_no_outcome()
    {
        using var files = fixture.Copy();
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        EntityId archivedId = fixture.Expected(WorldPaidQuoteOutcomeKind.Filled).Outcome.Quote.QuoteId;
        foreach (EntityId quoteId in new[] { archivedId, fixture.UnknownId })
        {
            PaidQuoteView? result = authority.ReadRecordedPaidQuoteForController(quoteId, fixture.Outsider,
                fixture.Delivery(fixture.Outsider), (state, outcome) =>
                {
                    Assert.Equal(WorldSnapshotCodec.Encode(fixture.Head), WorldSnapshotCodec.Encode(state));
                    Assert.Null(outcome);
                    return PaidQuoteProjection.ProjectRecorded(state, outcome, quoteId, fixture.Outsider);
                });
            Assert.Null(result);
        }
    }

    [Fact]
    public void Wrong_delivery_is_refused_before_projection_for_both_known_and_unknown_quotes()
    {
        using var files = fixture.Copy();
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        EntityId archivedId = fixture.Expected(WorldPaidQuoteOutcomeKind.Filled).Outcome.Quote.QuoteId;
        var journal = authority.InspectForHost().Journal;
        int projections = 0;
        TrustedCommandDelivery player = fixture.Delivery(fixture.Player);
        TrustedCommandDelivery outsider = fixture.Delivery(fixture.Outsider);
        TrustedCommandDelivery[] wrongDeliveries =
        [
            outsider,
            new(player.PrincipalId, outsider.SessionId),
            new(outsider.PrincipalId, player.SessionId)
        ];
        foreach (EntityId quoteId in new[] { archivedId, fixture.UnknownId })
        {
            foreach (TrustedCommandDelivery delivery in wrongDeliveries)
                Assert.Throws<AuthorityDispatchException>(() => authority.ReadRecordedPaidQuoteForController(quoteId,
                    fixture.Player, delivery, (_, _) => ++projections));
        }
        Assert.Equal(0, projections);
        Assert.Equal(fixture.Head.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Equal(journal, authority.InspectForHost().Journal);
    }

    [Theory]
    [InlineData("UPDATE world_commits SET outcomes=x'01' WHERE sequence=(SELECT max(sequence) FROM world_commits)")]
    [InlineData("UPDATE world_command_outcomes SET receipt=x'01' WHERE sequence=(SELECT max(sequence) FROM world_commits)")]
    [InlineData("DELETE FROM world_occupied_identity WHERE sequence=(SELECT max(sequence) FROM world_commits)")]
    [InlineData("INSERT INTO world_checkpoints SELECT sequence,advancing_steps,state,hash FROM world_head")]
    [InlineData("UPDATE world_head SET advancing_steps=x'0000000000000000'")]
    [InlineData("UPDATE world_material_care_segments SET state=x'01'")]
    [InlineData("DELETE FROM world_material_care_segments")]
    public void Captured_outcome_cannot_escape_later_corruption_or_repair_the_saved_evidence(string mutation)
    {
        using var files = fixture.Copy();
        files.Execute(mutation);
        byte[] corrupted = File.ReadAllBytes(files.Path);
        RecordedWorldPaidQuoteOutcome? returned = null;
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            // OpenExisting would reject first; exercise the historical reader itself against the damage.
            Assert.Throws<InvalidDataException>(() => returned = new WorldJournalRecovery().ReadPaidQuoteOutcome(database,
                fixture.Expected(WorldPaidQuoteOutcomeKind.Filled).Outcome.Quote.QuoteId));
        }
        Assert.Null(returned);
        Assert.Equal(corrupted, File.ReadAllBytes(files.Path));
    }
}
