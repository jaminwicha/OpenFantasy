using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Immutable structural extraction of existing material rights, bound to the exact supplied owners.
/// It neither removes their history nor enables continued production, care or world publication.
/// </summary>
/// <remarks>
/// Capture validates production against inventory, but source capacity is not evidence of issuance.
/// Complete world admission, actual source-issuance owners and authenticated archive/journal recovery
/// must qualify any future continuation. A digest or this observation alone grants no material rights.
/// </remarks>
public sealed class ProductionContinuationBasis
{
    public const int MaximumLiveLots = CustodySnapshot.MaximumLots;
    public const int MaximumOutputBudgets = MaximumLiveLots;
    public const int MaximumBatchWitnesses = MaximumLiveLots;
    // A closed V1/V2 owner contains at most this many direct retirement records before merging.
    // Fragmentation has its own bound; it cannot be inferred from the number of live lots.
    public const int MaximumRawRetirementRanges = ProductionSnapshot.MaximumBatches
        * MaterialRecipeDefinition.MaximumTermCount + ProductionSnapshot.MaximumConsumedItems;
    public const int MaximumEncodedByteLength = 8 * 1024 * 1024;

    private readonly Dictionary<(EntityId BatchId, uint OutputIndex), ImportedOutputBudget> _outputBudgets;

    private ProductionContinuationBasis(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        WorldId = inventory.WorldId;
        BranchId = inventory.BranchId;
        InventoryHash = inventory.ContentHash;
        ProductionHash = production.ContentHash;
        ProductionRevision = production.Revision;
        CustodyRevision = inventory.Custody.Revision;
        RawLots = Array.AsReadOnly(inventory.Custody.Lots.OrderBy(value => value.LotId).Select(value =>
            new CountedSourceLot(value.LotId, value.CustodianId, value.Definition,
                new(value.Source.SourceId, value.Source.Start, value.Source.End), value.Revision,
                value.Protection, value.ReservedBy)).ToArray());
        TransformedLots = Array.AsReadOnly(production.Lots.OrderBy(value => value.LotId).Select(value =>
            new TransformedLot(value.LotId, value.CustodianId, value.Definition,
                new(value.Output.BatchId, value.Output.OutputIndex, value.Output.Start, value.Output.End),
                value.Revision, value.Protection, value.ReservedBy)).ToArray());
        RawRetirements = CaptureRawRetirements(production);
        OutputBudgets = CaptureOutputBudgets(TransformedLots);
        _outputBudgets = OutputBudgets.ToDictionary(value => (value.BatchId, value.OutputIndex));
        var liveProducers = OutputBudgets.Select(value => value.BatchId).ToHashSet();
        IEnumerable<ProductionBatchWitness> priorWitnesses = production.Continuation is { } context
            ? context.Basis.BatchWitnesses : [];
        BatchWitnesses = Array.AsReadOnly(production.Batches.Select(value => new ProductionBatchWitness(value))
            .Concat(priorWitnesses).Where(value => liveProducers.Contains(value.BatchId))
            .OrderBy(value => value.BatchId).ToArray());
        if (BatchWitnesses.Count != liveProducers.Count || BatchWitnesses.Count > MaximumBatchWitnesses)
            throw new ArgumentException("Every imported live output requires one bounded actual producing witness.", nameof(production));
        ContentHash = ContentHash.Compute(ProductionContinuationBasisEncoding.Encode(this));
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ContentHash InventoryHash { get; }
    public ContentHash ProductionHash { get; }
    public ulong ProductionRevision { get; }
    public ulong CustodyRevision { get; }
    public ReadOnlyCollection<CountedSourceLot> RawLots { get; }
    public ReadOnlyCollection<TransformedLot> TransformedLots { get; }
    public ReadOnlyCollection<DirectRawRetirementRange> RawRetirements { get; }
    public ReadOnlyCollection<ImportedOutputBudget> OutputBudgets { get; }
    public ReadOnlyCollection<ProductionBatchWitness> BatchWitnesses { get; }
    public ContentHash ContentHash { get; }

    /// <summary>
    /// Derives the observation from the actual supplied immutable owners. It accepts no caller-made
    /// frontier and supplies no structural decoder that could restore spendable rights from its bytes.
    /// </summary>
    public static ProductionContinuationBasis Capture(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        production.ValidateForAuthority(inventory);
        if (inventory.Custody.Lots.Count + production.Lots.Count > MaximumLiveLots)
            throw new ArgumentException("The captured material frontier exceeds its combined live-lot bound.", nameof(production));
        return new(inventory, production);
    }

    public ImportedOutputBudget GetOutputBudget(EntityId batchId, uint outputIndex) =>
        _outputBudgets.TryGetValue((batchId, outputIndex), out var budget) ? budget
            : throw new ArgumentException("The producing output has no live exported rights in this basis.", nameof(batchId));

    /// <summary>Fresh canonical observation bytes, not an independently restorable inventory or authority archive.</summary>
    public byte[] Encode() => ProductionContinuationBasisEncoding.Encode(this);

    private static ReadOnlyCollection<DirectRawRetirementRange> CaptureRawRetirements(ProductionSnapshot production)
    {
        List<DirectRawRetirementRange> copied = production.Continuation?.Basis.RawRetirements.ToList() ?? [];
        foreach (var retirement in ProductionRetirementHistory.CurrentRetirements(production))
        {
            if (retirement.Ingredient.Source is not { } source) continue;
            if (copied.Count == 2 * MaximumRawRetirementRanges)
                throw new ArgumentException("The imported and active raw retirement input exceeds its bounded envelope.", nameof(production));
            copied.Add(new(retirement.CustodianId, retirement.Ingredient.Definition,
                new(source.SourceId, source.Start, source.End)));
        }
        var ordered = copied.OrderBy(value => value.Source.SourceId).ThenBy(value => value.Source.Start);
        List<DirectRawRetirementRange> normalized = [];
        foreach (var range in ordered)
        {
            if (normalized.Count != 0)
            {
                var prior = normalized[^1];
                if (range.Source.SourceId == prior.Source.SourceId)
                {
                    if (range.Source.Start < prior.Source.End || range.Definition != prior.Definition)
                        throw new ArgumentException("Raw retirement must retain disjoint rights and one exact source pin.", nameof(production));
                    if (range.Source.Start == prior.Source.End && range.CustodianId == prior.CustodianId)
                    {
                        normalized[^1] = new(prior.CustodianId, prior.Definition,
                            new(prior.Source.SourceId, prior.Source.Start, range.Source.End));
                        continue;
                    }
                }
            }
            normalized.Add(range);
        }
        if (normalized.Count > MaximumRawRetirementRanges)
            throw new ArgumentException("Normalized direct raw retirement exceeds its exact coverage envelope.", nameof(production));
        return normalized.AsReadOnly();
    }

    private static ReadOnlyCollection<ImportedOutputBudget> CaptureOutputBudgets(IReadOnlyCollection<TransformedLot> lots)
    {
        List<ImportedOutputBudget> budgets = [];
        foreach (var group in lots.GroupBy(value => (value.Output.BatchId, value.Output.OutputIndex))
            .OrderBy(value => value.Key.BatchId).ThenBy(value => value.Key.OutputIndex))
        {
            var definition = group.First().Definition;
            if (budgets.Count == MaximumOutputBudgets || group.Any(value => value.Definition != definition))
                throw new ArgumentException("Imported output budgets require bounded live rights with an exact item pin.", nameof(lots));
            budgets.Add(new(group.Key.BatchId, group.Key.OutputIndex, definition, group.Select(value => value.Output)));
        }
        return budgets.AsReadOnly();
    }
}
