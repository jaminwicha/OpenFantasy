using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Content.Cargo.Integration;

/// <summary>Independent authentication of the closed circuit exports; report labels do not supply expected effects.</summary>
public sealed class CargoCircuitEvidenceTests
{
    [Fact]
    public void Opt_in_saved_circuit_preserves_its_prefix_and_settles_exact_parties_and_cargo()
    {
        string? evidence = Environment.GetEnvironmentVariable("OPENFANTASY_CARGO_CIRCUIT_RESULTS");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(evidence), "A completed durable circuit export is required.");
        byte[] genesisBytes = File.ReadAllBytes(Path.Combine(evidence!, "genesis.snapshot"));
        WorldSnapshot genesis = CargoWorldAdmission.CreateEmbedded("cargo-circuit-journey", "Traveler");
        Assert.Equal(WorldSnapshotCodec.Encode(genesis), genesisBytes);
        var sources = CargoCircuitSources.LoadEmbedded();
        CargoCircuitMarkets markets = CargoWorldAdmission.Find(genesis, sources)!;
        EntityId player = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId marla = markets.Origin.MerchantActorId;
        EntityId kegga = markets.Destination.MerchantActorId;
        string scratch = Path.Combine(Path.GetTempPath(), "openfantasy-cargo-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string midPath = CopySave(evidence!, scratch, "mid-trip");
            string finalPath = CopySave(evidence!, scratch, "final");
            StoredWorldCommit[] prefix;
            StoredWorldCommit[] complete;
            StoredWorldCommandOutcome[] outcomes;
            WorldSnapshot mid;
            WorldSnapshot final;
            using (var database = WorldDatabase.Open(midPath, create: false))
            {
                Assert.Equal(genesisBytes, database.ReadGenesis());
                prefix = database.ReadJournal().ToArray();
                Assert.Equal(File.ReadAllBytes(Path.Combine(evidence!, "mid-trip-before-save.snapshot")), database.ReadHead().State);
            }
            using (var authority = SqliteWorldAuthority.OpenExisting(midPath, genesisBytes))
                mid = authority.ReadStateForHost();
            using (var database = WorldDatabase.Open(finalPath, create: false))
            {
                Assert.Equal(genesisBytes, database.ReadGenesis());
                complete = database.ReadJournal().ToArray();
                outcomes = database.ReadCommandOutcomes().ToArray();
                Assert.Equal(File.ReadAllBytes(Path.Combine(evidence!, "final.snapshot")), database.ReadHead().State);
            }
            Assert.NotEmpty(prefix);
            Assert.True(complete.Length > prefix.Length);
            foreach (StoredWorldCommit before in prefix)
            {
                StoredWorldCommit retained = Assert.Single(complete, row => row.Sequence == before.Sequence);
                Assert.Equal((before.Kind, before.CauseId, before.AdvancingSteps, before.BeforeHash, before.AfterHash),
                    (retained.Kind, retained.CauseId, retained.AdvancingSteps, retained.BeforeHash, retained.AfterHash));
                Assert.Equal(before.AcceptedBatch, retained.AcceptedBatch);
                Assert.Equal(before.Outcomes, retained.Outcomes);
            }
            using (var authority = SqliteWorldAuthority.OpenExisting(finalPath, genesisBytes))
            {
                final = authority.ReadStateForHost();
                Assert.Equal(WorldSnapshotCodec.Encode(mid), WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(mid.Revision).State));
                var accepts = outcomes.Select(row => (Row: row, Command: WorldActionCodec.Decode(row.Command),
                        Receipt: WorldReceiptCodecV1.Decode(row.Receipt)))
                    .Where(entry => entry.Command.Intent is AcceptSpotQuoteIntent).ToArray();
                Assert.Equal(3, accepts.Length);
                Assert.All(accepts, entry => Assert.Equal(player, entry.Command.ActorId));
                var refused = Assert.Single(accepts, entry => entry.Receipt.Status == CommandReceiptStatus.Rejected);
                Assert.Equal(WorldActionRejection.Unavailable, refused.Receipt.Rejection);
                Assert.Equal(2, accepts.Count(entry => entry.Receipt.Status == CommandReceiptStatus.Applied));
                foreach (var entry in accepts.Where(entry => entry.Receipt.Status == CommandReceiptStatus.Applied))
                {
                    WorldSnapshot before = authority.ReadRecordedStateForHost(entry.Row.Sequence - 1).State;
                    WorldSnapshot after = authority.ReadRecordedStateForHost(entry.Row.Sequence).State;
                    var acceptance = ((AcceptSpotQuoteIntent)entry.Command.Intent).Acceptance;
                    var quote = before.Coordination!.PaidExchange!.FindOpen(acceptance.QuoteId)!.Quote;
                    Assert.Equal(2UL, acceptance.Quantity);
                    Assert.Equal(quote.ContentHash, acceptance.QuoteHash);
                    bool buying = quote.IssuerActorId == marla;
                    Assert.Equal(buying ? marla : kegga, quote.IssuerActorId);
                    Assert.Equal(player, quote.CounterpartyActorId);
                    Assert.Equal(before.Progression!.Currency.GetActorAccount(buying ? player : kegga).AccountId, quote.BuyerAccountId);
                    Assert.Equal(before.Progression.Currency.GetActorAccount(buying ? marla : player).AccountId, quote.SellerAccountId);
                    Assert.Equal(buying ? 44UL : 62UL, acceptance.Payment);
                    Assert.Equal(0UL, acceptance.Fees);
                    var oldAccounts = before.Progression!.Currency.Accounts.ToDictionary(account => account.AccountId);
                    Assert.Equal(oldAccounts.Keys.Order(), after.Progression!.Currency.Accounts.Select(account => account.AccountId).Order());
                    foreach (var account in after.Progression.Currency.Accounts)
                    {
                        ulong old = oldAccounts[account.AccountId].Balance;
                        ulong expected = account.AccountId == quote.BuyerAccountId ? old - acceptance.Payment
                            : account.AccountId == quote.SellerAccountId ? old + acceptance.Payment : old;
                        Assert.Equal(expected, account.Balance);
                    }
                }
            }
            Assert.Equal(complete[^1].AfterHash, final.ContentHash);
            Assert.Equal((156UL, 44UL, 80UL), Balances(mid, player, marla, kegga));
            Assert.Equal((218UL, 44UL, 18UL), Balances(final, player, marla, kegga));
            Assert.Equal(280UL, final.Progression!.Currency.AdmittedSupply);
            Assert.Equal(280UL, final.Progression.Currency.Accounts.Aggregate(0UL, (sum, account) => sum + account.Balance));
            var shelf = Assert.Single(genesis.Inventory.Custody.Lots, lot => lot.Definition == markets.Origin.Commodity);
            var carried = Assert.Single(mid.Inventory.Custody.Lots, lot => lot.Definition == shelf.Definition
                && lot.CustodianId == mid.GetResident(player).InventoryCustodianId);
            Assert.Equal(new SourceInterval(shelf.Source.SourceId, 0, 2), carried.Source);
            CountedSourceLot[] stock = final.Inventory.Custody.Lots.Where(lot => lot.Definition == shelf.Definition).ToArray();
            Assert.Equal(2, stock.Length);
            Assert.Equal(carried.Source, Assert.Single(stock, lot => lot.CustodianId == final.GetResident(kegga).InventoryCustodianId).Source);
            Assert.Equal(new SourceInterval(shelf.Source.SourceId, 2, 8),
                Assert.Single(stock, lot => lot.CustodianId == final.GetResident(marla).InventoryCustodianId).Source);
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    private static (ulong Player, ulong Origin, ulong Destination) Balances(WorldSnapshot world, EntityId player, EntityId origin, EntityId destination) =>
        (world.Progression!.Currency.GetActorAccount(player).Balance, world.Progression.Currency.GetActorAccount(origin).Balance,
            world.Progression.Currency.GetActorAccount(destination).Balance);

    private static string CopySave(string source, string destination, string name)
    {
        string target = Path.Combine(destination, name + ".sqlite");
        File.Copy(Path.Combine(source, name + ".sqlite"), target);
        foreach (string suffix in new[] { "-wal", "-shm" })
        {
            string side = Path.Combine(source, name + ".sqlite" + suffix);
            if (File.Exists(side)) File.Copy(side, target + suffix);
        }
        return target;
    }
}
