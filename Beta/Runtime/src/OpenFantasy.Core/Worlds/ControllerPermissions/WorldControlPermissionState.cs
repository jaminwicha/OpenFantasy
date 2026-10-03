using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Current offered or accepted permissions and bounded revoked revision slots, never a control binding.</summary>
public sealed class WorldControlPermissionState
{
    public const int MaximumSlots = WorldControllerState.MaximumActors * (WorldControllerState.MaximumActors - 1);
    public const string RulesText = "world.control-permissions.v1; twelve ordered distinct human home-actor pairs; "
        + "permanent owner offers known nonzero explicit capabilities to a named permanent principal/session; "
        + "named human grantee accepts exact grant terms and slot revision; owner replaces or revokes; "
        + "each successful change increments one retained slot revision; revocation is allowed while owner absent; "
        + "offer and acceptance require submitting human control; isolated commands advance no gameplay time; "
        + "permission is neither takeover nor credentials, title, inventory, onward delegation or readiness";
    public static ContentHash RulesPin { get; } = ContentHash.Compute(CanonicalEncodingV1.Text(RulesText, nameof(RulesText)));
    public static WorldControlPermissionState Empty { get; } = new([]);

    public WorldControlPermissionState(IEnumerable<WorldControlPermissionSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var ordered = new SortedDictionary<(EntityId, EntityId), WorldControlPermissionSlot>();
        var commands = new HashSet<EntityId>();
        foreach (var slot in slots)
        {
            ArgumentNullException.ThrowIfNull(slot);
            if (ordered.Count == MaximumSlots || !ordered.TryAdd((slot.OwnerActorId, slot.GranteeActorId), slot)
                || slot.GrantCommandId is { } grant && !commands.Add(grant)
                || slot.AcceptanceCommandId is { } accept && !commands.Add(accept))
                throw new ArgumentException("Permission slots and their retained command causes must be bounded and unique.", nameof(slots));
        }
        Slots = Array.AsReadOnly(ordered.Values.ToArray());
        ValidateChronology(Slots);
        Revision = Slots.Aggregate(0UL, (revision, slot) => checked(revision + slot.Revision));
    }
    private static void ValidateChronology(IReadOnlyList<WorldControlPermissionSlot> slots)
    {
        var milestones = new SortedDictionary<ulong, ((EntityId, EntityId) Pair, ulong Revision)>();
        foreach (var slot in slots)
        {
            var pair = (slot.OwnerActorId, slot.GranteeActorId);
            if (!milestones.TryAdd(slot.ChangedSequence, (pair, slot.Revision))
                || slot.AcceptanceCommandId is not null && !milestones.TryAdd(slot.OfferedSequence, (pair, slot.Revision - 1)))
                throw new ArgumentException("Isolated permission changes cannot share a journal sequence.");
        }
        var lowerBounds = new Dictionary<(EntityId, EntityId), ulong>();
        UInt128 required = 0;
        foreach (var milestone in milestones)
        {
            var (pair, revision) = milestone.Value;
            ulong previous = lowerBounds.GetValueOrDefault(pair);
            if (revision <= previous) throw new ArgumentException("Permission chronology cannot reverse a slot revision.");
            required += revision - previous;
            if (required > milestone.Key)
                throw new ArgumentException("Permission revisions cannot require more isolated changes than the recorded sequence permits.");
            lowerBounds[pair] = revision;
        }
    }

    public ReadOnlyCollection<WorldControlPermissionSlot> Slots { get; }
    public ulong Revision { get; }
    public ContentHash ContentHash => ContentHash.Compute(WorldControlPermissionCodecV1.Encode(this));
    public WorldControlPermissionSlot? Find(EntityId ownerActorId, EntityId granteeActorId) =>
        Slots.FirstOrDefault(slot => slot.OwnerActorId == ownerActorId && slot.GranteeActorId == granteeActorId);
    internal WorldControlPermissionState Replace(WorldControlPermissionSlot replacement) => new(
        Slots.Where(slot => slot.OwnerActorId != replacement.OwnerActorId || slot.GranteeActorId != replacement.GranteeActorId).Append(replacement));
}
