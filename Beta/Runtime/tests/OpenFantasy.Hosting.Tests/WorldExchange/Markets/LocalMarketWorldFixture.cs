using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Markets;

/// <summary>
/// A fresh profile-33 world composed from the unplayed paid-trade genesis: Wren keeps her admitted berry
/// stock and empty wallet and runs one berry stall; the third human keeps an empty pack and wallet and runs a
/// second stall. Both rules cite the legacy berry price. The admission mints no stock or money: purchases are
/// funded only by the player's earned quest reward and the merchants' own later sales, and the third human's
/// supply arrives only through real saved gifts. Tests reach SQLite only through the authority.
/// </summary>
internal sealed class LocalMarketWorldFixture
{
    private const ulong Scope = 0x4D4B_7E70_0000_0000;
    private ulong _identity = 0x100;

    internal LocalMarketWorldFixture(Func<ulong, MarketPricePolicy> wrenRule, Func<ulong, MarketPricePolicy>? traderRule = null)
        : this((stock, _) => wrenRule(stock), traderRule)
    {
    }

    /// <param name="wrenRule">Wren's rule from her genesis stock and the third human's own account, a possible due payee.</param>
    internal LocalMarketWorldFixture(Func<ulong, EntityId, MarketPricePolicy> wrenRule, Func<ulong, MarketPricePolicy>? traderRule = null)
    {
        Paid = new PaidTradeWorldFixture();
        WorldSnapshot paid = Paid.Genesis;
        Berries = paid.Inventory.Custody.GetLot(Paid.OrdinaryLot).Definition;
        WrenGenesisStock = UsableUnits(paid, paid.GetResident(Paid.Helper).InventoryCustodianId, Berries);
        Assert.Equal(0UL, UsableUnits(paid, paid.GetResident(Paid.Merchant).InventoryCustodianId, Berries));
        WrenMarket = MarketCircuitPolicies.BindResident(paid, new EntityId(Scope, 1), Paid.Helper, Berries,
            wrenRule(WrenGenesisStock, paid.Progression!.Currency.GetActorAccount(Paid.Merchant).AccountId));
        TraderMarket = MarketCircuitPolicies.BindResident(paid, new EntityId(Scope, 2), Paid.Merchant, Berries,
            traderRule is null ? Gentle(0) : traderRule(0));
        Genesis = WorldMarketAdmission.CreateFresh(paid, [TraderMarket, WrenMarket]);
        Assert.Equal(WorldMarketAdmission.ProfileVersion, Genesis.ProfileVersion);
        // The admission changes no owner but the explicit market interpretation and its admission version.
        Assert.Equal(paid.Inventory.ContentHash, Genesis.Inventory.ContentHash);
        Assert.Equal(paid.Progression!.ContentHash, Genesis.Progression!.ContentHash);
        Assert.Equal(Genesis.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(Genesis)).ContentHash);
        Journey = new CareRewardJourney(false, Genesis);
    }

    internal PaidTradeWorldFixture Paid { get; }
    internal WorldSnapshot Genesis { get; }
    internal CareRewardJourney Journey { get; }
    internal CountedItemDefinition Berries { get; }
    internal ulong WrenGenesisStock { get; }
    internal MarketBinding WrenMarket { get; }
    internal MarketBinding TraderMarket { get; }
    internal EntityId Player => Paid.Player;
    internal EntityId Wren => Paid.Helper;
    internal EntityId Trader => Paid.Merchant;

    /// <summary>A gentle stall around the given stock: target two above it, demand up to eight above it.</summary>
    internal static MarketPricePolicy Gentle(ulong stock) => Gentle(stock, 64);

    internal static MarketPricePolicy Gentle(ulong stock, ulong lifetime) =>
        new(MarketCircuitPolicies.Berries, stockingTarget: stock + 2, holdingLimit: stock + 8, softening: 4,
            minimumUnitPrice: 1, maximumUnitPrice: 40, askMarkup: 1, bidMarkdown: 1, unitFee: 0, feePayeeAccountId: null,
            maximumLot: 4, quoteLifetimeSteps: lifetime);

    /// <summary>
    /// The reviewed coalition case: a constant mid price of five, one unit of markup and markdown and a
    /// three-unit due paid to the third human, who is neither the merchant nor the cycling trader.
    /// </summary>
    internal static MarketPricePolicy ConstantWithDue(ulong stock, EntityId payee) =>
        new(MarketCircuitPolicies.Berries, stockingTarget: stock + 2, holdingLimit: stock + 8, softening: 4,
            minimumUnitPrice: 5, maximumUnitPrice: 5, askMarkup: 1, bidMarkdown: 1, unitFee: 3, feePayeeAccountId: payee,
            maximumLot: 4, quoteLifetimeSteps: 64);

    /// <summary>A scarcity-sensitive stall whose single-unit ask changes with every unit near its stock.</summary>
    internal static MarketPricePolicy Steep(ulong stock) =>
        new(MarketCircuitPolicies.Berries, stockingTarget: 8 * stock + 8, holdingLimit: 8 * stock + 8, softening: 1,
            minimumUnitPrice: 1, maximumUnitPrice: 2_000, askMarkup: 1, bidMarkdown: 1, unitFee: 0,
            feePayeeAccountId: null, maximumLot: 4, quoteLifetimeSteps: 64);

    internal SqliteWorldAuthority Create(StorageFiles files) => SqliteWorldAuthority.CreateNew(files.Path, Genesis);

    internal SqliteWorldAuthority Reopen(StorageFiles files) =>
        SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(Genesis));

    internal static TrustedCommandDelivery DeliveryOf(WorldSnapshot state, EntityId actor)
    {
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
    }

    internal static TrustedCommandDelivery Delivery(WorldActionCommand command) => new(command.PrincipalId, command.OriginSessionId);

    internal static ulong UsableUnits(WorldSnapshot state, EntityId custodian, CountedItemDefinition definition) =>
        state.Inventory.Custody.Lots.Where(value => value.CustodianId == custodian && value.Definition == definition
                && value.ReservedBy is null && value.Protection == LotProtection.None)
            .Aggregate(0UL, (total, value) => checked(total + value.Quantity))
        + state.Production!.Lots.Where(value => value.CustodianId == custodian && value.Definition == definition
                && value.ReservedBy is null && value.Protection == LotProtection.None)
            .Aggregate(0UL, (total, value) => checked(total + value.Quantity));

    /// <summary>The player's real quest: approach, accept, deliver and close; the reserve pays the reward.</summary>
    internal WorldSnapshot EarnReward(SqliteWorldAuthority authority)
    {
        var session = new CareWorldSession(authority);
        Journey.Approach(session);
        WorldActionCommand reward = Journey.PrepareReward(session);
        WorldSnapshot state = session.ReadStateForHost();
        state = CareRewardJourney.Commit(session, Journey.Batch(state, WorldBatchKind.HeldInteraction, [reward])).Candidate;
        state = CareRewardJourney.Commit(session, Journey.Batch(state, WorldBatchKind.HeldInteraction,
            [Journey.Command(state, Player, new CloseDialogueIntent(state.Dialogues.Single().DialogueId))])).Candidate;
        Assert.True(state.Progression!.Currency.GetActorAccount(Player).Balance > 0, "Only the earned reward funds the player.");
        return state;
    }

    /// <summary>
    /// A real saved gift: the sender proposes an exact interval of its own usable lot and the recipient
    /// accepts it in a later step. Returns the recipient's moved lot identity.
    /// </summary>
    internal EntityId Gift(SqliteWorldAuthority authority, EntityId sender, EntityId recipient, EntityId lotId, ulong quantity)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        CountedSourceLot lot = state.Inventory.Custody.GetLot(lotId);
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, sender, recipient,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(recipient).InventoryCustodianId, quantity, lot.Revision);
        WorldActionCommand propose = Command(state, sender, new ProposeMaterialIntent(terms, state.AdvancingSteps + 17));
        Assert.Equal(WorldActionRejection.None,
            authority.SubmitMaterialProposalCommand(propose, Delivery(propose), NextId()).Rejection);
        state = authority.ReadStateForHost();
        WorldMaterialProposal proposal = state.Coordination!.Proposals!.Open.Single(value => value.CreationCommandId == propose.CommandId);
        WorldActionCommand accept = Command(state, recipient,
            AcceptMaterialProposalIntent.Approving(proposal, state.Space.GetActor(sender).Revision));
        Assert.Equal(WorldActionRejection.None,
            authority.SubmitMaterialProposalCommand(accept, Delivery(accept), NextId()).Rejection);
        state = authority.ReadStateForHost();
        EntityId moved = quantity == lot.Quantity ? lot.LotId : proposal.SplitChildId;
        Assert.Equal(state.GetResident(recipient).InventoryCustodianId, state.Inventory.Custody.GetLot(moved).CustodianId);
        return moved;
    }

    /// <summary>
    /// Walks one actor through actual advancing steps along the real ground route toward a goal until the
    /// condition holds. Every step must move the actor; nothing is teleported.
    /// </summary>
    internal WorldSnapshot Walk(SqliteWorldAuthority authority, EntityId actor, SubunitPosition goal, Func<WorldSnapshot, bool> done)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        for (int step = 0; !done(state); step++)
        {
            Assert.True(step < 256, "The walk must finish along the bounded real ground route.");
            var route = state.Space.QueryGroundRoute(actor, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            var feet = state.Space.GetActor(actor).Feet;
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            authority.Commit(new WorldStepBatch(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new WorldMovementInput(DeliveryOf(state, actor), actor, dx, dy)], [],
                state.ProfileVersion));
            state = authority.ReadStateForHost();
            Assert.Equal(feet.Offset(dx, dy, 0), state.Space.GetActor(actor).Feet);
        }
        return state;
    }

    /// <summary>
    /// After Wren's approach the player stands enclosed: the two admitted wolves one tile to either side, Wren one
    /// tile behind and the third human one tile ahead, with gaps narrower than a body. The third human therefore
    /// steps one tile aside along the exit its admission already proves reachable, through real advancing
    /// steps, which opens the tiles ahead of the player. Nobody is teleported and no reach rule changes.
    /// </summary>
    internal WorldSnapshot StepTraderAside(SqliteWorldAuthority authority)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        SubunitPosition exit = state.Space.GetActor(Trader).Feet.Offset(256, 0, 0);
        Assert.NotEqual(GroundRouteStatus.Reachable, state.Space.QueryGroundRoute(Player, DistantGoal(state, Player)).Status);
        return Walk(authority, Trader, exit, value => value.Space.GetActor(Trader).Feet == exit);
    }

    /// <summary>A goal four tiles ahead of the actor along the cleared lane; the walk itself proves the route.</summary>
    internal static SubunitPosition DistantGoal(WorldSnapshot state, EntityId actor, long sideways = 0) =>
        state.Space.GetActor(actor).Feet.Offset(sideways, 1024, 0);

    internal WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent)
    {
        TrustedCommandDelivery delivery = DeliveryOf(state, actor);
        return new WorldActionCommand(NextId(), delivery.PrincipalId, delivery.SessionId, actor, state.WorldId,
            state.BranchId, state.Space.GetActor(actor).Revision, intent);
    }

    /// <summary>The counterparty's exact acceptance of a quote, restating its terms and the issuer it saw.</summary>
    internal WorldActionCommand Accept(WorldSnapshot state, EntityId counterparty, SpotQuote quote, ulong quantity)
    {
        TrustedCommandDelivery delivery = DeliveryOf(state, counterparty);
        EntityId commandId = NextId();
        var acceptance = new SpotAcceptance(commandId, delivery.PrincipalId, delivery.SessionId, quote.QuoteId,
            quote.ContentHash, quantity, quote.PaymentFor(quantity), quote.FeesFor(quantity));
        return new WorldActionCommand(commandId, delivery.PrincipalId, delivery.SessionId, counterparty, state.WorldId,
            state.BranchId, state.Space.GetActor(counterparty).Revision,
            new AcceptSpotQuoteIntent(acceptance, state.Space.GetActor(quote.IssuerActorId).Revision));
    }

    internal EntityId NextId()
    {
        var id = new EntityId(Scope | 0x0001_0000_0000, ++_identity);
        Assert.False(Genesis.IdentityIndex.Contains(id));
        return id;
    }
}
