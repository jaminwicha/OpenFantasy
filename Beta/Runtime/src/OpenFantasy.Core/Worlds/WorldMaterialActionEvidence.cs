using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// One bounded observation of an accepted material or work action inside a real complete-world
/// transaction. It is structural, not authenticated: the interpreting processor derived it from the
/// running candidates it admitted, and <see cref="ContentHash"/> binds only these fields. Nothing here
/// is signed, published or persisted, and it never grants material, authority or issuance. Only
/// digests, identities and the immutable command are kept; no world, owner, earlier observation or
/// parent chain is reachable from it.
/// </summary>
public sealed class WorldMaterialActionEvidence
{
    internal WorldMaterialActionEvidence(EntityId worldId, EntityId branchId, ContentHash inputWorldHash,
        ContentHash sourceWorldHash, ulong sourceWorldRevision, ulong publicationSequence, int actionOrdinal,
        WorldActionCommand command, WorldMaterialEvidenceKind kind, WorldMaterialOwnerPins before,
        WorldMaterialOwnerPins after, IEnumerable<EntityId> createdIds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actionOrdinal);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        WorldId = worldId;
        BranchId = branchId;
        InputWorldHash = inputWorldHash;
        SourceWorldHash = sourceWorldHash;
        SourceWorldRevision = sourceWorldRevision;
        PublicationSequence = publicationSequence;
        ActionOrdinal = actionOrdinal;
        Command = command;
        Kind = kind;
        Before = before;
        After = after;
        CreatedIds = Array.AsReadOnly(createdIds.ToArray());
        ContentHash = WorldMaterialEvidenceEncoding.Hash(this);
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }

    /// <summary>
    /// The durable root selected by the caller. It differs from <see cref="SourceWorldHash"/> only
    /// when resource preparation sealed an archive segment before the batch was interpreted.
    /// </summary>
    public ContentHash InputWorldHash { get; }

    /// <summary>
    /// The exact root the transaction interpreted. After an archival cut this is the continued root
    /// carrying the archive anchor, which is never itself published; it keeps the input revision.
    /// </summary>
    public ContentHash SourceWorldHash { get; }
    public ulong SourceWorldRevision { get; }

    /// <summary>The world sequence the prepared candidate and its receipts would publish at.</summary>
    public ulong PublicationSequence { get; }

    /// <summary>The index in the selected ordered batch, not the position among sorted receipts.</summary>
    public int ActionOrdinal { get; }
    public WorldActionCommand Command { get; }
    public WorldMaterialEvidenceKind Kind { get; }

    /// <summary>The running candidates immediately before this action, after every earlier action.</summary>
    public WorldMaterialOwnerPins Before { get; }

    /// <summary>
    /// The reconciled candidates admitted for this action, before the selected step applies time,
    /// recovery, liquid flow or movement. The decision candidate binds the final root instead.
    /// </summary>
    public WorldMaterialOwnerPins After { get; }

    /// <summary>Identities this action spawned or produced, in the order the transaction reserved them.</summary>
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
    public ContentHash ContentHash { get; }
}
