using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>A complete placement candidate pinned to its exact material owner, profile and starting boundary.</summary>
public sealed class LiquidCellPlan
{
    private readonly ContentHash _basis;

    internal LiquidCellPlan(LiquidCellState basis, LiquidCellState candidate)
    {
        _basis = basis.ContentHash;
        Candidate = candidate;
    }

    public LiquidCellState Candidate { get; }

    public LiquidCellState ApplyTo(LiquidCellState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ContentHash != _basis)
        {
            throw new InvalidOperationException("The complete liquid placement changed before publication.");
        }
        return Candidate;
    }
}
