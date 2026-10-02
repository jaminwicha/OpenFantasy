using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Simulation.Retreat;
using CargoPresentation = OpenFantasy.Hosting.Simulation.CargoPresentation;
using EncounterOutcome = OpenFantasy.Hosting.Simulation.EncounterOutcome;
using Retreat = OpenFantasy.Hosting.Simulation.Retreat;
using RoadThreat = OpenFantasy.Hosting.Simulation.RoadThreat;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

internal sealed record MaterialProposalViewData(
    ReadOnlyCollection<MaterialProposalView> Offers,
    LocalMaterialProposalPreparation? Preparation)
{
    internal LocalMaterialProposalView ToView() => new(Offers, Preparation);

    internal static MaterialProposalViewData From(LocalMaterialProposalView value) => new(
        value.Offers,
        value.Preparation);
}

internal sealed record MarketStallViewData(
    EntityId MarketId,
    EntityId MerchantActorId,
    CountedItemDefinition Commodity,
    string CommodityName,
    ulong MaximumUnitsPerOffer,
    ulong MaximumWindowSteps,
    bool IsAvailableInPerson,
    LocalMarketShowingView? OwnShowing,
    ReadOnlyCollection<LocalMarketLotView> OwnLots,
    EntityId CapturedPlayerAccountId,
    ulong CapturedAdvancingStep)
{
    internal LocalMarketStallView ToView() => new(MarketId, MerchantActorId, Commodity, CommodityName, MaximumUnitsPerOffer, MaximumWindowSteps, IsAvailableInPerson, OwnShowing, OwnLots, CapturedPlayerAccountId, CapturedAdvancingStep);

    internal static MarketStallViewData From(LocalMarketStallView value) => new(
        value.MarketId,
        value.MerchantActorId,
        value.Commodity,
        value.CommodityName,
        value.MaximumUnitsPerOffer,
        value.MaximumWindowSteps,
        value.IsAvailableInPerson,
        value.OwnShowing,
        value.OwnLots,
        value.CapturedPlayerAccountId,
        value.CapturedAdvancingStep);
}

internal sealed record PaidQuoteViewData(
    EntityId QuoteId,
    EntityId? MarketId,
    EntityId IssuerActorId,
    EntityId CounterpartyActorId,
    bool ViewerIsIssuer,
    SpotQuoteSide Side,
    CountedItemDefinition Definition,
    SpotLotKind LotKind,
    EntityId LotId,
    ulong ExpectedLotRevision,
    EntityId SellerCustodianId,
    EntityId BuyerCustodianId,
    EntityId CurrencyDefinitionId,
    ContentHash CurrencyVersion,
    EntityId SellerAccountId,
    EntityId BuyerAccountId,
    ulong UnitPrice,
    ReadOnlyCollection<SpotFee> Fees,
    ulong QuantityAllowance,
    ulong IssuedAtStep,
    ulong ExpiresAtStep,
    ContentHash QuoteHash,
    PaidQuoteStatus Status,
    ulong? ClosedAtSequence,
    ulong? FilledQuantity,
    ulong? FilledPayment,
    ulong? FilledFees,
    ulong? ObservedIssuerRevision)
{
    internal PaidQuoteView ToView() => new(QuoteId, MarketId, IssuerActorId, CounterpartyActorId, ViewerIsIssuer, Side, Definition, LotKind, LotId, ExpectedLotRevision, SellerCustodianId, BuyerCustodianId, CurrencyDefinitionId, CurrencyVersion, SellerAccountId, BuyerAccountId, UnitPrice, Fees, QuantityAllowance, IssuedAtStep, ExpiresAtStep, QuoteHash, Status, ClosedAtSequence, FilledQuantity, FilledPayment, FilledFees, ObservedIssuerRevision);

    internal static PaidQuoteViewData From(PaidQuoteView value) => new(
        value.QuoteId,
        value.MarketId,
        value.IssuerActorId,
        value.CounterpartyActorId,
        value.ViewerIsIssuer,
        value.Side,
        value.Definition,
        value.LotKind,
        value.LotId,
        value.ExpectedLotRevision,
        value.SellerCustodianId,
        value.BuyerCustodianId,
        value.CurrencyDefinitionId,
        value.CurrencyVersion,
        value.SellerAccountId,
        value.BuyerAccountId,
        value.UnitPrice,
        value.Fees,
        value.QuantityAllowance,
        value.IssuedAtStep,
        value.ExpiresAtStep,
        value.QuoteHash,
        value.Status,
        value.ClosedAtSequence,
        value.FilledQuantity,
        value.FilledPayment,
        value.FilledFees,
        value.ObservedIssuerRevision);
}

internal sealed record MarketViewData(
    ulong AdvancingStep,
    ReadOnlyCollection<LocalMarketStallView> Stalls)
{
    internal LocalMarketView ToView() => new(AdvancingStep, Stalls);

    internal static MarketViewData From(LocalMarketView value) => new(
        value.AdvancingStep,
        value.Stalls);
}
