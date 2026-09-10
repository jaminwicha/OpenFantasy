using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Custody;

public sealed class SourceDeliveryPlan
{
    internal SourceDeliveryPlan(CountedAuthorityState candidate, WorldActionRejection rejection,
        IEnumerable<EntityId> createdIds, IEnumerable<EntityId> deliveredIds)
    {
        Candidate = candidate;
        Rejection = rejection;
        CreatedIds = Array.AsReadOnly(createdIds.ToArray());
        DeliveredIds = Array.AsReadOnly(deliveredIds.ToArray());
    }

    public CountedAuthorityState Candidate { get; }
    public WorldActionRejection Rejection { get; }
    public bool Succeeded => Rejection == WorldActionRejection.None;
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
    public ReadOnlyCollection<EntityId> DeliveredIds { get; }
}
