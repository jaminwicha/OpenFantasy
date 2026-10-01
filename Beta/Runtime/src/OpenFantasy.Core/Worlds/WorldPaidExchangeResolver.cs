using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>One decided paid submission: every candidate owner, or the untouched originals and a refusal.</summary>
internal sealed record WorldPaidExchangePlan(CountedAuthorityState Inventory, ProductionSnapshot Production,
    ProgressionSnapshot Progression, WorldPaidExchangeBook Book, WorldActionRejection Rejection,
    IReadOnlyList<EntityId> CreatedIds, WorldCustodyWitness? Witness, EntityId? CounterpartActorId);

/// <summary>
/// Decides one paid spot-exchange submission against the world's actual owners. All three submissions
/// share the ordinary command contract: current control, the caller's own presence revision, an advancing
/// unheld step with a ready actor, and only then the quote. Settlement rechecks the issuer's recorded control,
/// both opportunities, presence, living bodies, reach, fixed terms, the market revision, funds, stock and
/// capacity, and then applies the shared spot goods and payment arithmetic. A fill supersedes the issuer's
/// other open quotes in the same owner. It uses neither the standalone ordered-material capability nor any
/// gift approval; production revisions keep their craft meaning.
/// </summary>
internal static class WorldPaidExchangeResolver
{
    internal static WorldPaidExchangePlan Resolve(WorldSnapshot state, WorldStepBatch batch,
        CountedAuthorityState inventory, LocalSpatialSnapshot space, ProductionSnapshot production,
        ProgressionSnapshot progression, WorldPaidExchangeBook book, bool held, WorldActionCommand action,
        uint actionIndex, ulong sequence, IReadOnlySet<EntityId> worked, Func<EntityId, bool> occupied)
    {
        WorldPaidExchangePlan Refuse(WorldActionRejection reason) =>
            new(inventory, production, progression, book, reason, [], null, null);

        var delivery = new TrustedCommandDelivery(action.PrincipalId, action.OriginSessionId);
        if (action.WorldId != state.WorldId || action.BranchId != state.BranchId
            || !inventory.CanControl(delivery, action.ActorId))
        {
            return Refuse(WorldActionRejection.NotAuthorized);
        }
        ActorPresence? presence = space.Actors.FirstOrDefault(value => value.ActorId == action.ActorId);
        if (presence is null)
        {
            return Refuse(WorldActionRejection.Unavailable);
        }
        if (presence.Revision != action.ExpectedActorRevision)
        {
            return Refuse(WorldActionRejection.RevisionConflict);
        }
        if (batch.Kind != WorldBatchKind.AdvancingStep || held || !WorldActorAvailability.PermitsAction(state, action))
        {
            return Refuse(WorldActionRejection.Unavailable);
        }
        return action.Intent switch
        {
            IssueSpotQuoteIntent issue => Issue(state, inventory, production, progression, book, action, issue.Quote,
                sequence, occupied, Refuse),
            AcceptSpotQuoteIntent accept => Accept(state, batch, inventory, space, production, progression, book,
                action, accept, actionIndex, sequence, worked, occupied, Refuse),
            WithdrawSpotQuoteIntent withdraw => Withdraw(state, inventory, production, progression, book, action,
                withdraw.QuoteId, delivery, actionIndex, sequence, Refuse),
            _ => throw new ArgumentException("Only paid exchange submissions are decided here.", nameof(action))
        };
    }

    /// <summary>
    /// The issuer closes its own open quote. Current control, presence and readiness were already checked
    /// for the submitting actor. A missing, closed, due or foreign quote gives one refusal, so a caller
    /// learns nothing about quotes it did not issue. Withdrawal moves nothing and performs no work.
    /// </summary>
    private static WorldPaidExchangePlan Withdraw(WorldSnapshot state, CountedAuthorityState inventory,
        ProductionSnapshot production, ProgressionSnapshot progression, WorldPaidExchangeBook book,
        WorldActionCommand action, EntityId quoteId, TrustedCommandDelivery delivery, uint actionIndex, ulong sequence,
        Func<WorldActionRejection, WorldPaidExchangePlan> refuse)
    {
        WorldPaidQuote? open = book.FindOpen(quoteId);
        if (open is null || open.Quote.IssuerActorId != action.ActorId || open.IsDue(state.AdvancingSteps) || !book.CanClose)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        var withdrawn = new WorldPaidQuoteOutcome(open, sequence, new WorldPaidWithdrawal(action.CommandId, delivery, actionIndex));
        return new(inventory, production, progression, book.Withdraw(withdrawn), WorldActionRejection.None, [], null, null);
    }

