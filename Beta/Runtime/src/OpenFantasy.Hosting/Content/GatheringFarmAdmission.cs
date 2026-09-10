using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Creates a new finite gathering profile without converting or replacing an existing farm save.</summary>
public static class GatheringFarmAdmission
{
    public static WorldSnapshot CreateEmbedded(string seedText, string playerName)
    {
        GatheringFarmInput input = GatheringFarmInput.Create(seedText, playerName);
        WorldSnapshot basis = FarmWorldAdmission.CreateEmbedded(input.BaseSeedText, playerName);
        return Create(FarmContentFiles.Embedded(), basis, input);
    }

    internal static WorldSnapshot Create(string contentRoot, string seedText, string playerName)
    {
        GatheringFarmInput input = GatheringFarmInput.Create(seedText, playerName);
        WorldSnapshot basis = FarmWorldAdmission.Create(contentRoot, input.BaseSeedText, playerName);
        return Create(new FarmContentFiles(contentRoot), basis, input);
    }

    private static WorldSnapshot Create(FarmContentFiles files, WorldSnapshot basis, GatheringFarmInput input)
    {
        GatheringFarmDefinition definition = GatheringFarmDefinition.Load(files, basis);
        GatheringFarmSources sources = GatheringFarmSources.Load(files, definition.Root);
        definition.ValidateSources(sources);
        return new GatheringFarmBuilder(basis, definition, sources, input).Build();
    }
}
