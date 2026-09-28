using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Exact direct raw retirement at a captured boundary. Adjacent rights coalesce only within the
/// same source, item pin and retiring custodian; this records no new issuance or live entitlement.
/// </summary>
public sealed class DirectRawRetirementRange
{
    internal DirectRawRetirementRange(EntityId custodianId, CountedItemDefinition definition, SourceInterval source)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(source);
        CustodianId = custodianId;
        Definition = definition;
        Source = source;
    }

    public EntityId CustodianId { get; }
    public CountedItemDefinition Definition { get; }
    public SourceInterval Source { get; }
}
