using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The four terminal causes of a world quote. A fill is the counterparty's paid acceptance; supersession
/// closes the issuer's other open quotes when that fill advances its market revision; withdrawal is the
/// issuer's own command; expiry is the fixed-world time boundary.
/// </summary>
public enum WorldPaidQuoteOutcomeKind { Filled = 1, Expired = 2, Superseded = 3, Withdrawn = 4 }

/// <summary>
/// One issued spot quote in a complete world: the issuer's fixed terms, submitted under the identity of
/// its own creation command, and the world revision that accepted the issuance. The issuer's recorded
/// delivery is exactly the principal and session its terms claim; it is compared with current control at
/// settlement and never authenticates a caller. A quote owns no stock or money and reserves neither.
/// </summary>
public sealed class WorldPaidQuote
{
    public const int MaximumByteLength = SpotExchangeCodecV1.MaximumQuoteByteLength + 256;

    /// <summary>Development envelope: a quote outlives at most this many advancing steps.</summary>
    public const ulong MaximumLifetimeSteps = 65_536;

    // Distinct from the split child ordinal, so one creation command names two separate identities.
    private const ulong IdentityOrdinal = 0x5351_0001;

    public WorldPaidQuote(EntityId creationCommandId, SpotQuote quote, ulong createdSequence)
    {
        ArgumentNullException.ThrowIfNull(quote);
        if (createdSequence == 0 || quote.IssuedAtStep == 0 || quote.QuoteId != DeriveIdentity(creationCommandId)
            || quote.ExpiresAtStep <= quote.IssuedAtStep || quote.ExpiresAtStep - quote.IssuedAtStep > MaximumLifetimeSteps
            || creationCommandId == quote.LotId)
        {
            throw new ArgumentException("A world quote requires its creation identity and a bounded later expiry step.");
        }
        CreationCommandId = creationCommandId;
        Quote = quote;
        CreatedSequence = createdSequence;
        ContentHash = ContentHash.Compute(Encode());
    }

    public static EntityId DeriveIdentity(EntityId creationCommandId) =>
        EntityId.DeriveSpawned(creationCommandId, IdentityOrdinal);

    public EntityId QuoteId => Quote.QuoteId;
    public EntityId CreationCommandId { get; }
    public SpotQuote Quote { get; }

    /// <summary>The issuer's delivery recorded by the accepted issuance.</summary>
    public TrustedCommandDelivery IssuerDelivery => new(Quote.IssuerPrincipalId, Quote.IssuerSessionId);

    /// <summary>The world revision published by the step that accepted the issuance.</summary>
    public ulong CreatedSequence { get; }

    /// <summary>The advancing-step count published by the issuing step.</summary>
    public ulong CreatedAdvancingStep => Quote.IssuedAtStep;

    /// <summary>The last advancing-step count whose step may still settle the quote.</summary>
    public ulong ExpiresAtAdvancingStep => Quote.ExpiresAtStep;

    /// <summary>The pinned identity of a partial amount's child lot, spawned from an acceptance command.</summary>
    public static EntityId SplitChildOf(EntityId acceptanceId) =>
        EntityId.DeriveSpawned(acceptanceId, CustodyTransferPlanner.SplitChildOrdinal);

    public ContentHash ContentHash { get; }

    public bool IsDue(ulong advancingSteps) => advancingSteps >= ExpiresAtAdvancingStep;

    public bool HasParticipant(EntityId actorId) => Quote.IssuerActorId == actorId || Quote.CounterpartyActorId == actorId;

    public byte[] Encode() => CanonicalEncodingV1.Frame("world.paid-quote.v1", SpotExchangeCodecV1.Encode(Quote),
        CreationCommandId.ToByteArray(), CanonicalEncodingV1.Unsigned64(CreatedSequence));
}

/// <summary>
/// Evidence of the one paid movement a filled quote executed: the counterparty's own acceptance, its
/// command position and the exact moved rights. Balances stay with progression and stock with its owners.
/// </summary>
public sealed class WorldPaidSettlement
{
    public const int MaximumByteLength = SpotExchangeCodecV1.MaximumAcceptanceByteLength + 256;

