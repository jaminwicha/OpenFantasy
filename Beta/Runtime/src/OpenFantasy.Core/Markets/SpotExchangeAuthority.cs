using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;

namespace OpenFantasy.Core.Markets;

/// <summary>
/// Issues fixed spot quotes and settles one accepted quote as one coupled transaction over the actual
/// counted custody, produced stock and finite currency owners plus the quote book. It performs no IO,
/// authenticates nobody, proves no spatial reach and advances no clock: the host supplies verified
/// deliveries and the world supplies the current fixed step and complete-history identity occupancy.
/// </summary>
public sealed class SpotExchangeAuthority
{

    /// <summary>Records an open quote. Issuance reserves no stock and moves no money.</summary>
    public SpotQuoteIssuePlan Issue(CountedAuthorityState inventory, ProductionSnapshot production, ProgressionSnapshot progression, SpotQuoteBook book,
        SpotQuote quote, TrustedCommandDelivery issuerDelivery, ulong currentStep, Func<EntityId, bool> historical)
    {
        RequireScope(inventory, production, progression, book);
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(issuerDelivery);
        ArgumentNullException.ThrowIfNull(historical);
        SpotQuoteIssuePlan Reject(SpotExchangeRejection reason) => new(inventory, production, progression, book, book, reason);
        if (quote.WorldId != book.WorldId || quote.BranchId != book.BranchId
            || !IsCurrentController(inventory, issuerDelivery, quote.IssuerPrincipalId, quote.IssuerSessionId, quote.IssuerActorId)
            || !OwnsSettlementEnds(inventory, progression, quote))
        {
            return Reject(SpotExchangeRejection.NotAuthorized);
        }

        if (book.Entries.Count == SpotQuoteBook.MaximumQuotes || (IsOccupied(inventory, progression, book, historical, quote.QuoteId) || production.ContainsIdentity(quote.QuoteId)))
        {
            return Reject(SpotExchangeRejection.IdentityConflict);
        }

        if (quote.IssuedAtStep != currentStep)
        {
            return Reject(SpotExchangeRejection.Expired);
        }

        if (quote.MarketRevision != book.GetIssuerRevision(quote.IssuerActorId))
        {
            return Reject(SpotExchangeRejection.RevisionConflict);
        }

        try
        {
            return new(inventory, production, progression, book, book.WithIssued(new SpotQuoteEntry(quote, issuerDelivery)),
                SpotExchangeRejection.None);
        }
        catch (OverflowException)
        {
            return Reject(SpotExchangeRejection.Overflow);
        }
    }

    /// <param name="historical">
    /// Complete-history identity occupancy for this world, including retired lots and every earlier command,
    /// acceptance and spawned identity. Current owners alone cannot establish freshness.
    /// </param>
    public SpotExchangePlan Accept(CountedAuthorityState inventory, ProductionSnapshot production,
        ProgressionSnapshot progression, SpotQuoteBook book, SpotAcceptance acceptance,
        TrustedCommandDelivery counterpartyDelivery, ulong currentStep, Func<EntityId, bool> historical)
    {
        RequireScope(inventory, production, progression, book);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(acceptance);
        ArgumentNullException.ThrowIfNull(counterpartyDelivery);
        ArgumentNullException.ThrowIfNull(historical);
        if (production.WorldId != inventory.WorldId || production.BranchId != inventory.BranchId)
        {
            throw new ArgumentException("Every exchange owner must belong to the same world and branch.");
        }

        var settlement = new Settlement(inventory, production, progression, book);
        SpotQuoteEntry? entry = book.FindEntry(acceptance.QuoteId);

        // Authentication precedes every availability report: an unapproved caller learns nothing.
        if (entry is null || !IsApproved(inventory, progression, entry, acceptance, counterpartyDelivery))
        {
            return settlement.Reject(SpotExchangeRejection.NotAuthorized);
        }

        SpotQuote quote = entry.Quote;
        if (entry.Fill is { } recorded)
        {
            return recorded.AcceptanceId == acceptance.AcceptanceId && recorded.AcceptanceHash == acceptance.ContentHash
                ? settlement.Duplicate(recorded) : settlement.Reject(SpotExchangeRejection.QuoteClosed);
        }

        if (IsOccupied(inventory, progression, book, historical, acceptance.AcceptanceId)
            || production.ContainsIdentity(acceptance.AcceptanceId))
        {
            return settlement.Reject(SpotExchangeRejection.IdentityConflict);
        }

        if (currentStep < quote.IssuedAtStep || currentStep > quote.ExpiresAtStep)
        {
            return settlement.Reject(SpotExchangeRejection.Expired);
        }

        if (quote.MarketRevision != book.GetIssuerRevision(quote.IssuerActorId))
        {
            return settlement.Reject(SpotExchangeRejection.RevisionConflict);
        }

        CurrencyLedger ledger = progression.Currency;
        if (ledger.Currency.DefinitionId != quote.CurrencyDefinitionId || ledger.Currency.Version != quote.CurrencyVersion
            || quote.Fees.Any(fee => !ledger.Accounts.Any(account => account.AccountId == fee.PayeeAccountId)))
        {
            return settlement.Reject(SpotExchangeRejection.RevisionConflict);
        }

        try
        {
            ulong owed = checked(acceptance.Payment + acceptance.Fees);
            if (ledger.GetAccount(quote.BuyerAccountId).Balance < owed)
            {
                return settlement.Reject(SpotExchangeRejection.InsufficientFunds);
            }

            return settlement.Move(entry, acceptance, counterpartyDelivery, currentStep, historical);
        }
        catch (ProductionFormatException error) when (error.Failure == ProductionFormatFailure.LimitExceeded)
        {
            return settlement.Reject(SpotExchangeRejection.InsufficientCapacity);
        }
        catch (OverflowException)
        {
            return settlement.Reject(SpotExchangeRejection.Overflow);
        }
    }

