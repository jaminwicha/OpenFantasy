using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Complete admitted state of the current exploration profile. Presence, inventory, dialogue and time
/// change together; this bounded locality is not the unimplemented global simulation's complete schema.
/// </summary>
public sealed class WorldSnapshot
{
    private readonly Dictionary<EntityId, WorldResident> _residents;

    public WorldSnapshot(CountedAuthorityState inventory, LocalSpatialSnapshot space,
        IEnumerable<WorldResident> residents, ContentHash admissionVersion, WorldInstant calendar,
        ulong revision, ulong advancingSteps, IEnumerable<DialogueSession>? dialogues = null,
        GatheringSnapshot? gathering = null, ProductionSnapshot? production = null,
        ProgressionSnapshot? progression = null, QuestSnapshot? quests = null, ConstructionSnapshot? construction = null,
        PreparedWatershed? watershed = null, PreparedWatershedNetwork? watershedNetwork = null)
        : this(inventory, space, residents, admissionVersion, calendar, revision, advancingSteps, dialogues,
            gathering, production, progression, quests, construction, watershed, watershedNetwork, null)
    {
    }

    private WorldSnapshot(CountedAuthorityState inventory, LocalSpatialSnapshot space,
        IEnumerable<WorldResident> residents, ContentHash admissionVersion, WorldInstant calendar,
        ulong revision, ulong advancingSteps, IEnumerable<DialogueSession>? dialogues, GatheringSnapshot? gathering,
        ProductionSnapshot? production, ProgressionSnapshot? progression, QuestSnapshot? quests,
        ConstructionSnapshot? construction, PreparedWatershed? watershed, PreparedWatershedNetwork? watershedNetwork,
        WorldIdentityIndex? previous)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(residents);
        ArgumentNullException.ThrowIfNull(admissionVersion);
        if (space.Scope.WorldId != inventory.WorldId || space.Scope.BranchId != inventory.BranchId)
        {
            throw new ArgumentException("Presence and inventory must belong to the same world and branch.");
        }
        _residents = [];
        foreach (WorldResident resident in residents)
        {
            ArgumentNullException.ThrowIfNull(resident);
            if (_residents.Count == WorldStepBatch.MaximumActorsPerBatch || !_residents.TryAdd(resident.ActorId, resident))
            {
                throw new ArgumentException("Residents need bounded unique actor identities.");
            }
            if (!inventory.Policies.Any(policy => policy.CustodianId == resident.InventoryCustodianId
                && policy.OwnerActorId == resident.ActorId))
            {
                throw new ArgumentException("Each resident's primary inventory requires its own admitted custody policy.");
            }
        }
        if (_residents.Count == 0 || !inventory.Actors.SequenceEqual(_residents.Keys.Order())
            || !space.Actors.Select(actor => actor.ActorId).SequenceEqual(_residents.Keys.Order()))
        {
            throw new ArgumentException("The local actor, resident and counted authority sets must be complete and equal.");
        }
        var sessions = new SortedDictionary<EntityId, DialogueSession>();
        HashSet<EntityId> listeners = [];
        foreach (DialogueSession session in dialogues ?? [])
        {
            ArgumentNullException.ThrowIfNull(session);
            if (sessions.Count == 4 || !sessions.TryAdd(session.DialogueId, session)
                || !listeners.Add(session.ListenerActorId) || !_residents.ContainsKey(session.ListenerActorId)
                || !_residents.TryGetValue(session.SpeakerActorId, out WorldResident? speaker)
                || speaker.OpeningUtterance?.DefinitionId != session.UtteranceDefinitionId
                || session.StartedAt > calendar)
            {
                throw new ArgumentException("The active dialogue set violates its addressed utterance or clock binding.");
            }
        }
        Inventory = inventory;
        Space = space;
        AdmissionVersion = admissionVersion;
        Calendar = calendar;
        Revision = revision;
        AdvancingSteps = advancingSteps;
        Residents = Array.AsReadOnly(_residents.Values.OrderBy(resident => resident.ActorId).ToArray());
        Dialogues = Array.AsReadOnly(sessions.Values.ToArray());
        Gathering = gathering;
        Production = production;
        Progression = progression;
        Quests = quests;
        Construction = construction;
        Watershed = watershed;
        WatershedNetwork = watershedNetwork;
        if (watershed is not null && watershedNetwork is not null)
        {
            throw new ArgumentException("A world cannot independently own both a single basin and a watershed network.");
        }
        if ((watershed is not null || watershedNetwork is not null) && construction is null)
        {
            throw new ArgumentException("The watershed world profile requires its complete construction basis.");
        }
        if (construction is not null && quests is null || construction is null && space.Structures.Count != 0)
        {
            throw new ArgumentException("Placed structures require their complete construction and quest profile.");
        }
        if ((progression is null) != (quests is null) || quests is not null && production is null)
        {
            throw new ArgumentException("Quest worlds require both funded progression and retained production components.");
        }
        if (production is not null && gathering is null)
        {
            throw new ArgumentException("The production profile requires its retained gathering component.");
        }
        IdentityIndex = WorldAdmissionValidator.Validate(this, previous);
    }

    public EntityId WorldId => Inventory.WorldId;
    public EntityId BranchId => Inventory.BranchId;
    public CountedAuthorityState Inventory { get; }
    public LocalSpatialSnapshot Space { get; }
    public ContentHash AdmissionVersion { get; }
    public WorldInstant Calendar { get; }
    public ulong Revision { get; }
    public ulong AdvancingSteps { get; }
    public ReadOnlyCollection<WorldResident> Residents { get; }
    public ReadOnlyCollection<DialogueSession> Dialogues { get; }
    public GatheringSnapshot? Gathering { get; }
    public ProductionSnapshot? Production { get; }
    public ProgressionSnapshot? Progression { get; }
    public QuestSnapshot? Quests { get; }
    public ConstructionSnapshot? Construction { get; }
    public PreparedWatershed? Watershed { get; }
    public PreparedWatershedNetwork? WatershedNetwork { get; }
    public ushort ProfileVersion => WatershedNetwork is not null ? (ushort)7 : Watershed is not null ? (ushort)6
        : Construction is not null ? (ushort)5 : Quests is not null ? (ushort)4
        : Production is not null ? (ushort)3 : Gathering is null ? (ushort)1 : (ushort)2;
    public WorldIdentityIndex IdentityIndex { get; }
    public ContentHash ContentHash => ContentHash.Compute(WorldSnapshotCodec.Encode(this));
    public bool HasReadingHold => Dialogues.Count != 0;
    public WorldResident GetResident(EntityId actorId) => _residents.TryGetValue(actorId, out WorldResident? resident)
        ? resident : throw new ArgumentException("The actor is not admitted in this locality.", nameof(actorId));

    internal WorldSnapshot WithState(CountedAuthorityState inventory, LocalSpatialSnapshot space,
        IEnumerable<WorldResident> residents, WorldInstant calendar, ulong revision, ulong advancingSteps,
        IEnumerable<DialogueSession> dialogues, GatheringSnapshot? gathering, ProductionSnapshot? production,
        ProgressionSnapshot? progression, QuestSnapshot? quests, ConstructionSnapshot? construction = null) =>
        new(inventory, space, residents, AdmissionVersion, calendar, revision, advancingSteps, dialogues,
            gathering, production, progression, quests, construction, Watershed, WatershedNetwork, IdentityIndex);
}
