using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Prepares a pinned production component without replacing or resetting an existing gathered world.</summary>
public static class ProductionFarmAdmission
{
    public static ContentHash AdmissionVersion => new(ProductionFarmDefinition.AcceptedHash);

    public static ProductionSnapshot CreateProduction(WorldSnapshot world) =>
        Create(FarmContentFiles.Embedded(), world);

    internal static ProductionSnapshot CreateProduction(string contentRoot, WorldSnapshot world) =>
        Create(new FarmContentFiles(contentRoot), world);

    private static ProductionSnapshot Create(FarmContentFiles files, WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ProductionFarmDefinition.RequireGatheredWorld(world);
        ProductionFarmDefinition definition = ProductionFarmDefinition.Load(files);
        ProductionFarmSourceValidation.Validate(files, definition);
        return new ProductionFarmBuilder(world, definition).Build();
    }
}
