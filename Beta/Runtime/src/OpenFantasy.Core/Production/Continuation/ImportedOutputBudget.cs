using System.Collections.ObjectModel;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Only the output intervals still live at a captured boundary, not the original recipe's full
/// output budget. Custody, designation and revision remain on the captured individual lots.
/// </summary>
public sealed class ImportedOutputBudget
{
    internal ImportedOutputBudget(EntityId batchId, uint outputIndex, CountedItemDefinition definition,
        IEnumerable<ProductionOutputInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(intervals);
        List<ProductionOutputInterval> copied = [];
        foreach (var interval in intervals)
        {
            ArgumentNullException.ThrowIfNull(interval);
            if (copied.Count == ProductionContinuationBasis.MaximumLiveLots
                || interval.BatchId != batchId || interval.OutputIndex != outputIndex)
                throw new ArgumentException("Imported output rights must fit one bounded producing output.", nameof(intervals));
            copied.Add(interval);
        }
        if (copied.Count == 0)
            throw new ArgumentException("An imported output budget requires actual live rights.", nameof(intervals));
        copied.Sort((left, right) => left.Start.CompareTo(right.Start));
        List<ProductionOutputInterval> normalized = [];
        foreach (var interval in copied)
        {
            if (normalized.Count != 0)
            {
                var prior = normalized[^1];
                if (interval.Start < prior.End)
                    throw new ArgumentException("Imported output intervals cannot overlap.", nameof(intervals));
                if (interval.Start == prior.End)
                {
                    normalized[^1] = new(batchId, outputIndex, prior.Start, interval.End);
                    continue;
                }
            }
            normalized.Add(new(batchId, outputIndex, interval.Start, interval.End));
        }
        BatchId = batchId;
        OutputIndex = outputIndex;
        Definition = definition;
        Intervals = normalized.AsReadOnly();
    }

    public EntityId BatchId { get; }
    public uint OutputIndex { get; }
    public CountedItemDefinition Definition { get; }
    public ReadOnlyCollection<ProductionOutputInterval> Intervals { get; }

    /// <summary>
    /// Tests exact interval coverage only. It does not authenticate the producer or grant control,
    /// possession, publication, or permission to spend a protected/reserved lot.
    /// </summary>
    public bool Covers(ProductionOutputInterval interval, CountedItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(definition);
        return interval.BatchId == BatchId && interval.OutputIndex == OutputIndex && definition == Definition
            && Intervals.Any(value => value.Start <= interval.Start && interval.End <= value.End);
    }
}
