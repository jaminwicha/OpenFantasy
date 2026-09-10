using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Progression;

/// <summary>
/// One finite denomination with an exact admitted supply. Transactions only redistribute balances.
/// The world must authorize admission and retain history; constructor consistency does not prove prior ownership.
/// </summary>
public sealed class CurrencyLedger
{
    public const int MaximumAccounts = 256;
    private readonly Dictionary<EntityId, CurrencyAccount> _accounts;

    public CurrencyLedger(EntityId worldId, EntityId branchId, ulong revision, CurrencyDefinition currency,
        ulong admittedSupply, IEnumerable<CurrencyAccount> accounts)
    {
        ArgumentNullException.ThrowIfNull(currency);
        Accounts = ProgressionValidation.Sorted(accounts, value => value.AccountId, MaximumAccounts, nameof(accounts));
        if (Accounts.Count == 0)
        {
            throw new ArgumentException("A finite currency ledger requires at least one account.", nameof(accounts));
        }
        HashSet<EntityId> identities = [];
        foreach (EntityId id in new[] { worldId, branchId, currency.DefinitionId })
        {
            if (!identities.Add(id))
            {
                throw new ArgumentException("World, branch and currency definition identities must be distinct.");
            }
        }
        foreach (CurrencyAccount account in Accounts)
        {
            if (!identities.Add(account.AccountId))
            {
                throw new ArgumentException("An account identity aliases another admitted identity.");
            }
        }
        ulong total = 0;
        foreach (CurrencyAccount account in Accounts)
        {
            if (account.ActorId is { } actor && !identities.Add(actor))
            {
                throw new ArgumentException("Each actor requires one account and a distinct actor identity.");
            }
            total = checked(total + account.Balance);
        }
        if (total != admittedSupply)
        {
            throw new ArgumentException("Currency balances must equal the exact finite admitted supply.");
        }
        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Currency = currency;
        AdmittedSupply = admittedSupply;
        _accounts = Accounts.ToDictionary(value => value.AccountId);
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public CurrencyDefinition Currency { get; }
    public ulong AdmittedSupply { get; }
    public ReadOnlyCollection<CurrencyAccount> Accounts { get; }

    public CurrencyAccount GetAccount(EntityId accountId) => _accounts.TryGetValue(accountId, out var account)
        ? account : throw new ArgumentException("The currency account is not admitted.", nameof(accountId));

    public CurrencyAccount GetActorAccount(EntityId actorId) =>
        Accounts.FirstOrDefault(value => value.ActorId == actorId)
        ?? throw new ArgumentException("The actor currency account is not admitted.", nameof(actorId));

    internal CurrencyLedger PayReward(CurrencyAccount reserve, CurrencyAccount recipient, ulong amount)
    {
        CurrencyAccount debit = reserve.WithBalance(checked(reserve.Balance - amount));
        CurrencyAccount credit = recipient.WithBalance(checked(recipient.Balance + amount));
        return new CurrencyLedger(WorldId, BranchId, checked(Revision + 1), Currency, AdmittedSupply,
            Accounts.Select(value => value.AccountId == debit.AccountId ? debit
                : value.AccountId == credit.AccountId ? credit : value));
    }
}
