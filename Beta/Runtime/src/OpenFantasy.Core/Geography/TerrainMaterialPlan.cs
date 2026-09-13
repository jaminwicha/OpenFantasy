using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>One atomic immutable representation candidate, pinned to its complete input. Not a durable command receipt.</summary>
public sealed class TerrainMaterialPlan
{
    private readonly ulong _baseRevision;
    private readonly ContentHash _baseHash;
    internal TerrainMaterialPlan(TerrainMaterialState basis, TerrainMaterialState candidate)
    {
        _baseRevision = basis.Revision;
        _baseHash = basis.ContentHash;
        Candidate = candidate;
    }

    public TerrainMaterialState Candidate { get; }
    public TerrainMaterialState ApplyTo(TerrainMaterialState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Revision != _baseRevision || state.ContentHash != _baseHash)
        {
            throw new InvalidOperationException("The terrain material plan is stale or belongs to a different complete state.");
        }
        return Candidate;
    }
}
