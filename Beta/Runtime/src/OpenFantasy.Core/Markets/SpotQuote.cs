using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Markets;

/// <summary>Which named participant delivers the goods; the other pays.</summary>
public enum SpotQuoteSide
{
    IssuerSells = 1,
    IssuerBuys = 2
}

/// <summary>The actual owner of the quoted lot: raw counted custody or produced stock.</summary>
public enum SpotLotKind
{
    CountedSource = 1,
    Produced = 2
}

/// <summary>A named whole-unit charge per traded unit, paid by the buyer to one existing account.</summary>
public sealed record SpotFee
{
    public SpotFee(EntityId payeeAccountId, ulong unitAmount)
    {
        ArgumentOutOfRangeException.ThrowIfZero(unitAmount);
        PayeeAccountId = payeeAccountId;
        UnitAmount = unitAmount;
    }

    public EntityId PayeeAccountId { get; }
    public ulong UnitAmount { get; }
}

/// <summary>
/// Fixed spot terms one issuer offers one named counterparty: scope, the exact lot and definition pin,
/// both storages, the currency pin and both accounts, whole-unit price and fees, a finite allowance,
/// the issuer's market revision and a visible fixed-world step window. The issuer's principal and
/// session are submitted claims compared with a recorded host delivery; terms prove nothing, reserve
/// nothing and move nothing.
/// </summary>
public sealed class SpotQuote
{
    public const int MaximumFees = 4;
    public const ulong MaximumQuantity = 1_000_000;

    public SpotQuote(EntityId quoteId, EntityId worldId, EntityId branchId, EntityId issuerActorId,
        EntityId issuerPrincipalId, EntityId issuerSessionId, EntityId counterpartyActorId, SpotQuoteSide side,
        SpotLotKind lotKind, EntityId lotId, CountedItemDefinition definition, ulong expectedLotRevision,
        EntityId sellerCustodianId, EntityId buyerCustodianId, EntityId currencyDefinitionId,
        ContentHash currencyVersion, EntityId sellerAccountId, EntityId buyerAccountId, ulong unitPrice,
        IEnumerable<SpotFee> fees, ulong quantityAllowance, ulong marketRevision, ulong issuedAtStep,
        ulong expiresAtStep)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(currencyVersion);
        ArgumentNullException.ThrowIfNull(fees);
        if (!Enum.IsDefined(side) || !Enum.IsDefined(lotKind))
        {
            throw new ArgumentOutOfRangeException(nameof(side), "A quote names one known side and lot owner.");
        }

        if (issuerActorId == counterpartyActorId)
        {
            throw new ArgumentException("A quote names two different participants.", nameof(counterpartyActorId));
        }

        // Distinct revision owners keep the shared movement request constructible for every valid quote.
        if (sellerCustodianId == buyerCustodianId || lotId == sellerCustodianId || lotId == buyerCustodianId)
        {
            throw new ArgumentException("The lot and both storages must be three different identities.",
                nameof(buyerCustodianId));
        }

        if (sellerAccountId == buyerAccountId)
        {
            throw new ArgumentException("Payment moves between two different accounts.", nameof(buyerAccountId));
        }

