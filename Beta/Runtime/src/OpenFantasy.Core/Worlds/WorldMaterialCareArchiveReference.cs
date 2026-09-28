using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Exact identity of one sealed coordinated boundary. The closing sequence is the world revision of
/// the complete closing root. The digest identifies bytes; it neither authenticates nor authorizes them.
/// </summary>
public sealed record WorldMaterialCareArchiveReference
{
    public WorldMaterialCareArchiveReference(EntityId worldId, EntityId branchId, ulong ordinal,
        ulong boundarySequence, ContentHash boundaryHash)
    {
        ArgumentNullException.ThrowIfNull(boundaryHash);
        if (worldId == branchId || boundarySequence == 0)
        {
            throw new ArgumentException("A coordinated archive requires distinct scope identities and a positive closing boundary.");
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

    internal byte[] Encode() => CanonicalEncodingV1.Frame("world.material-care-reference.v1", WorldId.ToByteArray(),
        BranchId.ToByteArray(), CanonicalEncodingV1.Unsigned64(Ordinal), CanonicalEncodingV1.Unsigned64(BoundarySequence),
        BoundaryHash.ToByteArray());
}
