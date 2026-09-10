using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

/// <summary>New empty project custody only; it contains no existing lots, source supply or storage replacement.</summary>
public sealed class QuestProjectCustody
{
    public QuestProjectCustody(EntityId custodianId, EntityId ownerActorId, ulong unitCapacity,
        EntityId reservationId, CustodyDepositRule depositRule)
    {
        if (unitCapacity == 0 || !Enum.IsDefined(depositRule) || custodianId == ownerActorId
            || custodianId == reservationId || reservationId == ownerActorId)
        {
            throw new ArgumentException("A new project requires distinct identities, positive capacity and an explicit deposit rule.");
        }
        CustodianId = custodianId;
        OwnerActorId = ownerActorId;
        UnitCapacity = unitCapacity;
        ReservationId = reservationId;
        DepositRule = depositRule;
    }

    public EntityId CustodianId { get; }
    public EntityId OwnerActorId { get; }
    public ulong UnitCapacity { get; }
    public EntityId ReservationId { get; }
    public CustodyDepositRule DepositRule { get; }
}

/// <summary>Fresh pinned quest metadata and finite reward supply, independent of the retained world's progress.</summary>
public sealed class WorldQuestAdmission
{
    public const int MaximumProjects = 32;

    public WorldQuestAdmission(ContentHash contentVersion, ProgressionSnapshot progression, QuestSnapshot quests,
        IEnumerable<QuestProjectCustody> projects)
    {
        ArgumentNullException.ThrowIfNull(contentVersion);
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(projects);
        if (progression.WorldId != quests.WorldId || progression.BranchId != quests.BranchId
            || progression.Revision != 0 || progression.Currency.Revision != 0
            || progression.Actors.Any(value => value.Revision != 0)
            || progression.Currency.Accounts.Any(value => value.Revision != 0)
            || quests.Revision != 0 || quests.Participations.Count != 0 || quests.Definitions.Count == 0)
        {
            throw new ArgumentException("Quest admission requires fresh revisions and no participation in one exact world scope.");
        }
        SortedDictionary<EntityId, QuestProjectCustody> values = [];
        foreach (QuestProjectCustody project in projects)
        {
            ArgumentNullException.ThrowIfNull(project);
            if (values.Count == MaximumProjects || !values.TryAdd(project.CustodianId, project)
                || !progression.Actors.Any(value => value.ActorId == project.OwnerActorId))
            {
                throw new ArgumentException("New projects must be bounded, unique and owned by an admitted actor.");
            }
        }
        if (values.Count == 0)
        {
            throw new ArgumentException("Quest admission requires an explicit new project.", nameof(projects));
        }
        foreach (DeliveryQuestDefinition quest in quests.Definitions)
        {
            if (!values.TryGetValue(quest.DestinationCustodianId, out QuestProjectCustody? project)
                || project.OwnerActorId != quest.GiverActorId || project.ReservationId != quest.MaterialReservationId
                || quest.Quantity > project.UnitCapacity)
            {
                throw new ArgumentException("Each quest must reference its exact newly admitted giver-owned project and reservation.");
            }
            CurrencyAccount reserve = progression.Currency.GetAccount(quest.Reward.ReserveAccountId);
            if (reserve.Kind != CurrencyAccountKind.Reserve
                || quest.Reward.Currency.DefinitionId != progression.Currency.Currency.DefinitionId
                || quest.Reward.Currency.Version != progression.Currency.Currency.Version)
            {
                throw new ArgumentException("A quest reward requires its exact newly admitted currency and reserve.");
            }
        }
        if (values.Values.Any(project => !quests.Definitions.Any(quest => quest.DestinationCustodianId == project.CustodianId)))
        {
            throw new ArgumentException("Every new project must be referenced by the admitted quest definitions.");
        }
        ContentVersion = contentVersion;
        Progression = progression;
        Quests = quests;
        Projects = Array.AsReadOnly(values.Values.ToArray());
        (DefinitionIds, EntityIds) = ReadNewIdentities();
        // Schema-three journal records retain a 256 KiB request limit. Admission must fit before publication.
        WorldQuestMigrationCodecV1.EncodeAdmission(this);
    }

    public ContentHash ContentVersion { get; }
    public EntityId WorldId => Progression.WorldId;
    public EntityId BranchId => Progression.BranchId;
    public ProgressionSnapshot Progression { get; }
    public QuestSnapshot Quests { get; }
    public ReadOnlyCollection<QuestProjectCustody> Projects { get; }
    internal ReadOnlyCollection<EntityId> DefinitionIds { get; }
    internal ReadOnlyCollection<EntityId> EntityIds { get; }

    private (ReadOnlyCollection<EntityId> Definitions, ReadOnlyCollection<EntityId> Entities) ReadNewIdentities()
    {
        Dictionary<EntityId, (string Role, ContentHash Version)> definitions = [];
        HashSet<EntityId> existingRoles = [WorldId, BranchId, .. Progression.Actors.Select(value => value.ActorId)];
        void Define(EntityId id, string role, ContentHash version)
        {
            if (existingRoles.Contains(id) || definitions.TryGetValue(id, out var known)
                && (known.Role != role || known.Version != version))
            {
                throw new ArgumentException("Quest admission aliases definition kinds or conflicts with an exact pin.");
            }
            definitions[id] = (role, version);
        }
        foreach (ActorStatProfileDefinition profile in Progression.Profiles)
        {
            Define(profile.DefinitionId, "stat-profile", profile.Version);
        }
        foreach (LevelProgressionDefinition curve in Progression.Curves)
        {
            Define(curve.DefinitionId, "level-curve", curve.Version);
        }
        Define(Progression.Currency.Currency.DefinitionId, "currency", Progression.Currency.Currency.Version);
        foreach (DeliveryQuestDefinition quest in Quests.Definitions)
        {
            Define(quest.DefinitionId, "quest", quest.Version);
            Define(quest.Reward.DefinitionId, "reward", quest.Reward.Version);
        }
        SortedSet<EntityId> entities = [];
        void Entity(EntityId id)
        {
            if (existingRoles.Contains(id) || definitions.ContainsKey(id) || !entities.Add(id))
            {
                throw new ArgumentException("Quest accounts, projects and reservations require distinct new identities.");
            }
        }
        foreach (CurrencyAccount account in Progression.Currency.Accounts)
        {
            Entity(account.AccountId);
        }
        foreach (QuestProjectCustody project in Projects)
        {
            Entity(project.CustodianId);
            Entity(project.ReservationId);
        }
        foreach (DeliveryQuestDefinition quest in Quests.Definitions)
        {
            if (definitions.ContainsKey(quest.Item.DefinitionId) || entities.Contains(quest.Item.DefinitionId)
                || existingRoles.Contains(quest.Item.DefinitionId))
            {
                throw new ArgumentException("A retained source item pin must not alias new admission identities or actor roles.");
            }
        }
        return (Array.AsReadOnly(definitions.Keys.Order().ToArray()), Array.AsReadOnly(entities.ToArray()));
    }
}
