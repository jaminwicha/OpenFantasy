using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Gathering;

/// <summary>A finite renewable reserve. Regrowth restores swings, never the issued-source cursor.</summary>
public sealed class GatherNodeState
{
    public GatherNodeState(EntityId nodeId, GatherNodeDefinition definition, EntityId sourceId, ulong unitCapacity,
        SubunitPosition feet, ulong revision, ulong nextUnissuedUnit, int remainingSwings, ulong? depletedAtStep = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (unitCapacity == 0 || nextUnissuedUnit > unitCapacity || remainingSwings < 0
            || remainingSwings > definition.MaxSwings || nextUnissuedUnit < (ulong)(definition.MaxSwings - remainingSwings)
            || (remainingSwings == 0) != depletedAtStep.HasValue)
        {
            throw new ArgumentException("The finite node stock, swing count or depletion clock is inconsistent.");
        }
        if (depletedAtStep is
        {
            } depleted) { _ = checked(depleted + definition.RegrowthDelaySteps);
        }
        NodeId = nodeId;
        Definition = definition;
        SourceId = sourceId;
        UnitCapacity = unitCapacity;
        Feet = feet;
        Revision = revision;
        NextUnissuedUnit = nextUnissuedUnit;
        RemainingSwings = remainingSwings;
        DepletedAtStep = depletedAtStep;
    }
    public EntityId NodeId { get; }
    public GatherNodeDefinition Definition { get; }
    public EntityId SourceId { get; }
    public ulong UnitCapacity { get; }
    public SubunitPosition Feet { get; }
    public ulong Revision { get; }
    public ulong NextUnissuedUnit { get; }
    public int RemainingSwings { get; }
    public ulong? DepletedAtStep { get; }
    public ulong? RespawnAtAdvancingStep => DepletedAtStep is { } step ? checked(step + Definition.RegrowthDelaySteps) : null;
    public GatherNodeVisibility VisibilityAt(ulong advancingStep)
    {
        if (RemainingSwings != 0)
        {
            return GatherNodeVisibility.Ready;
        }
        if (advancingStep < DepletedAtStep!.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(advancingStep));
        }
        return advancingStep - DepletedAtStep.Value < Definition.StumpSteps ? GatherNodeVisibility.Stump : GatherNodeVisibility.Absent;
    }
    internal GatherNodeState Harvest(uint power, ulong step) => new(NodeId, Definition, SourceId, UnitCapacity,
        Feet, checked(Revision + 1), checked(NextUnissuedUnit + power), RemainingSwings - 1,
        RemainingSwings == 1 ? step : null);
    internal GatherNodeState Regrow() => new(NodeId, Definition, SourceId, UnitCapacity, Feet,
        checked(Revision + 1), NextUnissuedUnit, Definition.MaxSwings);
}
