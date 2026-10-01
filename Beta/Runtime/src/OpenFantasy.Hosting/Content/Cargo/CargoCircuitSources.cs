using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>
/// The retained source binding of the two-market cargo circuit: Marla in Brasshollow, Kegga Toolwright in
/// Gearford and their shared rations. It reads the original packet files by exact digest and binds records
/// by their source identities; it copies no record onto another actor and edits no field. Admission of these
/// people, their stock and their funds into a world is a separate step (see the cargo-circuit reference).
/// </summary>
public sealed class CargoCircuitSources
{
    public const string OriginMerchantId = "marla";
    public const string DestinationMerchantId = "kegga";
    public const string OriginPlaceId = "brasshollow";
    public const string DestinationPlaceId = "gearford";
    public const string SharedGoodId = "rations";

    private const string Packet = "legacy/aldermere-first-loop/source-support/data/";

    // Exact retained file digests, equal to the package closure manifest and the cargo source inventory.
    internal const string NpcsDigest = "a75e0f3e565e21cd751dc3697f948b773439935478053c29d5467e868d439746";
    internal const string ItemsDigest = "ac0944b98fbfc10474ae1d1309bd7ad6b74c511fb2da9c314ab41f6c8a655609";
    internal const string WorldDigest = "9f46be269ea80ddeef908d9a8305b70bf26a8ca11a544bed32c640fbfee7668d";

    private CargoCircuitSources(CargoSourceMerchant origin, CargoSourceMerchant destination, CargoSourceGood sharedGood,
        MarketSourcePrice tradedPrice, IReadOnlyList<CargoSourcePlace> places)
    {
        Origin = origin;
        Destination = destination;
        SharedGood = sharedGood;
        TradedPrice = tradedPrice;
        Places = places;
    }

    /// <summary>
    /// Every retained <c>world.json</c> place in source order, read-only with its pointer and payload hash. The
    /// corridor checks its declared overlays against all of them.
    /// </summary>
    public IReadOnlyList<CargoSourcePlace> Places { get; }

    /// <summary>Marla, whose counter starts the circuit.</summary>
    public CargoSourceMerchant Origin { get; }

    /// <summary>Kegga Toolwright, whose counter ends the outbound leg.</summary>
    public CargoSourceMerchant Destination { get; }

    public CargoSourceGood SharedGood { get; }

    /// <summary>The immutable source price the authored market rules cite.</summary>
    public MarketSourcePrice TradedPrice { get; }

    public static CargoCircuitSources LoadEmbedded() => Load(FarmContentFiles.Embedded(), MarketCircuitPolicies.Rations);

    public static CargoCircuitSources Load(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        return Load(new FarmContentFiles(contentRoot), MarketCircuitPolicies.Rations);
    }

    internal static CargoCircuitSources Load(FarmContentFiles files, MarketSourcePrice tradedPrice)
    {
        ArgumentNullException.ThrowIfNull(files);
        using JsonDocument npcs = FarmContentFiles.Parse(files.Read(Packet + "npcs.json", 65_536, NpcsDigest));
        using JsonDocument items = FarmContentFiles.Parse(files.Read(Packet + "items.json", 16_384, ItemsDigest));
        using JsonDocument world = FarmContentFiles.Parse(files.Read(Packet + "world.json", 131_072, WorldDigest));
        return Bind(npcs.RootElement, items.RootElement, world.RootElement, tradedPrice);
    }

    /// <summary>
    /// Binds parsed source documents. A missing or duplicated identity, a merchant outside its source place, a
    /// shop list without the shared good, or a market rule citing a different good, price or source file is
    /// refused; nothing is repaired or substituted.
    /// </summary>
    internal static CargoCircuitSources Bind(JsonElement npcs, JsonElement items, JsonElement world,
        MarketSourcePrice tradedPrice)
    {
        ArgumentNullException.ThrowIfNull(tradedPrice);
        CargoSourceGood good = BindGood(FindUnique(items, "items", SharedGoodId));
        if (tradedPrice.ItemKey != good.SourceId || tradedPrice.UnitPrice != good.SourcePrice
            || tradedPrice.SourceDigest.Value != ItemsDigest)
        {
            throw new InvalidDataException("The cited market price differs from the retained shared good's item, price or source file.");
        }
        IReadOnlyList<CargoSourcePlace> places = BindPlaces(world);
        CargoSourceMerchant origin = BindMerchant(npcs, places, OriginMerchantId, OriginPlaceId, good);
        CargoSourceMerchant destination = BindMerchant(npcs, places, DestinationMerchantId, DestinationPlaceId, good);
        return new CargoCircuitSources(origin, destination, good, tradedPrice, places);
    }

