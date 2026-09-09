using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Custody;

/// <summary>A complete candidate replacement, publishable only with the future authority transaction.</summary>
public sealed class CustodyTransferPlan
{
    internal CustodyTransferPlan(CustodySnapshot original, CustodySnapshot candidate, EntityId movedLotId)
    {
        BaseRevision = original.Revision;
        BaseHash = original.ContentHash;
        Candidate = candidate;
        MovedLotId = movedLotId;
    }

    public ulong BaseRevision { get; }
    public ContentHash BaseHash { get; }
    public CustodySnapshot Candidate { get; }
    public EntityId MovedLotId { get; }

    public CustodySnapshot ApplyTo(CustodySnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Revision != BaseRevision || current.ContentHash != BaseHash)
        {
            throw new CustodyConflictException(CustodyConflict.StalePlan);
        }
        return Candidate;
    }
}
