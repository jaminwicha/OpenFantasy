using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Structures;

/// <summary>One explicitly calibrated load-bearing element; material hardness supplies no capacity.</summary>
public sealed class StructuralElement
{
    public const uint MaximumConditionParts = 10_000;

    public StructuralElement(EntityId elementId, MassMilligrams ownMass, MassMilligrams appliedMass,
        MassMilligrams ratedCapacity, uint conditionParts, bool isFoundation, ushort minimumSupportCount = 1)
    {
        if (ratedCapacity.Value == 0 || conditionParts > MaximumConditionParts
            || (isFoundation ? minimumSupportCount != 0 : minimumSupportCount is < 1 or > StructuralLoadGraph.MaximumBearingsPerElement))
        {
            throw new ArgumentException("An element needs positive rated capacity, bounded condition and an explicit foundation or support requirement.");
        }
        ElementId = elementId;
        OwnMass = ownMass;
        AppliedMass = appliedMass;
        RatedCapacity = ratedCapacity;
        ConditionParts = conditionParts;
        IsFoundation = isFoundation;
        MinimumSupportCount = minimumSupportCount;
    }

    public EntityId ElementId { get; }
    public MassMilligrams OwnMass { get; }
    public MassMilligrams AppliedMass { get; }
    public MassMilligrams RatedCapacity { get; }
    public uint ConditionParts { get; }
    public bool IsFoundation { get; }
    public ushort MinimumSupportCount { get; }
    public UInt128 InputMass => (UInt128)OwnMass.Value + AppliedMass.Value;
    public UInt128 EffectiveCapacity => (UInt128)RatedCapacity.Value * ConditionParts / MaximumConditionParts;
}
