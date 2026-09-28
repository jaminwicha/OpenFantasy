using System.Collections.ObjectModel;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Actual admitted geography qualification, exact continuation context and opaque current-segment evidence for nested decoding.</summary>
public sealed class GeographyProductionDependencies
{
    public GeographyProductionDependencies(WatershedRunoffCapacityState owner,
        IReadOnlyCollection<GatherIssueEvidence>? gatherEvidence = null,
        IReadOnlyCollection<ConstructionCompletionEvidence>? constructionEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Context = owner.Production?.Continuation
            ?? throw new ArgumentException("Geography dependencies require an actual continued capacity owner.", nameof(owner));
        Qualification = owner.ContinuationQualification
            ?? throw new ArgumentException("Geography dependencies require the actual admitted fixed domain cut.", nameof(owner));
        GatherEvidence = Copy(gatherEvidence, MaterialOperationHistory.MaximumOperations);
        ConstructionEvidence = Copy(constructionEvidence, ConstructionSnapshot.MaximumProjects);
        if (!Context.SupportsMaterialOperations && GatherEvidence.Count != 0
            || !Context.SupportsConstruction && ConstructionEvidence.Count != 0)
        {
            throw new ArgumentException("Opaque evidence must match the explicitly selected production capability.");
        }
    }

    public GeographyProductionDependencies(LiquidReservoirState owner,
        IReadOnlyCollection<GatherIssueEvidence>? gatherEvidence = null,
        IReadOnlyCollection<ConstructionCompletionEvidence>? constructionEvidence = null)
        : this((owner ?? throw new ArgumentNullException(nameof(owner))).Capacity, gatherEvidence, constructionEvidence)
    {
        ReservoirGeometryHashes = Array.AsReadOnly(owner.Geometries.Select(value => value.ContentHash).ToArray());
    }

    public ProductionContinuationContext Context { get; }
    public ReadOnlyCollection<GatherIssueEvidence> GatherEvidence { get; }
    public ReadOnlyCollection<ConstructionCompletionEvidence> ConstructionEvidence { get; }
    internal RunoffMaterialContinuation Qualification { get; }
    internal ReadOnlyCollection<ContentHash>? ReservoirGeometryHashes { get; }

    internal ProductionSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        return Context.SupportsConstruction ? ProductionCodecV5.Decode(bytes, Context, GatherEvidence, ConstructionEvidence)
            : Context.SupportsMaterialOperations ? ProductionCodecV4.Decode(bytes, Context, GatherEvidence)
            : ProductionCodecV3.Decode(bytes, Context);
    }

    private static ReadOnlyCollection<T> Copy<T>(IReadOnlyCollection<T>? values, int maximum) where T : class
    {
        if (values?.Count > maximum)
        {
            throw new ArgumentException("Geography decoding evidence exceeds its finite bound.", nameof(values));
        }
        T[] copy = values?.Take(maximum + 1).ToArray() ?? [];
        if (copy.Length > maximum || copy.Any(value => value is null))
        {
            throw new ArgumentException("Geography decoding requires bounded non-null opaque evidence.", nameof(values));
        }
        return Array.AsReadOnly(copy);
    }
}