    public WorldPaidSettlement(uint actionIndex, SpotAcceptance acceptance, EntityId movedLotId, EntityId rightsOwnerId,
        uint outputIndex, ulong movedStart, ulong movedEnd)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        if (actionIndex >= WorldStepBatch.MaximumActorsPerBatch || movedEnd <= movedStart
            || movedEnd - movedStart != acceptance.Quantity || movedLotId == acceptance.AcceptanceId
            || movedLotId == acceptance.QuoteId)
        {
            throw new ArgumentException("A paid settlement requires its command position and the exact moved interval.");
        }
        ActionIndex = actionIndex;
        Acceptance = acceptance;
        MovedLotId = movedLotId;
        RightsOwnerId = rightsOwnerId;
        OutputIndex = outputIndex;
        MovedStart = movedStart;
        MovedEnd = movedEnd;
    }

    /// <summary>The acceptance's position in its world batch; it orders currency causes within one revision.</summary>
    public uint ActionIndex { get; }
    public SpotAcceptance Acceptance { get; }
    public TrustedCommandDelivery CounterpartyDelivery => new(Acceptance.PrincipalId, Acceptance.OriginSessionId);
    public EntityId MovedLotId { get; }

    /// <summary>The producing batch of produced stock, or the admitted source of raw stock.</summary>
    public EntityId RightsOwnerId { get; }
    public uint OutputIndex { get; }
    public ulong MovedStart { get; }
    public ulong MovedEnd { get; }

    public byte[] Encode() => CanonicalEncodingV1.Frame("world.paid-settlement.v1",
        CanonicalEncodingV1.Unsigned64(ActionIndex), SpotExchangeCodecV1.Encode(Acceptance), MovedLotId.ToByteArray(),
        RightsOwnerId.ToByteArray(), CanonicalEncodingV1.Unsigned64(OutputIndex),
        CanonicalEncodingV1.Unsigned64(MovedStart), CanonicalEncodingV1.Unsigned64(MovedEnd));
}

/// <summary>
/// The cause of one supersession: the fill, by the same issuer in the same step, that advanced the
/// issuer's market revision. It names that fill's acceptance command and quote; it moves no money or goods.
/// </summary>
public sealed class WorldPaidSupersession
{
    public const int MaximumByteLength = 256;

    public WorldPaidSupersession(EntityId acceptanceId, EntityId filledQuoteId)
    {
        if (acceptanceId == filledQuoteId)
        {
            throw new ArgumentException("A supersession names a distinct acceptance and filled quote.");
        }
        AcceptanceId = acceptanceId;
        FilledQuoteId = filledQuoteId;
    }

    /// <summary>The acceptance command of the superseding fill.</summary>
    public EntityId AcceptanceId { get; }

    /// <summary>The issuer's quote that fill closed.</summary>
    public EntityId FilledQuoteId { get; }

    public byte[] Encode() => CanonicalEncodingV1.Frame("world.paid-supersession.v1", AcceptanceId.ToByteArray(),
        FilledQuoteId.ToByteArray());
}

/// <summary>
/// Evidence of the issuer's own withdrawal command: its identity, the delivery that submitted it and its
/// position in its world batch. The delivery is history; it never authenticates a later caller.
/// </summary>
public sealed class WorldPaidWithdrawal
{
    public const int MaximumByteLength = 256;

    public WorldPaidWithdrawal(EntityId commandId, TrustedCommandDelivery delivery, uint actionIndex)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (actionIndex >= WorldStepBatch.MaximumActorsPerBatch)
        {
            throw new ArgumentException("A withdrawal requires its command position inside one world batch.");
        }
        CommandId = commandId;
        Delivery = delivery;
        ActionIndex = actionIndex;
    }

    public EntityId CommandId { get; }
    public TrustedCommandDelivery Delivery { get; }

    /// <summary>The withdrawal's position in its world batch; it orders outcomes within one revision.</summary>
    public uint ActionIndex { get; }

    public byte[] Encode() => CanonicalEncodingV1.Frame("world.paid-withdrawal.v1", CommandId.ToByteArray(),
        Delivery.PrincipalId.ToByteArray(), Delivery.SessionId.ToByteArray(), CanonicalEncodingV1.Unsigned64(ActionIndex));
}

/// <summary>
/// The single terminal outcome of one world quote. It retains the complete quote and its typed terminal
/// cause, so the closing archive holds the whole lifecycle; command receipts stay separate immutable facts.
/// Only a fill carries settlement evidence, only a supersession names a fill and only a withdrawal names
/// its command; an expiry has no command cause.
/// </summary>
public sealed class WorldPaidQuoteOutcome
{
    public const int MaximumByteLength = WorldPaidQuote.MaximumByteLength + WorldPaidSettlement.MaximumByteLength + 256;

    /// <summary>A fill with its settlement evidence, or an expiry without any.</summary>
    public WorldPaidQuoteOutcome(WorldPaidQuote quote, WorldPaidQuoteOutcomeKind kind, ulong worldSequence,
        WorldPaidSettlement? settlement) : this(quote, kind, worldSequence, settlement, null, null)
    {
    }

    /// <summary>A sibling quote closed, without any movement, by its issuer's fill in the same step.</summary>
    public WorldPaidQuoteOutcome(WorldPaidQuote quote, ulong worldSequence, WorldPaidSupersession supersession)
        : this(quote, WorldPaidQuoteOutcomeKind.Superseded, worldSequence, null,
            supersession ?? throw new ArgumentNullException(nameof(supersession)), null)
    {
    }

