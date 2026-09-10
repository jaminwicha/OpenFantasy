using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Gathering;

/// <summary>Private candidate values; the owning world must check historical identities and commit every effect together.</summary>
public sealed class GatherPlan
{
    internal GatherPlan(CountedAuthorityState inventory, GatheringSnapshot gathering, WorldActionRejection rejection, EntityId? spawnedId)
    {
        CandidateInventory = inventory;
        CandidateGathering = gathering;
        Rejection = rejection;
        SpawnedId = spawnedId;
    }
    public CountedAuthorityState CandidateInventory { get; }
    public GatheringSnapshot CandidateGathering { get; }
    public WorldActionRejection Rejection { get; }
    public EntityId? SpawnedId { get; }
}
