namespace OpenFantasy.Core.Geography;

/// <summary>Signed macro column address, ordered by row then column, independently of basin membership.</summary>
public readonly record struct WatershedColumnAddress(long X, long Y) : IComparable<WatershedColumnAddress>
{
    public int CompareTo(WatershedColumnAddress other)
    {
        int row = Y.CompareTo(other.Y);
        return row != 0 ? row : X.CompareTo(other.X);
    }

    public bool IsAdjacentTo(WatershedColumnAddress other) =>
        Int128.Abs((Int128)X - other.X) + Int128.Abs((Int128)Y - other.Y) == 1;
}

public readonly record struct WatershedColumnReference(EntityId BasinId, int ColumnIndex);

/// <summary>One physical macro face shared by two basins; only an explicit outlet closure permits drainage.</summary>
public sealed record WatershedNetworkFace(EntityId BoundaryId,
    WatershedColumnReference First, WatershedColumnReference Second,
    long FirstNaturalElevation, long SecondNaturalElevation, EntityId? DrainsFromBasinId);

/// <summary>Finite topological coverage witness. Neither the count nor its capacity is liquid storage.</summary>
public sealed record WatershedTerminalContribution(EntityId BasinId, EntityId ExternalBoundaryId,
    int ContributingColumns, int ColumnCapacity);
