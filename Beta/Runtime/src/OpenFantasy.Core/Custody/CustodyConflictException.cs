namespace OpenFantasy.Core.Custody;

public enum CustodyConflict
{
    WrongScope,
    MissingRevision,
    UnknownRevisionTarget,
    RevisionConflict,
    MissingLot,
    MissingStorage,
    WrongCustodian,
    ResourceReserved,
    EquippedLot,
    InsufficientQuantity,
    CapacityExceeded,
    IdentityCollision,
    StalePlan
}

/// <summary>Planning failure; never an authenticated or durable command receipt.</summary>
public sealed class CustodyConflictException(CustodyConflict conflict) : InvalidOperationException(conflict.ToString())
{
    public CustodyConflict Conflict { get; } = conflict;
}
