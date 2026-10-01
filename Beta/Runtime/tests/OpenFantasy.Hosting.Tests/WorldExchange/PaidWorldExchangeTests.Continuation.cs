using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange;

public sealed partial class PaidWorldExchangeTests
{
    [Fact]
    public void Earned_payment_crafted_split_recraft_healing_and_buyback_keep_one_causal_world()
    {
        var access = new ConsentedWorldPublicationTests.DurableAccess();
        var genesis = WorldPaidExchangeAdmission.CreateFresh(access.OrdinaryGenesis);
        var ordinary = new OrdinaryCareWorldFixture(genesis);
        var journey = ordinary.Journey;
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var session = new CareWorldSession(authority);
            EarnReward(journey, session);
            var state = ordinary.ReachInjuredExploration(session);
            Assert.InRange(state.Progression!.GetActor(journey.Player).CurrentHp, 1,
                state.Progression.GetActor(journey.Player).Stats.MaxHp - 1);
            var initialLedger = state.Progression.Currency;
            var berries = state.Inventory.Custody.Lots.Single(value => value.CustodianId ==
                state.GetResident(journey.Helper).InventoryCustodianId && access.IsBerries(value));
            var craft = ConsentedWorldPublicationTests.DurableAccess.CraftFor(state, journey, journey.Helper,
                access.Bulk, new(IngredientLotKind.Source, berries.LotId, berries.Revision, 12));
            state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep, [craft])).Candidate;
            var producer = state.Production!.Batches.Single(value => value.CommandId == craft.CommandId);
            var stock = state.Production.Lots.Single(value => value.Output.BatchId == producer.BatchId);
            Assert.Equal(4UL, stock.Quantity);
            var (issue, quote) = ProducedQuote(state, journey, stock, issuerBuys: false, allowance: 2);
            state = Commit(session, state, issue, WorldActionRejection.None);
            var accept = AcceptCommand(state, journey.Player, quote, journey.Helper, 2, 2);
            state = Commit(session, state, accept, WorldActionRejection.None);
            var bought = state.Production!.Lots.Single(value => value.LotId == WorldPaidQuote.SplitChildOf(accept.CommandId));
            Assert.Equal((producer.BatchId, 0UL, 2UL), (bought.Output.BatchId, bought.Output.Start, bought.Output.End));
            Assert.Equal(initialLedger.GetActorAccount(journey.Player).Balance - 2,
                state.Progression!.Currency.GetActorAccount(journey.Player).Balance);
            var head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            session = new CareWorldSession(authority);
            state = authority.ReadStateForHost();
            Assert.Equal(head, state.ContentHash);
            var repack = ConsentedWorldPublicationTests.DurableAccess.CraftFor(state, journey, journey.Player,
                access.Repack, new(IngredientLotKind.Transformed, bought.LotId, bought.Revision, 1));
            state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep, [repack])).Candidate;
            var repacked = state.Production!.Batches.Single(value => value.CommandId == repack.CommandId);
            Assert.Equal((producer.BatchId, 0UL, 1UL),
                (repacked.Ingredients.Single().Output!.BatchId, repacked.Ingredients.Single().Output!.Start,
                 repacked.Ingredients.Single().Output!.End));
            var dose = state.Production.Lots.Single(value => value.Output.BatchId == repacked.BatchId);
            var prepared = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.Prepare(state,
                journey.Player, journey.Player, access.Self.DefinitionId, null));
            var use = new UseRestorativeItemIntent(prepared.RecipientActorId, prepared.UseDefinitionId,
                prepared.UseDefinitionVersion, prepared.PermissionId, prepared.PermissionGrantVersion,
                prepared.ExpectedPermissionRevision, prepared.ExpectedPatientPresenceRevision,
                prepared.ExpectedPatientProgressionRevision, prepared.ExpectedStorageRevision,
                prepared.ExpectedProductionRevision, new(IngredientLotKind.Transformed, dose.LotId, dose.Revision, 1));
            int hp = state.Progression!.GetActor(journey.Player).CurrentHp;
            var healing = journey.Command(state, journey.Player, use);
            state = Commit(session, state, healing, WorldActionRejection.None);
            Assert.True(state.Progression!.GetActor(journey.Player).CurrentHp > hp);
            Assert.Contains(state.ResourceHistory!.Evidence, value => value.CommandId == healing.CommandId);
            var remainder = state.Production!.Lots.Single(value => value.LotId == bought.LotId);
            Assert.Equal((1UL, 2UL), (remainder.Output.Start, remainder.Output.End));
            (issue, quote) = ProducedQuote(state, journey, remainder, issuerBuys: true, allowance: 1);
            state = Commit(session, state, issue, WorldActionRejection.None);
            var sell = AcceptCommand(state, journey.Player, quote, journey.Helper, 1, 1);
            var batch = journey.Batch(state, WorldBatchKind.AdvancingStep, [sell]);
            ExportBefore(authority, "produced", batch);
            state = CareRewardJourney.Commit(session, batch).Candidate;
            ExportAfter(authority, "produced", state);
            Assert.Equal(initialLedger.AdmittedSupply, state.Progression!.Currency.AdmittedSupply);
            Assert.Equal(initialLedger.GetActorAccount(journey.Player).Balance - 1,
                state.Progression.Currency.GetActorAccount(journey.Player).Balance);
            Assert.Equal(initialLedger.GetActorAccount(journey.Helper).Balance + 1,
                state.Progression.Currency.GetActorAccount(journey.Helper).Balance);
            Assert.Equal(state.GetResident(journey.Helper).InventoryCustodianId,
                state.Production!.Lots.Single(value => value.LotId == remainder.LotId).CustodianId);
            Assert.DoesNotContain(state.ResourceHistory!.History.Batches,
                value => value.CommandId == accept.CommandId || value.CommandId == sell.CommandId);
            Assert.Equal(2, state.Coordination!.PaidExchange!.Fills.Count());
            var finalHash = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(finalHash, authority.ReadStateForHost().ContentHash);
        }
        finally { authority.Dispose(); }
    }

    [Fact]
    public void Automatic_quote_cuts_carry_open_terms_then_archive_fills_without_reusing_identities()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var session = new CareWorldSession(authority);
            var state = EarnReward(journey, session);
            var reserve = state.Progression!.Currency.Accounts.Single(value => value.Kind == CurrencyAccountKind.Reserve);
            void ExpireOne()
            {
                var stock = SellableRawLot(state, journey.Helper);
                var (expired, _) = IssueCommand(state, journey.Helper, journey.Player, stock, 1, reserve.AccountId, 1, 1);
                state = Commit(session, state, expired, WorldActionRejection.None);
                state = session.Commit(journey.Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
            }
            for (int index = 0; index < WorldPaidExchangeBook.MaximumOutcomes - 2; index++) ExpireOne();
            Assert.Null(state.Coordination!.Anchor);
            Assert.Equal(62, state.Coordination.PaidExchange!.Outcomes.Count);
            var lot = SellableRawLot(state, journey.Helper);
            var (issue, quote) = IssueCommand(state, journey.Helper, journey.Player, lot, 1, reserve.AccountId, 1, 1024);
            state = Commit(session, state, issue, WorldActionRejection.None);
            var closing = state;
            var trigger = IssueCommand(state, journey.Helper, journey.Player, lot, 1, reserve.AccountId, 1, 1).Command;
            var cut = session.Commit(journey.Batch(state, WorldBatchKind.AdvancingStep, [trigger]));
            Assert.Equal(WorldActionRejection.None, Assert.Single(cut.Receipts).Rejection);
            Assert.NotNull(cut.SealedMaterialCareSegment);
            Assert.Equal(closing.ContentHash, cut.SealedMaterialCareSegment.Reference.BoundaryHash);
            Assert.Equal(closing.Progression!.ContentHash, cut.SealedMaterialCareSegment.Continued.Progression!.ContentHash);
            state = cut.Candidate;
            Assert.Equal(quote.ContentHash, state.Coordination!.PaidExchange!.FindOpen(quote.QuoteId)!.Quote.ContentHash);
            Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            var head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            session = new CareWorldSession(authority);
            state = authority.ReadStateForHost();
            Assert.Equal(head, state.ContentHash);
            var accept = AcceptCommand(state, journey.Player, quote, journey.Helper, 1, 1);
            var cause = NextId();
            var batch = new WorldStepBatch(cause, state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [accept], state.ProfileVersion);
            ExportBefore(authority, "continued-raw", batch);
            var receipt = authority.SubmitPaidExchangeCommand(accept, Delivery(accept), cause);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            state = authority.ReadStateForHost();
            ExportAfter(authority, "continued-raw", state);
            Assert.Equal(1UL, state.Coordination!.PaidExchange!.GetIssuerRevision(journey.Helper));
            // Continue real expiry actions until another automatically published closing boundary.
            for (int index = 0; authority.InspectForHost().Journal.MaterialCareArchiveCount < 2; index++)
            {
                Assert.True(index < WorldPaidExchangeBook.MaximumOutcomes + 2);
                ExpireOne();
            }
            Assert.DoesNotContain(state.Coordination!.PaidExchange!.Outcomes, value => value.Quote.QuoteId == quote.QuoteId);
            var finalHash = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(finalHash, authority.ReadStateForHost().ContentHash);
            foreach (var id in new[] { issue.CommandId, quote.QuoteId, accept.CommandId, WorldPaidQuote.SplitChildOf(accept.CommandId) })
                Assert.True(authority.IsIdentityOccupiedForHost(id));
            var original = authority.SubmitPaidExchangeCommand(accept, Delivery(accept), NextId());
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(original));
            Assert.Equal(finalHash, authority.ReadStateForHost().ContentHash);
        }
        finally { authority.Dispose(); }
    }

    private (WorldActionCommand Command, SpotQuote Quote) ProducedQuote(WorldSnapshot state, CareRewardJourney journey,
        TransformedLot stock, bool issuerBuys, ulong allowance)
    {
        EntityId issuer = journey.Helper, other = journey.Player;
        EntityId seller = issuerBuys ? other : issuer, buyer = issuerBuys ? issuer : other;
        var control = state.Inventory.Controls.Single(value => value.ActorId == issuer);
        var ledger = state.Progression!.Currency;
        var commandId = NextId();
        ulong step = state.AdvancingSteps + 1;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(commandId), state.WorldId, state.BranchId,
            issuer, control.PrincipalId, control.SessionId, other,
            issuerBuys ? SpotQuoteSide.IssuerBuys : SpotQuoteSide.IssuerSells, SpotLotKind.Produced,
            stock.LotId, stock.Definition, stock.Revision, stock.CustodianId, state.GetResident(buyer).InventoryCustodianId,
            ledger.Currency.DefinitionId, ledger.Currency.Version, ledger.GetActorAccount(seller).AccountId,
            ledger.GetActorAccount(buyer).AccountId, 1, [], allowance,
            state.Coordination!.PaidExchange!.GetIssuerRevision(issuer), step, step + 1024);
        return (new(commandId, control.PrincipalId, control.SessionId, issuer, state.WorldId, state.BranchId,
            state.Space.GetActor(issuer).Revision, new IssueSpotQuoteIntent(quote)), quote);
    }

    private static string? ExportDirectory(string label)
    {
        string? root = Environment.GetEnvironmentVariable("OPENFANTASY_PAID_WORLD_FIXTURES");
        if (string.IsNullOrEmpty(root)) return null;
        string directory = Path.Combine(root, label);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void ExportBefore(SqliteWorldAuthority authority, string label, WorldStepBatch batch)
    {
        if (ExportDirectory(label) is not { } directory) return;
        authority.BackupForHost(Path.Combine(directory, "before.sqlite3"));
        File.WriteAllBytes(Path.Combine(directory, "batch.ofc"), WorldStepCodec.Encode(batch));
    }

    private static void ExportAfter(SqliteWorldAuthority authority, string label, WorldSnapshot state)
    {
        if (ExportDirectory(label) is not { } directory) return;
        authority.BackupForHost(Path.Combine(directory, "after.sqlite3"));
        File.WriteAllBytes(Path.Combine(directory, "after.ofc"), WorldSnapshotCodec.Encode(state));
        File.WriteAllText(Path.Combine(directory, "fixture.json"), JsonSerializer.Serialize(new
        {
            state.ProfileVersion, state.Revision, state.AdvancingSteps, StateHash = state.ContentHash.Value,
            Archives = authority.InspectForHost().Journal.MaterialCareArchiveCount,
            Note = "Actual admitted fresh paid world. Managed gameplay expectations; external process qualification is separate."
        }));
    }
}
