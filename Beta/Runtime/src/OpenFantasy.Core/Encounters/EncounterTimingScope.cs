namespace OpenFantasy.Core.Encounters;

/// <summary>Names a retained tactical clock; its relationship to a world requires separate admission.</summary>
public sealed record EncounterTimingScope
{
    public EncounterTimingScope(EntityId worldId, EntityId branchId, EntityId encounterId)
    {
        if (worldId == branchId || worldId == encounterId || branchId == encounterId)
        {
            throw new ArgumentException("World, branch and encounter identities must be distinct.");
        }
        WorldId = worldId;
        BranchId = branchId;
        EncounterId = encounterId;
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId EncounterId { get; }

    internal bool Contains(EntityId identity) =>
        identity == WorldId || identity == BranchId || identity == EncounterId;
}
