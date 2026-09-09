namespace OpenFantasy.Core.Authority;

public enum CustodyDepositRule
{
    OwnerOnly = 1,
    AnyControlledActor = 2
}

/// <summary>Explicit withdrawal owner and receiving rule for one counted storage.</summary>
public sealed record CustodianAccessPolicy
{
    public CustodianAccessPolicy(EntityId custodianId, EntityId ownerActorId, CustodyDepositRule depositRule)
    {
        if (!Enum.IsDefined(depositRule))
        {
            throw new ArgumentOutOfRangeException(nameof(depositRule));
        }

        CustodianId = custodianId;
        OwnerActorId = ownerActorId;
        DepositRule = depositRule;
    }

    public EntityId CustodianId { get; }
    public EntityId OwnerActorId { get; }
    public CustodyDepositRule DepositRule { get; }

    internal bool AllowsDeposit(EntityId actorId) => OwnerActorId == actorId
        || DepositRule == CustodyDepositRule.AnyControlledActor;
}
