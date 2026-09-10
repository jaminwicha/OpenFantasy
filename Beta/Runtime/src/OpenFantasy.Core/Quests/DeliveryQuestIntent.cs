using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Quests;

/// <summary>Caller-visible revisions only; shared reward funding is adjudicated by the authority.</summary>
public abstract record DeliveryQuestIntent : WorldActionIntent
{
    protected DeliveryQuestIntent(EntityId questId, ContentHash questVersion, EntityId giverActorId,
        ulong expectedGiverRevision, ulong expectedParticipationRevision, ulong expectedProgressionRevision)
    {
        ArgumentNullException.ThrowIfNull(questVersion);
        QuestId = questId;
        QuestVersion = questVersion;
        GiverActorId = giverActorId;
        ExpectedGiverRevision = expectedGiverRevision;
        ExpectedParticipationRevision = expectedParticipationRevision;
        ExpectedProgressionRevision = expectedProgressionRevision;
    }

    public EntityId QuestId { get; }
    public ContentHash QuestVersion { get; }
    public EntityId GiverActorId { get; }
    public ulong ExpectedGiverRevision { get; }
    public ulong ExpectedParticipationRevision { get; }
    public ulong ExpectedProgressionRevision { get; }
}

public sealed record AcceptDeliveryQuestIntent : DeliveryQuestIntent
{
    public AcceptDeliveryQuestIntent(EntityId questId, ContentHash questVersion, EntityId giverActorId,
        ulong expectedGiverRevision, ulong expectedParticipationRevision, ulong expectedProgressionRevision)
        : base(questId, questVersion, giverActorId, expectedGiverRevision, expectedParticipationRevision, expectedProgressionRevision)
    {
    }
}

public sealed record CompleteDeliveryQuestIntent : DeliveryQuestIntent
{
    public CompleteDeliveryQuestIntent(EntityId questId, ContentHash questVersion, EntityId giverActorId,
        ulong expectedGiverRevision, ulong expectedParticipationRevision, ulong expectedProgressionRevision,
        ulong expectedInventoryRevision, IEnumerable<SourceLotSelection> selections)
        : base(questId, questVersion, giverActorId, expectedGiverRevision, expectedParticipationRevision, expectedProgressionRevision)
    {
        ArgumentNullException.ThrowIfNull(selections);
        var sorted = new SortedDictionary<EntityId, SourceLotSelection>();
        foreach (SourceLotSelection selection in selections)
        {
            ArgumentNullException.ThrowIfNull(selection);
            if (sorted.Count == SourceDeliveryRequest.MaximumSelections || !sorted.TryAdd(selection.LotId, selection))
            {
                throw new ArgumentException("Quest material selections must be bounded and unique.");
            }
        }
        if (sorted.Count == 0)
        {
            throw new ArgumentException("Quest delivery needs explicit selected materials.");
        }
        ExpectedInventoryRevision = expectedInventoryRevision;
        Selections = Array.AsReadOnly(sorted.Values.ToArray());
    }

    public ulong ExpectedInventoryRevision { get; }
    public ReadOnlyCollection<SourceLotSelection> Selections { get; }
}
