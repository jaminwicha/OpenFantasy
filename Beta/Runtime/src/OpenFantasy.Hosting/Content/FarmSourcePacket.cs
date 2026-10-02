using System.Text.Json;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Content;

/// <summary>Verified immutable source witnesses, distinct from newly admitted gameplay definitions.</summary>
internal sealed class FarmSourcePacket
{
    private const string PacketDirectory = "legacy/aldermere-first-loop/";
    private const string ManifestHash = "9a826b9698d7e17dd34d77e849bac2ada30b6d96168cd5df2e99e5fab672eb66";
    private const int MaximumPacketFiles = 64;
    private const int MaximumPacketFileBytes = 262_144;
    private const int MaximumPacketBytes = 1_048_576;
    private readonly Dictionary<string, JsonElement> _selected;

    private FarmSourcePacket(Dictionary<string, JsonElement> selected, PackageManifest manifest)
    {
        _selected = selected;
        Manifest = manifest;
    }

    internal PackageManifest Manifest { get; }

    internal JsonElement Wren => _selected["openfantasy.aldermere:npcs/farmer_wren"];
    internal JsonElement Farm => _selected["openfantasy.aldermere:regions/farm_verdance_small"];
    internal JsonElement Wood => _selected["openfantasy.aldermere:items/wood"];
    internal JsonElement Quest => _selected["openfantasy.aldermere:quests/wren_orchard"];

    internal static FarmSourcePacket Load(FarmContentFiles files, JsonElement admission)
    {
        byte[] manifestBytes = files.Read(admission.GetProperty("packageManifest").GetString()!,
            PackageManifestJson.MaximumDocumentBytes, ManifestHash);
        PackageManifest manifest = PackageManifestJson.Read(manifestBytes);
        if (manifest.Id.Value != "openfantasy.aldermere" || manifest.Dependencies.Count != 0
            || manifest.PayloadHash.Value != admission.GetProperty("packagePayloadSha256").GetString())
        {
            throw new InvalidDataException("The installed farm package does not match the admitted namespace and dependency pins.");
        }
        byte[] closureBytes = files.Read(PacketDirectory + "closure-manifest.json", 65_536, manifest.PayloadHash.Value);
        manifest.ValidatePayload(closureBytes);
        using JsonDocument closure = FarmContentFiles.Parse(closureBytes);
        Dictionary<string, byte[]> witnesses = ReadWitnesses(files, closure.RootElement.GetProperty("files"));
        byte[] recordsBytes = files.Read(PacketDirectory + "records.json", 65_536,
            admission.GetProperty("recordsSha256").GetString()!);
        using JsonDocument records = FarmContentFiles.Parse(recordsBytes);
        JsonElement recordArray = records.RootElement.GetProperty("records");
        if (records.RootElement.GetProperty("schemaVersion").GetInt32() != 1
            || records.RootElement.GetProperty("packageId").GetString() != manifest.Id.Value
            || recordArray.GetArrayLength() != 49)
        {
            throw new InvalidDataException("The retained farm record packet has an incompatible shape.");
        }
        var byKey = recordArray.EnumerateArray().ToDictionary(record => record.GetProperty("targetKey").GetString()!,
            StringComparer.Ordinal);
        Dictionary<string, JsonElement> selected = [];
        foreach (JsonElement required in admission.GetProperty("sourceRecords").EnumerateArray())
        {
            string key = required.GetProperty("targetKey").GetString()!;
            if (!byKey.TryGetValue(key, out JsonElement record) || selected.ContainsKey(key))
            {
                throw new InvalidDataException("The farm admission requires unique retained source records.");
            }
            JsonElement identity = record.GetProperty("sourceIdentity");
            JsonElement payload = record.GetProperty("sourcePayload");
            if (FarmCanonicalJson.Hash(identity) != FarmCanonicalJson.Hash(required.GetProperty("sourceIdentity"))
                || FarmCanonicalJson.Hash(payload).Value != identity.GetProperty("payloadSha256").GetString())
            {
                throw new InvalidDataException("A selected source identity or payload differs from its retained pin.");
            }
            ValidateOriginalRecord(identity, payload, witnesses);
            selected.Add(key, payload.Clone());
        }
        if (selected.Count != 4)
        {
            throw new InvalidDataException("This admission requires exactly four selected source records.");
        }
        return new FarmSourcePacket(selected, manifest);
    }

    private static Dictionary<string, byte[]> ReadWitnesses(FarmContentFiles files, JsonElement entries)
    {
        if (entries.GetArrayLength() > MaximumPacketFiles)
        {
            throw new InvalidDataException("The source packet exceeds its file count limit.");
        }
        Dictionary<string, byte[]> result = [];
        long total = 0;
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string path = entry.GetProperty("path").GetString()!;
            int length = entry.GetProperty("bytes").GetInt32();
            total = checked(total + length);
            if (length is < 1 or > MaximumPacketFileBytes || total > MaximumPacketBytes || result.ContainsKey(path))
            {
                throw new InvalidDataException("Source packet file sizes or identities exceed the admission profile.");
            }
            byte[] bytes = files.Read(PacketDirectory + path, length, entry.GetProperty("sha256").GetString()!);
            if (bytes.Length != length)
            {
                throw new InvalidDataException("A retained source witness has the wrong exact length.");
            }
            result.Add(path, bytes);
        }
        return result;
    }

    private static void ValidateOriginalRecord(JsonElement identity, JsonElement payload, Dictionary<string, byte[]> witnesses)
    {
        string sourcePath = identity.GetProperty("path").GetString()!;
        string pointer = identity.GetProperty("jsonPointer").GetString()!;
        if (identity.GetProperty("inventoryId").GetString() != sourcePath + "#" + pointer)
        {
            throw new InvalidDataException("A source inventory pointer is inconsistent.");
        }
        using JsonDocument original = FarmContentFiles.Parse(witnesses["source-support/" + sourcePath]);
        string[] segments = pointer.Split('/');
        if (segments.Length != 3 || segments[0] != ""
            || !int.TryParse(segments[2], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int index))
        {
            throw new InvalidDataException("The selected source pointer is outside this admission profile.");
        }
        JsonElement originalRecord = original.RootElement.GetProperty(segments[1])[index];
        if (FarmCanonicalJson.Hash(originalRecord) != FarmCanonicalJson.Hash(payload))
        {
            throw new InvalidDataException("The selected source record differs from its original file witness.");
        }
    }
}
