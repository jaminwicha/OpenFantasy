using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>A local column referring to existing macro ancestry; these references do not independently own those identities.</summary>
public sealed record WatershedTerrainColumn(long X, long Y, WatershedColumnReference MacroColumn,
    EntityId FormationId, EntityId SubstrateParcelId, long SurfaceZ, int OffsetX, int OffsetY);

/// <summary>Exact geometric ancestry address, not a minted source identity, quantity interval or extraction right.</summary>
public sealed record WatershedSubstrateAddress(EntityId FormationId, EntityId SubstrateParcelId,
    ContentHash MappingVersion, CellCoordinate Coordinate);

/// <summary>Dry candidate occupancy. A void has no substrate address; neither case admits live collision or support.</summary>
public sealed record WatershedTerrainCell(CellCoordinate Coordinate, WatershedTerrainColumn Column,
    WatershedSubstrateAddress? Ancestry)
{
    public bool IsSubstrate => Ancestry is not null;
}
