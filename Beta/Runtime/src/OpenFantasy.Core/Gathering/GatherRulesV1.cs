namespace OpenFantasy.Core.Gathering;

/// <summary>Explicit Squire-derived gathering profile, not a universal future character-stat formula.</summary>
public static class GatherRulesV1
{
    public const uint MaximumEffort = 1000;
    public const uint EffortPerAdvancingStep = 10;
    public const uint EffortPerSwing = 400;
    public const long ReachSubunits = 448;
    public const ulong StumpSteps = 20;
}

public enum GatherToolKind
{
    Axe = 1,
    Sickle = 2
}
public enum GatherNodeKind
{
    Oak = 1,
    Berry = 2
}
public enum GatherNodeVisibility
{
    Ready = 1,
    Stump = 2,
    Absent = 3
}
