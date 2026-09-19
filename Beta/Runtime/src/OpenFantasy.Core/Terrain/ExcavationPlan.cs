using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

public sealed record ExcavationPlan(CountedAuthorityState Inventory, LocalSpatialSnapshot Space,
    GatheringSnapshot Gathering, ExcavationSnapshot Excavation, WorldActionRejection Rejection, EntityId? SpawnedId)
{
    public bool Succeeded => Rejection == WorldActionRejection.None;
}
