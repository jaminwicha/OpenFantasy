using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Construction.Ownership.StoredConstructionWorkOwners;

namespace OpenFantasy.Core.Construction.Ownership;

/// <summary>Work facts decoded from selected stored owners; no retained full owner or historical identity authority.</summary>
internal sealed class StoredConstructionDecisionSource : ConstructionDecisionSource
{
    private readonly ContentHash _commandHash;
    private readonly EntityId _actor;
    private readonly EntityId _custodian;
    private readonly EntityId _principal;
    private readonly EntityId _session;
    private readonly int _hitPoints;
    private readonly HashSet<EntityId> _completedQuests;

    internal StoredConstructionDecisionSource(TerrainScope scope, WorldActionCommand command,
        IReadOnlyDictionary<ConstructionOwnerKey, byte[][]> fields)
    {
        WorldId = scope.WorldId;
        BranchId = scope.BranchId;
        _commandHash = command.ContentHash;
        _actor = command.ActorId;
        var actor = Get(fields, new(ConstructionOwnerKind.Actor, _actor), 3);
        if (!actor[0].AsSpan().SequenceEqual(scope.RealmId.ToByteArray())) throw new FormatException("Wrong actor realm.");
        var resident = WorldSnapshotCodecV1.DecodeResidents(actor[2]).Single(value => value.ActorId == _actor);
        _custodian = resident.InventoryCustodianId;
        var control = Get(fields, new(ConstructionOwnerKind.Control, _actor), 2);
        _principal = EntityId.FromBytes(control[0]);
        _session = EntityId.FromBytes(control[1]);
        _hitPoints = ProgressionCodecV1.Decode(Get(fields, new(ConstructionOwnerKind.Progression, WorldId), 1)[0])
            .GetActor(_actor).CurrentHp;
        _completedQuests = QuestCodecV1.Decode(Get(fields, new(ConstructionOwnerKind.Quests, WorldId), 1)[0])
            .Participations.Where(value => value.Status == ActorQuestStatus.Completed).Select(value => value.QuestId).ToHashSet();
        HasReadingHold = WorldSnapshotCodecV1.DecodeDialogues(Get(fields, new(ConstructionOwnerKind.Dialogues, WorldId), 1)[0]).Count != 0;
    }

    internal override EntityId WorldId { get; }
    internal override EntityId BranchId { get; }
    internal override bool HasReadingHold { get; }
    internal override bool HasProgressionAndQuests => true;
    internal override bool Authorizes(WorldActionCommand command) => command.ContentHash == _commandHash
        && command.ActorId == _actor && command.ControlWitness is null
        && command.PrincipalId == _principal && command.OriginSessionId == _session;
    internal override int CurrentHp(EntityId actorId) => actorId == _actor ? _hitPoints : throw new ArgumentException("Actor outside work cohort.");
    internal override EntityId InventoryCustodian(EntityId actorId) => actorId == _actor ? _custodian : throw new ArgumentException("Actor outside work cohort.");
    internal override bool QuestCompleted(EntityId questId) => _completedQuests.Contains(questId);
    internal override bool ContainsIdentity(EntityId identity) => throw new InvalidOperationException("Qualification does not certify final spawned identity exclusion.");
}
