using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>The two ration counters of one cargo world.</summary>
public sealed record CargoCircuitMarkets(MarketBinding Origin, MarketBinding Destination);

/// <summary>
/// One merchant's opening shelf as typed Host authoring over existing owners: the admitted raw counted source with
/// its derived role identity and capacity, the provision version that declared it, and the farm recipe output whose
/// definition it shares. It is recomputed and checked against the world, never persisted: the save records only the
/// admitted source (identity, definition, capacity) and the admission hash that covers the provision version.
/// </summary>
public sealed record CargoOpeningStock(string MerchantSourceId, string Role, ContentHash ProvisionVersion,
    AdmittedCountedSource Source, EntityId RecipeId, CountedItemDefinition Definition);

/// <summary>
/// Explicit fresh selection of the two-market cargo world. It composes the unplayed ordinary-care farm (Wren, her
/// quest, crafting and care meaning unchanged), extends that genesis with the authored corridor and the retained
/// merchants, then admits paid exchange and the two authored counters. The two wolf scenarios keep their
/// participants, abilities and entry range but gain both merchants as side-zero bystanders, as the lifecycle owner
/// requires every resident in every scenario; their scenario versions therefore differ from the farm's, and an
/// entered encounter's tactical timing spans every resident of this world. Composition never opens, upgrades or
/// relabels an existing save, and it is distinct from the native farm and market admissions, which stay unchanged.
/// </summary>
public static class CargoWorldAdmission
{
    private const string IdentityProfile = "cargo-world";

    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        Compose(RestorativeJourneyAdmission.CreateEmbedded(seedText, playerName), CargoCircuitSources.LoadEmbedded());

    /// <summary>Admits the circuit into an unplayed ordinary-care root; a played or other interpretation is refused.</summary>
    public static WorldSnapshot Compose(WorldSnapshot ordinaryCareGenesis, CargoCircuitSources sources) =>
        Compose(ordinaryCareGenesis, sources, null);

    /// <summary>
    /// The same composition with an optional further fresh genesis extension (for example the road threat's camp
    /// resident) applied to the unplayed extended root before paid exchange and the counters are admitted.
    /// </summary>
    internal static WorldSnapshot Compose(WorldSnapshot ordinaryCareGenesis, CargoCircuitSources sources,
        Func<WorldSnapshot, WorldSnapshot>? extendGenesis)
    {
        ArgumentNullException.ThrowIfNull(ordinaryCareGenesis);
        ArgumentNullException.ThrowIfNull(sources);
        WorldSnapshot extended = new CargoGenesisExtension(ordinaryCareGenesis, sources).Build();
        if (extendGenesis is not null)
        {
            extended = extendGenesis(extended);
        }
        WorldSnapshot paid = WorldPaidExchangeAdmission.CreateFresh(extended);
        CountedItemDefinition rations = CargoGenesisExtension.RationDefinition(paid);
        MarketBinding origin = Bind(paid, sources.Origin, rations, CargoCircuitPolicy.BrasshollowRations());
        MarketBinding destination = Bind(paid, sources.Destination, rations, CargoCircuitPolicy.GearfordRations());
        return WorldMarketAdmission.CreateFresh(paid, [origin, destination]);
    }

