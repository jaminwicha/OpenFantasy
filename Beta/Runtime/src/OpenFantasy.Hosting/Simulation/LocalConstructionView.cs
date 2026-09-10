using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>A visible commissioned project; material claims, tool inventory and receipt history stay private.</summary>
public sealed record LocalConstructionView(EntityId ProjectId, EntityId BuilderActorId, string DisplayName,
    SubunitPosition SiteFeet, SubunitPosition WorkFeet, ulong CompletedWork, ulong RequiredWork, bool IsCompleted);
