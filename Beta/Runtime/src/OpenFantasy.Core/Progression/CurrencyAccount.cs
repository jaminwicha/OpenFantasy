namespace OpenFantasy.Core.Progression;

public enum CurrencyAccountKind
{
    Actor = 1,
    Reserve = 2
}

/// <summary>A separately conserved balance. A reserve does not impersonate an actor or occupy backpack capacity.</summary>
public sealed class CurrencyAccount
{
    public CurrencyAccount(EntityId accountId, CurrencyAccountKind kind, EntityId? actorId,
        ulong balance, ulong revision)
    {
        if (!Enum.IsDefined(kind) || (kind == CurrencyAccountKind.Actor) != actorId.HasValue
            || accountId == actorId)
        {
            throw new ArgumentException("An actor account requires a distinct actor; a reserve has no actor owner.");
        }
        AccountId = accountId;
        Kind = kind;
        ActorId = actorId;
        Balance = balance;
        Revision = revision;
    }

    public EntityId AccountId { get; }
    public CurrencyAccountKind Kind { get; }
    public EntityId? ActorId { get; }
    public ulong Balance { get; }
    public ulong Revision { get; }

    internal CurrencyAccount WithBalance(ulong balance) =>
        new(AccountId, Kind, ActorId, balance, checked(Revision + 1));
}
