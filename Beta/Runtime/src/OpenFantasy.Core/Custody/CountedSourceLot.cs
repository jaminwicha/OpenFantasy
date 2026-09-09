namespace OpenFantasy.Core.Custody;

[Flags]
public enum LotProtection
{
    None = 0,
    Equipped = 1,
    Favorite = 2,
    Heirloom = 4,
    UserReserved = 8
}

/// <summary>
/// An untransformed counted lot with one exact source interval. Crafted mixtures require a
/// different provenance contract; output item count cannot stand in for their input claims.
/// </summary>
public sealed record CountedSourceLot
{
    private const LotProtection KnownProtections = LotProtection.Equipped | LotProtection.Favorite
        | LotProtection.Heirloom | LotProtection.UserReserved;

    public CountedSourceLot(EntityId lotId, EntityId custodianId, CountedItemDefinition definition,
        SourceInterval source, ulong revision, LotProtection protection = LotProtection.None,
        EntityId? reservedBy = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(source);
        if ((protection & ~KnownProtections) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(protection));
        }
        LotId = lotId;
        CustodianId = custodianId;
        Definition = definition;
        Source = source;
        Revision = revision;
        Protection = protection;
        ReservedBy = reservedBy;
    }

    public EntityId LotId { get; }
    public EntityId CustodianId { get; }
    public CountedItemDefinition Definition { get; }
    public SourceInterval Source { get; }
    public ulong Quantity => Source.Quantity;
    public ulong Revision { get; }
    public LotProtection Protection { get; }
    public EntityId? ReservedBy { get; }
}