    private static WorldPaidExchangePlan Issue(WorldSnapshot state, CountedAuthorityState inventory,
        ProductionSnapshot production, ProgressionSnapshot progression, WorldPaidExchangeBook book,
        WorldActionCommand action, SpotQuote quote, ulong sequence, Func<EntityId, bool> occupied,
        Func<WorldActionRejection, WorldPaidExchangePlan> refuse)
    {
        if (quote.WorldId != state.WorldId || quote.BranchId != state.BranchId || quote.IssuerActorId != action.ActorId
            || quote.IssuerPrincipalId != action.PrincipalId || quote.IssuerSessionId != action.OriginSessionId
            || quote.QuoteId != WorldPaidQuote.DeriveIdentity(action.CommandId)
            || !OwnsSettlementEnds(inventory, progression, quote))
        {
            return refuse(WorldActionRejection.NotAuthorized);
        }
        ulong issuedStep = checked(state.AdvancingSteps + 1);
        if (quote.IssuedAtStep != issuedStep || quote.ExpiresAtStep <= issuedStep
            || quote.ExpiresAtStep - issuedStep > WorldPaidQuote.MaximumLifetimeSteps
            || !book.CanIssue || occupied(quote.QuoteId) || quote.LotId == action.CommandId
            || state.Residents.All(value => value.ActorId != quote.CounterpartyActorId))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        // Fixed terms name the issuer's current market revision, currency pin and actual payees.
        if (quote.MarketRevision != book.GetIssuerRevision(quote.IssuerActorId) || !MatchesCurrency(progression, quote))
        {
            return refuse(WorldActionRejection.RevisionConflict);
        }
        // The pin must describe actual current stock able to supply the whole allowance; it reserves nothing.
        WorldActionRejection pin = CheckPinnedLot(inventory, production, quote);
        if (pin != WorldActionRejection.None)
        {
            return refuse(pin);
        }
        var issued = new WorldPaidQuote(action.CommandId, quote, sequence);
        return new(inventory, production, progression, book.Issue(issued), WorldActionRejection.None,
            [quote.QuoteId], null, null);
    }

