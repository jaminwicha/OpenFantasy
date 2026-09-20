using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Spatial;

/// <summary>One rigid supplied assembly or actor. Geometry conveys no material or movement authority.</summary>
public sealed class VerticalMotionBody
{
    private VerticalMotionBody(SpatialStructure? structure, ActorPresence? actor, IEnumerable<SubunitBox> boxes)
    {
        Structure = structure;
        Actor = actor;
        BodyId = structure?.StructureId ?? actor!.ActorId;
        Boxes = Array.AsReadOnly(boxes.ToArray());
    }

    public EntityId BodyId { get; }
    public SpatialStructure? Structure { get; }
    public ActorPresence? Actor { get; }
    public ReadOnlyCollection<SubunitBox> Boxes { get; }

    public static VerticalMotionBody ForStructure(SpatialStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        if (structure.BearingSurfaces.Any(surface => surface.TopTicks % StructureBearingSurface.TicksPerSubunit != 0))
        {
            throw new ArgumentException("Moving construction requires exact subunit contact planes.", nameof(structure));
        }
        return new(structure, null, structure.Boxes);
    }

    public static VerticalMotionBody ForActor(ActorPresence actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        BodyVolume volume = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
        return new(null, actor, [new(new(volume.MinimumX, volume.MinimumY, volume.MinimumZ),
            new(volume.MaximumX, volume.MaximumY, volume.MaximumZ))]);
    }

    internal IEnumerable<SubunitBox> Bases => Structure is { } structure
        ? Boxes.Where(box => box.Minimum.Z == structure.GroundPlaneZ) : Boxes;

    internal SpatialStructure StructureAt(long drop)
    {
        SpatialStructure original = Structure ?? throw new InvalidOperationException("This moving body is an actor.");
        return new(original.StructureId, Boxes.Select(box => At(box, drop)), original.BearingSurfaces.Select(surface =>
            new StructureBearingSurface(surface.Bounds, surface.TopTicks - (Int128)drop * StructureBearingSurface.TicksPerSubunit)));
    }

    /// <summary>The integrating owner revises authoritative presence; a geometric proposal preserves the original revision.</summary>
    internal ActorPresence ActorAt(long drop)
    {
        ActorPresence original = Actor ?? throw new InvalidOperationException("This moving body is construction.");
        return new(original.Binding, original.Condition, original.Scope, original.Feet.Offset(0, 0, -drop),
            original.Revision, original.HomePlaceId);
    }

    internal static SubunitBox At(SubunitBox box, long drop) =>
        new(box.Minimum.Offset(0, 0, -drop), box.Maximum.Offset(0, 0, -drop));
}
