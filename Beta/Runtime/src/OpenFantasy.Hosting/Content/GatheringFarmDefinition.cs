using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Exact authored finite calibration, retaining source mechanics and explicit pending work.</summary>
internal sealed class GatheringFarmDefinition
{
    internal const string RelativePath = "adventure-farm/admission-v1.json";
    internal const string AcceptedHash = "f90505e21a578b6e8dd0df78ea195e1c2ee9e5451fee22779480b76ab04239cd";

    private GatheringFarmDefinition(JsonElement root, ContentHash version)
    {
        Root = root;
        Version = version;
    }

    internal JsonElement Root { get; }
    internal ContentHash Version { get; }

    internal static GatheringFarmDefinition Load(FarmContentFiles files, WorldSnapshot basis)
    {
        byte[] bytes = files.Read(RelativePath, 16_384, AcceptedHash);
        using JsonDocument document = FarmContentFiles.Parse(bytes);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("id").GetString() != "openfantasy.adventure-farm.admission"
            || root.GetProperty("baseAdmissionSha256").GetString() != basis.AdmissionVersion.Value
            || basis.ProfileVersion != 1 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold
            || root.GetProperty("items").GetArrayLength() != 3 || root.GetProperty("nodes").GetArrayLength() != 2
            || !root.GetProperty("starterKit").GetProperty("preserveBaseStock").GetBoolean())
        {
            throw new InvalidDataException("The gathering admission requires the exact uncommitted base farm and supported profile.");
        }
        JsonElement work = root.GetProperty("workProfile");
        if (work.GetProperty("initialEffort").GetUInt32() != 0
            || work.GetProperty("gainPerAdvancingStep").GetUInt32() != 10
            || work.GetProperty("maximumEffort").GetUInt32() != 1000
            || work.GetProperty("swingCost").GetUInt32() != 400
            || work.GetProperty("sourceTicksPerSecond").GetUInt32() != 20
            || work.GetProperty("calendarMillisecondsPerAdvancingStep").GetUInt32() != 1800
            || root.GetProperty("gatherRangeSubunits").GetUInt32() != 448)
        {
            throw new InvalidDataException("The authored gathering work profile is incompatible with its retained rule mapping.");
        }
        return new GatheringFarmDefinition(root.Clone(), ContentHash.Compute(bytes));
    }

    internal void ValidateSources(GatheringFarmSources sources)
    {
        foreach (JsonElement item in Root.GetProperty("items").EnumerateArray())
        {
            JsonElement original = sources.Record(item.GetProperty("sourceTargetKey").GetString()!);
            if ("items/" + original.GetProperty("id").GetString() != item.GetProperty("key").GetString()
                || FarmCanonicalJson.Hash(original).Value != item.GetProperty("sourcePayloadSha256").GetString())
            {
                throw new InvalidDataException("A counted item admission differs from its retained source identity.");
            }
        }
        foreach (JsonElement tool in Root.GetProperty("starterKit").GetProperty("tools").EnumerateArray())
        {
            string key = tool.GetProperty("itemKey").GetString()!;
            JsonElement original = sources.Record("openfantasy.aldermere:" + key);
            string expectedKind = tool.GetProperty("kind").GetInt32() == 1 ? "Axe" : "Sickle";
            if (original.GetProperty("kind").GetString() != "Tool" || original.GetProperty("tool").GetString() != expectedKind
                || original.GetProperty("toolPower").GetUInt32() != tool.GetProperty("power").GetUInt32()
                || tool.GetProperty("quantity").GetUInt64() != 1)
            {
                throw new InvalidDataException("The supplied tool kit differs from its source tool mechanics.");
            }
        }
        foreach (JsonElement node in Root.GetProperty("nodes").EnumerateArray())
        {
            JsonElement spawner = sources.Record(node.GetProperty("sourceSpawnerTargetKey").GetString()!);
            JsonElement archetype = sources.Archetype(node.GetProperty("sourceArchetypeTargetKey").GetString()!)
                .GetProperty("extractedFields");
            int x = node.GetProperty("tileX").GetInt32();
            int y = node.GetProperty("tileY").GetInt32();
            if (spawner.GetProperty("type").GetString() != node.GetProperty("role").GetString()
                || archetype.GetProperty("hp").GetInt32() != node.GetProperty("swingsPerCycle").GetInt32()
                || "items/" + archetype.GetProperty("yieldItem").GetString() != node.GetProperty("yieldItemKey").GetString()
                || spawner.GetProperty("respawnTicks").GetUInt64() != node.GetProperty("regrowthDelaySteps").GetUInt64()
                || node.GetProperty("completeCycles").GetUInt64() != 32 || node.GetProperty("stumpSteps").GetUInt64() != 20
                || !GatheringFarmSources.Contains(spawner.GetProperty("region"), x, y)
                || !sources.IsOriginalWalkableOutsideSafeZones(x, y))
            {
                throw new InvalidDataException("A representative gathering node violates its source mechanics or authored placement.");
            }
        }
    }
}
