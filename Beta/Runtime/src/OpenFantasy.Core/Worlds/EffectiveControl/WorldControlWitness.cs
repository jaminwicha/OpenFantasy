using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Immutable attribution retained by an accepted delegated request. Its bytes do not authenticate a caller.</summary>
public sealed record WorldControlWitness
{
    public WorldControlWitness(EntityId ownerActorId, EntityId granteeActorId, EntityId grantCommandId,
        EntityId acceptanceCommandId, ulong grantRevision, WorldControlCapabilities capabilities, ContentHash termsHash,
        EntityId assignmentId, ulong assignmentRevision, ulong acquiredSequence, EntityId disconnectId, ulong ownerControlRevision)
    {
        ArgumentNullException.ThrowIfNull(termsHash);
        WorldControlPermissionTerms.ValidateCapabilities(capabilities);
        EntityId[] ids = [ownerActorId, granteeActorId, grantCommandId, acceptanceCommandId, assignmentId, disconnectId];
        if (ids.Distinct().Count() != ids.Length || grantRevision < 2 || assignmentRevision == 0
            || ownerControlRevision < 2 || acquiredSequence <= grantRevision || acquiredSequence <= ownerControlRevision
            || acquiredSequence < assignmentRevision)
        {
            throw new ArgumentException("A control witness requires distinct causes and earned permission, episode and assignment revisions.");
        }
        OwnerActorId = ownerActorId;
        GranteeActorId = granteeActorId;
        GrantCommandId = grantCommandId;
        AcceptanceCommandId = acceptanceCommandId;
        GrantRevision = grantRevision;
        Capabilities = capabilities;
        TermsHash = termsHash;
        AssignmentId = assignmentId;
        AssignmentRevision = assignmentRevision;
        AcquiredSequence = acquiredSequence;
        DisconnectId = disconnectId;
        OwnerControlRevision = ownerControlRevision;
    }

    public EntityId OwnerActorId { get; }
    public EntityId GranteeActorId { get; }
    public EntityId GrantCommandId { get; }
    public EntityId AcceptanceCommandId { get; }
    public ulong GrantRevision { get; }
    public WorldControlCapabilities Capabilities { get; }
    public ContentHash TermsHash { get; }
    public EntityId AssignmentId { get; }
    public ulong AssignmentRevision { get; }
    public ulong AcquiredSequence { get; }
    public EntityId DisconnectId { get; }
    public ulong OwnerControlRevision { get; }
}
