using System.Text;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Tests.Content.Cargo;

/// <summary>The retained merchants, places and shared good bind by exact source identity; altered inputs are refused.</summary>
public sealed class CargoSourceBindingTests
{
    private const string Data = "legacy/aldermere-first-loop/source-support/data/";

    [Fact]
    public void The_retained_merchants_places_and_shared_good_bind_with_their_original_facts()
    {
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        CargoSourceMerchant marla = sources.Origin;
        CargoSourceMerchant kegga = sources.Destination;
        Assert.Equal(("marla", "Marla", 96L, 350L, "brasshollow"), (marla.SourceId, marla.Name, marla.TileX, marla.TileY, marla.Place.SourceId));
        Assert.Equal(("kegga", "Kegga Toolwright", 347L, 140L, "gearford"), (kegga.SourceId, kegga.Name, kegga.TileX, kegga.TileY, kegga.Place.SourceId));
        Assert.Equal((94L, 339L, 33, 27), (marla.Place.MinimumX, marla.Place.MinimumY, marla.Place.Width, marla.Place.Depth));
        Assert.Equal((344L, 127L, 33, 27), (kegga.Place.MinimumX, kegga.Place.MinimumY, kegga.Place.Width, kegga.Place.Depth));
        Assert.Equal(new[] { "potion", "tonic", "ether", "rations", "copper_axe", "copper_pickaxe", "sickle" }, marla.Shop);
        Assert.Equal(new[] { "iron_axe", "iron_pickaxe", "copper_axe", "copper_pickaxe", "sickle", "rations" }, kegga.Shop);
        Assert.Empty(marla.Quests);
        Assert.Equal(new[] { "kegga_timber" }, kegga.Quests);
        Assert.Equal("Welcome to Brasshollow General. Steam-fresh stock, fair prices.", marla.Dialogue);
        Assert.Equal("A dull tool is a slow funeral. Buy sharp.", kegga.Dialogue);
        Assert.Equal((1, 1), (marla.WanderRadius, kegga.WanderRadius));
        Assert.StartsWith("/npcs/", marla.Pointer);
        Assert.StartsWith("/regions/", kegga.Place.Pointer);
        Assert.NotEqual(marla.AppearanceHash, kegga.AppearanceHash);
        Assert.Equal(("rations", "Rations", "Consumable", 25UL), (sources.SharedGood.SourceId, sources.SharedGood.Name,
            sources.SharedGood.Kind, sources.SharedGood.SourcePrice));
        Assert.Same(MarketCircuitPolicies.Rations, sources.TradedPrice);
    }

    [Fact]
    public void A_rule_citing_another_good_price_or_source_file_is_refused()
    {
        ContentHash digest = MarketCircuitPolicies.LegacyItemsDigest;
        ContentHash other = ContentHash.Compute(Encoding.UTF8.GetBytes("not the retained item table"));
        foreach (MarketSourcePrice cited in new[] { new MarketSourcePrice("rations", 26, digest),
            new MarketSourcePrice("berries", 5, digest), new MarketSourcePrice("rations", 25, other) })
        {
            using Documents documents = Documents.Retained();
            Assert.Throws<InvalidDataException>(() => documents.Bind(cited));
        }
    }

    [Fact]
    public void A_missing_merchant_or_a_moved_place_is_refused_rather_than_substituted()
    {
        using (Documents renamed = Documents.Retained(npcs: text => text.Replace("\"id\": \"marla\"", "\"id\": \"marla_retired\"")))
        {
            Assert.Contains("missing", Assert.Throws<InvalidDataException>(() => renamed.Bind(MarketCircuitPolicies.Rations)).Message);
        }
        using (Documents moved = Documents.Retained(world: text => text.Replace("{\"id\":\"gearford\",\"x\":344", "{\"id\":\"gearford\",\"x\":360")))
        {
            Assert.Contains("outside", Assert.Throws<InvalidDataException>(() => moved.Bind(MarketCircuitPolicies.Rations)).Message);
        }
    }

    [Fact]
    public void Changed_source_bytes_are_refused_by_their_exact_file_digest()
    {
        string root = Path.Combine(Path.GetTempPath(), "openfantasy-cargo-sources-" + Guid.NewGuid().ToString("N"));
        try
        {
            string data = Path.Combine(root, Data.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(data);
            foreach ((string name, string digest) in new[] { ("npcs.json", CargoCircuitSources.NpcsDigest),
                ("items.json", CargoCircuitSources.ItemsDigest), ("world.json", CargoCircuitSources.WorldDigest) })
            {
                File.WriteAllBytes(Path.Combine(data, name), FarmContentFiles.Embedded().Read(Data + name, 131_072, digest));
            }
            Assert.Equal("marla", CargoCircuitSources.Load(root).Origin.SourceId);
            string npcs = Path.Combine(data, "npcs.json");
            // One trailing byte keeps every record parseable but is no longer the retained file.
            File.AppendAllText(npcs, " ");
            Assert.Throws<InvalidDataException>(() => CargoCircuitSources.Load(root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void The_authored_counters_report_their_actual_terms_without_editing_the_source_price()
    {
        MarketPricePolicy brasshollow = CargoCircuitPolicy.BrasshollowRations();
        MarketPricePolicy gearford = CargoCircuitPolicy.GearfordRations();
        Assert.Same(MarketCircuitPolicies.Rations, brasshollow.Source);
        Assert.Same(MarketCircuitPolicies.Rations, gearford.Source);
        Assert.NotEqual(brasshollow.Version, gearford.Version);
        // At each target the mid price is the retained source price; terms elsewhere follow actual stock only.
        Assert.Equal(25UL + 2UL, brasshollow.Ask(5, 1));
        Assert.Equal(brasshollow.Ask(8, 2), gearford.Ask(8, 2));
        Assert.Null(gearford.Bid(8, 1));
        Assert.True(gearford.Bid(0, 2) > brasshollow.Ask(8, 2));
    }

    /// <summary>Parsed copies of the retained files, optionally edited in memory for a refusal case.</summary>
    private sealed class Documents : IDisposable
    {
        private readonly JsonDocument _npcs;
        private readonly JsonDocument _items;
        private readonly JsonDocument _world;

        private Documents(JsonDocument npcs, JsonDocument items, JsonDocument world)
        {
            _npcs = npcs;
            _items = items;
            _world = world;
        }

        internal static Documents Retained(Func<string, string>? npcs = null, Func<string, string>? world = null)
        {
            FarmContentFiles files = FarmContentFiles.Embedded();
            string Text(string name, string digest) => Encoding.UTF8.GetString(files.Read(Data + name, 131_072, digest));
            return new Documents(
                JsonDocument.Parse((npcs ?? (text => text))(Text("npcs.json", CargoCircuitSources.NpcsDigest))),
                JsonDocument.Parse(Text("items.json", CargoCircuitSources.ItemsDigest)),
                JsonDocument.Parse((world ?? (text => text))(Text("world.json", CargoCircuitSources.WorldDigest))));
        }

        internal CargoCircuitSources Bind(MarketSourcePrice cited) =>
            CargoCircuitSources.Bind(_npcs.RootElement, _items.RootElement, _world.RootElement, cited);

        public void Dispose()
        {
            _npcs.Dispose();
            _items.Dispose();
            _world.Dispose();
        }
    }
}
