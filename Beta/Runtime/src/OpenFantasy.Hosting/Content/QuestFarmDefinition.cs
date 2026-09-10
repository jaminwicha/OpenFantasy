using System.Text.Json;

namespace OpenFantasy.Hosting.Content;

/// <summary>Bounded, immutable authored admissions kept separate from unchanged original source records.</summary>
internal sealed class QuestFarmDefinition
{
    internal const string RelativePath = "quest-farm/admission-v1.json";
    internal const string AcceptedHash = "261e86948438c7bd8ad34e001c918c40053acedf82041a3f89909a54db095918";
    internal const int MaximumByteLength = 16_384;

    private QuestFarmDefinition(JsonElement root) => Root = root;
    internal JsonElement Root { get; }

    internal static QuestFarmDefinition Load(FarmContentFiles files)
    {
        using JsonDocument document = FarmContentFiles.Parse(files.Read(RelativePath, MaximumByteLength, AcceptedHash));
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("id").GetString() != "openfantasy.quest-farm.admission"
            || root.GetProperty("productionAdmissionSha256").GetString() != ProductionFarmDefinition.AcceptedHash
            || root.GetProperty("sourceRecords").GetArrayLength() != 4
            || root.GetProperty("sourceWitnesses").GetArrayLength() != 5
            || root.GetProperty("levelCurve").GetProperty("thresholds").GetArrayLength() != 9
            || root.GetProperty("quest").GetProperty("initialParticipations").GetArrayLength() != 0
            || root.GetProperty("compatibility").GetProperty("requiresWorldProfile").GetInt32() != 3)
        {
            throw new InvalidDataException("The quest farm manifest has an unsupported finite shape or admission pin.");
        }
        return new QuestFarmDefinition(root.Clone());
    }
}
