using System.Reflection;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PrivateHistoryDirectory;

public sealed class PrivateHistoryDirectoryStorageTests(PrivateHistoryDirectoryFixture fixture)
    : IClassFixture<PrivateHistoryDirectoryFixture>
{
    [Fact]
    public void Tactical_due_but_unclosed_offers_are_absent_from_the_directory()
    {
        using var files = fixture.Copy();
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var trade = fixture.Trade;
        var control = trade.Control;
        control.Revoke(authority);
        var state = control.Event(authority, trade.Owner, WorldControllerEventKind.Reconnected);
        var scenario = state.EncounterLifecycle!.GetScenario(control.Journey.FirstScenario);
        control.Journey.WalkTo(authority, trade.Owner, control.Journey.Beside(state, trade.Owner, scenario.TargetActorId));
        state = trade.Ready(authority);
        for (int step = 0; scenario.Combatants.Where(value => value.Side != 0)
            .Any(value => !WorldActorAvailability.CanCooperate(state, value.ActorId)); step++)
        {
            Assert.True(step < 64);
            state = control.Journey.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        }
        ulong expires = checked(state.AdvancingSteps + 4);
        var prepared = trade.Offer(state, trade.Helper, trade.Owner, trade.Lot(state, trade.Helper));
        var terms = Assert.IsType<ProposeMaterialIntent>(prepared.Intent).Terms;
        var offer = control.Commands.Command(state, trade.Helper, new ProposeMaterialIntent(terms, expires));
        trade.Submit(authority, offer);
        EntityId proposalId = WorldMaterialProposal.DeriveIdentity(offer.CommandId);
        state = authority.ReadStateForHost();
        var (issueBasis, basis) = trade.Quote(state, trade.Helper, trade.Owner, trade.Lot(state, trade.Helper));
        var quote = new SpotQuote(basis.QuoteId, basis.WorldId, basis.BranchId, basis.IssuerActorId,
            basis.IssuerPrincipalId, basis.IssuerSessionId, basis.CounterpartyActorId, basis.Side, basis.LotKind,
            basis.LotId, basis.Definition, basis.ExpectedLotRevision, basis.SellerCustodianId, basis.BuyerCustodianId,
            basis.CurrencyDefinitionId, basis.CurrencyVersion, basis.SellerAccountId, basis.BuyerAccountId,
            basis.UnitPrice, basis.Fees, basis.QuantityAllowance, basis.MarketRevision, basis.IssuedAtStep, expires);
        trade.Submit(authority, new WorldActionCommand(issueBasis.CommandId, issueBasis.PrincipalId,
            issueBasis.OriginSessionId, issueBasis.ActorId, issueBasis.WorldId, issueBasis.BranchId,
            issueBasis.ExpectedActorRevision, new IssueSpotQuoteIntent(quote)));
        control.Grant(authority, WorldControlCapabilities.All);
        control.LoseAndExpire(authority);
        state = control.Acquire(authority);
        var witness = state.EffectiveControl!.Find(trade.Owner)!.Witness!;
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
            Assert.True(boundary < 64, "The expiry steps must be earned by actual tactical ticks.");
            state = authority.ReadStateForHost();
            var chosen = state.Pacing!.Encounter!.Decisions.Select(value => value.ActorId).ToHashSet();
            var due = WorldEncounterActions.DueActors(state);
            var choices = due.Where(value => !chosen.Contains(value.ActorId)).Select(value =>
            {
                var waiting = control.Commands.Wait(state, value.ActorId);
                return value.ActorId == trade.Owner ? control.Delegated(state, waiting.Intent) : waiting;
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
        var page = authority.ReadSelectedPrivateHistory(trade.Owner, control.Delivery(trade.Grantee), witness, new());
        Assert.DoesNotContain(page.Entries, value => value.SubjectId == proposalId || value.SubjectId == quote.QuoteId);
        Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public void Original_genesis_reopen_discovers_every_archived_subject_and_preserves_mixed_kind_ties_across_pages()
    {
        using var files = fixture.Copy();
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var before = authority.ReadStateForHost().ContentHash;
        LocalPrivateHistoryEntry[] entries = ReadAll(authority);
        Assert.Equal(fixture.AllSubjects.Order(), entries.Select(value => value.SubjectId).Order());
        Assert.Equal(entries.Length, entries.Select(value => value.SubjectId).Distinct().Count());
        var ties = entries.Where(value => value.ClosedAtSequence == fixture.TiedSequence).ToArray();
        Assert.Equal(19, ties.Length);
        Assert.Equal(fixture.TiedSubjects.Order(), ties.Select(value => value.SubjectId).Order());
        Assert.Equal(ties.OrderBy(value => value.Kind).ThenBy(value => value.SubjectId), ties);
        Assert.All(ties, value => Assert.True(value.PaidQuoteStatus == PaidQuoteStatus.Expired
            || value.MaterialProposalStatus == MaterialProposalStatus.Expired));
        Assert.All(entries, value => Assert.Equal(fixture.Trade.Helper, value.CounterpartyActorId));
        Assert.Equal(before, authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public void Continuation_excludes_new_publications_and_independent_refresh_discovers_them()
    {
        using var files = fixture.Copy();
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var first = Read(authority, new());
        Assert.NotNull(first.Next);
        EntityId appended = fixture.Append(authority);
        var retained = first.Entries.Concat(ReadAll(authority, new(first.Next))).ToArray();
        Assert.Equal(fixture.AllSubjects.Order(), retained.Select(value => value.SubjectId).Order());
        Assert.DoesNotContain(retained, value => value.SubjectId == appended);
        Assert.Equal(appended, Read(authority, new()).Entries[0].SubjectId);
        Assert.True(authority.ReadStateForHost().Revision > first.ThroughRevision);
    }

    [Fact]
    public void Directory_permission_is_independent_and_foreign_history_is_empty_without_affecting_authority()
    {
        using var files = fixture.Copy();
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var trade = fixture.Trade;
        var witness = authority.ReadStateForHost().EffectiveControl!.Find(trade.Owner)!.Witness!;
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedPrivateHistory(trade.Owner,
            trade.Control.Delivery(trade.Grantee), witness, new()));
        var foreign = authority.ReadSelectedPrivateHistory(trade.Grantee, trade.Control.Delivery(trade.Grantee), null, new());
        Assert.Empty(foreign.Entries);
        Assert.Null(foreign.Next);
        trade.Control.Revoke(authority);
        trade.Control.Event(authority, trade.Owner, WorldControllerEventKind.Reconnected);
        trade.Control.Grant(authority, WorldControlCapabilities.PrivateHistory);
        trade.Control.LoseAndExpire(authority);
        var state = trade.Control.Acquire(authority);
        var current = state.EffectiveControl!.Find(trade.Owner)!.Witness!;
        var page = authority.ReadSelectedPrivateHistory(trade.Owner, trade.Control.Delivery(trade.Grantee), current, new());
        Assert.NotEmpty(page.Entries);
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedOpenPaidQuote(fixture.TiedSubjects[^1],
            trade.Owner, trade.Control.Delivery(trade.Grantee), current));
        trade.Control.Revoke(authority);
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadSelectedPrivateHistory(trade.Owner,
            trade.Control.Delivery(trade.Grantee), current, new()));
        Assert.NotEmpty(Read(authority, new()).Entries);
    }

    [Fact]
    public void Malformed_and_future_cursors_refuse_without_fencing_or_reading_history()
    {
        using var files = fixture.Copy();
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        int reads = 0;
        authority.PrivateHistoryReadHoldsGate = () => reads++;
        var before = authority.ReadStateForHost().ContentHash;
        foreach (var cursor in new[]
        {
            new LocalPrivateHistoryCursor(fixture.Head.Revision + 1, 1, LocalPrivateHistoryKind.PaidQuote, fixture.TiedSubjects[0]),
            new LocalPrivateHistoryCursor(1, 2, LocalPrivateHistoryKind.PaidQuote, fixture.TiedSubjects[0]),
            new LocalPrivateHistoryCursor(1, 1, (LocalPrivateHistoryKind)99, fixture.TiedSubjects[0]),
            new LocalPrivateHistoryCursor(0, 0, LocalPrivateHistoryKind.PaidQuote, fixture.TiedSubjects[0])
        })
            Assert.Throws<AuthorityDispatchException>(() => Read(authority, new(cursor)));
        Assert.Equal(0, reads);
        Assert.Equal(before, authority.ReadStateForHost().ContentHash);
        Assert.NotEmpty(Read(authority, new()).Entries);
    }

    [Fact]
    public void Corruption_after_cursor_ceiling_prevents_disclosure_and_fences_the_authority()
    {
        using var files = fixture.Copy();
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path, fixture.Genesis);
        var first = Read(authority, new());
        Assert.NotNull(first.Next);
        fixture.Append(authority);
        Assert.True(authority.ReadStateForHost().Revision > first.ThroughRevision);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var database = Assert.IsType<WorldDatabase>(typeof(SqliteWorldAuthority).GetField("_database", flags)!.GetValue(authority));
        var lease = Assert.IsType<SqliteConnectionLease>(typeof(WorldDatabase).GetField("_lease", flags)!.GetValue(database));
        using var command = lease.CreateCommand("UPDATE world_commits SET outcomes=x'01' WHERE sequence=(SELECT max(sequence) FROM world_commits)");
        Assert.Equal(1, command.ExecuteNonQuery());
        Assert.Throws<InvalidDataException>(() => Read(authority, new(first.Next)));
        Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
    }

    private LocalPrivateHistoryPage Read(SqliteWorldAuthority authority, LocalPrivateHistoryPageRequest request) =>
        authority.ReadSelectedPrivateHistory(fixture.Trade.Owner, fixture.Trade.Control.Delivery(fixture.Trade.Owner), null, request);

    private LocalPrivateHistoryEntry[] ReadAll(SqliteWorldAuthority authority, LocalPrivateHistoryPageRequest? initial = null)
    {
        List<LocalPrivateHistoryEntry> entries = [];
        LocalPrivateHistoryPageRequest request = initial ?? new();
        for (int count = 0; count < 10; count++)
        {
            var page = Read(authority, request);
            page.ValidateFor(request);
            entries.AddRange(page.Entries);
            if (page.Next is null) return entries.ToArray();
            request = new(page.Next);
        }
        throw new InvalidOperationException("The finite recorded fixture must finish within ten pages.");
    }
}
