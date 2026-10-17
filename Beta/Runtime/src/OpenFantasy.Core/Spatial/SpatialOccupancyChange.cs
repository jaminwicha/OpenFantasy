namespace OpenFantasy.Core.Spatial;

/// <summary>One exact metadata replacement, insertion or removal authorized by the entity's actual owner.</summary>
public sealed class SpatialOccupancyChange
{
    public SpatialOccupancyChange(SpatialOccupancyReference? before, SpatialOccupancyReference? after)
    {
        if (before is null && after is null)
            throw new ArgumentException("An occupancy change must name an existing or resulting entity.");
        if (before is not null && after is not null
            && (before.Scope != after.Scope || before.EntityId != after.EntityId || before.Kind != after.Kind))
            throw new ArgumentException("A replacement preserves scope, entity identity and kind.");
        if (before == after)
            throw new ArgumentException("An occupancy change must change its exact reference.");
        Before = before;
        After = after;
    }

    public SpatialOccupancyReference? Before { get; }
    public SpatialOccupancyReference? After { get; }
    public EntityId EntityId => (Before ?? After!).EntityId;
}
