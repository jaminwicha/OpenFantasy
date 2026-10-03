using System.Reflection;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.SelectedInspections;

public sealed class SelectedInspectionTests(SelectedInspectionFixture fixture) : IClassFixture<SelectedInspectionFixture>
{
    [Fact]
    public void Inventory_and_trade_read_current_participant_terms_but_do_not_confer_private_history()
    {
        using var files = fixture.Copy(history: false);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var witness = fixture.Witness(fixture.OpenHead);
        var quote = Assert.IsType<PaidQuoteView>(authority.ReadSelectedOpenPaidQuote(
            fixture.QuoteId, fixture.Owner, fixture.Delivery, witness));
        var proposal = Assert.IsType<MaterialProposalView>(authority.ReadSelectedOpenMaterialProposal(
            fixture.ProposalId, fixture.Owner, fixture.Delivery, witness));
        Assert.Equal(PaidQuoteStatus.Open, quote.Status);
        Assert.Equal(MaterialProposalStatus.Open, proposal.Status);
        Assert.Equal(fixture.Trade.OwnerMarket!.MarketId, quote.MarketId);
        Assert.NotNull(proposal.SenderPresenceRevision);
        Assert.Equal(fixture.ProposalId, proposal.PrepareAcceptance().ProposalId);
        foreach (var subject in new[] { fixture.QuoteId, fixture.UnknownId })
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedPaidQuoteHistory(
                subject, fixture.Owner, fixture.Delivery, witness));
        foreach (var subject in new[] { fixture.ProposalId, fixture.UnknownId })
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedMaterialProposalHistory(
                subject, fixture.Owner, fixture.Delivery, witness));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.OpenHead), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    [Theory]
    [InlineData(WorldControlCapabilities.Trade)]
    [InlineData(WorldControlCapabilities.Inventory)]
    public void Open_inspection_requires_its_own_scope_independently(WorldControlCapabilities capability)
    {
        using var files = fixture.Copy(history: false);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var witness = fixture.Reassign(authority, capability);
        if (capability == WorldControlCapabilities.Trade)
        {
            Assert.NotNull(authority.ReadSelectedOpenPaidQuote(fixture.QuoteId, fixture.Owner, fixture.Delivery, witness));
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedOpenMaterialProposal(
                fixture.ProposalId, fixture.Owner, fixture.Delivery, witness));
        }
        else
        {
            Assert.NotNull(authority.ReadSelectedOpenMaterialProposal(fixture.ProposalId, fixture.Owner, fixture.Delivery, witness));
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedOpenPaidQuote(
                fixture.QuoteId, fixture.Owner, fixture.Delivery, witness));
        }
    }

    [Fact]
    public void Live_terminal_proposal_history_also_strips_presence_and_never_returns_open_terms()
    {
        using var files = fixture.Copy(history: false);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var witness = fixture.Reassign(authority, WorldControlCapabilities.PrivateHistory);
        Assert.Null(authority.ReadSelectedMaterialProposalHistory(fixture.ProposalId, fixture.Owner, fixture.Delivery, witness));
        fixture.Trade.Submit(authority, fixture.Trade.Control.Commands.Command(authority.ReadStateForHost(),
            fixture.Trade.Helper, new WithdrawMaterialProposalIntent(fixture.ProposalId)));
        Assert.Equal(0UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
        var view = Assert.IsType<MaterialProposalView>(authority.ReadSelectedMaterialProposalHistory(
            fixture.ProposalId, fixture.Owner, fixture.Delivery, witness));
        Assert.Equal(MaterialProposalStatus.Withdrawn, view.Status);
        Assert.NotNull(view.ClosedAtSequence);
        Assert.Null(view.SenderPresenceRevision);
        Assert.Throws<InvalidOperationException>(() => view.PrepareAcceptance());
        Assert.Null(authority.ReadSelectedOpenMaterialProposal(fixture.ProposalId, fixture.Owner,
            fixture.Trade.Control.Delivery(fixture.Owner), null));
    }

    [Fact]
    public void Tactical_time_can_make_open_books_due_but_neither_actionable_inspection_nor_terminal_history_discloses_them()
    {
        using var files = fixture.Copy(history: false);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var control = fixture.Trade.Control;
        control.Revoke(authority);
        var state = control.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected);
        var scenario = state.EncounterLifecycle!.GetScenario(control.Journey.FirstScenario);
        control.Journey.WalkTo(authority, fixture.Owner,
            control.Journey.Beside(state, fixture.Owner, scenario.TargetActorId));
        state = fixture.Trade.Ready(authority);
        for (int step = 0; scenario.Combatants.Where(value => value.Side != 0)
            .Any(value => !WorldActorAvailability.CanCooperate(state, value.ActorId)); step++)
        {
            Assert.True(step < 64);
            state = control.Journey.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        }

        ulong expires = checked(state.AdvancingSteps + 4);
        var prepared = fixture.Trade.Offer(state, fixture.Trade.Helper, fixture.Owner,
            fixture.Trade.Lot(state, fixture.Trade.Helper));
        var terms = Assert.IsType<ProposeMaterialIntent>(prepared.Intent).Terms;
        var offer = control.Commands.Command(state, fixture.Trade.Helper, new ProposeMaterialIntent(terms, expires));
        fixture.Trade.Submit(authority, offer);
        var proposalId = WorldMaterialProposal.DeriveIdentity(offer.CommandId);
        state = authority.ReadStateForHost();
        var (issueBasis, quoteBasis) = fixture.Trade.Quote(state, fixture.Trade.Helper, fixture.Owner,
            fixture.Trade.Lot(state, fixture.Trade.Helper));
        var quote = new SpotQuote(quoteBasis.QuoteId, quoteBasis.WorldId, quoteBasis.BranchId,
            quoteBasis.IssuerActorId, quoteBasis.IssuerPrincipalId, quoteBasis.IssuerSessionId,
            quoteBasis.CounterpartyActorId, quoteBasis.Side, quoteBasis.LotKind, quoteBasis.LotId,
            quoteBasis.Definition, quoteBasis.ExpectedLotRevision, quoteBasis.SellerCustodianId,
            quoteBasis.BuyerCustodianId, quoteBasis.CurrencyDefinitionId, quoteBasis.CurrencyVersion,
            quoteBasis.SellerAccountId, quoteBasis.BuyerAccountId, quoteBasis.UnitPrice, quoteBasis.Fees,
            quoteBasis.QuantityAllowance, quoteBasis.MarketRevision, quoteBasis.IssuedAtStep, expires);
        var issue = new WorldActionCommand(issueBasis.CommandId, issueBasis.PrincipalId, issueBasis.OriginSessionId,
            issueBasis.ActorId, issueBasis.WorldId, issueBasis.BranchId, issueBasis.ExpectedActorRevision,
            new IssueSpotQuoteIntent(quote));
        fixture.Trade.Submit(authority, issue);
        control.Grant(authority, WorldControlCapabilities.All);
        control.LoseAndExpire(authority);
        state = control.Acquire(authority);
        var witness = fixture.Witness(state);
        control.Apply(authority, WorldBatchKind.EncounterLifecycle,
            control.Delegated(state, control.Commands.Enter(state, scenario.DefinitionId).Intent));
        for (int stage = 0; authority.ReadStateForHost().EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; stage++)
        {
            Assert.True(stage < 3);
            state = authority.ReadStateForHost();
            authority.Commit(control.Commands.Batch(state, WorldBatchKind.EncounterLifecycle));
        }

        for (int boundary = 0; authority.ReadStateForHost().AdvancingSteps < expires; boundary++)
        {
            Assert.True(boundary < 64, "The two expiry steps must be earned by actual tactical ticks.");
            state = authority.ReadStateForHost();
            var chosen = state.Pacing!.Encounter!.Decisions.Select(value => value.ActorId).ToHashSet();
            var due = WorldEncounterActions.DueActors(state);
            var choices = due.Where(value => !chosen.Contains(value.ActorId)).Select(value =>
            {
                var waiting = control.Commands.Wait(state, value.ActorId);
                return value.ActorId == fixture.Owner ? control.Delegated(state, waiting.Intent) : waiting;
            }).ToArray();
            var kind = choices.Length != 0 ? WorldBatchKind.EncounterDecision
                : due.Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
            var decision = authority.Commit(control.Commands.Batch(state, kind, choices));
            Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        }

        state = authority.ReadStateForHost();
        Assert.Equal(expires, state.AdvancingSteps);
        Assert.True(state.Coordination!.Proposals!.FindOpen(proposalId)!.IsDue(state.AdvancingSteps));
        Assert.True(state.Coordination.PaidExchange!.FindOpen(quote.QuoteId)!.IsDue(state.AdvancingSteps));
        Assert.Null(state.Coordination.Proposals.FindOutcome(proposalId));
        Assert.Null(state.Coordination.PaidExchange.FindOutcome(quote.QuoteId));
        var displayedProposal = MaterialProposalService.Project(state, fixture.Owner).Single(value => value.ProposalId == proposalId);
        var displayedQuote = PaidQuoteProjection.Project(state, fixture.Owner).Single(value => value.QuoteId == quote.QuoteId);
        Assert.Equal(MaterialProposalStatus.Expired, displayedProposal.Status);
        Assert.Equal(PaidQuoteStatus.Expired, displayedQuote.Status);
        Assert.Null(displayedProposal.ClosedAtSequence);
        Assert.Null(displayedQuote.ClosedAtSequence);
        Assert.Null(authority.ReadSelectedOpenMaterialProposal(proposalId, fixture.Owner, fixture.Delivery, witness));
        Assert.Null(authority.ReadSelectedOpenPaidQuote(quote.QuoteId, fixture.Owner, fixture.Delivery, witness));
        Assert.Null(authority.ReadSelectedMaterialProposalHistory(proposalId, fixture.Owner, fixture.Delivery, witness));
        Assert.Null(authority.ReadSelectedPaidQuoteHistory(quote.QuoteId, fixture.Owner, fixture.Delivery, witness));
    }

    [Fact]
    public void Private_history_alone_returns_archived_terminal_terms_and_original_market_after_full_original_genesis_reopen()
    {
        using var files = fixture.Copy(history: true);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var witness = fixture.Witness(fixture.HistoryHead);
        Assert.Equal(WorldControlCapabilities.PrivateHistory, witness.Capabilities);
        Assert.Null(authority.ReadStateForHost().Coordination!.Markets!.FindQuote(fixture.QuoteId));
        var quote = Assert.IsType<PaidQuoteView>(authority.ReadSelectedPaidQuoteHistory(
            fixture.QuoteId, fixture.Owner, fixture.Delivery, witness));
        var proposal = Assert.IsType<MaterialProposalView>(authority.ReadSelectedMaterialProposalHistory(
            fixture.ProposalId, fixture.Owner, fixture.Delivery, witness));
        Assert.Equal(PaidQuoteStatus.Withdrawn, quote.Status);
        Assert.Equal(fixture.ClosedQuote.QuoteHash, quote.QuoteHash);
        Assert.Equal(fixture.ClosedQuote.MarketId, quote.MarketId);
        Assert.Equal(fixture.ClosedQuote.ClosedAtSequence, quote.ClosedAtSequence);
        Assert.Null(quote.ObservedIssuerRevision);
        Assert.Equal(fixture.ClosedProposal, proposal);
        Assert.Equal(MaterialProposalStatus.Withdrawn, proposal.Status);
        Assert.NotNull(proposal.ClosedAtSequence);
        Assert.Null(proposal.SenderPresenceRevision);
        Assert.Throws<InvalidOperationException>(() => proposal.PrepareAcceptance());
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedOpenPaidQuote(
            fixture.LaterOpenQuoteId, fixture.Owner, fixture.Delivery, witness));
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedOpenMaterialProposal(
            fixture.LaterOpenProposalId, fixture.Owner, fixture.Delivery, witness));
        Assert.Null(authority.ReadSelectedPaidQuoteHistory(fixture.LaterOpenQuoteId, fixture.Owner, fixture.Delivery, witness));
        Assert.Null(authority.ReadSelectedMaterialProposalHistory(fixture.LaterOpenProposalId, fixture.Owner, fixture.Delivery, witness));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.HistoryHead), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    [Fact]
    public void Missing_and_another_participants_subjects_are_indistinguishable_and_open_reads_never_replay_history()
    {
        using var files = fixture.Copy(history: true);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var ownDelivery = fixture.Trade.Control.Delivery(fixture.Owner);
        int historyEntries = 0;
        authority.RecordedQuoteReadHoldsGate = () => historyEntries++;
        foreach (var subject in new[] { fixture.QuoteId, fixture.UnknownId })
        {
            Assert.Null(authority.ReadSelectedOpenPaidQuote(subject, fixture.Owner, ownDelivery, null));
            Assert.Null(authority.ReadSelectedPaidQuoteHistory(subject, fixture.Grantee, fixture.Delivery, null));
        }
        foreach (var subject in new[] { fixture.ProposalId, fixture.UnknownId })
        {
            Assert.Null(authority.ReadSelectedOpenMaterialProposal(subject, fixture.Owner, ownDelivery, null));
            Assert.Null(authority.ReadSelectedMaterialProposalHistory(subject, fixture.Grantee, fixture.Delivery, null));
        }
        Assert.Equal(2, historyEntries);
        // The unchanged legacy APIs still require permanent control; a selected witness does not widen them.
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadRecordedPaidQuoteForController(
            fixture.QuoteId, fixture.Owner, fixture.Delivery, (_, _) => true));
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadRecordedProposalForController(
            fixture.ProposalId, fixture.Owner, fixture.Delivery, (_, _) => true));
    }

    [Fact]
    public void Revoked_replaced_and_foreign_deliveries_refuse_before_any_recorded_lookup()
    {
        using var files = fixture.Copy(history: false);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var old = fixture.Witness(fixture.OpenHead);
        fixture.Trade.Control.Revoke(authority);
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedOpenPaidQuote(
            fixture.QuoteId, fixture.Owner, fixture.Delivery, old));
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedOpenMaterialProposal(
            fixture.ProposalId, fixture.Owner, fixture.Delivery, old));
        fixture.Trade.Control.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected);
        fixture.Trade.Control.Grant(authority, WorldControlCapabilities.Inventory | WorldControlCapabilities.Trade
            | WorldControlCapabilities.PrivateHistory);
        fixture.Trade.Control.LoseAndExpire(authority);
        var current = fixture.Witness(fixture.Trade.Control.Acquire(authority));
        Assert.NotEqual(old, current);
        int lookups = 0;
        authority.RecordedQuoteReadHoldsGate = () => lookups++;
        var foreign = fixture.Trade.Control.Delivery(fixture.Trade.Helper);
        var wrongSession = new TrustedCommandDelivery(fixture.Delivery.PrincipalId, foreign.SessionId);
        foreach (var subject in new[] { fixture.QuoteId, fixture.UnknownId })
        {
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedPaidQuoteHistory(subject, fixture.Owner, fixture.Delivery, old));
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedPaidQuoteHistory(subject, fixture.Owner, foreign, current));
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedPaidQuoteHistory(subject, fixture.Owner, wrongSession, current));
        }
        foreach (var subject in new[] { fixture.ProposalId, fixture.UnknownId })
        {
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedMaterialProposalHistory(subject, fixture.Owner, fixture.Delivery, old));
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedMaterialProposalHistory(subject, fixture.Owner, foreign, current));
        }
        Assert.Equal(0, lookups);
        Assert.NotNull(authority.ReadSelectedOpenPaidQuote(fixture.QuoteId, fixture.Owner, fixture.Delivery, current));
        Assert.NotNull(authority.ReadSelectedOpenMaterialProposal(fixture.ProposalId, fixture.Owner, fixture.Delivery, current));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Archived_capture_cannot_escape_corrupted_later_history_and_failed_read_fences_authority(bool quote)
    {
        using var files = fixture.Copy(history: true);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var witness = fixture.Witness(fixture.HistoryHead);
        Assert.True(fixture.HistoryHead.Revision > fixture.ClosedQuote.ClosedAtSequence!.Value);
        Assert.True(fixture.HistoryHead.Revision > fixture.ClosedProposal.ClosedAtSequence!.Value);
        // Damage follows the requested terminal publication, after the authority has pinned its healthy head.
        CorruptLaterCommitThroughOwnedConnection(authority);
        var ownerDelivery = fixture.Trade.Control.Delivery(fixture.Owner);
        Assert.NotNull(authority.ReadSelectedOpenPaidQuote(fixture.LaterOpenQuoteId, fixture.Owner, ownerDelivery, null));
        Assert.NotNull(authority.ReadSelectedOpenMaterialProposal(fixture.LaterOpenProposalId, fixture.Owner, ownerDelivery, null));
        Assert.Null(authority.ReadSelectedOpenPaidQuote(fixture.QuoteId, fixture.Owner, ownerDelivery, null));
        Assert.Null(authority.ReadSelectedOpenMaterialProposal(fixture.ProposalId, fixture.Owner, ownerDelivery, null));
        var foreign = fixture.Trade.Control.Delivery(fixture.Trade.Helper);
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedPaidQuoteHistory(
            fixture.QuoteId, fixture.Owner, foreign, witness));
        Assert.Equal(fixture.HistoryHead.ContentHash, authority.ReadStateForHost().ContentHash);
        if (quote)
            Assert.Throws<InvalidDataException>(() => authority.ReadSelectedPaidQuoteHistory(
                fixture.QuoteId, fixture.Owner, fixture.Delivery, witness));
        else
            Assert.Throws<InvalidDataException>(() => authority.ReadSelectedMaterialProposalHistory(
                fixture.ProposalId, fixture.Owner, fixture.Delivery, witness));
        Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        Assert.Throws<InvalidOperationException>(() => authority.ReadSelectedOpenPaidQuote(
            fixture.LaterOpenQuoteId, fixture.Owner, fixture.Trade.Control.Delivery(fixture.Owner), null));
    }

    private static void CorruptLaterCommitThroughOwnedConnection(SqliteWorldAuthority authority)
    {
        // SQLite owns an exclusive lease. Mutate its actual connection after the healthy open, without
        // exposing a production corruption API or pretending a second writer can bypass that lease.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var database = Assert.IsType<WorldDatabase>(typeof(SqliteWorldAuthority).GetField("_database", flags)!.GetValue(authority));
        var lease = Assert.IsType<SqliteConnectionLease>(typeof(WorldDatabase).GetField("_lease", flags)!.GetValue(database));
        using var command = lease.CreateCommand(
            "UPDATE world_commits SET outcomes=x'01' WHERE sequence=(SELECT max(sequence) FROM world_commits)");
        Assert.Equal(1, command.ExecuteNonQuery());
    }
}
