using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Home-authorized consent facts; permanent principals, sessions and delivery credentials stay private.</summary>
public sealed class LocalControlPermissionBoard
{
    public LocalControlPermissionBoard(EntityId worldId, EntityId branchId, ulong worldRevision,
        EntityId homeActorId, ulong homeActorRevision, ulong permissionRevision,
        IReadOnlyList<EntityId> humanActorIds, IReadOnlyList<LocalControlPermissionSlot> slots,
        WorldControlWitness? heldWitness = null)
    {
        ArgumentNullException.ThrowIfNull(humanActorIds);
        ArgumentNullException.ThrowIfNull(slots);
        var actors = new List<EntityId>();
        foreach (var actor in humanActorIds)
        {
            if (actor == worldId || actor == branchId || actors.Count == WorldControllerState.MaximumActors
                || actors.Count != 0 && actors[^1].CompareTo(actor) >= 0)
                throw new ArgumentException("Home actor identities require bounded strict order.", nameof(humanActorIds));
            actors.Add(actor);
        }
        if (worldId == branchId || worldId == homeActorId || branchId == homeActorId
            || !actors.Contains(homeActorId) || permissionRevision > worldRevision)
            throw new ArgumentException("A permission board names its world, branch and original rostered home.");
        var visible = new List<LocalControlPermissionSlot>();
        UInt128 visibleRevision = 0;
        foreach (var slot in slots)
        {
            ArgumentNullException.ThrowIfNull(slot);
            var pair = (slot.OwnerActorId, slot.GranteeActorId);
            if (visible.Count == WorldControlPermissionState.MaximumSlots
                || slot.OwnerActorId != homeActorId && slot.GranteeActorId != homeActorId
                || !actors.Contains(slot.OwnerActorId) || !actors.Contains(slot.GranteeActorId)
                || slot.ChangedSequence > worldRevision
                || visible.Count != 0 && Comparer<(EntityId, EntityId)>.Default.Compare(
                    (visible[^1].OwnerActorId, visible[^1].GranteeActorId), pair) >= 0
                || slot.Acquisition is not null && slot.GranteeActorId != homeActorId)
                throw new ArgumentException("Permission rows must be ordered relevant home pairs within this world.", nameof(slots));
            visibleRevision += slot.Revision;
            visible.Add(slot);
        }
        if (visibleRevision > permissionRevision)
            throw new ArgumentException("Visible permission changes cannot exceed the world's permission revision.");
        if (heldWitness is not null)
        {
            var grant = visible.SingleOrDefault(slot => slot.OwnerActorId == heldWitness.OwnerActorId
                && slot.GranteeActorId == homeActorId);
            if (heldWitness.GranteeActorId != homeActorId || heldWitness.AcquiredSequence > worldRevision
                || grant is not { Status: WorldControlPermissionStatus.Accepted }
                || grant.GrantCommandId != heldWitness.GrantCommandId
                || grant.AcceptanceCommandId != heldWitness.AcceptanceCommandId
                || grant.Revision != heldWitness.GrantRevision || grant.TermsHash != heldWitness.TermsHash
                || grant.Capabilities != heldWitness.Capabilities || grant.AcceptedSequence >= heldWitness.AcquiredSequence
                || visible.Any(slot => slot.Acquisition is not null))
                throw new ArgumentException("Held custody must belong to this port's home and exact accepted incoming grant.", nameof(heldWitness));
        }
        WorldId = worldId;
        BranchId = branchId;
        WorldRevision = worldRevision;
        HomeActorId = homeActorId;
        HomeActorRevision = homeActorRevision;
        PermissionRevision = permissionRevision;
        HumanActorIds = Array.AsReadOnly(actors.ToArray());
        Slots = Array.AsReadOnly(visible.ToArray());
        HeldWitness = heldWitness;
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong WorldRevision { get; }
    public EntityId HomeActorId { get; }
    public ulong HomeActorRevision { get; }
    public ulong PermissionRevision { get; }
    public IReadOnlyList<EntityId> HumanActorIds { get; }
    public IReadOnlyList<LocalControlPermissionSlot> Slots { get; }
    public WorldControlWitness? HeldWitness { get; }
}

/// <summary>One relevant pair's current consent and exact public acceptance pins, never its private delivery.</summary>
public sealed record LocalControlPermissionSlot
{
    public LocalControlPermissionSlot(EntityId ownerActorId, EntityId granteeActorId, ulong revision,
        WorldControlPermissionStatus status, ulong changedSequence, ulong offeredSequence, ulong acceptedSequence,
        EntityId? grantCommandId, EntityId? acceptanceCommandId, WorldControlCapabilities? capabilities,
        ContentHash? termsHash, LocalControlAcquisition? acquisition)
    {
        bool revoked = status == WorldControlPermissionStatus.Revoked;
        bool accepted = status == WorldControlPermissionStatus.Accepted;
        if (ownerActorId == granteeActorId || revision == 0 || changedSequence < revision || !Enum.IsDefined(status)
            || revoked != (grantCommandId is null) || revoked != (termsHash is null) || revoked != (capabilities is null)
            || revoked && (offeredSequence != 0 || acceptedSequence != 0 || revision < 2)
            || !revoked && (offeredSequence == 0 || offeredSequence > changedSequence
                || capabilities == 0 || (capabilities!.Value & ~WorldControlCapabilities.All) != 0)
            || accepted != (acceptanceCommandId is not null)
            || accepted && (revision < 2 || acceptedSequence != changedSequence || acceptedSequence <= offeredSequence
                || offeredSequence < revision - 1 || grantCommandId == acceptanceCommandId)
            || !accepted && acceptedSequence != 0
            || status == WorldControlPermissionStatus.Offered && offeredSequence != changedSequence)
            throw new ArgumentException("A permission row must retain a coherent offered, accepted or revoked slot.");
        if (acquisition is not null && (!accepted || acquisition.OwnerActorId != ownerActorId
            || acquisition.GrantCommandId != grantCommandId || acquisition.AcceptanceCommandId != acceptanceCommandId
            || acquisition.GrantRevision != revision || acquisition.Capabilities != capabilities
            || acquisition.TermsHash != termsHash || acquisition.OwnerControlRevision == 0))
            throw new ArgumentException("An acquisition opportunity must bind this exact accepted permission.", nameof(acquisition));
        OwnerActorId = ownerActorId;
        GranteeActorId = granteeActorId;
        Revision = revision;
        Status = status;
        ChangedSequence = changedSequence;
        OfferedSequence = offeredSequence;
        AcceptedSequence = acceptedSequence;
        GrantCommandId = grantCommandId;
        AcceptanceCommandId = acceptanceCommandId;
        Capabilities = capabilities;
        TermsHash = termsHash;
        Acquisition = acquisition;
    }

    public EntityId OwnerActorId { get; }
    public EntityId GranteeActorId { get; }
    public ulong Revision { get; }
    public WorldControlPermissionStatus Status { get; }
    public ulong ChangedSequence { get; }
    public ulong OfferedSequence { get; }
    public ulong AcceptedSequence { get; }
    public EntityId? GrantCommandId { get; }
    public EntityId? AcceptanceCommandId { get; }
    public WorldControlCapabilities? Capabilities { get; }
    public ContentHash? TermsHash { get; }
    public LocalControlAcquisition? Acquisition { get; }
}
