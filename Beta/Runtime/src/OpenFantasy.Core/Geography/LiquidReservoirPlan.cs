using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>Atomic candidate pinning both the capacity owner and every physical reservoir.</summary>
public sealed class LiquidReservoirPlan
{
    private readonly ContentHash _basisHash;

    internal LiquidReservoirPlan(LiquidReservoirState basis, LiquidReservoirState candidate)
    {
        _basisHash = basis.ContentHash;
        Candidate = candidate;
    }

    public LiquidReservoirState Candidate { get; }

    public LiquidReservoirState ApplyTo(LiquidReservoirState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ContentHash != _basisHash)
        {
            throw new InvalidOperationException("The complete reservoir geometry or capacity basis changed before publication.");
        }
        return Candidate;
    }
}
