using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

public abstract record ControlPermissionIntent : WorldActionIntent;
public sealed record OfferControlPermissionIntent : ControlPermissionIntent
{
    public OfferControlPermissionIntent(EntityId granteeActorId, ulong expectedSlotRevision, WorldControlCapabilities capabilities)
    {
        WorldControlPermissionTerms.ValidateCapabilities(capabilities);
        GranteeActorId = granteeActorId; ExpectedSlotRevision = expectedSlotRevision; Capabilities = capabilities;
    }
    public EntityId GranteeActorId { get; }
    public ulong ExpectedSlotRevision { get; }
    public WorldControlCapabilities Capabilities { get; }
}
public sealed record AcceptControlPermissionIntent : ControlPermissionIntent
{
    public AcceptControlPermissionIntent(EntityId ownerActorId, ulong expectedSlotRevision, EntityId grantCommandId, ContentHash termsHash)
    {
        ArgumentNullException.ThrowIfNull(termsHash);
        if (expectedSlotRevision == 0 || ownerActorId == grantCommandId)
            throw new ArgumentException("Acceptance requires an existing distinct grant and its slot revision.");
        OwnerActorId = ownerActorId; ExpectedSlotRevision = expectedSlotRevision; GrantCommandId = grantCommandId; TermsHash = termsHash;
    }
    public EntityId OwnerActorId { get; }
    public ulong ExpectedSlotRevision { get; }
    public EntityId GrantCommandId { get; }
    public ContentHash TermsHash { get; }
}
public sealed record RevokeControlPermissionIntent(EntityId GranteeActorId, ulong ExpectedSlotRevision) : ControlPermissionIntent;
