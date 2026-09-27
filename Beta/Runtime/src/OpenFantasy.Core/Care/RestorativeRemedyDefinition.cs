using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Care;

/// <summary>Pinned external administration of one real item dose; it neither grants possession nor consent.</summary>
public sealed class RestorativeRemedyDefinition
{
    public RestorativeRemedyDefinition(EntityId definitionId, CountedItemDefinition item, uint restoredHp,
        ulong rangeSubunits)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (definitionId == item.DefinitionId || restoredHp == 0 || restoredHp > int.MaxValue || rangeSubunits == 0
            || rangeSubunits > 4_096)
            throw new ArgumentException("A remedy requires distinct pinned identity, positive bounded health and interaction range.");
        DefinitionId = definitionId;
        Item = item;
        RestoredHp = restoredHp;
        RangeSubunits = rangeSubunits;
    }

    public EntityId DefinitionId { get; }
    public CountedItemDefinition Item { get; }
    public uint RestoredHp { get; }
    public ulong RangeSubunits { get; }
    public ContentHash Version => ContentHash.Compute(CareCodecV1.EncodeRemedy(this));
}
