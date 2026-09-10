using System.Globalization;
using System.Text.Json;

namespace OpenFantasy.Hosting.Content;

/// <summary>Exact quest, giver, class and item records, each verified against its original file and pointer.</summary>
internal sealed class QuestFarmSources
{
    private const string PacketDirectory = "legacy/aldermere-first-loop/";
    private readonly Dictionary<string, JsonElement> _records;
    private static readonly IReadOnlyDictionary<string, string> OriginalFiles = new Dictionary<string, string>
    {
        ["data/classes.json"] = "27c5956e9b269acdc36ecba339522c426380ea2f35bc33fead49ae778add6234",
        ["data/quests.json"] = "3ce9eabab223fc66131a9e048a43e47c8c745d2fe6e33542e762291a74148045",
        ["data/npcs.json"] = "a75e0f3e565e21cd751dc3697f948b773439935478053c29d5467e868d439746",
        ["data/items.json"] = "ac0944b98fbfc10474ae1d1309bd7ad6b74c511fb2da9c314ab41f6c8a655609"
    };

    private QuestFarmSources(Dictionary<string, JsonElement> records) => _records = records;
    internal JsonElement Quest => _records["openfantasy.aldermere:quests/wren_orchard"];
    internal JsonElement Wren => _records["openfantasy.aldermere:npcs/farmer_wren"];
    internal JsonElement Squire => _records["openfantasy.aldermere:classes/squire"];

    internal static QuestFarmSources Load(FarmContentFiles files, QuestFarmDefinition definition)
    {
        // This also revalidates the entire retained original closure, not just the four records used below.
        ProductionFarmDefinition production = ProductionFarmDefinition.Load(files);
        ProductionFarmSourceValidation.Validate(files, production);
        foreach (JsonElement witness in definition.Root.GetProperty("sourceWitnesses").EnumerateArray())
        {
            int length = witness.GetProperty("bytes").GetInt32();
            if (length is < 1 or > 262_144
                || files.Read(witness.GetProperty("path").GetString()!, length,
                    witness.GetProperty("sha256").GetString()!).Length != length)
            {
                throw new InvalidDataException("A quest source witness has an unsupported or changed byte length.");
            }
        }
        using JsonDocument packet = FarmContentFiles.Parse(files.Read(PacketDirectory + "records.json", 65_536,
            "9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b"));
        var available = packet.RootElement.GetProperty("records").EnumerateArray()
            .ToDictionary(value => value.GetProperty("targetKey").GetString()!, StringComparer.Ordinal);
        Dictionary<string, JsonElement> selected = [];
        foreach (JsonElement required in definition.Root.GetProperty("sourceRecords").EnumerateArray())
        {
            string key = required.GetProperty("targetKey").GetString()!;
            if (selected.Count == 4 || selected.ContainsKey(key) || !available.TryGetValue(key, out JsonElement record)
                || FarmCanonicalJson.Hash(record.GetProperty("sourceIdentity")) != FarmCanonicalJson.Hash(required.GetProperty("sourceIdentity")))
            {
                throw new InvalidDataException("The quest source record selection differs from the frozen packet.");
            }
            JsonElement identity = record.GetProperty("sourceIdentity");
            JsonElement payload = record.GetProperty("sourcePayload");
            string path = identity.GetProperty("path").GetString()!;
            string pointer = identity.GetProperty("jsonPointer").GetString()!;
            string[] segments = pointer.Split('/');
            if (segments.Length != 3 || segments[0] != ""
                || !int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                || !OriginalFiles.TryGetValue(path, out string? fileHash)
                || identity.GetProperty("inventoryId").GetString() != path + "#" + pointer
                || FarmCanonicalJson.Hash(payload).Value != identity.GetProperty("payloadSha256").GetString())
            {
                throw new InvalidDataException("The quest source identity has an invalid original pointer or payload pin.");
            }
            using JsonDocument original = FarmContentFiles.Parse(files.Read(PacketDirectory + "source-support/" + path, 262_144, fileHash));
            if (FarmCanonicalJson.Hash(original.RootElement.GetProperty(segments[1])[index]) != FarmCanonicalJson.Hash(payload))
            {
                throw new InvalidDataException("A quest source payload differs from its original file.");
            }
            selected.Add(key, payload.Clone());
        }
        var sources = new QuestFarmSources(selected);
        sources.ValidateSemantics(definition);
        return sources;
    }

    private void ValidateSemantics(QuestFarmDefinition definition)
    {
        JsonElement quest = definition.Root.GetProperty("quest");
        if (Quest.GetProperty("kind").GetString() != "Fetch" || Quest.GetProperty("giver").GetString() != Wren.GetProperty("id").GetString()
            || !Wren.GetProperty("quests").EnumerateArray().Any(value => value.GetString() == Quest.GetProperty("id").GetString())
            || Quest.GetProperty("targetItem").GetString() != _records["openfantasy.aldermere:items/wood"].GetProperty("id").GetString()
            || Quest.GetProperty("targetCount").GetUInt64() != quest.GetProperty("expectedQuantity").GetUInt64()
            || Quest.GetProperty("rewardXp").GetUInt64() != quest.GetProperty("expectedExperience").GetUInt64()
            || Quest.GetProperty("rewardGil").GetUInt64() != quest.GetProperty("expectedGil").GetUInt64()
            || Quest.GetProperty("minLevel").GetUInt32() != 1
            || Squire.GetProperty("id").GetString() != "squire"
            || Squire.GetProperty("name").GetString() != definition.Root.GetProperty("playerProfile").GetProperty("displayName").GetString())
        {
            throw new InvalidDataException("The quest admission differs from the retained source obligation or reward.");
        }
    }
}
