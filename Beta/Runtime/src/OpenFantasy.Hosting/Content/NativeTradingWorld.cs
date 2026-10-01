using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>
/// The explicit fresh native market world. The unplayed ordinary-care journey is admitted directly for paid
/// exchange, and Wren's own admitted berry supply is bound to one stall rule. Composition adds no person and
/// mints no stock or money: Wren trades the source-counted berries the care farm already gives her, the same
/// finite supply her potion recipe uses, and the player's purchases are funded only by the ordinary quest
/// reward it earns. Composing a world never opens, upgrades or relabels an existing save.
/// </summary>
public static class NativeTradingWorld
{
    private const string IdentityProfile = "native-trading";
    private const string StallRole = "wren-berry-stall";

    /// <summary>The embedded source content composed into a fresh native market genesis.</summary>
    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        Compose(RestorativeJourneyAdmission.CreateEmbedded(seedText, playerName));

    /// <summary>
    /// Admits paid exchange and Wren's stall into an unplayed ordinary-care root. The paid admission refuses any
    /// played or differently interpreted root, so a lived world can never be selected through this entry.
    /// </summary>
    public static WorldSnapshot Compose(WorldSnapshot ordinaryCareGenesis)
    {
        ArgumentNullException.ThrowIfNull(ordinaryCareGenesis);
        WorldSnapshot paid = WorldPaidExchangeAdmission.CreateFresh(ordinaryCareGenesis);
        EntityId marketId = StallIdentity(paid);
        if (paid.IdentityIndex.Contains(marketId))
        {
            throw new InvalidDataException("The native stall identity collides with admitted content.");
        }
        MarketBinding stall = MarketCircuitPolicies.BindResident(paid, marketId, Merchant(paid), Berries(paid),
            NativeTradingPolicy.WrenBerryStall());
        return WorldMarketAdmission.CreateFresh(paid, [stall]);
    }

    /// <summary>Wren's stall when the world is a native market world, otherwise null.</summary>
    public static MarketBinding? FindStall(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Coordination?.Markets?.FindMarket(StallIdentity(world)) is { } stall
            && world.Quests is not null && stall.MerchantActorId == Merchant(world)
            && stall.Policy.Version == NativeTradingPolicy.WrenBerryStall().Version ? stall : null;
    }

    /// <summary>The stall identity is derived from the world and branch, so a same-seed world reproduces it exactly.</summary>
    private static EntityId StallIdentity(WorldSnapshot world) =>
        ConstructionFarmIdentityScope.InstanceId(world, IdentityProfile, StallRole);

    /// <summary>Wren is the farm's single quest giver and keeps her admitted host control.</summary>
    private static EntityId Merchant(WorldSnapshot world) =>
        world.Quests!.Definitions.Select(quest => quest.GiverActorId).Distinct().Single();

    private static CountedItemDefinition Berries(WorldSnapshot world) =>
        world.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry).Definition.YieldDefinition;
}
