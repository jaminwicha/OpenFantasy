using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// One player-owned work target and its exact committed request revisions. Stance is the authored
/// interaction anchor, not proof of current reach, safe consequences, or acceptance of another action.
/// No unissued output or source entitlement is exposed.
/// </summary>
public sealed record LocalExcavationView(EntityId SiteId, CellCoordinate Cell, SubunitPosition Stance,
    ulong SiteRevision, ulong TerrainRevision, ulong RequiredWork, ulong CompletedWork,
    EntityId RequiredToolDefinitionId, ulong DestinationRevision)
{
    public bool IsComplete => CompletedWork == RequiredWork;
    public ulong RemainingWork => RequiredWork - CompletedWork;
}

/// <summary>An actual matching source tool lot in permitted player-owned custody, without its source claim.</summary>
public sealed record LocalExcavationToolView(EntityId LotId, EntityId DefinitionId, ulong Revision);
