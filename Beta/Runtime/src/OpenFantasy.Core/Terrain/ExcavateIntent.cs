using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

public sealed record ExcavateIntent(EntityId SiteId, ulong ExpectedSiteRevision, ulong ExpectedTerrainRevision,
    EntityId ToolLotId, ulong ExpectedToolLotRevision, ulong ExpectedDestinationRevision) : WorldActionIntent;
