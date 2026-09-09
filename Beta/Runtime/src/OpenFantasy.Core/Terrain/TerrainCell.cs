using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Terrain;

public enum TerrainStratum
{
    Void = 0,
    Surface = 1,
    Soil = 2,
    Stone = 3
}

public enum StandingSpace
{
    Unavailable = 0,
    Blocked = 1,
    Unsupported = 2,
    Supported = 3
}

/// <summary>
/// Initial substrate plus current whole-cell occupancy. Excavation retains its original lineage;
/// source identity is not a minted item, a material quantity, or a right to export material.
/// </summary>
public sealed class TerrainCell
{
    internal TerrainCell(CellCoordinate coordinate, TerrainStratum stratum, CountedItemDefinition? material,
        EntityId? formationId, EntityId? sourceId, bool isExcavated = false)
    {
        Coordinate = coordinate;
        Stratum = stratum;
        Material = material;
        FormationId = formationId;
        SourceId = sourceId;
        IsExcavated = isExcavated;
    }

    public CellCoordinate Coordinate { get; }
    public TerrainStratum Stratum { get; }
    public CountedItemDefinition? Material { get; }
    public EntityId? FormationId { get; }
    public EntityId? SourceId { get; }
    public bool IsExcavated { get; }
    public bool IsSolid => Stratum != TerrainStratum.Void && !IsExcavated;

    internal TerrainCell Excavate() => new(Coordinate, Stratum, Material, FormationId, SourceId, true);
}
