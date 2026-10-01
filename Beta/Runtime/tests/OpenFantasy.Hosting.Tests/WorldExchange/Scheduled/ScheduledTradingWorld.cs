using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldExchange.Markets;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

/// <summary>
/// The accepted profile-33 market fixture driven through an actual local session. Wren keeps her real berry
/// stock and her funded construction commitment; the third human runs the second, initially empty stall; the
/// player is funded only by its earned quest reward. Setup before a session uses only the fixture's genuine
/// reward, gift, trade and walking routes; afterwards every trading effect is selected at a session cutoff.
/// </summary>
internal sealed class ScheduledTradingWorld : IDisposable
{
    private readonly StorageFiles _files = new();

    internal ScheduledTradingWorld(Func<ulong, MarketPricePolicy>? traderRule = null)
    {
        Fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.Gentle, traderRule);
        Authority = Fixture.Create(_files);
        Fixture.EarnReward(Authority);
    }

    internal LocalMarketWorldFixture Fixture { get; }
    internal SqliteWorldAuthority Authority { get; private set; }
    internal MarketTradingService Service => new(Authority);
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal EntityId Player => Fixture.Player;
    internal EntityId Wren => Fixture.Wren;
    internal EntityId Trader => Fixture.Trader;
    internal CountedItemDefinition Berries => Fixture.Berries;
    internal ConstructionProject Project => State.Construction!.Projects.Single();
    internal ulong Progress => Project.Progress.CompletedUnits;

    internal TrustedCommandDelivery DeliveryOf(EntityId actor) => LocalMarketWorldFixture.DeliveryOf(State, actor);

    internal ulong Balance(EntityId actor) => State.Progression!.Currency.GetActorAccount(actor).Balance;

    internal ulong Stock(MarketBinding market) => market.CountUsableStock(State.Inventory, State.Production!.Lots);

    internal static LocalTradingConfiguration Merchants(params EntityId[] merchants) =>
        new(merchants.Select(value => new LocalMerchantPreference(value)));

    internal LocalWorldSession Open(ManualSessionTimeProvider time, LocalTradingConfiguration? trading) =>
        new(Authority, Player, DeliveryOf(Player), time, null, trading);

    internal void Reopen()
    {
        Authority.Dispose();
        Authority = Fixture.Reopen(_files);
    }

    /// <summary>Wren follows her own construction controller's actual route to her worksite.</summary>
    internal void WalkWrenToWork()
    {
        var controller = new LocalNpcController();
        for (int step = 0; State.Space.GetActor(Wren).Feet != Project.WorkFeet; step++)
        {
            Assert.True(step < 256, "The funded builder must reach her worksite through bounded movement.");
            Move(Assert.Single(controller.SelectMovement(State), value => value.ActorId == Wren));
        }
    }

    /// <summary>
    /// Walks one actor along the real ground route to a free tile beside Wren's worksite. With
    /// <paramref name="awayFrom"/> it takes the reachable side farthest from that point, so a builder still walking
    /// in from there finds her own approach open.
    /// </summary>
    internal void WalkBesideWork(EntityId actor, SubunitPosition? awayFrom = null)
    {
        WorldSnapshot state = State;
        SubunitPosition work = Project.WorkFeet;
        SubunitPosition destination = new[]
        {
            work.Offset(-256, 0, 0), work.Offset(256, 0, 0), work.Offset(0, -256, 0), work.Offset(0, 256, 0)
        }.Where(feet => state.Space.Actors.All(value => value.Feet != feet)
            && state.Space.QueryGroundRoute(actor, feet).Status == GroundRouteStatus.Reachable)
            .OrderByDescending(feet => awayFrom is { } origin
                ? Math.Abs((double)feet.X - origin.X) + Math.Abs((double)feet.Y - origin.Y) : 0).First();
        Fixture.Walk(Authority, actor, destination, value => value.Space.GetActor(actor).Feet == destination);
    }

    /// <summary>
    /// A genuine pre-session sale at the third human's own stall: its controller prepares the rule-priced offer of
    /// its own lot and the player accepts it through the paid authority, which funds the stall.
    /// </summary>
    internal SpotQuote BuyFromTraderDirectly(EntityId lotId, ulong units)
    {
        TrustedCommandDelivery trader = DeliveryOf(Trader);
        WorldActionCommand offer = Service.PrepareOffer(trader, Fixture.TraderMarket.MarketId, Player, SpotQuoteSide.IssuerSells,
            lotId, units, 32, Fixture.NextId())!;
        Assert.Equal(WorldActionRejection.None, Service.Submit(offer, trader, Fixture.NextId()).Rejection);
        SpotQuote quote = ((IssueMarketQuoteIntent)offer.Intent).Quote;
        WorldActionCommand buy = Fixture.Accept(State, Player, quote, units);
        Assert.Equal(WorldActionRejection.None,
            Authority.SubmitPaidExchangeCommand(buy, LocalMarketWorldFixture.Delivery(buy), Fixture.NextId()).Rejection);
        return quote;
    }

    /// <summary>The sender's own saved offer of an exact interval of its usable lot, created under its control; nothing moves yet.</summary>
    internal WorldMaterialProposal Propose(EntityId sender, EntityId recipient, EntityId lotId, ulong quantity, ulong lifetime = 64)
    {
        WorldSnapshot state = State;
        CountedSourceLot lot = state.Inventory.Custody.GetLot(lotId);
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, sender, recipient,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(recipient).InventoryCustodianId, quantity, lot.Revision);
        WorldActionCommand propose = Fixture.Command(state, sender, new ProposeMaterialIntent(terms, state.AdvancingSteps + lifetime));
        Assert.Equal(WorldActionRejection.None,
            Authority.SubmitMaterialProposalCommand(propose, LocalMarketWorldFixture.Delivery(propose), Fixture.NextId()).Rejection);
        return State.Coordination!.Proposals!.Open.Single(value => value.CreationCommandId == propose.CommandId);
    }

    /// <summary>The player's request to speak with another actor, which opens that actor's dialogue and a reading hold.</summary>
    internal WorldActionCommand Speak(EntityId target) =>
        PlayerCommand(new SpeakToActorIntent(target, State.Space.GetActor(target).Revision), Fixture.NextId());

    /// <summary>
    /// A genuine pre-session trade: Wren's own controller prepares her rule-priced offer of one unit and the
    /// player accepts it through the paid authority. Returns the quote and the player's moved lot.
    /// </summary>
    internal (SpotQuote Quote, EntityId Lot) BuyOneFromWrenDirectly() => BuyFromWrenDirectly(1);

    internal (SpotQuote Quote, EntityId Lot) BuyFromWrenDirectly(ulong units)
    {
        TrustedCommandDelivery wren = DeliveryOf(Wren);
        WorldActionCommand offer = Service.PrepareOffer(wren, Fixture.WrenMarket.MarketId, Player, SpotQuoteSide.IssuerSells,
            Fixture.Paid.OrdinaryLot, units, 32, Fixture.NextId())!;
        Assert.Equal(WorldActionRejection.None, Service.Submit(offer, wren, Fixture.NextId()).Rejection);
        SpotQuote quote = ((IssueMarketQuoteIntent)offer.Intent).Quote;
        WorldActionCommand buy = Fixture.Accept(State, Player, quote, units);
        Assert.Equal(WorldActionRejection.None,
            Authority.SubmitPaidExchangeCommand(buy, LocalMarketWorldFixture.Delivery(buy), Fixture.NextId()).Rejection);
        return (quote, OwnedLotFrom(quote.LotId, buy.CommandId, Player));
    }

    /// <summary>The lot a fill moved into the actor's pack: the whole pinned lot or the acceptance's split child.</summary>
    internal EntityId OwnedLotFrom(EntityId pinnedLotId, EntityId acceptanceId, EntityId owner)
    {
        EntityId bag = State.GetResident(owner).InventoryCustodianId;
        EntityId moved = State.Inventory.Custody.Lots.Any(value => value.LotId == pinnedLotId && value.CustodianId == bag)
            ? pinnedLotId : WorldPaidQuote.SplitChildOf(acceptanceId);
        Assert.Equal(bag, State.Inventory.Custody.GetLot(moved).CustodianId);
        return moved;
    }

    internal WorldActionCommand PlayerCommand(WorldActionIntent intent, EntityId commandId)
    {
        TrustedCommandDelivery player = DeliveryOf(Player);
        return new WorldActionCommand(commandId, player.PrincipalId, player.SessionId, Player, State.WorldId,
            State.BranchId, State.Space.GetActor(Player).Revision, intent);
    }

    /// <summary>The player's own exact acceptance prepared from an inspected participant view.</summary>
    internal WorldActionCommand PlayerAcceptance(PaidQuoteView view, ulong quantity)
    {
        EntityId commandId = Fixture.NextId();
        return PlayerCommand(view.PrepareAcceptance(commandId, DeliveryOf(Player), quantity), commandId);
    }

    internal static async Task<WorldActionReceipt> Submit(LocalWorldSession session, ManualSessionTimeProvider time,
        WorldActionCommand command)
    {
        Task<WorldActionReceipt> pending = session.SubmitAction(command);
        await SessionFixture.Tick(time);
        await SessionFixture.Until(() => pending.IsCompleted);
        return await pending;
    }

    internal static async Task<LocalTradeInquiryView> Ask(LocalWorldSession session, ManualSessionTimeProvider time,
        LocalTradeInquiry inquiry)
    {
        Assert.Equal(LocalTradeInquiryState.Waiting, (await session.SubmitTradeInquiryAsync(inquiry)).State);
        await SessionFixture.Tick(time);
        return session.ReadTradeInquiry()!;
    }

    /// <summary>The batch a committed step actually recorded, read from a disposable backup copy.</summary>
    internal WorldStepBatch RecordedBatch(ulong sequence) => RecordedBatches(sequence, sequence).Single();

    /// <summary>Every batch committed in an inclusive range of world revisions, read once from a disposable backup copy.</summary>
    internal IReadOnlyList<WorldStepBatch> RecordedBatches(ulong first, ulong last)
    {
        using var copy = new StorageFiles();
        Authority.BackupForHost(copy.Path);
        using var database = WorldDatabase.Open(copy.Path, create: false);
        return database.ReadJournal().Where(value => value.Sequence >= first && value.Sequence <= last)
            .OrderBy(value => value.Sequence).Select(value => WorldStepCodec.Decode(value.AcceptedBatch)).ToList();
    }

    /// <summary>
    /// Real archive pressure: Wren's ordinary one-step bilateral quotes to the player expire, one per pair of
    /// steps, until the host automatically selects a coordinated cut.
    /// </summary>
    internal void CutArchive()
    {
        for (int index = 0; Authority.InspectForHost().Journal.MaterialCareArchiveCount == 0; index++)
        {
            Assert.True(index < WorldPaidExchangeBook.MaximumOutcomes + 8, "The bounded outcome window must cut automatically.");
            WorldSnapshot state = State;
            CountedSourceLot lot = state.Inventory.Custody.Lots.First(value => value.CustodianId == state.GetResident(Wren).InventoryCustodianId
                && value.Definition == Berries && value.ReservedBy is null && value.Protection == LotProtection.None);
            CurrencyLedger ledger = state.Progression!.Currency;
            TrustedCommandDelivery wren = DeliveryOf(Wren);
            EntityId commandId = Fixture.NextId();
            ulong issuedAt = state.AdvancingSteps + 1;
            var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(commandId), state.WorldId, state.BranchId, Wren,
                wren.PrincipalId, wren.SessionId, Player, SpotQuoteSide.IssuerSells, SpotLotKind.CountedSource, lot.LotId,
                lot.Definition, lot.Revision, lot.CustodianId, state.GetResident(Player).InventoryCustodianId,
                ledger.Currency.DefinitionId, ledger.Currency.Version, ledger.GetActorAccount(Wren).AccountId,
                ledger.GetActorAccount(Player).AccountId, 1, [], 1,
                state.Coordination!.PaidExchange!.GetIssuerRevision(Wren), issuedAt, issuedAt + 1);
            var issue = new WorldActionCommand(commandId, wren.PrincipalId, wren.SessionId, Wren, state.WorldId,
                state.BranchId, state.Space.GetActor(Wren).Revision, new IssueSpotQuoteIntent(quote));
            Assert.Equal(WorldActionRejection.None, Authority.SubmitPaidExchangeCommand(issue, wren, Fixture.NextId()).Rejection);
            state = State;
            Authority.Commit(new WorldStepBatch(Fixture.NextId(), state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion));
        }
    }

    /// <summary>No public member of a participant view equals another actor's principal or session credential.</summary>
    internal static void AssertCarriesNoDelivery(PaidQuoteView view, params TrustedCommandDelivery[] foreign)
    {
        object?[] values = typeof(PaidQuoteView).GetProperties().Select(property => property.GetValue(view)).ToArray();
        foreach (TrustedCommandDelivery delivery in foreign)
        {
            Assert.DoesNotContain(delivery.PrincipalId, values);
            Assert.DoesNotContain(delivery.SessionId, values);
        }
        Assert.DoesNotContain(typeof(PaidQuoteView).GetProperties(), property => property.Name.Contains("Principal")
            || property.Name.Contains("Session") || property.Name.Contains("Stock") || property.Name.Contains("Delivery"));
    }

    private void Move(WorldMovementInput movement)
    {
        WorldSnapshot state = State;
        SubunitPosition original = state.Space.GetActor(movement.ActorId).Feet;
        WorldSnapshot after = Authority.Commit(new WorldStepBatch(Fixture.NextId(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [movement], [], state.ProfileVersion)).Candidate;
        Assert.Equal(original.Offset(movement.DeltaX, movement.DeltaY, 0), after.Space.GetActor(movement.ActorId).Feet);
    }

    public void Dispose()
    {
        Authority.Dispose();
        _files.Dispose();
    }
}
