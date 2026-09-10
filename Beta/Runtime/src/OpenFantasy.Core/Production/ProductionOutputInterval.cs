namespace OpenFantasy.Core.Production;

/// <summary>A disjoint claim on one batch's output budget; never a newly admitted raw-material source.</summary>
public sealed record ProductionOutputInterval
{
    public ProductionOutputInterval(EntityId batchId, uint outputIndex, ulong start, ulong end)
    {
        if (outputIndex >= MaterialRecipeDefinition.MaximumTermCount || start >= end)
        {
            throw new ArgumentException("A produced interval requires a supported output index and positive quantity.");
        }
        BatchId = batchId;
        OutputIndex = outputIndex;
        Start = start;
        End = end;
    }

    public EntityId BatchId { get; }
    public uint OutputIndex { get; }
    public ulong Start { get; }
    public ulong End { get; }
    public ulong Quantity => End - Start;
}