        if (unitPrice == 0 || quantityAllowance == 0 || quantityAllowance > MaximumQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityAllowance),
                "A quote requires a positive price and a bounded positive allowance.");
        }

        if (expiresAtStep < issuedAtStep)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtStep), "A quote cannot expire before issuance.");
        }

        List<SpotFee> copied = [];
        foreach (SpotFee fee in fees)
        {
            ArgumentNullException.ThrowIfNull(fee);
            if (copied.Count == MaximumFees || copied.Any(value => value.PayeeAccountId == fee.PayeeAccountId)
                || fee.PayeeAccountId == buyerAccountId)
            {
                throw new ArgumentException("Fees name bounded distinct payees other than the paying buyer.", nameof(fees));
            }

            copied.Add(fee);
        }

        try
        {
            ulong unitFees = 0;
            foreach (SpotFee fee in copied)
            {
                unitFees = checked(unitFees + fee.UnitAmount);
            }

            // Every acceptable quantity must have an exact representable total.
            _ = checked(checked(unitPrice + unitFees) * quantityAllowance);
            UnitFees = unitFees;
        }
        catch (OverflowException)
        {
            throw new ArgumentException("Quote arithmetic must be exactly representable for its whole allowance.",
                nameof(unitPrice));
        }

        QuoteId = quoteId;
        WorldId = worldId;
        BranchId = branchId;
        IssuerActorId = issuerActorId;
        IssuerPrincipalId = issuerPrincipalId;
        IssuerSessionId = issuerSessionId;
        CounterpartyActorId = counterpartyActorId;
        Side = side;
        LotKind = lotKind;
        LotId = lotId;
        Definition = definition;
        ExpectedLotRevision = expectedLotRevision;
        SellerCustodianId = sellerCustodianId;
        BuyerCustodianId = buyerCustodianId;
        CurrencyDefinitionId = currencyDefinitionId;
        CurrencyVersion = currencyVersion;
        SellerAccountId = sellerAccountId;
        BuyerAccountId = buyerAccountId;
        UnitPrice = unitPrice;
        Fees = copied.OrderBy(value => value.PayeeAccountId).ToList().AsReadOnly();
        QuantityAllowance = quantityAllowance;
        MarketRevision = marketRevision;
        IssuedAtStep = issuedAtStep;
        ExpiresAtStep = expiresAtStep;
    }

    public EntityId QuoteId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId IssuerActorId { get; }
    public EntityId IssuerPrincipalId { get; }
    public EntityId IssuerSessionId { get; }
    public EntityId CounterpartyActorId { get; }
    public SpotQuoteSide Side { get; }
    public SpotLotKind LotKind { get; }
    public EntityId LotId { get; }
    public CountedItemDefinition Definition { get; }

    /// <summary>The stock revision the quote was priced against; any spending, split or movement advances it.</summary>
    public ulong ExpectedLotRevision { get; }
    public EntityId SellerCustodianId { get; }
    public EntityId BuyerCustodianId { get; }
    public EntityId CurrencyDefinitionId { get; }
    public ContentHash CurrencyVersion { get; }
    public EntityId SellerAccountId { get; }
    public EntityId BuyerAccountId { get; }

    /// <summary>Whole currency units paid to the seller for each traded unit.</summary>
    public ulong UnitPrice { get; }
    public ReadOnlyCollection<SpotFee> Fees { get; }
    public ulong UnitFees { get; }
    public ulong QuantityAllowance { get; }

    /// <summary>The issuer's quote-book revision; each settled fill of that issuer advances it.</summary>
    public ulong MarketRevision { get; }

    /// <summary>Fixed-world advancing steps; the quote is acceptable in [IssuedAtStep, ExpiresAtStep].</summary>
    public ulong IssuedAtStep { get; }
    public ulong ExpiresAtStep { get; }
    public EntityId SellerActorId => Side == SpotQuoteSide.IssuerSells ? IssuerActorId : CounterpartyActorId;
    public EntityId BuyerActorId => Side == SpotQuoteSide.IssuerSells ? CounterpartyActorId : IssuerActorId;
    public ContentHash ContentHash => ContentHash.Compute(SpotExchangeCodecV1.Encode(this));

    public ulong PaymentFor(ulong quantity) => checked(UnitPrice * quantity);
    public ulong FeesFor(ulong quantity) => checked(UnitFees * quantity);

    /// <summary>
    /// The buyer's exact credits for one fill: the seller's payment and each named fee. This is the shared
    /// payment arithmetic of every settlement and replay; the ledger aggregates credits per account.
    /// </summary>
    internal IReadOnlyList<(EntityId PayeeAccountId, ulong Amount)> PaymentsFor(ulong quantity)
    {
        List<(EntityId, ulong)> payments = [(SellerAccountId, PaymentFor(quantity))];
        payments.AddRange(Fees.Select(fee => (fee.PayeeAccountId, checked(fee.UnitAmount * quantity))));
        return payments;
    }
}
