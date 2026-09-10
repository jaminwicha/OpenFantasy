using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

public sealed class TransformedLot
{
    public TransformedLot(EntityId lotId, EntityId custodianId, CountedItemDefinition definition,
        ProductionOutputInterval output, ulong revision, LotProtection protection = LotProtection.None, EntityId? reservedBy = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(output);
        const LotProtection known = LotProtection.Equipped | LotProtection.Favorite | LotProtection.Heirloom | LotProtection.UserReserved;
        if ((protection & ~known) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(protection));
        }
        LotId = lotId;
        CustodianId = custodianId;
        Definition = definition;
        Output = output;
        Revision = revision;
        Protection = protection;
        ReservedBy = reservedBy;
    }

    public EntityId LotId { get; }
    public EntityId CustodianId { get; }
    public CountedItemDefinition Definition { get; }
    public ProductionOutputInterval Output { get; }
    public ulong Quantity => Output.Quantity;
    public ulong Revision { get; }
    public LotProtection Protection { get; }
    public EntityId? ReservedBy { get; }
}
