using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>Publication candidate pinned to the complete reservation owner, including capacity-only mutations.</summary>
public sealed class WatershedRunoffCapacityPlan
{
    private readonly ContentHash _basisHash;

    internal WatershedRunoffCapacityPlan(WatershedRunoffCapacityState basis, WatershedRunoffCapacityState candidate)
    {
        _basisHash = basis.ContentHash;
        Candidate = candidate;
    }

    public WatershedRunoffCapacityState Candidate { get; }

    public WatershedRunoffCapacityState ApplyTo(WatershedRunoffCapacityState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ContentHash != _basisHash)
        {
            throw new InvalidOperationException("The complete runoff capacity owner changed before publication.");
        }
        return Candidate;
    }
}
