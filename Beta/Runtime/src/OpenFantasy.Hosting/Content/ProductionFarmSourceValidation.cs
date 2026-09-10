using System.Text.Json;

namespace OpenFantasy.Hosting.Content;

/// <summary>Reads the complete retained closure and binds the small packing recipe to its original records.</summary>
internal static class ProductionFarmSourceValidation
{
    internal static void Validate(FarmContentFiles files, ProductionFarmDefinition definition)
    {
        FarmAdmissionDefinition baseDefinition = FarmAdmissionDefinition.Load(files);
        FarmSourcePacket baseSource = FarmSourcePacket.Load(files, baseDefinition.Root);
        baseDefinition.ValidateSources(baseSource);
        using JsonDocument gathered = FarmContentFiles.Parse(files.Read(GatheringFarmDefinition.RelativePath,
            16_384, GatheringFarmDefinition.AcceptedHash));
        GatheringFarmSources source = GatheringFarmSources.Load(files, gathered.RootElement);
        var admittedSources = gathered.RootElement.GetProperty("sourceRecords").EnumerateArray()
            .ToDictionary(value => value.GetProperty("targetKey").GetString()!, StringComparer.Ordinal);
        foreach (JsonElement selected in definition.Root.GetProperty("sourceRecords").EnumerateArray())
        {
            string key = selected.GetProperty("targetKey").GetString()!;
            if (!admittedSources.TryGetValue(key, out JsonElement original)
                || FarmCanonicalJson.Hash(selected.GetProperty("sourceIdentity"))
                    != FarmCanonicalJson.Hash(original.GetProperty("sourceIdentity"))
                || FarmCanonicalJson.Hash(source.Record(key)).Value
                    != selected.GetProperty("sourceIdentity").GetProperty("payloadSha256").GetString())
            {
                throw new InvalidDataException("A production source record differs from its frozen original pointer or payload.");
            }
        }

        JsonElement recipe = source.Record(definition.Recipe.GetProperty("sourceTargetKey").GetString()!);
        if (recipe.GetProperty("id").GetString() != "craft_rations"
            || recipe.GetProperty("output").GetString() != "rations"
            || recipe.GetProperty("outputCount").GetUInt64() != 2
            || recipe.GetProperty("inputs").GetArrayLength() != 2
            || FarmCanonicalJson.Hash(recipe).Value != definition.Recipe.GetProperty("sourcePayloadSha256").GetString())
        {
            throw new InvalidDataException("The production operation differs from the retained ration recipe.");
        }
        foreach (JsonElement input in recipe.GetProperty("inputs").EnumerateArray())
        {
            string key = "items/" + input.GetProperty("item").GetString();
            JsonElement term = definition.Recipe.GetProperty("terms").EnumerateArray().Single(value =>
                value.GetProperty("role").GetInt32() == 1 && value.GetProperty("itemKey").GetString() == key);
            if (input.GetProperty("count").GetUInt64() != term.GetProperty("count").GetUInt64())
            {
                throw new InvalidDataException("The production input counts must retain the original recipe.");
            }
        }
        foreach (JsonElement item in definition.Root.GetProperty("outputItems").EnumerateArray())
        {
            if (item.GetProperty("sourceTargetKey").ValueKind == JsonValueKind.Null)
            {
                continue;
            }
            JsonElement original = source.Record(item.GetProperty("sourceTargetKey").GetString()!);
            if (FarmCanonicalJson.Hash(original).Value != item.GetProperty("sourcePayloadSha256").GetString()
                || original.GetProperty("name").GetString() != item.GetProperty("displayName").GetString())
            {
                throw new InvalidDataException("The admitted ration item must retain its original source content.");
            }
        }
    }
}
