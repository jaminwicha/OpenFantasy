using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Requests one ordinary advancing step of work; it cannot select a work amount or elapsed time.</summary>
public sealed record WorkConstructionIntent(EntityId ProjectId, ulong ExpectedProjectRevision,
    ulong ExpectedProjectStorageRevision, EntityId ToolLotId, ulong ExpectedToolLotRevision) : WorldActionIntent;
