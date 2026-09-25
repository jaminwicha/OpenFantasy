using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>One whole-cell placement within a uniquely owned fixed vessel.</summary>
public readonly record struct LiquidCellKey(EntityId ReservoirId, CellCoordinate Coordinate) : IComparable<LiquidCellKey>
{
    public int CompareTo(LiquidCellKey other)
    {
        int ownerOrder = ReservoirId.CompareTo(other.ReservoirId);
        return ownerOrder != 0 ? ownerOrder : Coordinate.CompareTo(other.Coordinate);
    }
}
