using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Spatial;

/// <summary>An exact contact-distance event, independent of rounded flight-time presentation.</summary>
public sealed class VerticalMotionEvent
{
    internal VerticalMotionEvent(long dropSubunits, IEnumerable<EntityId> bodyIds)
    {
        DropSubunits = dropSubunits;
        BodyIds = Array.AsReadOnly(bodyIds.Order().ToArray());
    }

    public long DropSubunits { get; }
    public ReadOnlyCollection<EntityId> BodyIds { get; }
}

/// <summary>Complete proposed endpoint geometry; no load, damage, time or publication authority is implied.</summary>
public sealed class VerticalMotionResult
{
    internal VerticalMotionResult(VerticalMotionScene scene, IReadOnlyList<long> drops,
        IEnumerable<VerticalMotionEvent> events)
    {
        Drops = new ReadOnlyDictionary<EntityId, long>(scene.MovingBodies.Select((body, index) =>
            (body.BodyId, Drop: drops[index])).ToDictionary(value => value.BodyId, value => value.Drop));
        Structures = Array.AsReadOnly(scene.StationaryStructures.Concat(scene.MovingBodies.Select((body, index) =>
            (Body: body, Drop: drops[index])).Where(value => value.Body.Structure is not null)
            .Select(value => value.Body.StructureAt(value.Drop))).OrderBy(value => value.StructureId).ToArray());
        Actors = Array.AsReadOnly(scene.StationaryActors.Concat(scene.MovingBodies.Select((body, index) =>
            (Body: body, Drop: drops[index])).Where(value => value.Body.Actor is not null)
            .Select(value => value.Body.ActorAt(value.Drop))).OrderBy(value => value.ActorId).ToArray());
        Events = Array.AsReadOnly(events.ToArray());
    }

    public IReadOnlyDictionary<EntityId, long> Drops { get; }
    public ReadOnlyCollection<SpatialStructure> Structures { get; }
    public ReadOnlyCollection<ActorPresence> Actors { get; }
    public ReadOnlyCollection<VerticalMotionEvent> Events { get; }
}
