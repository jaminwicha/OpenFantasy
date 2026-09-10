using System.Globalization;
using System.Text.Json;

namespace OpenFantasy.Hosting.Content;

/// <summary>Additional exact records from the already validated original farm closure.</summary>
internal sealed class GatheringFarmSources
{
    private const string PacketDirectory = "legacy/aldermere-first-loop/";
    private readonly Dictionary<string, JsonElement> _records;
    private readonly Dictionary<string, JsonElement> _archetypes;

    private GatheringFarmSources(Dictionary<string, JsonElement> records,
        Dictionary<string, JsonElement> archetypes, JsonElement map)
    {
        _records = records;
        _archetypes = archetypes;
        Map = map;
    }

    internal JsonElement Map { get; }
    internal JsonElement Record(string key) => _records[key];
    internal JsonElement Archetype(string key) => _archetypes[key];

    internal static GatheringFarmSources Load(FarmContentFiles files, JsonElement admission)
    {
        // The unchanged base admission has validated the complete package and closure. Read these
        // specific bytes again so this source adapter also binds its selected records and witnesses.
        files.Read(PacketDirectory + "closure-manifest.json", 65_536,
            admission.GetProperty("closureSha256").GetString()!);
        Dictionary<string, byte[]> witnesses = [];
        foreach (JsonElement witness in admission.GetProperty("sourceWitnesses").EnumerateArray())
        {
            string path = witness.GetProperty("path").GetString()!;
            int length = witness.GetProperty("bytes").GetInt32();
            if (witnesses.Count == 9 || length is < 1 or > 262_144 || witnesses.ContainsKey(path))
            {
                throw new InvalidDataException("The gathering source witness set exceeds its bounded profile.");
            }
            byte[] bytes = files.Read(PacketDirectory + path, length, witness.GetProperty("sha256").GetString()!);
            if (bytes.Length != length)
            {
                throw new InvalidDataException("A gathering source witness has the wrong length.");
            }
            witnesses.Add(path, bytes);
        }
        using JsonDocument packet = FarmContentFiles.Parse(files.Read(PacketDirectory + "records.json", 65_536,
            admission.GetProperty("recordsSha256").GetString()!));
        var byKey = packet.RootElement.GetProperty("records").EnumerateArray()
            .ToDictionary(value => value.GetProperty("targetKey").GetString()!, StringComparer.Ordinal);
        Dictionary<string, JsonElement> selected = [];
        foreach (JsonElement required in admission.GetProperty("sourceRecords").EnumerateArray())
        {
            string key = required.GetProperty("targetKey").GetString()!;
            if (selected.Count == 11 || selected.ContainsKey(key) || !byKey.TryGetValue(key, out JsonElement record))
            {
                throw new InvalidDataException("The gathering source record set is incompatible.");
            }
            JsonElement identity = record.GetProperty("sourceIdentity");
            JsonElement payload = record.GetProperty("sourcePayload");
            if (FarmCanonicalJson.Hash(identity) != FarmCanonicalJson.Hash(required.GetProperty("sourceIdentity"))
                || FarmCanonicalJson.Hash(payload).Value != identity.GetProperty("payloadSha256").GetString())
            {
                throw new InvalidDataException("A gathering source record differs from its exact retained pin.");
            }
            ValidateOriginal(identity, payload, witnesses);
            selected.Add(key, payload.Clone());
        }
        if (selected.Count != 11 || witnesses.Count != 9)
        {
            throw new InvalidDataException("The gathering source closure is incomplete.");
        }
        var archetypes = packet.RootElement.GetProperty("codeDefinedRecords").EnumerateArray()
            .ToDictionary(value => value.GetProperty("targetKey").GetString()!, value => value.Clone(), StringComparer.Ordinal);
        using JsonDocument map = FarmContentFiles.Parse(witnesses["source-support/data/world.json"]);
        return new GatheringFarmSources(selected, archetypes, map.RootElement.Clone());
    }

    private static void ValidateOriginal(JsonElement identity, JsonElement payload, Dictionary<string, byte[]> witnesses)
    {
        string path = identity.GetProperty("path").GetString()!;
        string pointer = identity.GetProperty("jsonPointer").GetString()!;
        string[] segments = pointer.Split('/');
        if (identity.GetProperty("inventoryId").GetString() != path + "#" + pointer
            || segments.Length != 3 || segments[0] != ""
            || !int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out int index))
        {
            throw new InvalidDataException("A gathering source record has an unsupported original pointer.");
        }
        using JsonDocument original = FarmContentFiles.Parse(witnesses["source-support/" + path]);
        if (FarmCanonicalJson.Hash(original.RootElement.GetProperty(segments[1])[index]) != FarmCanonicalJson.Hash(payload))
        {
            throw new InvalidDataException("A gathering source payload differs from its original file.");
        }
    }

    internal bool IsOriginalWalkableOutsideSafeZones(int x, int y)
    {
        if (Map.GetProperty("width").GetInt32() != 512 || Map.GetProperty("height").GetInt32() != 512
            || x is < 0 or >= 512 || y is < 0 or >= 512)
        {
            return false;
        }
        JsonElement row = Map.GetProperty("rows")[y];
        int cursor = 0;
        int selectedTile = -1;
        for (int index = 0; index < row.GetArrayLength(); index += 2)
        {
            int tile = row[index].GetInt32();
            int count = row[index + 1].GetInt32();
            if (x >= cursor && x < checked(cursor + count)) selectedTile = tile;
            cursor = checked(cursor + count);
        }
        bool walkable = cursor == 512 && Map.GetProperty("tileset").EnumerateArray()
            .Any(tile => tile.GetProperty("id").GetInt32() == selectedTile && tile.GetProperty("walkable").GetBoolean());
        bool safe = Map.GetProperty("safeZones").EnumerateArray().Any(zone => Contains(zone, x, y));
        return walkable && !safe;
    }

    internal static bool Contains(JsonElement rectangle, int x, int y) =>
        x >= rectangle.GetProperty("x").GetInt32() && y >= rectangle.GetProperty("y").GetInt32()
        && x < checked(rectangle.GetProperty("x").GetInt32() + rectangle.GetProperty("w").GetInt32())
        && y < checked(rectangle.GetProperty("y").GetInt32() + rectangle.GetProperty("h").GetInt32());
}
