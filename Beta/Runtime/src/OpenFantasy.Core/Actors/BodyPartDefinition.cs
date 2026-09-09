using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Actors;

/// <summary>An immutable slot with explicit parentage, dimensions and supported functions.</summary>
public sealed class BodyPartDefinition
{
    public BodyPartDefinition(BodySlotKey slot, BodySlotKey? parentSlot, BodyDimensions dimensions,
        IEnumerable<BodyFunction> functions)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(dimensions);
        ArgumentNullException.ThrowIfNull(functions);
        var distinct = new SortedSet<BodyFunction>();
        foreach (BodyFunction function in functions)
        {
            if (!Enum.IsDefined(function) || !distinct.Add(function))
            {
                throw new ArgumentException("Part functions must be known and unique.", nameof(functions));
            }
        }

        Slot = slot;
        ParentSlot = parentSlot;
        Dimensions = dimensions;
        Functions = Array.AsReadOnly(distinct.ToArray());
    }

    public BodySlotKey Slot { get; }
    public BodySlotKey? ParentSlot { get; }
    public BodyDimensions Dimensions { get; }
    public ReadOnlyCollection<BodyFunction> Functions { get; }
}
