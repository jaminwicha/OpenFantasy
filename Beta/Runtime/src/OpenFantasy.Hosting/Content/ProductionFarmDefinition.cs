using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>The exact authored packing interpretation, separate from immutable source item content.</summary>
internal sealed class ProductionFarmDefinition
{
    internal const string RelativePath = "production-farm/admission-v1.json";
    internal const string AcceptedHash = "7419a27a9936c8157c98ab99537b2d4433b4d891a6ad9e61320346b551dfe7e1";
    internal const int MaximumByteLength = 16_384;

    private ProductionFarmDefinition(JsonElement root) => Root = root;

    internal JsonElement Root { get; }
    internal JsonElement Recipe => Root.GetProperty("recipe");

    internal static void RequireGatheredWorld(WorldSnapshot world)
    {
        if (world.ProfileVersion != 2 || world.Gathering is null
            || world.AdmissionVersion.Value != GatheringFarmDefinition.AcceptedHash)
        {
            throw new ArgumentException("Production admission requires the exact gathered farm profile and an explicit migration.", nameof(world));
        }
    }

    internal static ProductionFarmDefinition Load(FarmContentFiles files)
    {
        byte[] bytes = files.Read(RelativePath, MaximumByteLength, AcceptedHash);
        using JsonDocument document = FarmContentFiles.Parse(bytes);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("id").GetString() != "openfantasy.production-farm.admission"
            || root.GetProperty("gatheringAdmissionSha256").GetString() != GatheringFarmDefinition.AcceptedHash
            || root.GetProperty("sourceRecords").GetArrayLength() != 4
            || root.GetProperty("existingItems").GetArrayLength() != 2
            || root.GetProperty("outputItems").GetArrayLength() != 2
            || root.GetProperty("physicalMaterials").GetArrayLength() != 4
            || root.GetProperty("recipe").GetProperty("requiredWork").GetUInt64() != 1
            || root.GetProperty("recipe").GetProperty("terms").GetArrayLength() != 4
            || root.GetProperty("recipe").GetProperty("allocations").GetArrayLength() != 3)
        {
            throw new InvalidDataException("The production farm manifest has an unsupported finite shape or rule pin.");
        }
        return new ProductionFarmDefinition(root.Clone());
    }

    internal static CountedItemDefinition ExistingItem(JsonElement item) => new(
        OpenFantasy.Core.EntityId.Parse(item.GetProperty("definitionId").GetString()),
        new ContentHash(item.GetProperty("version").GetString()!));
}
