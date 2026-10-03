using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.Worlds.ControllerPermissions;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.CoPlayerTrade;

/// <summary>Original fresh admission, real travel/quest funding, and journaled consent and takeover.</summary>
internal sealed class CoPlayerTradeFixture
{
    internal EffectiveControlFixture Control { get; } = new();
    internal CoPlayerTradeFixture(bool ownerMarket = false)
    {
        Genesis = ownerMarket ? WithFreshOwnerMarket() : Control.Genesis;
    }
    internal WorldSnapshot Genesis { get; }
    internal MarketBinding? OwnerMarket { get; private set; }

    private WorldSnapshot WithFreshOwnerMarket()
    {
        var basis = new PermissionStorageFixture().Genesis;
        var recipe = basis.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var berries = recipe.ItemFor(Assert.Single(recipe.Inputs));
        var policy = new MarketPricePolicy(MarketCircuitPolicies.Berries, 8, 16, 4, 5, 5, 1, 1, 0, null, 4, 64);
        OwnerMarket = MarketCircuitPolicies.BindResident(basis, new(0xc070ade000000000, 1), Owner, berries, policy);
        var markets = new WorldMarketBook(basis.Coordination!.Markets!.Markets.Append(OwnerMarket).OrderBy(value => value.MarketId), [], []);
        var admission = ContentHash.Compute(basis.ContentHash.ToByteArray().Concat(markets.ContentHash.ToByteArray()).ToArray());
        // Author only the market catalog of this unplayed calibration through the public canonical codec.
        var fields = WorldResourceJourneyTests.ReadFrame(WorldSnapshotCodec.Encode(basis));
        var coordination = WorldResourceJourneyTests.ReadFrame(fields[22]);
        coordination[7] = WorldMarketCodec.Encode(markets);
        fields[22] = WorldResourceJourneyTests.WriteFrame(coordination);
        var marketBasis = WorldSnapshotCodec.Decode(WorldResourceJourneyTests.WriteFrame(fields));
        // This is declared before the first transaction. The extra market binds existing custody and money only.
        var authored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: new WorldResourceHistory(admission, new ActorResourceHistory(basis.Progression!, 0, []), []),
            coordination: marketBasis.Coordination, retreat: basis.Retreat,
            encounterOutcome: basis.EncounterOutcome, controllers: basis.Controllers, recovery: basis.Recovery,
            controlPermissions: basis.ControlPermissions);
        Assert.Equal(basis.Inventory.ContentHash, authored.Inventory.ContentHash);
        Assert.Equal(basis.Progression!.ContentHash, authored.Progression!.ContentHash);
        return WorldDelegatedControlAdmission.CreateFresh(authored);
    }
    internal EntityId Owner => Control.Owner;
    internal EntityId Helper => Control.Other;
    internal EntityId Grantee => Control.Grantee;

    internal WorldSnapshot Prepare(SqliteWorldAuthority authority, bool fund)
    {
        var state = authority.ReadStateForHost();
        Control.Journey.WalkTo(authority, Owner, Control.Journey.Beside(state, Owner, Helper));
        Ready(authority);
        if (fund)
        {
            Act(authority, Owner, new SpeakToActorIntent(Helper, authority.ReadStateForHost().Space.GetActor(Helper).Revision));
            state = authority.ReadStateForHost();
            var quest = Assert.Single(state.Quests!.Definitions);
            Act(authority, Owner, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
                state.Space.GetActor(Helper).Revision, 0, state.Progression!.GetActor(Owner).Revision), WorldBatchKind.HeldInteraction);
            state = authority.ReadStateForHost();
            var bag = state.GetResident(Owner).InventoryCustodianId;
            var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == quest.Item);
            Act(authority, Owner, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
                state.Space.GetActor(Helper).Revision, 1, state.Progression!.GetActor(Owner).Revision,
                state.Inventory.Custody.GetStorage(bag).Revision, [new(lot.LotId, lot.Revision, quest.Quantity)]), WorldBatchKind.HeldInteraction);
            state = authority.ReadStateForHost();
            Assert.Equal(quest.Reward.CurrencyAmount, state.Progression!.Currency.GetActorAccount(Owner).Balance);
            Act(authority, Owner, new CloseDialogueIntent(Assert.Single(state.Dialogues).DialogueId), WorldBatchKind.HeldInteraction);
            Ready(authority);
        }
        Control.Grant(authority, WorldControlCapabilities.Inventory | WorldControlCapabilities.Trade);
        Control.LoseAndExpire(authority);
        return Control.Acquire(authority);
    }

    internal WorldSnapshot Ready(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        for (int step = 0; !WorldActorAvailability.CanAct(state, Owner) || !WorldActorAvailability.CanAct(state, Helper); step++)
        {
            Assert.True(step < 64, "The ordinary work debts must retire within the existing finite readiness window.");
            state = Control.Journey.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        }
        return state;
    }

    internal WorldSnapshot Act(SqliteWorldAuthority authority, EntityId actor, WorldActionIntent intent,
        WorldBatchKind kind = WorldBatchKind.AdvancingStep) =>
        Control.Apply(authority, kind, Control.Commands.Command(authority.ReadStateForHost(), actor, intent));

    internal CountedSourceLot Lot(WorldSnapshot state, EntityId actor) => state.Inventory.Custody.Lots.First(value =>
        value.CustodianId == state.GetResident(actor).InventoryCustodianId && value.Quantity > 0
        && value.ReservedBy is null && value.Protection == LotProtection.None);

    internal WorldActionCommand Offer(WorldSnapshot state, EntityId sender, EntityId recipient, CountedSourceLot lot)
    {
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, sender, recipient,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(recipient).InventoryCustodianId, 1, lot.Revision);
        var intent = new ProposeMaterialIntent(terms, state.AdvancingSteps + 1_024);
        return sender == Owner ? Control.Delegated(state, intent) : Control.Commands.Command(state, sender, intent);
    }

    internal WorldActionCommand Offer(WorldSnapshot state, EntityId sender, EntityId recipient, TransformedLot lot)
    {
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, sender, recipient,
            ConsentedMaterialLotKind.Produced, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(recipient).InventoryCustodianId, 1, lot.Revision);
        var intent = new ProposeMaterialIntent(terms, state.AdvancingSteps + 1_024);
        return sender == Owner ? Control.Delegated(state, intent) : Control.Commands.Command(state, sender, intent);
    }

    internal (WorldActionCommand Command, SpotQuote Quote) ProducedQuote(WorldSnapshot state, TransformedLot lot)
    {
        var delivery = Control.Delivery(Grantee);
        var id = Control.Commands.NextId();
        var ledger = state.Progression!.Currency;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(id), state.WorldId, state.BranchId, Owner,
            delivery.PrincipalId, delivery.SessionId, Helper, SpotQuoteSide.IssuerSells, SpotLotKind.Produced,
            lot.LotId, lot.Definition, lot.Revision, lot.CustodianId, state.GetResident(Helper).InventoryCustodianId,
            ledger.Currency.DefinitionId, ledger.Currency.Version, ledger.GetActorAccount(Owner).AccountId,
            ledger.GetActorAccount(Helper).AccountId, 1, [], 1, state.Coordination!.PaidExchange!.GetIssuerRevision(Owner),
            state.AdvancingSteps + 1, state.AdvancingSteps + 1_024);
        return (new(id, delivery.PrincipalId, delivery.SessionId, Owner, state.WorldId, state.BranchId,
            state.Space.GetActor(Owner).Revision, new IssueSpotQuoteIntent(quote), state.EffectiveControl!.Find(Owner)!.Witness), quote);
    }

    internal WorldActionCommand Accept(WorldSnapshot state, WorldMaterialProposal offer)
    {
        var intent = AcceptMaterialProposalIntent.Approving(offer, state.Space.GetActor(offer.Terms.SenderActorId).Revision);
        return offer.Terms.RecipientActorId == Owner ? Control.Delegated(state, intent)
            : Control.Commands.Command(state, offer.Terms.RecipientActorId, intent);
    }

    internal (WorldActionCommand Command, SpotQuote Quote) Quote(WorldSnapshot state, EntityId seller,
        EntityId buyer, CountedSourceLot lot, ulong price = 1, MarketBinding? market = null)
    {
        var delivery = Control.Delivery(seller == Owner ? Grantee : seller);
        var id = Control.Commands.NextId();
        var ledger = state.Progression!.Currency;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(id), state.WorldId, state.BranchId, seller,
            delivery.PrincipalId, delivery.SessionId, buyer, SpotQuoteSide.IssuerSells, SpotLotKind.CountedSource,
            lot.LotId, lot.Definition, lot.Revision, lot.CustodianId, state.GetResident(buyer).InventoryCustodianId,
            ledger.Currency.DefinitionId, ledger.Currency.Version, ledger.GetActorAccount(seller).AccountId,
            ledger.GetActorAccount(buyer).AccountId, market is null ? price : market.Policy.Ask(market.CountUsableStock(state.Inventory, state.Production!.Lots), 1)!.Value,
            [], 1, state.Coordination!.PaidExchange!.GetIssuerRevision(seller),
            state.AdvancingSteps + 1, state.AdvancingSteps + (market?.Policy.QuoteLifetimeSteps ?? 1_024));
        return (new(id, delivery.PrincipalId, delivery.SessionId, seller, state.WorldId, state.BranchId,
            state.Space.GetActor(seller).Revision, market is null ? new IssueSpotQuoteIntent(quote) : new IssueMarketQuoteIntent(market.MarketId, quote),
            seller == Owner ? state.EffectiveControl!.Find(Owner)!.Witness : null), quote);
    }

    internal WorldActionCommand Accept(WorldSnapshot state, SpotQuote quote)
    {
        var actor = quote.CounterpartyActorId;
        var delivery = Control.Delivery(actor == Owner ? Grantee : actor);
        var id = Control.Commands.NextId();
        return new(id, delivery.PrincipalId, delivery.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, new AcceptSpotQuoteIntent(new SpotAcceptance(id,
                delivery.PrincipalId, delivery.SessionId, quote.QuoteId, quote.ContentHash, 1, quote.UnitPrice, quote.FeesFor(1)),
                state.Space.GetActor(quote.IssuerActorId).Revision),
            actor == Owner ? state.EffectiveControl!.Find(Owner)!.Witness : null);
    }

    internal WorldActionReceipt Submit(SqliteWorldAuthority authority, WorldActionCommand command,
        WorldActionRejection expected = WorldActionRejection.None)
    {
        var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
        var cause = Control.Commands.NextId();
        var receipt = command.Intent switch
        {
            MaterialProposalIntent => authority.SubmitMaterialProposalCommand(command, delivery, cause),
            MarketIntent => authority.SubmitMarketCommand(command, delivery, cause),
            _ => authority.SubmitPaidExchangeCommand(command, delivery, cause)
        };
        Assert.Equal(expected, receipt.Rejection);
        Assert.Equal(command.PrincipalId, receipt.PrincipalId);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
        return receipt;
    }

    internal (WorldSnapshot Closing, WorldStepDecision Publication) Cut(SqliteWorldAuthority authority)
    {
        var state = Ready(authority);
        for (int count = 0; count <= WorldMaterialProposalBook.MaximumOutcomes; count++)
        {
            var command = Offer(state, Helper, Owner, Lot(state, Helper));
            var before = state;
            var decision = Control.Journey.Commit(authority, WorldBatchKind.AdvancingStep, [command]);
            state = decision.Candidate;
            if (decision.SealedMaterialCareSegment is not null) return (before, decision);
            before = state;
            decision = Control.Journey.Commit(authority, WorldBatchKind.AdvancingStep,
                [Control.Commands.Command(state, Helper, new WithdrawMaterialProposalIntent(WorldMaterialProposal.DeriveIdentity(command.CommandId)))]);
            state = decision.Candidate;
            if (decision.SealedMaterialCareSegment is not null) return (before, decision);
        }
        throw new InvalidOperationException("Real material closure pressure must publish its ordinary finite archive.");
    }
}
