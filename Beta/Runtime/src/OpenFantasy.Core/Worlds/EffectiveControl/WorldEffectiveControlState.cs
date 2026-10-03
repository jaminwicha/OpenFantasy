using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

public sealed record WorldEffectiveControlSlot
{
    public WorldEffectiveControlSlot(EntityId actorId, ulong revision, ulong changedSequence, WorldControlWitness? witness = null)
    {
        if (revision == 0 || changedSequence < revision || (witness is not null) != (revision % 2 == 1) || witness is not null
            && (witness.OwnerActorId != actorId || witness.AssignmentRevision != revision || witness.AcquiredSequence != changedSequence))
        {
            throw new ArgumentException("An effective control slot requires its exact acquired assignment or a retained release revision.");
        }
        ActorId = actorId;
        Revision = revision;
        ChangedSequence = changedSequence;
        Witness = witness;
    }
    public EntityId ActorId { get; }
    public ulong Revision { get; }
    public ulong ChangedSequence { get; }
    public WorldControlWitness? Witness { get; }
}

/// <summary>Bounded active assignments, separate from permanent ownership and original controller lifecycle.</summary>
public sealed class WorldEffectiveControlState
{
    public const string RulesText = "world.effective-control.v1; explicit privileged acquisition after original thirty-second allowance; "
        + "exact accepted named home-player grant and immutable assignment witness; at most one actor per grantee; "
        + "current scope for new input, retained authority for accepted work; revoke replacement original return or grantee disconnect "
        + "clears active assignments atomically without erasing accepted commands; no title inventory credentials or clock transfer";
    public static ContentHash RulesPin { get; } = ContentHash.Compute(CanonicalEncodingV1.Text(RulesText, nameof(RulesText)));
    public static WorldEffectiveControlState Empty { get; } = new(0, []);
    public WorldEffectiveControlState(ulong revision, IEnumerable<WorldEffectiveControlSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var ordered = new SortedDictionary<EntityId, WorldEffectiveControlSlot>();
        var grantees = new HashSet<EntityId>();
        var assignments = new HashSet<EntityId>();
        foreach (var slot in slots)
        {
            ArgumentNullException.ThrowIfNull(slot);
            if (ordered.Count == WorldControllerState.MaximumActors || !ordered.TryAdd(slot.ActorId, slot)
                || slot.Revision > revision || slot.Witness is { } witness
                    && (!grantees.Add(witness.GranteeActorId) || !assignments.Add(witness.AssignmentId)))
            {
                throw new ArgumentException("Effective assignments must be bounded, unique and within their state revision.");
            }
        }
        if ((revision == 0) != (ordered.Count == 0))
        {
            throw new ArgumentException("An empty assignment history has revision zero.");
        }
        UInt128 acquisitions = 0;
        UInt128 mutations = 0;
        ulong releases = 0;
        foreach (var slot in ordered.Values)
        {
            acquisitions += ((UInt128)slot.Revision + 1) / 2;
            mutations += slot.Revision;
            releases = Math.Max(releases, slot.Revision / 2);
        }
        if (acquisitions + releases > revision || (UInt128)revision > mutations)
        {
            throw new ArgumentException("Effective state revisions must account for each acquisition and possible shared release boundary.");
        }
        Revision = revision;
        Slots = Array.AsReadOnly(ordered.Values.ToArray());
    }
    public ulong Revision { get; }
    public ReadOnlyCollection<WorldEffectiveControlSlot> Slots { get; }
    public ContentHash ContentHash => ContentHash.Compute(WorldEffectiveControlCodecV1.Encode(this));
    public WorldEffectiveControlSlot? Find(EntityId actorId) => Slots.FirstOrDefault(slot => slot.ActorId == actorId);
    internal WorldEffectiveControlState Replace(WorldEffectiveControlSlot next) => new(checked(Revision + 1),
        Slots.Where(slot => slot.ActorId != next.ActorId).Append(next));
    internal WorldEffectiveControlState Reconcile(WorldControllerState controllers, WorldControlPermissionState permissions, ulong sequence)
    {
        bool changed = false;
        var slots = Slots.Select(slot =>
        {
            if (slot.Witness is null || WorldActorCommandAuthority.IsEligibleAssignment(controllers, permissions, slot.Witness)) return slot;
            changed = true;
            return new WorldEffectiveControlSlot(slot.ActorId, checked(slot.Revision + 1), sequence);
        }).ToArray();
        return changed ? new(checked(Revision + 1), slots) : this;
    }
}

public enum WorldControlAssignmentEventKind { Acquire = 1, Release = 2 }

/// <summary>Privileged host evidence of deliberate live acquisition or release. Construction authenticates nothing.</summary>
public sealed record WorldControlAssignmentEvent
{
    public WorldControlAssignmentEvent(EntityId actorId, WorldControlAssignmentEventKind kind, ulong expectedAssignmentRevision,
        WorldControlWitness? witness = null)
    {
        if (!Enum.IsDefined(kind) || (kind == WorldControlAssignmentEventKind.Acquire) != (witness is not null)
            || witness is not null && (witness.OwnerActorId != actorId || witness.AssignmentRevision != checked(expectedAssignmentRevision + 1)))
        {
            throw new ArgumentException("An assignment event must bind its exact next revision and acquisition witness.");
        }
        ActorId = actorId;
        Kind = kind;
        ExpectedAssignmentRevision = expectedAssignmentRevision;
        Witness = witness;
    }
    public EntityId ActorId { get; }
    public WorldControlAssignmentEventKind Kind { get; }
    public ulong ExpectedAssignmentRevision { get; }
    public WorldControlWitness? Witness { get; }
}
