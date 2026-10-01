using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Content.Cargo;

/// <summary>
/// The complete managed cargo circuit in one durable fresh world: the player earns Wren's reward, buys Marla's
/// actual rations in Brasshollow, carries the owned lot along the whole authored corridor (saved and reopened at the
/// corner), shows and sells it to Kegga in Gearford, and walks back to Marla to read her changed price. Every action
/// is an ordinary session cutoff under the player's own control; merchants answer under their own host control.
/// </summary>
public sealed class CargoCircuitJourneyTests
{
    private const string SeedText = "cargo-circuit-journey";
    private const int WalkBound = 8_000;

    [Fact]
    public async Task The_player_earns_buys_carries_sells_and_returns_on_the_full_route_through_a_saved_reopening()
    {
        var evidence = new CargoJourneyEvidence();
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        WorldSnapshot genesis = CargoWorldAdmission.CreateEmbedded(SeedText, "Traveler");
        evidence.Snapshot("genesis", genesis);
        RecordSources(evidence, sources, genesis);
        EntityId player = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        CellCoordinate standing = genesis.Space.GetActor(player).Feet.Cell;
        CargoCorridor corridor = CargoCorridor.CreateCircuit(
            genesis.Space.Regions.Single(region => region.Bounds.Contains(standing)).Bounds, sources);
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-cargo-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string save = Path.Combine(directory, "world.sqlite");
        List<object> commands = [];
        List<object> funds = [];
        List<CargoWalkRecord> walks = [];
        try
        {
            CargoJourneyWorld world = new(save, genesis, sources, reopen: false);
            EntityId marla = world.Markets.Origin.MerchantActorId;
            EntityId kegga = world.Markets.Destination.MerchantActorId;
            EntityId marlaMarket = world.Markets.Origin.MarketId;
            EntityId keggaMarket = world.Markets.Destination.MarketId;
            CountedItemDefinition rations = world.Markets.Origin.Commodity;
            CountedSourceLot shelf = genesis.Inventory.Custody.Lots.Single(lot => lot.Definition == rations);
            void Funds(string label, CargoJourneyWorld at)
            {
                WorldSnapshot state = at.State;
                var currency = state.Progression!.Currency;
                ulong total = currency.Accounts.Aggregate(0UL, (sum, account) => sum + account.Balance);
                Assert.Equal(currency.AdmittedSupply, total);
                funds.Add(new { label, state.Revision, state.AdvancingSteps, player = at.Balance(player), marla = at.Balance(marla),
                    kegga = at.Balance(kegga), total, currency.AdmittedSupply,
                    marlaStock = at.Markets.Origin.CountUsableStock(state.Inventory, state.Production!.Lots),
                    keggaStock = at.Markets.Destination.CountUsableStock(state.Inventory, state.Production!.Lots) });
            }
            async Task<WorldActionReceipt> Act(string label, CargoJourneyWorld at, WorldActionCommand command)
            {
                WorldActionReceipt receipt = await at.Submit(command);
                commands.Add(new { label, commandId = command.CommandId.ToString(), rejection = receipt.Rejection.ToString() });
                return receipt;
            }
            Funds("genesis", world);

            // The inherited first wolf is engaged from the player's start and fought to its outcome and release. It
            // pulls every resident into tactical timing, yet both merchants keep HP, feet, presence, stock, purse
            // and readiness debt; the circuit below then shows both counters still answer.
            MerchantFacts[] beforeFight = [MerchantFacts.Of(world, marla), MerchantFacts.Of(world, kegga)];
            CargoEncounterRecord fight = await world.FightFirstWolfToOutcome(4_000);
            Assert.True(fight.PlayerHp > 0 && fight.WolfHp == 0, $"The player must win the inherited fight: {fight}.");
            Assert.Equal(WorldActionRejection.None, (await world.ReleaseEncounter()).Rejection);
            Assert.Equal(EncounterLifecyclePhase.Exploration, world.State.EncounterLifecycle!.Phase);
            Assert.Equal(beforeFight, new[] { MerchantFacts.Of(world, marla), MerchantFacts.Of(world, kegga) });
            int debtCutoffs = await world.UntilPlayerCanAct(400);
            evidence.Fact("encounter", new { fight, debtCutoffs, merchantsUnchanged = beforeFight });
            Funds("wolf-released", world);

            // Distant counters reveal nothing: neither observation can even be prepared from the farm.
            Assert.Null(world.Service.PrepareObservation(world.Delivery, player, marlaMarket, NewId()));
            Assert.Null(world.Service.PrepareObservation(world.Delivery, player, keggaMarket, NewId()));

            // Earn the purchase funds through Wren's ordinary quest; nothing else funds the player.
            foreach (FarmSemanticAction action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give,
                FarmSemanticAction.Give, FarmSemanticAction.CloseDialogue })
            {
                WorldActionReceipt receipt = await world.SubmitOffer(action);
                commands.Add(new { label = "quest." + action, rejection = receipt.Rejection.ToString() });
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            }
            ulong reward = world.Balance(player);
            Assert.True(reward > 0, "The quest reward is the player's only purchasing money.");
            Funds("reward-earned", world);

            CellCoordinate farmStart = new(standing.X, standing.Y, standing.Z - 1);
            CellCoordinate[] approach = [.. CargoCorridor.Lane([farmStart,
                new CellCoordinate(farmStart.X, corridor.OriginApproach.Y, farmStart.Z), corridor.OriginApproach])];
            walks.Add(await world.Walk("farm-to-brasshollow", corridor, approach, WalkBound));

            // In person at Brasshollow: read Marla's posted terms, then buy two of her actual rations.
            WorldActionCommand observeMarla = world.Service.PrepareObservation(world.Delivery, player, marlaMarket, NewId())!;
            Assert.NotNull(observeMarla);
            Assert.Equal(WorldActionRejection.None, (await Act("observe.marla.outbound", world, observeMarla)).Rejection);
            MarketKnowledgeEntry marlaFirst = world.Service.ReadKnowledge(player, world.Delivery).Single(value => value.MarketId == marlaMarket);
            LocalTradeInquiryView bought = await world.Ask(marlaMarket, LocalTradeDirection.Buy, 2);
            Assert.Equal(LocalTradeInquiryState.Offered, bought.State);
            PaidQuoteView buyOffer = world.Quote(bought.QuoteId!.Value);
            Assert.Equal(world.Markets.Origin.Policy.Ask(8, 2), buyOffer.UnitPrice);
            Assert.True(reward >= buyOffer.TotalFor(2), $"The earned reward {reward} must cover {buyOffer.TotalFor(2)}.");
            EntityId acceptId = NewId();
            Parties beforeBuy = Parties.Of(world, player, marla, kegga);
            Assert.Equal(WorldActionRejection.None, (await Act("buy.marla", world,
                world.PlayerCommand(buyOffer.PrepareAcceptance(acceptId, world.Delivery, 2), acceptId))).Rejection);
            Funds("bought-at-marla", world);
            // Exact parties: the player pays Marla the quoted total; Kegga is untouched; two units leave Marla's shelf.
            ulong buyTotal = buyOffer.TotalFor(2);
            Assert.Equal(beforeBuy with { Player = beforeBuy.Player - buyTotal, Origin = beforeBuy.Origin + buyTotal,
                OriginStock = beforeBuy.OriginStock - 2 }, Parties.Of(world, player, marla, kegga));
            CountedSourceLot cargo = CarriedLot(world.State, player, rations);
            Assert.Equal((2UL, shelf.Source.SourceId), (cargo.Quantity, cargo.Source.SourceId));
            Assert.Equal(new SourceInterval(shelf.Source.SourceId, 0, 2), cargo.Source);
            Assert.Equal(new SourceInterval(shelf.Source.SourceId, 2, 8), Assert.Single(world.State.Inventory.Custody.Lots,
                lot => lot.CustodianId == world.State.GetResident(marla).InventoryCustodianId && lot.Definition == rations).Source);
            evidence.Snapshot("before-departure", world.State);

            // A second standing offer is left unaccepted; it must close by its own window and never renew.
            LocalTradeInquiryView spare = await world.Ask(marlaMarket, LocalTradeDirection.Buy, 1);
            Assert.Equal(LocalTradeInquiryState.Offered, spare.State);
            EntityId staleQuote = spare.QuoteId!.Value;
            // The player captures the exact acceptance while the offer is open here, and carries it away unsent.
            EntityId staleAccept = NewId();
            AcceptSpotQuoteIntent staleIntent = world.Quote(staleQuote).PrepareAcceptance(staleAccept, world.Delivery, 1);

            int corner = corridor.Outbound.IndexOf(corridor.Corner);
            walks.Add(await world.Walk("brasshollow-to-corner", corridor, corridor.Outbound.Take(corner + 1).ToArray(), WalkBound));
            WorldSnapshot beforeStop = world.State;
            Assert.Equal(cargo.LotId, CarriedLot(beforeStop, player, rations).LotId);
            evidence.Snapshot("mid-trip-before-save", beforeStop);
            await world.StopAsync();
            evidence.Save("mid-trip", save);

            world = new CargoJourneyWorld(save, genesis, sources, reopen: true);
            WorldSnapshot reopened = world.State;
            Assert.Equal((beforeStop.Revision, beforeStop.ContentHash), (reopened.Revision, reopened.ContentHash));
            evidence.Fact("recovery", new { boundary = "corridor corner", beforeStop.Revision, beforeStop.AdvancingSteps,
                stateHash = beforeStop.ContentHash.Value, reopenedHash = reopened.ContentHash.Value,
                cargoLot = cargo.LotId.ToString(), corner = CargoJourneyWorld.CellText(corridor.Corner) });
            walks.Add(await world.Walk("corner-to-gearford", corridor, corridor.Outbound.Skip(corner + 1).ToArray(), WalkBound));
            Assert.Equal(cargo.LotId, CarriedLot(world.State, player, rations).LotId);

            // The spare Brasshollow offer is stale here: the view no longer prepares it, the world refuses the captured
            // acceptance, and nothing renews or requotes it.
            Assert.Throws<InvalidOperationException>(() => world.Quote(staleQuote).PrepareAcceptance(NewId(), world.Delivery, 1));
            int quotesBefore = world.Session.ReadView().PaidQuotes.Count;
            ulong balanceBefore = world.Balance(player);
            WorldActionReceipt staleReceipt = await Act("accept.stale-marla-offer", world,
                world.PlayerCommand(staleIntent, staleAccept));
            Assert.NotEqual(WorldActionRejection.None, staleReceipt.Rejection);
            Assert.Equal((balanceBefore, quotesBefore), (world.Balance(player), world.Session.ReadView().PaidQuotes.Count));
            Assert.NotEqual(PaidQuoteStatus.Open, world.Quote(staleQuote).Status);

            // In person at Gearford: Marla's memory stays dated, Kegga's terms are read and the carried lot is sold.
            WorldActionCommand observeKegga = world.Service.PrepareObservation(world.Delivery, player, keggaMarket, NewId())!;
            Assert.NotNull(observeKegga);
            Assert.Null(world.Service.PrepareObservation(world.Delivery, player, marlaMarket, NewId()));
            Assert.Equal(WorldActionRejection.None, (await Act("observe.kegga", world, observeKegga)).Rejection);
            IReadOnlyList<MarketKnowledgeEntry> known = world.Service.ReadKnowledge(player, world.Delivery);
            MarketKnowledgeEntry marlaRemembered = known.Single(value => value.MarketId == marlaMarket);
            Assert.Equal(marlaFirst.ObservedAdvancingStep, marlaRemembered.ObservedAdvancingStep);
            Assert.True(marlaRemembered.AgeInSteps > marlaFirst.AgeInSteps);
            MarketKnowledgeEntry keggaKnown = known.Single(value => value.MarketId == keggaMarket);
            EntityId showId = NewId();
            WorldActionCommand show = world.Service.PrepareDisclosure(world.Delivery, player, keggaMarket, cargo.LotId, 2,
                CargoCircuitPolicy.ShowingWindowSteps, showId)!;
            Assert.Equal(WorldActionRejection.None, (await Act("show.cargo.kegga", world, show)).Rejection);
            LocalTradeInquiryView sold = await world.Ask(keggaMarket, LocalTradeDirection.Sell, 2, cargo.LotId);
            Assert.Equal(LocalTradeInquiryState.Offered, sold.State);
            PaidQuoteView sellOffer = world.Quote(sold.QuoteId!.Value);
            Assert.Equal(world.Markets.Destination.Policy.Bid(0, 2), sellOffer.UnitPrice);
            ulong beforeSale = world.Balance(player);
            Parties beforeSell = Parties.Of(world, player, marla, kegga);
            EntityId sellId = NewId();
            Assert.Equal(WorldActionRejection.None, (await Act("sell.kegga", world,
                world.PlayerCommand(sellOffer.PrepareAcceptance(sellId, world.Delivery, 2), sellId))).Rejection);
            Assert.Equal(beforeSale + sellOffer.UnitPrice * 2, world.Balance(player));
            // Exact parties: Kegga pays the player the bid total; Marla is untouched; the shown lot is Kegga's stock.
            ulong sellTotal = sellOffer.UnitPrice * 2;
            Assert.Equal(beforeSell with { Player = beforeSell.Player + sellTotal, Destination = beforeSell.Destination - sellTotal,
                DestinationStock = beforeSell.DestinationStock + 2 }, Parties.Of(world, player, marla, kegga));
            Funds("sold-at-kegga", world);
            CountedSourceLot delivered = Assert.Single(world.State.Inventory.Custody.Lots,
                lot => lot.CustodianId == world.State.GetResident(kegga).InventoryCustodianId && lot.Definition == rations);
            Assert.Equal(cargo.Source, delivered.Source);
            Assert.DoesNotContain(world.State.Inventory.Custody.Lots, lot => lot.LotId == cargo.LotId
                && lot.CustodianId == world.State.GetResident(player).InventoryCustodianId);

            CellCoordinate[] homeward = [.. corridor.Outbound.Reverse().Skip(1), corridor.OriginApproach];
            walks.Add(await world.Walk("gearford-to-brasshollow", corridor, homeward, WalkBound));
            WorldActionCommand observeAgain = world.Service.PrepareObservation(world.Delivery, player, marlaMarket, NewId())!;
            Assert.Equal(WorldActionRejection.None, (await Act("observe.marla.return", world, observeAgain)).Rejection);
            MarketKnowledgeEntry marlaReturn = world.Service.ReadKnowledge(player, world.Delivery).Single(value => value.MarketId == marlaMarket);
            Assert.True(marlaReturn.ObservedAdvancingStep > marlaFirst.ObservedAdvancingStep);
            Assert.Equal(world.Markets.Origin.Policy.Ask(6, 1), marlaReturn.AskUnitPrice);
            Assert.NotEqual(marlaFirst.AskUnitPrice, marlaReturn.AskUnitPrice);
            Funds("returned", world);
            WorldSnapshot final = world.State;
            evidence.Snapshot("final", final);
            OpenFantasy.Hosting.Worlds.WorldJournalUsage journal = world.Authority.InspectForHost().Journal;
            evidence.Fact("journal", new { journal.CommitCount, journal.CommandOutcomeCount, journal.LastCheckpointSequence,
                journal.LastCheckpointSteps, journal.ResourceArchiveCount, journal.MaterialCareArchiveCount });
            await world.StopAsync();
            evidence.Save("final", save);

            int outboundTransitions = walks.Where(walk => walk.Label is "brasshollow-to-corner" or "corner-to-gearford")
                .Sum(walk => walk.RegionTransitions);
            Assert.Equal(13, outboundTransitions);
            Assert.Equal(13, walks.Single(walk => walk.Label == "gearford-to-brasshollow").RegionTransitions);
            // Every retained place the corridor and the walked lanes overlay, checked against the declared set.
            Assert.Equal(CargoCorridorOverlays.Declared.Count, corridor.Overlays.Count);
            IReadOnlyList<CargoPlaceOverlay> approachOverlays = CargoCorridorOverlays.Compute(sources.Places, corridor.Basis, [],
                [farmStart, new CellCoordinate(farmStart.X, corridor.OriginApproach.Y, farmStart.Z), corridor.OriginApproach]);
            Assert.All(approachOverlays, overlay => Assert.All(overlay.LaneCrossings, crossing =>
                Assert.Equal(crossing, crossing.Intersect(CargoCorridorOverlays.Tiles(corridor.Basis)))));
            evidence.Fact("route", new { corridorRegions = corridor.AllRegions.Count(), totalRegions = final.Space.Regions.Count,
                originApproach = CargoJourneyWorld.CellText(corridor.OriginApproach),
                destinationApproach = CargoJourneyWorld.CellText(corridor.DestinationApproach), walks,
                overlayVersion = CargoCorridorOverlays.Version.Value, overlays = corridor.Overlays.Select(Overlay),
                approachOverlaysInheritedFarm = approachOverlays.Select(Overlay),
                threatDependency = new { place = CargoCorridorOverlays.BanditCampPlaceId,
                    crossing = CargoCorridorOverlays.BanditCampCrossing.ToString() } });
            evidence.Fact("prices", new { marlaOutbound = Known(marlaFirst), marlaReturn = Known(marlaReturn),
                keggaArrival = Known(keggaKnown), buyUnit = buyOffer.UnitPrice, buyTotal = buyOffer.TotalFor(2),
                sellUnit = sellOffer.UnitPrice, sellTotal = sellOffer.UnitPrice * 2, reward,
                staleRejection = staleReceipt.Rejection.ToString() });
            evidence.Fact("lineage", new { shelf = Interval(shelf.Source), carried = Interval(cargo.Source),
                delivered = Interval(delivered.Source), carriedLot = cargo.LotId.ToString() });
            evidence.Fact("commands", commands);
            evidence.Fact("funds", funds);
            evidence.Fact("result", "passed");
        }
        finally
        {
            evidence.Fact("commandsSoFar", commands);
            evidence.Fact("walksSoFar", walks);
            evidence.Write();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static object Known(MarketKnowledgeEntry entry) => new { ask = entry.AskUnitPrice, bid = entry.BidUnitPrice,
        fee = entry.UnitFee, observedStep = entry.ObservedAdvancingStep, ageInSteps = entry.AgeInSteps };

    private static object Overlay(CargoPlaceOverlay overlay) => new { overlay.PlaceId, overlay.InheritedTiles, overlay.AddedTiles,
        laneCrossings = overlay.LaneCrossings.Select(crossing => crossing.ToString()) };

    /// <summary>Every participant's balance and both counters' usable stock at one commit.</summary>
    private sealed record Parties(ulong Player, ulong Origin, ulong Destination, ulong OriginStock, ulong DestinationStock)
    {
        internal static Parties Of(CargoJourneyWorld world, EntityId player, EntityId origin, EntityId destination)
        {
            WorldSnapshot state = world.State;
            var currency = state.Progression!.Currency;
            return new Parties(currency.GetActorAccount(player).Balance, currency.GetActorAccount(origin).Balance,
                currency.GetActorAccount(destination).Balance,
                world.Markets.Origin.CountUsableStock(state.Inventory, state.Production!.Lots),
                world.Markets.Destination.CountUsableStock(state.Inventory, state.Production!.Lots));
        }
    }

    /// <summary>A merchant's owned facts an encounter elsewhere must not change.</summary>
    internal sealed record MerchantFacts(string Actor, int Hp, string Feet, ulong PresenceRevision, ulong Balance, ulong Stock,
        ulong Debt)
    {
        internal static MerchantFacts Of(CargoJourneyWorld world, EntityId merchant)
        {
            WorldSnapshot state = world.State;
            MarketBinding market = world.Markets.Origin.MerchantActorId == merchant ? world.Markets.Origin : world.Markets.Destination;
            var presence = state.Space.GetActor(merchant);
            return new MerchantFacts(merchant.ToString(), state.Progression!.GetActor(merchant).CurrentHp,
                $"({presence.Feet.X},{presence.Feet.Y},{presence.Feet.Z})", presence.Revision,
                state.Progression.Currency.GetActorAccount(merchant).Balance,
                market.CountUsableStock(state.Inventory, state.Production!.Lots),
                state.EncounterLifecycle!.GetDebt(merchant).RemainingDelay);
        }
    }

    private static string Interval(SourceInterval interval) => $"{interval.SourceId}[{interval.Start},{interval.End})";

    private static CountedSourceLot CarriedLot(WorldSnapshot state, EntityId player, CountedItemDefinition rations) =>
        state.Inventory.Custody.Lots.Single(lot => lot.CustodianId == state.GetResident(player).InventoryCustodianId
            && lot.Definition == rations);

    private static void RecordSources(CargoJourneyEvidence evidence, CargoCircuitSources sources, WorldSnapshot genesis)
    {
        object Merchant(CargoSourceMerchant merchant) => new { merchant.SourceId, merchant.Name, merchant.TileX, merchant.TileY,
            place = merchant.Place.SourceId, placePointer = merchant.Place.Pointer, merchant.Pointer,
            payload = merchant.PayloadHash.Value, appearance = merchant.AppearanceHash.Value, merchant.Dialogue, merchant.Shop,
            merchant.Quests };
        evidence.Fact("sources", new { origin = Merchant(sources.Origin), destination = Merchant(sources.Destination),
            good = new { sources.SharedGood.SourceId, sources.SharedGood.SourcePrice, sources.SharedGood.Pointer,
                payload = sources.SharedGood.PayloadHash.Value } });
        evidence.Fact("admission", new { genesis.ProfileVersion, admission = genesis.AdmissionVersion.Value,
            residents = genesis.Residents.Select(value => value.DisplayName).ToArray(), regions = genesis.Space.Regions.Count,
            brasshollow = Provision(CargoCircuitPolicy.BrasshollowProvision), gearford = Provision(CargoCircuitPolicy.GearfordProvision),
            supply = genesis.Progression!.Currency.AdmittedSupply });
    }

    private static object Provision(CargoMerchantProvision provision) => new { provision.MerchantSourceId, provision.OpeningUnits,
        provision.StorageCapacity, provision.PurseGil, version = provision.Version.Value };

    private static EntityId NewId() => EntityId.FromBytes(System.Security.Cryptography.RandomNumberGenerator.GetBytes(EntityId.ByteLength));
}
