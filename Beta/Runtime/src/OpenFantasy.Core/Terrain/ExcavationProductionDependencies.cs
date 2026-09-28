using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Terrain;

/// <summary>Actual complete component qualification and the genuine evidence needed by its current material segment.</summary>
public sealed class ExcavationProductionDependencies
{
    public ExcavationProductionDependencies(MaterialExcavationState owner,
        IReadOnlyCollection<ExcavationWorkEvidence>? excavationEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Context = owner.Production.Continuation is { SupportsExcavation: true } context ? context
            : throw new ArgumentException("Excavation decoding requires its actual continued owner.", nameof(owner));
        if (excavationEvidence?.Count > MaterialOperationHistory.MaximumOperations)
            throw new ArgumentException("Excavation evidence exceeds its finite segment bound.");
        var copy = excavationEvidence?.Take(MaterialOperationHistory.MaximumOperations + 1).ToArray() ?? [];
        if (copy.Length > MaterialOperationHistory.MaximumOperations || copy.Any(value => value is null))
            throw new ArgumentException("Excavation evidence must be bounded and non-null.");
        ExcavationEvidence = Array.AsReadOnly(copy);
        OwnerHash = owner.ContentHash;
        Source = owner.Boundary.Source;
        SpatialVersion = ExcavationMaterialSource.SpatialVersion(owner.Space);
    }
    public ProductionContinuationContext Context { get; }
    public ReadOnlyCollection<ExcavationWorkEvidence> ExcavationEvidence { get; }
    internal ContentHash OwnerHash { get; }
    internal ExcavationMaterialSource Source { get; }
    internal ulong SpatialVersion { get; }
}
