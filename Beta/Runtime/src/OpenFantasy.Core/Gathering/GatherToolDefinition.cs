using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Gathering;

/// <summary>One explicitly admitted item pin and its source tool kind/power.</summary>
public sealed record GatherToolDefinition
{
    public GatherToolDefinition(CountedItemDefinition definition, GatherToolKind kind, uint power)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(kind) || power is < 1 or > 3)
        {
            throw new ArgumentException("The gathering tool kind or source power is outside this profile.");
        }
        Definition = definition;
        Kind = kind;
        Power = power;
    }
    public CountedItemDefinition Definition { get; }
    public GatherToolKind Kind { get; }
    public uint Power { get; }
}
