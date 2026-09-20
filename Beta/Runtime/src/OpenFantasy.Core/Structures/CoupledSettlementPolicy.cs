using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Structures;

/// <summary>Finite assembly and geometric work bounds for one coupled intact settlement interpretation.</summary>
public sealed class CoupledSettlementPolicy
{
    public CoupledSettlementPolicy(int maximumAssemblies)
    {
        if (maximumAssemblies is < 1 or > ConstructionSnapshot.MaximumProjects)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAssemblies));
        }
        MaximumAssemblies = maximumAssemblies;
    }

    public int MaximumAssemblies { get; }
    public int MaximumWorkUnits => 2 * MaximumAssemblies * ConstructionDefinition.MaximumMembers
        + 2 * LocalSpatialSnapshot.MaximumActors;
    public ContentHash ContentHash => ContentHash.Compute(CoupledSettlementPolicyCodecV1.Encode(this));
}
