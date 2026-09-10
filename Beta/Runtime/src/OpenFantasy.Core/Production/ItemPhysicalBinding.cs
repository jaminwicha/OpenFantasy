using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>An authored physical calibration attached to an unchanged counted item content pin.</summary>
public sealed record ItemPhysicalBinding
{
    public ItemPhysicalBinding(CountedItemDefinition item, MaterialDefinition physical)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(physical);
        if (item.DefinitionId == physical.DefinitionId)
        {
            throw new ArgumentException("Item content and its physical calibration require distinct identities.");
        }
        Item = item;
        Physical = physical;
    }

    public CountedItemDefinition Item { get; }
    public MaterialDefinition Physical { get; }
}
