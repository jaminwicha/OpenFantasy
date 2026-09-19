using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Derived collision geometry for one installed instance. Its owner validates the actual material,
/// definition and member bearing connections; this geometry proves neither fabrication nor strength.
/// </summary>
public sealed class SpatialStructure
{
    public const int MaximumBoxes = 32;
    public const long MaximumAxisSubunits = 2048;

    public SpatialStructure(EntityId structureId, IEnumerable<SubunitBox> boxes,
        IEnumerable<StructureBearingSurface>? bearingSurfaces = null)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        var copied = new HashSet<SubunitBox>();
        foreach (SubunitBox box in boxes)
        {
            ArgumentNullException.ThrowIfNull(box);
            if (copied.Count == MaximumBoxes || !copied.Add(box))
            {
                throw new ArgumentException("A structure requires bounded unique collision boxes.", nameof(boxes));
            }
        }
        if (copied.Count == 0)
        {
            throw new ArgumentException("A structure needs at least one collision box.", nameof(boxes));
        }
        long minX = copied.Min(value => value.Minimum.X);
        long minY = copied.Min(value => value.Minimum.Y);
        long minZ = copied.Min(value => value.Minimum.Z);
        if ((Int128)copied.Max(value => value.Maximum.X) - minX > MaximumAxisSubunits
            || (Int128)copied.Max(value => value.Maximum.Y) - minY > MaximumAxisSubunits
            || (Int128)copied.Max(value => value.Maximum.Z) - minZ > MaximumAxisSubunits)
        {
            throw new ArgumentException("The structure exceeds its qualified local span.", nameof(boxes));
        }
        StructureId = structureId;
        GroundPlaneZ = minZ;
        Boxes = Array.AsReadOnly(copied.OrderBy(value => value.Minimum.X).ThenBy(value => value.Minimum.Y)
            .ThenBy(value => value.Minimum.Z).ThenBy(value => value.Maximum.X)
            .ThenBy(value => value.Maximum.Y).ThenBy(value => value.Maximum.Z).ToArray());
        var surfaces = new HashSet<StructureBearingSurface>();
        foreach (StructureBearingSurface surface in bearingSurfaces ?? [])
        {
            ArgumentNullException.ThrowIfNull(surface);
            if (surfaces.Count == MaximumBoxes || !surfaces.Add(surface) || !Boxes.Any(surface.Fits))
            {
                throw new ArgumentException("Bearing surfaces require bounded unique physical tops inside their colliders.", nameof(bearingSurfaces));
            }
        }
        BearingSurfaces = Array.AsReadOnly(surfaces.OrderBy(value => value.TopTicks)
            .ThenBy(value => value.Bounds.MinU).ThenBy(value => value.Bounds.MinV)
            .ThenBy(value => value.Bounds.MaxU).ThenBy(value => value.Bounds.MaxV).ToArray());
    }

    public EntityId StructureId { get; }
    public ReadOnlyCollection<SubunitBox> Boxes { get; }
    public ReadOnlyCollection<StructureBearingSurface> BearingSurfaces { get; }
    public long GroundPlaneZ { get; }
}
