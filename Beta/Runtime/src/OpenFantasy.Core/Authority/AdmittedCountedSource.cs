using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Authority;

/// <summary>
/// An admitted finite interval [0, UnitCapacity) with one exact untransformed unit definition.
/// This branch-local admission is not an extraction operation or cross-branch export allowance.
/// </summary>
public sealed record AdmittedCountedSource
{
    public AdmittedCountedSource(EntityId sourceId, CountedItemDefinition definition, ulong unitCapacity)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (unitCapacity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCapacity));
        }

        SourceId = sourceId;
        Definition = definition;
        UnitCapacity = unitCapacity;
    }

    public EntityId SourceId { get; }
    public CountedItemDefinition Definition { get; }
    public ulong UnitCapacity { get; }
}
