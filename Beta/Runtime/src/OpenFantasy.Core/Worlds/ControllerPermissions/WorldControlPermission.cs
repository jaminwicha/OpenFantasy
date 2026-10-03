using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

[Flags]
public enum WorldControlCapabilities : ulong
{
    Combat = 1, Traversal = 2, Recovery = 4, Inventory = 8, Trade = 16, Production = 32,
    ConversationAndQuests = 64, CareDisposition = 128, PrivateHistory = 256,
    CombatAndSurvival = Combat | Traversal | Recovery,
    All = Combat | Traversal | Recovery | Inventory | Trade | Production | ConversationAndQuests | CareDisposition | PrivateHistory
}

/// <summary>Named permanent participants and exact offered scopes; this is not active actor control.</summary>
public sealed record WorldControlPermissionTerms
{
    public WorldControlPermissionTerms(EntityId worldId, EntityId branchId, EntityId ownerActorId,
        EntityId ownerPrincipalId, EntityId ownerSessionId, EntityId granteeActorId,
        EntityId granteePrincipalId, EntityId granteeSessionId, EntityId grantCommandId, WorldControlCapabilities capabilities)
    {
        ValidateCapabilities(capabilities);
        EntityId[] identities = [worldId, branchId, ownerActorId, ownerPrincipalId, ownerSessionId,
            granteeActorId, granteePrincipalId, granteeSessionId, grantCommandId];
        if (identities.Distinct().Count() != identities.Length)
            throw new ArgumentException("A permission requires distinct world, branch, actors, principals, sessions and grant cause.");
        WorldId = worldId; BranchId = branchId; OwnerActorId = ownerActorId;
        OwnerPrincipalId = ownerPrincipalId; OwnerSessionId = ownerSessionId;
        GranteeActorId = granteeActorId; GranteePrincipalId = granteePrincipalId; GranteeSessionId = granteeSessionId;
        GrantCommandId = grantCommandId; Capabilities = capabilities;
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId OwnerActorId { get; }
    public EntityId OwnerPrincipalId { get; }
    public EntityId OwnerSessionId { get; }
    public EntityId GranteeActorId { get; }
    public EntityId GranteePrincipalId { get; }
    public EntityId GranteeSessionId { get; }
    public EntityId GrantCommandId { get; }
    public WorldControlCapabilities Capabilities { get; }
    public ContentHash ContentHash => ContentHash.Compute(WorldControlPermissionCodecV1.EncodeTerms(this));

    internal static void ValidateCapabilities(WorldControlCapabilities capabilities)
    {
        if (capabilities == 0 || (capabilities & ~WorldControlCapabilities.All) != 0)
            throw new ArgumentException("Control permission scopes must be nonempty and known.", nameof(capabilities));
    }
}

public enum WorldControlPermissionStatus { Revoked = 1, Offered = 2, Accepted = 3 }

/// <summary>A bounded actor-pair slot. Revocation retains its revision, but no effective grant.</summary>
public sealed record WorldControlPermissionSlot
{
    public WorldControlPermissionSlot(EntityId ownerActorId, EntityId granteeActorId, ulong revision,
        ulong changedSequence, WorldControlPermissionTerms? terms = null, ulong offeredSequence = 0,
        EntityId? acceptanceCommandId = null, ulong acceptedSequence = 0)
    {
        if (ownerActorId == granteeActorId || revision == 0 || changedSequence < revision
            || (terms is null) != (offeredSequence == 0) || (acceptanceCommandId is null) != (acceptedSequence == 0)
            || terms is null && (acceptanceCommandId is not null || revision < 2)
            || terms is not null && (terms.OwnerActorId != ownerActorId || terms.GranteeActorId != granteeActorId
                || offeredSequence > changedSequence || offeredSequence == 0)
            || acceptanceCommandId is not null && (acceptedSequence <= offeredSequence || acceptedSequence != changedSequence
                || revision < 2 || offeredSequence < revision - 1 || acceptanceCommandId == terms!.GrantCommandId
                || new[] { terms.WorldId, terms.BranchId, ownerActorId, granteeActorId, terms.OwnerPrincipalId,
                    terms.OwnerSessionId, terms.GranteePrincipalId, terms.GranteeSessionId }.Contains(acceptanceCommandId.Value))
            || terms is not null && acceptanceCommandId is null && changedSequence != offeredSequence)
            throw new ArgumentException("A permission slot has inconsistent revision, terms or acceptance evidence.");
        OwnerActorId = ownerActorId; GranteeActorId = granteeActorId; Revision = revision;
        ChangedSequence = changedSequence; Terms = terms; OfferedSequence = offeredSequence;
        AcceptanceCommandId = acceptanceCommandId; AcceptedSequence = acceptedSequence;
    }
    public EntityId OwnerActorId { get; }
    public EntityId GranteeActorId { get; }
    public ulong Revision { get; }
    public ulong ChangedSequence { get; }
    public WorldControlPermissionTerms? Terms { get; }
    public ulong OfferedSequence { get; }
    public EntityId? AcceptanceCommandId { get; }
    public ulong AcceptedSequence { get; }
    public WorldControlPermissionStatus Status => Terms is null ? WorldControlPermissionStatus.Revoked
        : AcceptanceCommandId is null ? WorldControlPermissionStatus.Offered : WorldControlPermissionStatus.Accepted;
    public EntityId? GrantCommandId => Terms?.GrantCommandId;
    public ContentHash? TermsHash => Terms?.ContentHash;
}
