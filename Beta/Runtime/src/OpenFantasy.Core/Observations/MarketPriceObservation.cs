using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Observations;

/// <summary>
/// One actor's dated memory of a local market's posted terms. The world records it only from an admitted
/// observation command whose observer could actually reach the trading merchant at that revision. It pins
/// the market, its merchant, the commodity and rule version and the exact quoted single-unit terms, but
/// never the stock count or a balance. Reading it later neither refreshes it nor reveals later changes.
/// It is not a resource-quantity observation and grants no trading right; quotes stay separate acts.
/// </summary>
public sealed class MarketPriceObservation
{
    public const int MaximumByteLength = 512;

    public MarketPriceObservation(EntityId observerActorId, EntityId marketId, EntityId merchantActorId,
        CountedItemDefinition commodity, ContentHash policyVersion, ulong? askUnitPrice, ulong? bidUnitPrice,
        ulong unitFee, ulong observedSequence, ulong observedAdvancingStep, EntityId causeCommandId, uint causeActionIndex)
    {
        ArgumentNullException.ThrowIfNull(commodity);
        ArgumentNullException.ThrowIfNull(policyVersion);
        if (observerActorId == merchantActorId || observedSequence == 0 || observedAdvancingStep == 0
            || observedAdvancingStep > observedSequence || askUnitPrice == 0 || bidUnitPrice == 0
            || askUnitPrice is { } ask && bidUnitPrice is { } bid && bid >= ask)
        {
            throw new ArgumentException("A price observation records another actor's dated market with an ordered spread.");
        }
        ObserverActorId = observerActorId;
        MarketId = marketId;
        MerchantActorId = merchantActorId;
        Commodity = commodity;
        PolicyVersion = policyVersion;
        AskUnitPrice = askUnitPrice;
        BidUnitPrice = bidUnitPrice;
        UnitFee = unitFee;
        ObservedSequence = observedSequence;
        ObservedAdvancingStep = observedAdvancingStep;
        CauseCommandId = causeCommandId;
        CauseActionIndex = causeActionIndex;
        ContentHash = ContentHash.Compute(Encode());
    }

    public EntityId ObserverActorId { get; }
    public EntityId MarketId { get; }

    /// <summary>The source of the evidence: the merchant whose posted terms were read in person.</summary>
    public EntityId MerchantActorId { get; }
    public CountedItemDefinition Commodity { get; }
    public ContentHash PolicyVersion { get; }
    public ulong? AskUnitPrice { get; }
    public ulong? BidUnitPrice { get; }
    public ulong UnitFee { get; }

    /// <summary>The world revision that published the observation.</summary>
    public ulong ObservedSequence { get; }

    /// <summary>The fixed-world advancing-step count published by that revision.</summary>
    public ulong ObservedAdvancingStep { get; }

    /// <summary>The observer's own admitted command that produced this evidence.</summary>
    public EntityId CauseCommandId { get; }
    public uint CauseActionIndex { get; }
    public ContentHash ContentHash { get; }

    /// <summary>Whole advancing steps since the observation; it ages without ever refreshing itself.</summary>
    public ulong AgeAt(ulong advancingSteps) => advancingSteps > ObservedAdvancingStep ? advancingSteps - ObservedAdvancingStep : 0;

    public byte[] Encode() => CanonicalEncodingV1.Frame("market.price-observation.v1", ObserverActorId.ToByteArray(),
        MarketId.ToByteArray(), MerchantActorId.ToByteArray(), Commodity.DefinitionId.ToByteArray(),
        Commodity.Version.ToByteArray(), PolicyVersion.ToByteArray(), Optional(AskUnitPrice), Optional(BidUnitPrice),
        CanonicalEncodingV1.Unsigned64(UnitFee), CanonicalEncodingV1.Unsigned64(ObservedSequence),
        CanonicalEncodingV1.Unsigned64(ObservedAdvancingStep), CauseCommandId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(CauseActionIndex));

    // An absent side is an empty field; a present price is positive, so the two encodings never collide.
    private static byte[] Optional(ulong? value) =>
        value is { } present ? CanonicalEncodingV1.Unsigned64(present) : Array.Empty<byte>();
}