    /// <summary>A quote closed by its issuer's own withdrawal command.</summary>
    public WorldPaidQuoteOutcome(WorldPaidQuote quote, ulong worldSequence, WorldPaidWithdrawal withdrawal)
        : this(quote, WorldPaidQuoteOutcomeKind.Withdrawn, worldSequence, null, null,
            withdrawal ?? throw new ArgumentNullException(nameof(withdrawal)))
    {
    }

    private WorldPaidQuoteOutcome(WorldPaidQuote quote, WorldPaidQuoteOutcomeKind kind, ulong worldSequence,
        WorldPaidSettlement? settlement, WorldPaidSupersession? supersession, WorldPaidWithdrawal? withdrawal)
    {
        ArgumentNullException.ThrowIfNull(quote);
        if (!Enum.IsDefined(kind) || worldSequence <= quote.CreatedSequence
            || (kind == WorldPaidQuoteOutcomeKind.Filled) != (settlement is not null)
            || (kind == WorldPaidQuoteOutcomeKind.Superseded) != (supersession is not null)
            || (kind == WorldPaidQuoteOutcomeKind.Withdrawn) != (withdrawal is not null))
        {
            throw new ArgumentException("A quote outcome follows its issuance and carries exactly its own terminal cause.");
        }
        if (settlement is not null && (!settlement.Acceptance.Binds(quote.Quote)
            // A whole lot keeps its identity; a partial prefix moves as the acceptance's own split child.
            || settlement.MovedLotId != quote.Quote.LotId && settlement.MovedLotId != WorldPaidQuote.SplitChildOf(settlement.Acceptance.AcceptanceId)
            || quote.Quote.LotKind == SpotLotKind.CountedSource && settlement.OutputIndex != 0
            || settlement.Acceptance.AcceptanceId == quote.CreationCommandId))
        {
            throw new ArgumentException("A fill must bind its quote's exact terms, quantity and moved identity.");
        }
        if (supersession is not null && (supersession.FilledQuoteId == quote.QuoteId
            || supersession.AcceptanceId == quote.QuoteId || supersession.AcceptanceId == quote.CreationCommandId))
        {
            throw new ArgumentException("A supersession names another quote's fill, never the closed quote itself.");
        }
        if (withdrawal is not null && (withdrawal.CommandId == quote.QuoteId || withdrawal.CommandId == quote.CreationCommandId))
        {
            throw new ArgumentException("A withdrawal is a later command with its own identity.");
        }
        Quote = quote;
        Kind = kind;
        WorldSequence = worldSequence;
        Settlement = settlement;
        Supersession = supersession;
        Withdrawal = withdrawal;
        ContentHash = ContentHash.Compute(Encode());
    }

    public WorldPaidQuote Quote { get; }
    public WorldPaidQuoteOutcomeKind Kind { get; }

    /// <summary>The world revision that published this outcome.</summary>
    public ulong WorldSequence { get; }
    public WorldPaidSettlement? Settlement { get; }
    public WorldPaidSupersession? Supersession { get; }
    public WorldPaidWithdrawal? Withdrawal { get; }
    public ContentHash ContentHash { get; }

    /// <summary>The command whose own submission closed this quote: an acceptance or a withdrawal.</summary>
    public EntityId? CommandId => Settlement?.Acceptance.AcceptanceId ?? Withdrawal?.CommandId;

    /// <summary>The batch position of that command; superseded and expired outcomes have none of their own.</summary>
    public uint? CommandOrdinal => Settlement?.ActionIndex ?? Withdrawal?.ActionIndex;

    /// <summary>The produced-stock movement production replays; absent for raw stock, which custody owns.</summary>
    public ProducedCustodyMovement? ToProducedMovement()
    {
        if (Settlement is not { } settled || Quote.Quote.LotKind != SpotLotKind.Produced)
        {
            return null;
        }
        SpotQuote terms = Quote.Quote;
        return new ProducedCustodyMovement(terms.LotId, terms.ExpectedLotRevision, settled.MovedLotId,
            terms.SellerCustodianId, terms.BuyerCustodianId, settled.Acceptance.Quantity,
            new ProductionOutputInterval(settled.RightsOwnerId, settled.OutputIndex, settled.MovedStart, settled.MovedEnd),
            terms.Definition);
    }

    // Version 2 adds the typed supersession and withdrawal causes; the cause field is empty only for expiry.
    public byte[] Encode() => CanonicalEncodingV1.Frame("world.paid-quote-outcome.v2", Quote.Encode(),
        CanonicalEncodingV1.Unsigned64((ulong)Kind), CanonicalEncodingV1.Unsigned64(WorldSequence),
        Settlement?.Encode() ?? Supersession?.Encode() ?? Withdrawal?.Encode() ?? []);
}