    /// <summary>Binds every retained region by identity; a duplicated or malformed place is refused.</summary>
    private static IReadOnlyList<CargoSourcePlace> BindPlaces(JsonElement world)
    {
        List<CargoSourcePlace> places = [];
        int index = 0;
        foreach (JsonElement region in world.GetProperty("regions").EnumerateArray())
        {
            if (!region.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("A retained source place has no identity: /regions/" + index);
            }
            string placeId = id.GetString()!;
            if (places.Any(place => place.SourceId == placeId))
            {
                throw new InvalidDataException("A retained source identity is duplicated: regions/" + placeId);
            }
            places.Add(new CargoSourcePlace(placeId, region.GetProperty("x").GetInt64(), region.GetProperty("y").GetInt64(),
                region.GetProperty("w").GetInt32(), region.GetProperty("h").GetInt32(), "/regions/" + index,
                FarmCanonicalJson.Hash(region)));
            index++;
        }
        return places.AsReadOnly();
    }

    private static CargoSourceGood BindGood((JsonElement Record, string Pointer) found)
    {
        JsonElement record = found.Record;
        return new CargoSourceGood(record.GetProperty("id").GetString()!, record.GetProperty("name").GetString()!,
            record.GetProperty("kind").GetString()!, record.GetProperty("price").GetUInt64(), found.Pointer,
            FarmCanonicalJson.Hash(record));
    }

    private static CargoSourceMerchant BindMerchant(JsonElement npcs, IReadOnlyList<CargoSourcePlace> places, string merchantId,
        string placeId, CargoSourceGood good)
    {
        (JsonElement record, string pointer) = FindUnique(npcs, "npcs", merchantId);
        CargoSourcePlace place = places.SingleOrDefault(value => value.SourceId == placeId)
            ?? throw new InvalidDataException("A retained source identity is missing: regions/" + placeId);
        long tileX = record.GetProperty("tileX").GetInt64();
        long tileY = record.GetProperty("tileY").GetInt64();
        if (!place.Contains(tileX, tileY))
        {
            throw new InvalidDataException("A retained merchant's tile lies outside its retained source place: " + merchantId);
        }
        string[] shop = record.GetProperty("shop").EnumerateArray().Select(entry => entry.GetString()!).ToArray();
        if (!shop.Contains(good.SourceId, StringComparer.Ordinal))
        {
            throw new InvalidDataException("A retained merchant's shop list does not offer the shared good: " + merchantId);
        }
        string[] quests = record.GetProperty("quests").EnumerateArray().Select(entry => entry.GetString()!).ToArray();
        return new CargoSourceMerchant(merchantId, record.GetProperty("name").GetString()!, tileX, tileY, place,
            record.GetProperty("dialog").GetString()!, Array.AsReadOnly(shop), Array.AsReadOnly(quests),
            record.GetProperty("wander").GetInt32(), FarmCanonicalJson.Hash(record.GetProperty("appearance")), pointer,
            FarmCanonicalJson.Hash(record));
    }

    /// <summary>Finds exactly one record by source identity and returns it with its JSON pointer.</summary>
    private static (JsonElement Record, string Pointer) FindUnique(JsonElement root, string collection, string sourceId)
    {
        JsonElement entries = root.GetProperty(collection);
        (JsonElement Record, string Pointer)? found = null;
        int index = 0;
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (entry.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String
                && id.GetString() == sourceId)
            {
                if (found is not null)
                {
                    throw new InvalidDataException("A retained source identity is duplicated: " + collection + "/" + sourceId);
                }
                found = (entry, "/" + collection + "/" + index);
            }
            index++;
        }
        return found ?? throw new InvalidDataException("A retained source identity is missing: " + collection + "/" + sourceId);
    }
}
