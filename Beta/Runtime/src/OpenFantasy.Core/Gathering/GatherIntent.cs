using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Gathering;

public sealed record GatherIntent(EntityId NodeId, ulong ExpectedNodeRevision,
    EntityId ToolLotId, ulong ExpectedToolLotRevision) : WorldActionIntent;
