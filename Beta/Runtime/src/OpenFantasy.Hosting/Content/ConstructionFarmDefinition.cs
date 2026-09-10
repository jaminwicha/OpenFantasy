using System.Text.Json;

namespace OpenFantasy.Hosting.Content;

/// <summary>Pinned authored fabrication and site metadata, separate from unchanged source records.</summary>
internal sealed class ConstructionFarmDefinition
{
    internal const string RelativePath = "construction-farm/admission-v1.json";
    internal const string AcceptedHash = "6bce3173a4f5aadc1378cd69b687b74b9af54243221c33183299a686d0da6f0e";
    internal const int MaximumByteLength = 16_384;
    private ConstructionFarmDefinition(JsonElement root) => Root = root;
    internal JsonElement Root { get; }

    internal static ConstructionFarmDefinition Load(FarmContentFiles files)
    {
        using JsonDocument document = FarmContentFiles.Parse(files.Read(RelativePath, MaximumByteLength, AcceptedHash));
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("id").GetString() != "openfantasy.construction-farm.admission"
            || root.GetProperty("questAdmissionSha256").GetString() != QuestFarmDefinition.AcceptedHash
            || root.GetProperty("requiresWorldProfile").GetInt32() != 4
            || root.GetProperty("sourceWitnesses").GetArrayLength() != 4
            || root.GetProperty("construction").GetProperty("membersMillimetres").GetArrayLength() != 10)
        {
            throw new InvalidDataException("The construction manifest differs from its finite admitted shape.");
        }
        return new ConstructionFarmDefinition(root.Clone());
    }

    internal QuestFarmSources ValidateSources(FarmContentFiles files)
    {
        QuestFarmSources sources = QuestFarmSources.Load(files, QuestFarmDefinition.Load(files));
        foreach (JsonElement witness in Root.GetProperty("sourceWitnesses").EnumerateArray())
        {
            files.Read(witness.GetProperty("path").GetString()!, 262_144, witness.GetProperty("sha256").GetString()!);
        }
        JsonElement farm = Root.GetProperty("sourceFarm");
        using JsonDocument original = FarmContentFiles.Parse(files.Read(
            "legacy/aldermere-first-loop/source-support/data/world.json", 262_144,
            "9f46be269ea80ddeef908d9a8305b70bf26a8ca11a544bed32c640fbfee7668d"));
        if (FarmCanonicalJson.Hash(original.RootElement.GetProperty("regions")[18]).Value
                != farm.GetProperty("payloadSha256").GetString())
        {
            throw new InvalidDataException("The construction site no longer names its original farm record.");
        }
        return sources;
    }
}