    private static WorldPaidExchangePlan Accept(WorldSnapshot state, WorldStepBatch batch,
        CountedAuthorityState inventory, LocalSpatialSnapshot space, ProductionSnapshot production,
        ProgressionSnapshot progression, WorldPaidExchangeBook book, WorldActionCommand action,
        AcceptSpotQuoteIntent accept, uint actionIndex, ulong sequence, IReadOnlySet<EntityId> worked,
        Func<EntityId, bool> occupied, Func<WorldActionRejection, WorldPaidExchangePlan> refuse)
    {
        SpotAcceptance acceptance = accept.Acceptance;
        if (acceptance.AcceptanceId != action.CommandId || acceptance.PrincipalId != action.PrincipalId
            || acceptance.OriginSessionId != action.OriginSessionId)
        {
            return refuse(WorldActionRejection.NotAuthorized);
        }
        WorldPaidQuote? open = book.FindOpen(acceptance.QuoteId);
        // The window must hold the fill and every supersession it causes; the cut reservation ensures it.
        if (open is null || open.Quote.CounterpartyActorId != action.ActorId || open.IsDue(state.AdvancingSteps)
            || !book.CanFill(open))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        SpotQuote quote = open.Quote;
        if (!acceptance.Binds(quote))
        {
            // The counterparty approved something other than the fixed quote; nothing is requoted.
            return refuse(WorldActionRejection.RevisionConflict);
        }
        // The issuer submits no command of its own in the settling step. Its recorded delivery stays valid
        // only while it is the issuer's current control binding.
        EntityId issuerId = quote.IssuerActorId;
        if (!inventory.CanControl(open.IssuerDelivery, issuerId)
            || batch.Actions.Any(other => other.ActorId == issuerId)
            || worked.Contains(issuerId) || worked.Contains(action.ActorId)
            || !WorldActorAvailability.CanAct(state, issuerId))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        ActorPresence? issuer = space.Actors.FirstOrDefault(value => value.ActorId == issuerId);
        ActorPresence? counterparty = space.Actors.FirstOrDefault(value => value.ActorId == action.ActorId);
        if (issuer is null || counterparty is null)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        if (issuer.Revision != accept.ExpectedIssuerRevision)
        {
            return refuse(WorldActionRejection.RevisionConflict);
        }
        if (!issuer.Condition.SupportsFunction(BodyFunction.Manipulation)
            || !counterparty.Condition.SupportsFunction(BodyFunction.Manipulation)
            || progression.GetActor(issuerId).CurrentHp <= 0 || progression.GetActor(action.ActorId).CurrentHp <= 0)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        if (space.QueryInteraction(issuerId, action.ActorId) != SpatialInteraction.Reachable
            || space.QueryInteraction(action.ActorId, issuerId) != SpatialInteraction.Reachable)
        {
            return refuse(WorldActionRejection.OutOfReach);
        }
        if (!OwnsSettlementEnds(inventory, progression, quote))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        if (quote.MarketRevision != book.GetIssuerRevision(issuerId) || !MatchesCurrency(progression, quote))
        {
            return refuse(WorldActionRejection.RevisionConflict);
        }
        try
        {
            ulong owed = checked(acceptance.Payment + acceptance.Fees);
            if (progression.Currency.GetAccount(quote.BuyerAccountId).Balance < owed)
            {
                return refuse(WorldActionRejection.Unavailable);
            }
            // Only this quote's creation and the current acceptance are exempt from occupancy.
            bool MaterialOccupied(EntityId identity) => identity != action.CommandId && occupied(identity);
            SpotMaterialOutcome movement = new SpotMaterialTransferStep(inventory, production.Lots, MaterialOccupied)
                .Apply(quote, acceptance);
            if (movement.Rejection != SpotExchangeRejection.None || movement.MovedLotId is not { } movedLotId)
            {
                return refuse(movement.Rejection is SpotExchangeRejection.RevisionConflict
                    ? WorldActionRejection.RevisionConflict : WorldActionRejection.Unavailable);
            }
            // Custody movement is neither a craft nor a use, so the production revision keeps its meaning.
            var candidate = new ProductionSnapshot(production.WorldId, production.BranchId, production.Revision,
                production.Recipes, production.Batches, movement.ProducedLots,
                production.SupportsConsumption ? production.ConsumedItems : null, production.Continuation);
            candidate.ValidateForAuthority(movement.Inventory);
            // Payment redistributes existing balances; every touched account advances once.
            ProgressionSnapshot paid = progression.WithCurrency(
                progression.Currency.Transfer(quote.BuyerAccountId, quote.PaymentsFor(acceptance.Quantity)));
            WorldPaidSettlement settlement = Witness(actionIndex, acceptance, quote, movedLotId, movement.Inventory, candidate);
            var filled = new WorldPaidQuoteOutcome(open, WorldPaidQuoteOutcomeKind.Filled, sequence, settlement);
            var witness = new WorldCustodyWitness(quote.LotKind == SpotLotKind.Produced, inventory.ContentHash,
                production.ContentHash, movement.Inventory.ContentHash, candidate.ContentHash);
            return new(movement.Inventory, candidate, paid, book.Fill(filled), WorldActionRejection.None,
                movedLotId != quote.LotId ? [movedLotId] : [], witness, issuerId);
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
    }

    private static WorldActionRejection CheckPinnedLot(CountedAuthorityState inventory, ProductionSnapshot production,
        SpotQuote quote)
    {
        if (quote.LotKind == SpotLotKind.Produced)
        {
            TransformedLot? lot = production.Lots.FirstOrDefault(value => value.LotId == quote.LotId);
            if (lot is null || lot.CustodianId != quote.SellerCustodianId || !lot.Definition.Equals(quote.Definition)
                || lot.Quantity < quote.QuantityAllowance)
            {
                return WorldActionRejection.Unavailable;
            }
            return lot.Revision == quote.ExpectedLotRevision ? WorldActionRejection.None : WorldActionRejection.RevisionConflict;
        }
        CountedSourceLot? raw = inventory.Custody.Lots.FirstOrDefault(value => value.LotId == quote.LotId);
        if (raw is null || raw.CustodianId != quote.SellerCustodianId || !raw.Definition.Equals(quote.Definition)
            || raw.Quantity < quote.QuantityAllowance)
        {
            return WorldActionRejection.Unavailable;
        }
        return raw.Revision == quote.ExpectedLotRevision ? WorldActionRejection.None : WorldActionRejection.RevisionConflict;
    }

    private static bool MatchesCurrency(ProgressionSnapshot progression, SpotQuote quote)
    {
        CurrencyLedger ledger = progression.Currency;
        return ledger.Currency.DefinitionId == quote.CurrencyDefinitionId && ledger.Currency.Version == quote.CurrencyVersion
            && quote.Fees.All(fee => ledger.Accounts.Any(account => account.AccountId == fee.PayeeAccountId));
    }

    // Each party owns its own storage and account; deposit rules are neither consulted nor changed.
    internal static bool OwnsSettlementEnds(CountedAuthorityState inventory, ProgressionSnapshot progression, SpotQuote quote)
    {
        bool OwnsStorage(EntityId actor, EntityId custodian) =>
            inventory.Policies.Any(policy => policy.CustodianId == custodian && policy.OwnerActorId == actor);
        bool OwnsAccount(EntityId actor, EntityId account) =>
            progression.Currency.Accounts.Any(value => value.AccountId == account && value.ActorId == actor);
        return OwnsStorage(quote.SellerActorId, quote.SellerCustodianId) && OwnsStorage(quote.BuyerActorId, quote.BuyerCustodianId)
            && OwnsAccount(quote.SellerActorId, quote.SellerAccountId) && OwnsAccount(quote.BuyerActorId, quote.BuyerAccountId);
    }

    private static WorldPaidSettlement Witness(uint actionIndex, SpotAcceptance acceptance, SpotQuote quote,
        EntityId movedLotId, CountedAuthorityState candidateInventory, ProductionSnapshot candidateProduction)
    {
        if (quote.LotKind == SpotLotKind.Produced)
        {
            ProductionOutputInterval moved = candidateProduction.Lots.Single(value => value.LotId == movedLotId).Output;
            return new(actionIndex, acceptance, movedLotId, moved.BatchId, moved.OutputIndex, moved.Start, moved.End);
        }
        SourceInterval source = candidateInventory.Custody.GetLot(movedLotId).Source;
        return new(actionIndex, acceptance, movedLotId, source.SourceId, 0, source.Start, source.End);
    }
}
