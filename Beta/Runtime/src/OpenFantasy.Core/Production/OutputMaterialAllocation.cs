using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>The mass of one named input incorporated in each unit of one canonical output.</summary>
public sealed record OutputMaterialAllocation
{
    public OutputMaterialAllocation(CountedItemDefinition inputItem, uint outputIndex, MassMilligrams massPerOutputUnit)
    {
        ArgumentNullException.ThrowIfNull(inputItem);
        if (outputIndex >= MaterialRecipeDefinition.MaximumTermCount || massPerOutputUnit.Value == 0)
        {
            throw new ArgumentException("An output allocation requires a bounded output and positive input mass.");
        }
        InputItem = inputItem;
        OutputIndex = outputIndex;
        MassPerOutputUnit = massPerOutputUnit;
    }

    public CountedItemDefinition InputItem { get; }
    public uint OutputIndex { get; }
    public MassMilligrams MassPerOutputUnit { get; }
}
