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

    public SpatialStructure(EntityId structureId, IEnumerable<SubunitBox> boxes)
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
    }

    public EntityId StructureId { get; }
    public ReadOnlyCollection<SubunitBox> Boxes { get; }
    public long GroundPlaneZ { get; }
}
