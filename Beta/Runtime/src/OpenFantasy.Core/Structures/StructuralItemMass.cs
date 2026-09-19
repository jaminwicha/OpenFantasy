using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Structures;

/// <summary>Explicit positive unit mass for an exact item pin without an available production calibration.</summary>
public sealed record StructuralItemMass
{
    public StructuralItemMass(CountedItemDefinition item, MassMilligrams massPerUnit)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (massPerUnit.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(massPerUnit));
        }
        Item = item;
        MassPerUnit = massPerUnit;
    }

    public CountedItemDefinition Item { get; }
    public MassMilligrams MassPerUnit { get; }
}
