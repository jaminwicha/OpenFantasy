using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Quests;

public sealed class DeliveryQuestPlan
{
    internal DeliveryQuestPlan(CountedAuthorityState inventory, ProgressionSnapshot progression,
        QuestSnapshot quests, WorldActionRejection rejection, IEnumerable<EntityId> createdIds)
    {
        CandidateInventory = inventory;
        CandidateProgression = progression;
        CandidateQuests = quests;
        Rejection = rejection;
        CreatedIds = Array.AsReadOnly(createdIds.ToArray());
    }

    public CountedAuthorityState CandidateInventory { get; }
    public ProgressionSnapshot CandidateProgression { get; }
    public QuestSnapshot CandidateQuests { get; }
    public WorldActionRejection Rejection { get; }
    public bool Succeeded => Rejection == WorldActionRejection.None;
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
}
