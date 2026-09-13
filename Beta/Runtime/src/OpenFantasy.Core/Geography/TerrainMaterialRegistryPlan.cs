using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>A complete registry candidate. Durable acceptance and command retries belong to its integrating owner.</summary>
public sealed class TerrainMaterialRegistryPlan
{
    private readonly ulong _baseRevision;
    private readonly ContentHash _baseHash;
    internal TerrainMaterialRegistryPlan(TerrainMaterialRegistry basis, TerrainMaterialRegistry candidate)
    {
        _baseRevision = basis.Revision;
        _baseHash = basis.ContentHash;
        Candidate = candidate;
    }

    public TerrainMaterialRegistry Candidate { get; }
    public TerrainMaterialRegistry ApplyTo(TerrainMaterialRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (registry.Revision != _baseRevision || registry.ContentHash != _baseHash)
        {
            throw new InvalidOperationException("The material registry plan is stale or has a different complete basis.");
        }
        return Candidate;
    }
}
