using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Exact identity of a sealed complete-world boundary. The digest identifies bytes; it does not
/// authenticate their genesis or establish that a trusted host committed them.
/// </summary>
public sealed record WorldResourceArchiveReference
{
    public WorldResourceArchiveReference(EntityId worldId, EntityId branchId, ulong ordinal,
        ulong boundarySequence, ContentHash boundaryHash)
    {
        ArgumentNullException.ThrowIfNull(boundaryHash);
        if (worldId == branchId || boundarySequence == 0)
        {
            throw new ArgumentException("A resource archive requires distinct scope identities and a positive closing boundary.");
        }
        WorldId = worldId;
        BranchId = branchId;
        Ordinal = ordinal;
        BoundarySequence = boundarySequence;
        BoundaryHash = boundaryHash;
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Ordinal { get; }
    public ulong BoundarySequence { get; }
    public ContentHash BoundaryHash { get; }
}
