using System.Text;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Content;

/// <summary>Exact retained item witnesses and the original three-berry Potion recipe.</summary>
internal sealed class CareFarmSources
{
    private const string Packet = "legacy/aldermere-first-loop/";
    private const string RecordsHash = "9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b";
    private const string ItemsHash = "ac0944b98fbfc10474ae1d1309bd7ad6b74c511fb2da9c314ab41f6c8a655609";
    private const string RecipesHash = "0c81bcf33088668a7837e77cf01640cbfd2070e2a8caf9fa13ebdeb1e032a6b1";
    internal const string PotionPayloadHash = "3b7a5fdd562795e58d68a11007763b88cd25d40841c739c4fef9495161c097aa";
    internal const string RecipePayloadHash = "dd98e32ef5f0442f9d1c37d10d431aab74226a77f73bc6858a5075e4dad41dc2";

    private CareFarmSources(uint restoredHp, ulong berryCount, ulong outputCount)
    {
        RestoredHp = restoredHp;
        BerryCount = berryCount;
        OutputCount = outputCount;
        Version = ContentHash.Compute(Encoding.ASCII.GetBytes("care-farm.sources.v1")
            .Concat(new ContentHash(RecordsHash).ToByteArray()).Concat(new ContentHash(ItemsHash).ToByteArray())
            .Concat(new ContentHash(RecipesHash).ToByteArray()).Concat(PotionVersion.ToByteArray())
            .Concat(RecipeVersion.ToByteArray()).ToArray());
    }

    internal uint RestoredHp { get; }
    internal ulong BerryCount { get; }
    internal ulong OutputCount { get; }
    internal ContentHash PotionVersion => new(PotionPayloadHash);
    internal ContentHash RecipeVersion => new(RecipePayloadHash);
    internal ContentHash Version { get; }

    internal static CareFarmSources Load(FarmContentFiles files)
    {
        using JsonDocument records = FarmContentFiles.Parse(files.Read(Packet + "records.json", 65_536, RecordsHash));
        using JsonDocument items = FarmContentFiles.Parse(files.Read(Packet + "source-support/data/items.json", 32_768, ItemsHash));
        using JsonDocument recipes = FarmContentFiles.Parse(files.Read(Packet + "source-support/data/recipes.json", 32_768, RecipesHash));
        JsonElement root = records.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("packageId").GetString() != "openfantasy.aldermere"
            || root.GetProperty("records").GetArrayLength() != 49)
        {
            throw new InvalidDataException("The care source packet has an incompatible identity or shape.");
        }
        JsonElement potion = ReadItem(root, items.RootElement, "potion", 0, PotionPayloadHash);
        ReadItem(root, items.RootElement, "berries", 15,
            "a7b53e8648a12bb156269fe955ea868aeab9881ba75fb4b79bba057358a93214");
        // craft_potion is retained in the original recipe file, outside the selected record packet.
        JsonElement recipe = recipes.RootElement.GetProperty("recipes")[0];
        if (FarmCanonicalJson.Hash(recipe).Value != RecipePayloadHash
            || recipe.GetProperty("id").GetString() != CareJourneyAdmission.RecipeKey
            || recipe.GetProperty("output").GetString() != "potion"
            || recipe.GetProperty("outputCount").GetUInt64() != 1
            || recipe.GetProperty("inputs").GetArrayLength() != 1
            || recipe.GetProperty("inputs")[0].GetProperty("item").GetString() != "berries"
            || recipe.GetProperty("inputs")[0].GetProperty("count").GetUInt64() != 3
            || potion.GetProperty("kind").GetString() != "Consumable"
            || potion.GetProperty("healHp").GetUInt32() != 40 || potion.GetProperty("healMp").GetUInt32() != 0)
        {
            throw new InvalidDataException("Care must retain the original Potion effect and complete recipe counts.");
        }
        return new(potion.GetProperty("healHp").GetUInt32(),
            recipe.GetProperty("inputs")[0].GetProperty("count").GetUInt64(), recipe.GetProperty("outputCount").GetUInt64());
    }

    private static JsonElement ReadItem(JsonElement records, JsonElement original, string key, int index, string hash)
    {
        JsonElement record = records.GetProperty("records").EnumerateArray()
            .Single(value => value.GetProperty("targetKey").GetString() == "openfantasy.aldermere:items/" + key);
        JsonElement identity = record.GetProperty("sourceIdentity");
        JsonElement payload = record.GetProperty("sourcePayload");
        string pointer = "/items/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (identity.GetProperty("path").GetString() != "data/items.json"
            || identity.GetProperty("jsonPointer").GetString() != pointer
            || identity.GetProperty("inventoryId").GetString() != "data/items.json#" + pointer
            || identity.GetProperty("payloadSha256").GetString() != hash
            || payload.GetProperty("id").GetString() != key
            || FarmCanonicalJson.Hash(payload).Value != hash
            || FarmCanonicalJson.Hash(original.GetProperty("items")[index]).Value != hash)
        {
            throw new InvalidDataException("A care item differs from its retained pointer or payload.");
        }
        return payload;
    }
}
