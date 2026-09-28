using System.Collections.ObjectModel;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Exact context from an actual continued registry and the opaque evidence needed for its current segment.</summary>
public sealed class TerrainProductionDependencies
{
    public TerrainProductionDependencies(TerrainMaterialRegistry owner,
        IReadOnlyCollection<GatherIssueEvidence>? gatherEvidence = null,
        IReadOnlyCollection<ConstructionCompletionEvidence>? constructionEvidence = null,
        IReadOnlyCollection<TerrainRepresentationEvidence>? terrainEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Context = owner.Production?.Continuation is { SupportsTerrainRepresentation: true } context ? context
            : throw new ArgumentException("Terrain decoding requires an actual continued registry.", nameof(owner));
        GatherEvidence = Copy(gatherEvidence, MaterialOperationHistory.MaximumOperations);
        ConstructionEvidence = Copy(constructionEvidence, ConstructionSnapshot.MaximumProjects);
        TerrainEvidence = Copy(terrainEvidence, MaterialOperationHistory.MaximumOperations);
        if (!Context.SupportsConstruction && ConstructionEvidence.Count != 0)
            throw new ArgumentException("Construction evidence requires its explicitly retained capability.");
    }

    public ProductionContinuationContext Context { get; }
    public ReadOnlyCollection<GatherIssueEvidence> GatherEvidence { get; }
    public ReadOnlyCollection<ConstructionCompletionEvidence> ConstructionEvidence { get; }
    public ReadOnlyCollection<TerrainRepresentationEvidence> TerrainEvidence { get; }

    internal ProductionSnapshot Decode(ReadOnlySpan<byte> bytes) =>
        ProductionCodecV6.Decode(bytes, Context, GatherEvidence, ConstructionEvidence, TerrainEvidence);

    private static ReadOnlyCollection<T> Copy<T>(IReadOnlyCollection<T>? values, int maximum) where T : class
    {
        if (values?.Count > maximum) throw new ArgumentException("Terrain proof collection exceeds its finite bound.");
        T[] copy = values?.Take(maximum + 1).ToArray() ?? [];
        if (copy.Length > maximum || copy.Any(value => value is null))
            throw new ArgumentException("Terrain proof collections must be bounded and non-null.");
        return Array.AsReadOnly(copy);
    }
}
