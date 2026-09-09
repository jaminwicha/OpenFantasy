namespace OpenFantasy.Core.Custody;

/// <summary>
/// A half-open interval of counted source units. Source identity exists before extraction
/// and is retained across branch copies; this value grants no inter-branch export rights.
/// </summary>
public sealed record SourceInterval
{
    public SourceInterval(EntityId sourceId, ulong start, ulong end)
    {
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "A source interval must contain positive units.");
        }
        SourceId = sourceId;
        Start = start;
        End = end;
    }

    public EntityId SourceId { get; }
    public ulong Start { get; }
    public ulong End { get; }
    public ulong Quantity => End - Start;
}
