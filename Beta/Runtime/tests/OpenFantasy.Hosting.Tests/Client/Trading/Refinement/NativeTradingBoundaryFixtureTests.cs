using System.Text.Json;
using OpenFantasy.Client;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Content.Trading;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.Trading.Refinement;

/// <summary>
/// Prepares three closed real-authority market saves for native refusal journeys. Each offer is Wren's genuine answer
/// with the authored 1,200-step window, reached only through actual session cutoffs: nothing is teleported, minted,
/// shortened or synthesized. Preparation ends paused; every confirmation attempt and recovery happens natively.
/// </summary>
public sealed class NativeTradingBoundaryFixtureTests
{
    internal const ulong Units = 2;

    /// <summary>Advancing steps left on the near-expiry offer: about two seconds of running time at the nominal cutoff.</summary>
    internal const ulong NearExpirySteps = 40;

    internal static readonly string[] Scenarios = ["expired", "unfunded", "moved"];

    [Fact]
    public async Task Real_cutoffs_prepare_an_expiring_an_unfunded_and_a_soon_moving_issuers_offer()
    {
        string? root = Root();
        var manifest = new Dictionary<string, object>();

        await using (var world = new NativeMarketTestWorld())
        {
            await world.EarnReward();
            await world.WaitForWrenAtHome();
            PaidQuoteView offer = await Offer(world);
            // Ordinary cutoffs age the real window; the offer is never shortened or rewritten.
            while (offer.ExpiresAtStep - world.State.AdvancingSteps > NearExpirySteps) await world.Tick();
            PaidQuoteView open = await Settle(world, offer);
            Assert.Equal(NearExpirySteps, open.ExpiresAtStep - world.State.AdvancingSteps);
            Assert.True(world.Balance(world.Player) >= open.TotalFor(Units));
            manifest["expired"] = Export(world, root, "expired", open,
                "Earned reward, Wren's finished work and walk home, her genuine offer for two berries, then ordinary cutoffs "
                + $"until {NearExpirySteps} of its 1,200 steps remain. Native reviews it, lets it expire, attempts confirmation and asks again.");
        }

        await using (var world = new NativeMarketTestWorld())
        {
            PaidQuoteView offer = await Offer(world);
            PaidQuoteView open = await Settle(world, offer);
            Assert.Equal(0UL, world.Balance(world.Player));
            Assert.True(open.TotalFor(Units) > 0);
            manifest["unfunded"] = Export(world, root, "unfunded", open,
                "Fresh production world before the quest: the player's purse is the source zero and Wren answers a request for "
                + "two berries. Native reviews, attempts confirmation, then earns the quest reward and re-reviews the same offer.");
        }

        await using (var world = new NativeMarketTestWorld())
        {
            PaidQuoteView offer = await Offer(world);
            await world.EarnReward();
            PaidQuoteView open = await Settle(world, offer);
            Assert.True(world.Balance(world.Player) >= open.TotalFor(Units));
            Assert.Contains(world.State.Construction!.Projects, project => !project.IsCompleted);
            manifest["moved"] = Export(world, root, "moved", open,
                "Fresh world: Wren answers a request for two berries, then the ordinary quest pays its reward and commits her to "
                + "funded work she has not started. Native reviews while she stands beside the player, then she walks to work.");
        }

        if (root is null) return;
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
        {
            kind = "native-trading-boundary-fixtures-v1", units = Units, nearExpirySteps = NearExpirySteps,
            offerWindowSteps = NativeTradingPolicy.OfferWindowSteps, scenarios = manifest
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Asks Wren for the scenario's units through the session exactly as the stall panel does.</summary>
    private static async Task<PaidQuoteView> Offer(NativeMarketTestWorld world)
    {
        LocalTradeInquiryView answer = await world.Ask(LocalTradeDirection.Buy, Units);
        Assert.Equal(LocalTradeInquiryState.Offered, answer.State);
        PaidQuoteView offer = Assert.Single(world.View.PaidQuotes);
        Assert.Equal((answer.QuoteId, Units, NativeTradingPolicy.OfferWindowSteps),
            (offer.QuoteId, offer.QuantityAllowance, offer.ExpiresAtStep - offer.IssuedAtStep));
        Assert.True(MarketActionOffers.BelongsTo(world.StallView, offer));
        return offer;
    }

    /// <summary>Stops time like the native pause and proves the same offer is still open and observable in person.</summary>
    private static async Task<PaidQuoteView> Settle(NativeMarketTestWorld world, PaidQuoteView offer)
    {
        world.Session.SetReady(false);
        await world.Tick();
        PaidQuoteView open = Assert.Single(world.View.PaidQuotes);
        Assert.Equal((offer.QuoteId, offer.QuoteHash, PaidQuoteStatus.Open), (open.QuoteId, open.QuoteHash, open.Status));
        Assert.NotNull(open.ObservedIssuerRevision);
        Assert.True(world.StallView.IsAvailableInPerson);
        Assert.False(world.View.HasReadingHold);
        return open;
    }

    private static string? Root()
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_TRADING_BOUNDARY_FIXTURES") is not { Length: > 0 } requested) return null;
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(),
            "Boundary fixture export requires a fresh or empty directory; existing evidence is never overwritten.");
        Directory.CreateDirectory(root);
        return root;
    }

    private static object Export(NativeMarketTestWorld world, string? root, string scenario, PaidQuoteView offer, string setup)
    {
        WorldSnapshot before = world.State;
        LocalActorView wren = world.View.Actors.Single(actor => actor.ActorId == world.Wren);
        var metadata = new
        {
            kind = "native-trading-boundary-fixture-v1", scenario, profileVersion = before.ProfileVersion, setup,
            worldId = before.WorldId.ToString(), branchId = before.BranchId.ToString(),
            genesisHash = NativeMarketTestWorld.Genesis.ContentHash.Value, beforeHash = before.ContentHash.Value,
            beforeRevision = before.Revision, beforeAdvancingSteps = before.AdvancingSteps,
            playerId = world.Player.ToString(), wrenId = world.Wren.ToString(), marketId = world.Stall.MarketId.ToString(),
            quoteId = offer.QuoteId.ToString(), quoteHash = offer.QuoteHash.Value, units = Units, unitPrice = offer.UnitPrice,
            total = offer.TotalFor(Units), issuedAtStep = offer.IssuedAtStep, expiresAtStep = offer.ExpiresAtStep,
            playerBalance = world.Balance(world.Player), wrenPresenceRevision = wren.PresenceRevision,
            wrenBerries = world.Stall.CountUsableStock(before.Inventory, before.Production!.Lots),
            limits = "Closed real-authority preparation, then one injected keyboard route. No physical device, OS focus, "
                + "multiplayer Wait, migration or human acceptance claim."
        };
        if (root is null) return metadata;
        string directory = Path.Combine(root, scenario);
        Directory.CreateDirectory(directory);
        string save = Path.Combine(directory, "save.sqlite");
        world.Authority.BackupForHost(save);
        File.WriteAllBytes(Path.Combine(directory, "genesis.ofc"), WorldSnapshotCodec.Encode(NativeMarketTestWorld.Genesis));
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
        return new { directory, save, genesis = Path.Combine(directory, "genesis.ofc"), before = Path.Combine(directory, "before.ofc"),
            metadata = Path.Combine(directory, "metadata.json") };
    }
}