    private static void RequireScope(CountedAuthorityState inventory, ProductionSnapshot production, ProgressionSnapshot progression, SpotQuoteBook book)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(production);
        if (production.WorldId != inventory.WorldId || production.BranchId != inventory.BranchId
            || progression.WorldId != inventory.WorldId || progression.BranchId != inventory.BranchId
            || book.WorldId != inventory.WorldId || book.BranchId != inventory.BranchId)
        {
            throw new ArgumentException("Every exchange owner must belong to the same world and branch.");
        }
    }

    // The issuer's recorded delivery and the counterparty's delivery must each equal current control.
    private static bool IsApproved(CountedAuthorityState inventory, ProgressionSnapshot progression, SpotQuoteEntry entry,
        SpotAcceptance acceptance, TrustedCommandDelivery counterpartyDelivery)
    {
        SpotQuote quote = entry.Quote;
        return acceptance.Binds(quote)
            && IsCurrentController(inventory, entry.IssuerDelivery, quote.IssuerPrincipalId, quote.IssuerSessionId,
                quote.IssuerActorId)
            && IsCurrentController(inventory, counterpartyDelivery, acceptance.PrincipalId, acceptance.OriginSessionId,
                quote.CounterpartyActorId)
            && OwnsSettlementEnds(inventory, progression, quote);
    }

    private static bool IsCurrentController(CountedAuthorityState inventory, TrustedCommandDelivery delivery,
        EntityId claimedPrincipalId, EntityId claimedSessionId, EntityId actorId) =>
        claimedPrincipalId == delivery.PrincipalId && claimedSessionId == delivery.SessionId
        && inventory.CanControl(delivery, actorId);

    // Each party owns its own storage and account; deposit rules are neither consulted nor changed.
    private static bool OwnsSettlementEnds(CountedAuthorityState inventory, ProgressionSnapshot progression, SpotQuote quote)
    {
        bool OwnsStorage(EntityId actor, EntityId custodian) =>
            inventory.Policies.Any(policy => policy.CustodianId == custodian && policy.OwnerActorId == actor);
        bool OwnsAccount(EntityId actor, EntityId account) => progression.Currency.Accounts
            .Any(value => value.AccountId == account && value.ActorId == actor);
        return OwnsStorage(quote.SellerActorId, quote.SellerCustodianId) && OwnsStorage(quote.BuyerActorId, quote.BuyerCustodianId)
            && OwnsAccount(quote.SellerActorId, quote.SellerAccountId) && OwnsAccount(quote.BuyerActorId, quote.BuyerAccountId);
    }

    private static bool IsOccupied(CountedAuthorityState inventory, ProgressionSnapshot progression, SpotQuoteBook book,
        Func<EntityId, bool> historical, EntityId identity) => inventory.ContainsIdentity(identity)
        || progression.ContainsIdentity(identity) || book.ContainsIdentity(identity) || historical(identity);

    /// <summary>Builds the one complete candidate from the exact decided owners.</summary>
    private sealed class Settlement(CountedAuthorityState inventory, ProductionSnapshot production,
        ProgressionSnapshot progression, SpotQuoteBook book)
    {
        internal SpotExchangePlan Reject(SpotExchangeRejection reason) => new(inventory, production, progression, book,
            inventory, production, progression, book, SpotExchangeOutcome.Rejected, reason, null);

        internal SpotExchangePlan Duplicate(SpotFill fill) => new(inventory, production, progression, book,
            inventory, production, progression, book, SpotExchangeOutcome.Duplicate, SpotExchangeRejection.None, fill);

        internal SpotExchangePlan Move(SpotQuoteEntry entry, SpotAcceptance acceptance,
            TrustedCommandDelivery delivery, ulong step, Func<EntityId, bool> historical)
        {
            if (production.IsContinued && production.Continuation?.SupportsSpotExchange != true)
            {
                return Reject(SpotExchangeRejection.UnsupportedHistory);
            }
            if (production.MaterialHistory?.Operations.Count == MaterialOperationHistory.MaximumOperations)
            {
                return Reject(SpotExchangeRejection.InsufficientCapacity);
            }
            var movement = new SpotMaterialTransferStep(inventory, production.Lots,
                id => IsChildOccupied(id, entry, acceptance, historical)).Apply(entry.Quote, acceptance);
            if (movement.Rejection != SpotExchangeRejection.None || movement.MovedLotId is not { } movedId)
            {
                return Reject(movement.Rejection);
            }
            return Settle(entry, acceptance, delivery, step, movement.Inventory, movement.ProducedLots, movedId);
        }

        private SpotExchangePlan Settle(SpotQuoteEntry entry, SpotAcceptance acceptance,
            TrustedCommandDelivery delivery, ulong step, CountedAuthorityState movedInventory,
            IReadOnlyCollection<TransformedLot> movedLots, EntityId movedLotId)
        {
            SpotQuote quote = entry.Quote;
            List<(EntityId, ulong)> payments = [(quote.SellerAccountId, acceptance.Payment)];
            payments.AddRange(quote.Fees.Select(fee => (fee.PayeeAccountId, checked(fee.UnitAmount * acceptance.Quantity))));
            CurrencyLedger paid = progression.Currency.Transfer(quote.BuyerAccountId, payments);
            var fill = new SpotFill(checked(book.Revision + 1), acceptance.AcceptanceId, acceptance.ContentHash,
                acceptance.Quantity, movedLotId, acceptance.Payment, acceptance.Fees);
            SpotQuoteBook filled = book.WithFill(new SpotQuoteEntry(quote, entry.IssuerDelivery, fill));
            ProgressionSnapshot paidProgression = progression.WithCurrency(paid);
            SpotSettlementEvidence evidence = new(inventory, production, progression, book, entry,
                acceptance, delivery, step, movedInventory, paidProgression, filled, movedLotId);
            MaterialOperationHistory? history = production.MaterialHistory;
            ulong revision = production.Revision;
            if (history is not null)
            {
                revision = checked(revision + 1);
                history = history.Append(production.Continuation!, new MaterialSpotExchangeOperation(revision, evidence));
            }
            var movedProduction = new ProductionSnapshot(production.WorldId, production.BranchId, revision,
                production.Recipes, production.Batches, movedLots,
                production.SupportsConsumption ? production.ConsumedItems : null, production.Continuation, history);
            movedProduction.ValidateForAuthority(movedInventory);
            _ = movedProduction.ContentHash;
            return new(inventory, production, progression, book, movedInventory, movedProduction,
                paidProgression, filled, SpotExchangeOutcome.Applied, SpotExchangeRejection.None, fill, evidence);
        }

        private bool IsChildOccupied(EntityId childId, SpotQuoteEntry entry, SpotAcceptance acceptance,
            Func<EntityId, bool> historical) => childId == entry.Quote.QuoteId || childId == acceptance.AcceptanceId
            || inventory.ContainsIdentity(childId) || production.ContainsIdentity(childId)
            || progression.ContainsIdentity(childId) || book.ContainsIdentity(childId) || historical(childId);
    }
}
