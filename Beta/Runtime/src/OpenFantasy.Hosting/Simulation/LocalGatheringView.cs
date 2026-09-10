using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Visible state of one admitted local node; source allowances and issuance cursors stay private.</summary>
public sealed record LocalGatherNodeView(EntityId NodeId, string DisplayName, SubunitPosition Feet,
    ulong Revision, GatherNodeKind Kind, GatherToolKind RequiredTool, uint RemainingSwings,
    GatherNodeVisibility Visibility);

/// <summary>An actual tool lot owned by this view's controlled actor, joined to its exact admitted definition.</summary>
public sealed record LocalGatherToolLotView(EntityId LotId, ulong Revision, GatherToolKind Kind, uint Power);
