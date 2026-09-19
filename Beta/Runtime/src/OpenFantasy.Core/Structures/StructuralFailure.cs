namespace OpenFantasy.Core.Structures;

public enum StructuralFailureReason
{
    Removed = 1,
    Unsupported = 2,
    ConditionLost = 3,
    Overloaded = 4
}

/// <summary>The first reason and evaluated load that removed an element from this failure plan.</summary>
public sealed record StructuralFailure
{
    public StructuralFailure(EntityId elementId, StructuralFailureReason reason, UInt128 load, uint pass)
    {
        if (!Enum.IsDefined(reason) || (reason == StructuralFailureReason.Removed) != (pass == 0))
        {
            throw new ArgumentException("Explicit removals precede evaluation; other failures retain a positive pass number.");
        }
        ElementId = elementId;
        Reason = reason;
        Load = load;
        Pass = pass;
    }

    public EntityId ElementId { get; }
    public StructuralFailureReason Reason { get; }
    public UInt128 Load { get; }
    public uint Pass { get; }
}
