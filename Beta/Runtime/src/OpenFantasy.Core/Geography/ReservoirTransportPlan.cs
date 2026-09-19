using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>Pure publication candidate pinned to the entire physical owner and transport journal.</summary>
public sealed class ReservoirTransportPlan
{
    private readonly ContentHash _basis;

    internal ReservoirTransportPlan(ReservoirTransportState basis, ReservoirTransportState candidate, IEnumerable<EntityId>? createdIds = null)
    {
        _basis = basis.ContentHash;
        Candidate = candidate;
        CreatedIds = (createdIds ?? []).ToList().AsReadOnly();
    }

    public ReservoirTransportState Candidate { get; }
    public ReadOnlyCollection<EntityId> CreatedIds { get; }

    public ReservoirTransportState ApplyTo(ReservoirTransportState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ContentHash != _basis)
        {
            throw new InvalidOperationException("The complete transport owner changed before publication.");
        }
        return Candidate;
    }
}