    /// <summary>The circuit's counters when this is a cargo world of these sources, otherwise null.</summary>
    public static CargoCircuitMarkets? Find(WorldSnapshot world, CargoCircuitSources sources)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sources);
        MarketBinding? origin = FindCounter(world, sources.Origin, CargoCircuitPolicy.BrasshollowRations());
        MarketBinding? destination = FindCounter(world, sources.Destination, CargoCircuitPolicy.GearfordRations());
        return origin is null || destination is null ? null : new CargoCircuitMarkets(origin, destination);
    }

    /// <summary>
    /// The opt-in session advice: both merchants answer requests at their own counters under their own host control.
    /// No host shopper is configured. Supplied again on every opening; it is never saved.
    /// </summary>
    public static LocalTradingConfiguration Configuration(WorldSnapshot world, CargoCircuitSources sources)
    {
        CargoCircuitMarkets markets = Find(world, sources)
            ?? throw new InvalidDataException("This save is not the cargo world of these sources.");
        return new LocalTradingConfiguration(
            [new LocalMerchantPreference(markets.Origin.MerchantActorId, CargoCircuitPolicy.OfferWindowSteps),
                new LocalMerchantPreference(markets.Destination.MerchantActorId, CargoCircuitPolicy.OfferWindowSteps)],
            [], CargoCircuitPolicy.InquiryLifetimeSteps);
    }

    /// <summary>The admitted actor identity of a retained merchant in this world.</summary>
    public static EntityId MerchantActorId(WorldSnapshot world, CargoSourceMerchant merchant) =>
        ConstructionFarmIdentityScope.InstanceId(world, IdentityProfile, merchant.SourceId + ".actor");

    /// <summary>
    /// The named source-to-provision binding of one merchant's opening shelf in this world, or null when the provision
    /// declares no opening units. The role identity must be an admitted raw counted source of exactly the declared
    /// capacity and of the farm's <c>craft_rations</c> output definition; anything else is refused. It is a raw
    /// authored source, never a produced lot: no production lineage is claimed or fabricated.
    /// </summary>
    public static CargoOpeningStock? OpeningStock(WorldSnapshot world, CargoSourceMerchant merchant,
        CargoMerchantProvision provision)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(merchant);
        ArgumentNullException.ThrowIfNull(provision);
        if (provision.MerchantSourceId != merchant.SourceId)
        {
            throw new InvalidDataException("A provision names a different retained merchant.");
        }
        if (provision.OpeningUnits == 0)
        {
            return null;
        }
        string role = merchant.SourceId + ".opening-stock-source";
        EntityId sourceId = ConstructionFarmIdentityScope.InstanceId(world, IdentityProfile, role);
        CraftRecipeDefinition recipe = world.Production!.Recipes.Single(value =>
            value.PhysicalRecipe.Key == CargoGenesisExtension.RationsRecipeKey);
        CountedItemDefinition rations = CargoGenesisExtension.RationDefinition(world);
        AdmittedCountedSource source = world.Inventory.Sources.SingleOrDefault(value => value.SourceId == sourceId)
            ?? throw new InvalidDataException("The declared opening-stock source is not admitted in this world.");
        // Produced rations are transformed lots with production output intervals; this shelf is a raw source interval.
        if (source.UnitCapacity != provision.OpeningUnits || !source.Definition.Equals(rations))
        {
            throw new InvalidDataException("The admitted opening-stock source differs from its declared provision.");
        }
        return new CargoOpeningStock(merchant.SourceId, role, provision.Version, source, recipe.DefinitionId, rations);
    }

    private static MarketBinding Bind(WorldSnapshot paid, CargoSourceMerchant merchant, CountedItemDefinition rations,
        MarketPricePolicy policy)
    {
        EntityId marketId = MarketId(paid, merchant);
        if (paid.IdentityIndex.Contains(marketId))
        {
            throw new InvalidDataException("A cargo counter identity collides with admitted content.");
        }
        return MarketCircuitPolicies.BindResident(paid, marketId, MerchantActorId(paid, merchant), rations, policy);
    }

    private static MarketBinding? FindCounter(WorldSnapshot world, CargoSourceMerchant merchant, MarketPricePolicy policy) =>
        world.Coordination?.Markets?.FindMarket(MarketId(world, merchant)) is { } market
        && market.MerchantActorId == MerchantActorId(world, merchant) && market.Policy.Version == policy.Version ? market : null;

    private static EntityId MarketId(WorldSnapshot world, CargoSourceMerchant merchant) =>
        ConstructionFarmIdentityScope.InstanceId(world, IdentityProfile, "market." + merchant.SourceId);
}
