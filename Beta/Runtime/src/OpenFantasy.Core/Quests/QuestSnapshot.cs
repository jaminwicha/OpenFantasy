using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;

namespace OpenFantasy.Core.Quests;

/// <summary>Bounded definitions and permanent participation records; unresolved rewards remain funded obligations.</summary>
public sealed class QuestSnapshot
{
    public const int MaximumDefinitions = 128;
    public const int MaximumParticipations = 4096;
    private readonly Dictionary<EntityId, DeliveryQuestDefinition> _definitions;
    private readonly Dictionary<(EntityId Actor, EntityId Quest), ActorQuestState> _participations;

    public QuestSnapshot(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<DeliveryQuestDefinition> definitions, IEnumerable<ActorQuestState> participations)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(participations);
        _definitions = [];
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (DeliveryQuestDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (_definitions.Count == MaximumDefinitions || !_definitions.TryAdd(definition.DefinitionId, definition)
                || !keys.Add(definition.Key))
            {
                throw new ArgumentException("Quest definitions require bounded unique identities and keys.");
            }
        }
        _participations = [];
        HashSet<EntityId> causes = [];
        ulong minimumRevision = 0;
        foreach (ActorQuestState participation in participations)
        {
            ArgumentNullException.ThrowIfNull(participation);
            if (_participations.Count == MaximumParticipations || !_definitions.ContainsKey(participation.QuestId)
                || !_participations.TryAdd((participation.ActorId, participation.QuestId), participation)
                || !causes.Add(participation.AcceptanceCommandId)
                || participation.CompletionCommandId is { } completion && !causes.Add(completion))
            {
                throw new ArgumentException("Quest participants require known definitions and unique participation and causes.");
            }
            minimumRevision = checked(minimumRevision + participation.Revision);
        }
        if (revision != minimumRevision)
        {
            throw new ArgumentException("Quest revision must retain every accepted and completed transition.");
        }
        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Definitions = Array.AsReadOnly(_definitions.Values.OrderBy(value => value.DefinitionId).ToArray());
        Participations = Array.AsReadOnly(_participations.Values.OrderBy(value => value.ActorId)
            .ThenBy(value => value.QuestId).ToArray());
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<DeliveryQuestDefinition> Definitions { get; }
    public ReadOnlyCollection<ActorQuestState> Participations { get; }
    public ContentHash ContentHash => ContentHash.Compute(QuestCodecV1.Encode(this));
    public DeliveryQuestDefinition? FindDefinition(EntityId id) => _definitions.GetValueOrDefault(id);
    public ActorQuestState? FindParticipation(EntityId actorId, EntityId questId) =>
        _participations.GetValueOrDefault((actorId, questId));

    public UInt128 OutstandingReward(EntityId reserveAccountId) => Participations
        .Where(value => value.Status == ActorQuestStatus.Accepted)
        .Select(value => _definitions[value.QuestId].Reward)
        .Where(value => value.ReserveAccountId == reserveAccountId)
        .Aggregate((UInt128)0, (sum, reward) => sum + reward.CurrencyAmount);

    public void ValidateForAuthority(CountedAuthorityState inventory, ProgressionSnapshot progression)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(progression);
        if (inventory.WorldId != WorldId || inventory.BranchId != BranchId
            || progression.WorldId != WorldId || progression.BranchId != BranchId)
        {
            throw new ArgumentException("Quest, progression and custody must share one world and branch.");
        }
        progression.ValidateActorSet(inventory.Actors);
        foreach (DeliveryQuestDefinition definition in Definitions)
        {
            if (!inventory.Actors.Contains(definition.GiverActorId)
                || !inventory.Policies.Any(value => value.CustodianId == definition.DestinationCustodianId
                    && value.OwnerActorId == definition.GiverActorId)
                || !inventory.ReservationIds.Contains(definition.MaterialReservationId)
                || !inventory.Sources.Any(value => value.Definition == definition.Item))
            {
                throw new ArgumentException("A delivery quest needs an admitted giver, owned destination, reservation and source item.");
            }
            CurrencyAccount reserve = progression.Currency.GetAccount(definition.Reward.ReserveAccountId);
            if (reserve.Kind != CurrencyAccountKind.Reserve
                || definition.Reward.Currency.DefinitionId != progression.Currency.Currency.DefinitionId
                || definition.Reward.Currency.Version != progression.Currency.Currency.Version
                || OutstandingReward(reserve.AccountId) > reserve.Balance)
            {
                throw new ArgumentException("Every accepted reward must be backed by its exact currency reserve.");
            }
        }
        foreach (ActorQuestState participation in Participations)
        {
            if (!inventory.Actors.Contains(participation.ActorId)
                || participation.ActorId == _definitions[participation.QuestId].GiverActorId)
            {
                throw new ArgumentException("Each participant must be an admitted actor distinct from the quest giver.");
            }
        }
    }

    internal QuestSnapshot WithParticipation(ActorQuestState participation) => new(WorldId, BranchId,
        checked(Revision + 1), Definitions, Participations.Where(value =>
            value.ActorId != participation.ActorId || value.QuestId != participation.QuestId).Append(participation));
}
