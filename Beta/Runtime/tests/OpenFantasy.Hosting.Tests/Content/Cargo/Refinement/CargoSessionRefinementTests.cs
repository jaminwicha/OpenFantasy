using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Cargo;

namespace OpenFantasy.Hosting.Tests.Content.Cargo.Refinement;

/// <summary>
/// Actual local-session proofs in fresh durable cargo worlds: the inherited wolf encounter and its world-wide tactical
/// scope, an admitted movement refusal that latches the leg coordinator until a relevant change, and authority reach
/// and control refusals for an open quote and hand-built distant commands.
/// </summary>
public sealed class CargoSessionRefinementTests
{
    // The journey's seed: this fight replays the same deterministic episode the journey enters.
    private const string JourneySeed = "cargo-circuit-journey";

    [Fact]
    public async Task The_inherited_wolf_fight_latches_refused_movement_and_resumes_after_release_and_debt()
    {
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        WorldSnapshot farm = RestorativeJourneyAdmission.CreateEmbedded(JourneySeed, "Traveler");
        WorldSnapshot genesis = CargoWorldAdmission.Compose(farm, sources);
        // Side-zero merchants change the inherited scenarios' versions; participants and range are unchanged.
        foreach (EncounterScenarioDefinition scenario in genesis.EncounterLifecycle!.Scenarios)
        {
            EncounterScenarioDefinition retained = farm.EncounterLifecycle!.Scenarios.Single(value => value.Key == scenario.Key);
            Assert.NotEqual(retained.Version, scenario.Version);
            Assert.Equal((retained.InitiatingActorId, retained.TargetActorId, retained.EntryRangeSubunits),
                (scenario.InitiatingActorId, scenario.TargetActorId, scenario.EntryRangeSubunits));
            Assert.Equal(retained.Combatants.Count + 2, scenario.Combatants.Count);
        }
        await using var save = new TemporarySave();
        CargoJourneyWorld world = new(save.Path, genesis, sources, reopen: false);
        try
        {
            EntityId player = world.Player;
            EntityId marla = world.Markets.Origin.MerchantActorId;
            EntityId kegga = world.Markets.Destination.MerchantActorId;
            CargoCircuitJourneyTests.MerchantFacts[] before =
                [CargoCircuitJourneyTests.MerchantFacts.Of(world, marla), CargoCircuitJourneyTests.MerchantFacts.Of(world, kegga)];
            CargoEncounterRecord fight = await world.FightFirstWolfToOutcome(4_000);
            Assert.True(fight.PlayerHp > 0 && fight.WolfHp == 0, $"The player must win the inherited fight: {fight}.");
            WorldSnapshot outcome = world.State;
            Assert.Equal(EncounterLifecyclePhase.Outcome, outcome.EncounterLifecycle!.Phase);
            // The tactical timing spans every resident, including Kegga hundreds of cells away.
            Assert.All(outcome.Residents, resident => Assert.NotNull(outcome.Pacing!.Encounter!.GetActor(resident.ActorId)));
            Assert.Contains(outcome.Residents, resident => resident.ActorId == kegga);

            // Toward Marla along the ordinary approach: the session takes no movement while the outcome holds.
            CellCoordinate start = outcome.Space.GetActor(player).Feet.Cell.Offset(0, 0, -1);
            CargoCorridor corridor = CargoCorridor.CreateCircuit(outcome.Space.Regions.Single(region =>
                region.Bounds.Contains(start.Offset(0, 0, 1))).Bounds, sources);
            CellCoordinate[] approach = [.. CargoCorridor.Lane([start, new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z),
                corridor.OriginApproach])];
            var coordinator = new CargoLegCoordinator(player, approach.Select(CargoCorridor.FeetAt));
            SubunitPosition feet = outcome.Space.GetActor(player).Feet;
            Assert.Equal(CargoLegState.Moving, (await Sample(world, coordinator)).State);
            Assert.Equal(CargoLegState.Moving, (await Sample(world, coordinator)).State);
            CargoLegAdvice stop = coordinator.Advise(world.State);
            Assert.Equal((CargoLegState.Blocked, 1, true), (stop.State, coordinator.Replans, coordinator.IsLatched));
            int queries = coordinator.RouteQueries;
            for (int repeat = 0; repeat < 4; repeat++)
            {
                world.Session.SetMovement(0, 0);
                await world.Tick();
                Assert.Equal(stop, coordinator.Advise(world.State));
                Assert.Equal(stop, coordinator.Advise(world.State));
            }
            Assert.Equal((queries, 1, feet), (coordinator.RouteQueries, coordinator.Replans, world.State.Space.GetActor(player).Feet));

            Assert.Equal(WorldActionRejection.None, (await world.ReleaseEncounter()).Rejection);
            WorldSnapshot released = world.State;
            Assert.Equal(EncounterLifecyclePhase.Exploration, released.EncounterLifecycle!.Phase);
            Assert.Equal(before, new[] { CargoCircuitJourneyTests.MerchantFacts.Of(world, marla), CargoCircuitJourneyTests.MerchantFacts.Of(world, kegga) });
            Assert.True(WorldActorAvailability.CanAct(released, marla) && WorldActorAvailability.CanAct(released, kegga));

            // The release is a relevant change: the leg revalidates, then retained debt refuses samples until paid.
            int cutoffs = 0;
            int latchedCutoffs = 0;
            int samples = 0;
            while (true)
            {
                Assert.True(cutoffs++ < 2_000, "The resumed approach exceeded its bound.");
                WorldSnapshot state = world.State;
                CargoLegAdvice advice = coordinator.Advise(state);
                if (advice.State == CargoLegState.Arrived) break;
                if (advice.State == CargoLegState.Blocked)
                {
                    Assert.False(WorldActorAvailability.CanAct(state, player), "Only the retained debt may stop this ordinary lane.");
                    latchedCutoffs++;
                    world.Session.SetMovement(0, 0);
                }
                else
                {
                    world.Session.SetMovement(advice.DeltaX, advice.DeltaY);
                }
                SubunitPosition was = state.Space.GetActor(player).Feet;
                await world.Tick();
                if (world.State.Space.GetActor(player).Feet != was) samples++;
            }
            world.Session.SetMovement(0, 0);
            Assert.True(samples > 0 && coordinator.RouteQueries > queries);
            Assert.Equal(CargoCorridor.FeetAt(corridor.OriginApproach), world.State.Space.GetActor(player).Feet);
            // Marla answers in person after the fight.
            LocalTradeInquiryView offered = await world.Ask(world.Markets.Origin.MarketId, LocalTradeDirection.Buy, 1);
            Assert.Equal(LocalTradeInquiryState.Offered, offered.State);
        }
        finally
        {
            await world.StopAsync();
        }
    }

    [Fact]
    public async Task An_open_quote_out_of_reach_and_distant_or_foreign_commands_are_refused_without_effects()
    {
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        WorldSnapshot genesis = CargoWorldAdmission.CreateEmbedded("cargo-reach-refusal", "Traveler");
        await using var save = new TemporarySave();
        CargoJourneyWorld world = new(save.Path, genesis, sources, reopen: false);
        try
        {
            EntityId player = world.Player;
            EntityId marla = world.Markets.Origin.MerchantActorId;
            EntityId marlaMarket = world.Markets.Origin.MarketId;
            EntityId keggaMarket = world.Markets.Destination.MarketId;
            foreach (FarmSemanticAction action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give,
                FarmSemanticAction.Give, FarmSemanticAction.CloseDialogue })
            {
                Assert.Equal(WorldActionRejection.None, (await world.SubmitOffer(action)).Rejection);
            }
            WorldSnapshot state = world.State;
            CellCoordinate start = state.Space.GetActor(player).Feet.Cell.Offset(0, 0, -1);
            CargoCorridor corridor = CargoCorridor.CreateCircuit(state.Space.Regions.Single(region =>
                region.Bounds.Contains(start.Offset(0, 0, 1))).Bounds, sources);
            await world.Walk("to-marla", corridor, [.. CargoCorridor.Lane([start,
                new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z), corridor.OriginApproach])], 2_000);

            // Buy one unit in person so the player owns a lot to show; then capture a second open offer.
            PaidQuoteView first = world.Quote((await world.Ask(marlaMarket, LocalTradeDirection.Buy, 1)).QuoteId!.Value);
            EntityId bought = CargoJourneyWorld.NewId();
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(
                first.PrepareAcceptance(bought, world.Delivery, 1), bought))).Rejection);
            LocalTradeInquiryView open = await world.Ask(marlaMarket, LocalTradeDirection.Buy, 1);
            Assert.Equal(LocalTradeInquiryState.Offered, open.State);
            EntityId quoteId = open.QuoteId!.Value;
            EntityId acceptId = CargoJourneyWorld.NewId();
            AcceptSpotQuoteIntent captured = world.Quote(quoteId).PrepareAcceptance(acceptId, world.Delivery, 1);

            // Four cells east, still inside the offer's window: out of reach, not expired.
            await world.Walk("away", corridor, [corridor.OriginApproach.Offset(4, 0, 0)], 400);
            state = world.State;
            Assert.NotEqual(SpatialInteraction.Reachable, state.Space.QueryInteraction(player, marla));
            Assert.NotNull(state.Coordination!.PaidExchange!.FindOpen(quoteId));
            Assert.Equal(PaidQuoteStatus.Open, world.Quote(quoteId).Status);
            Snapshot before = Snapshot.Of(world);
            WorldActionReceipt far = await world.Submit(world.PlayerCommand(captured, acceptId));
            Assert.Equal(WorldActionRejection.OutOfReach, far.Rejection);
            Assert.Equal(before, Snapshot.Of(world));
            Assert.Equal(PaidQuoteStatus.Open, world.Quote(quoteId).Status);

            // Hand-built distant observations and showings reach the authority and reveal nothing.
            CountedSourceLot owned = world.State.Inventory.Custody.Lots.Single(lot =>
                lot.CustodianId == world.State.GetResident(player).InventoryCustodianId && lot.Definition == world.Markets.Origin.Commodity);
            foreach (EntityId market in new[] { marlaMarket, keggaMarket })
            {
                state = world.State;
                EntityId merchant = state.Coordination!.Markets!.FindMarket(market)!.MerchantActorId;
                Assert.Null(world.Service.PrepareObservation(world.Delivery, player, market, CargoJourneyWorld.NewId()));
                WorldActionReceipt observe = await world.Submit(world.PlayerCommand(
                    new ObserveMarketIntent(market, state.Space.GetActor(merchant).Revision), CargoJourneyWorld.NewId()));
                Assert.Equal(WorldActionRejection.OutOfReach, observe.Rejection);
                state = world.State;
                WorldActionReceipt show = await world.Submit(world.PlayerCommand(new DiscloseMarketLotIntent(market,
                    SpotLotKind.CountedSource, owned.LotId, owned.Definition, owned.Revision, 1,
                    state.Progression!.Currency.GetActorAccount(player).AccountId, state.AdvancingSteps + 100), CargoJourneyWorld.NewId()));
                Assert.Equal(WorldActionRejection.OutOfReach, show.Rejection);
                Assert.Null(world.State.Coordination!.Markets!.FindDisclosure(player, market));
            }
            Assert.Empty(world.Service.ReadKnowledge(player, world.Delivery));
            Assert.Equal(before, Snapshot.Of(world));

            // Actor control: the player's delivery cannot prepare or submit Marla's command, nor answer for her.
            state = world.State;
            Assert.Null(world.Service.PrepareObservation(world.Delivery, marla, marlaMarket, CargoJourneyWorld.NewId()));
            Assert.Null(world.Service.PrepareOffer(world.Delivery, marlaMarket, player, SpotQuoteSide.IssuerSells,
                world.State.Inventory.Custody.Lots.Single(lot => lot.CustodianId == state.GetResident(marla).InventoryCustodianId).LotId,
                1, 100, CargoJourneyWorld.NewId()));
            var asMarla = new WorldActionCommand(CargoJourneyWorld.NewId(), world.Delivery.PrincipalId, world.Delivery.SessionId,
                marla, state.WorldId, state.BranchId, state.Space.GetActor(marla).Revision,
                new ObserveMarketIntent(keggaMarket, state.Space.GetActor(world.Markets.Destination.MerchantActorId).Revision));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(asMarla));
            ActorControlBinding marlaControl = state.Inventory.Controls.Single(value => value.ActorId == marla);
            WorldActionCommand marlaOffer = world.Service.PrepareOffer(new TrustedCommandDelivery(marlaControl.PrincipalId,
                marlaControl.SessionId), marlaMarket, player, SpotQuoteSide.IssuerSells,
                state.Inventory.Custody.Lots.Single(lot => lot.CustodianId == state.GetResident(marla).InventoryCustodianId).LotId,
                1, 100, CargoJourneyWorld.NewId())!;
            var rewrapped = new WorldActionCommand(CargoJourneyWorld.NewId(), world.Delivery.PrincipalId, world.Delivery.SessionId,
                marla, state.WorldId, state.BranchId, state.Space.GetActor(marla).Revision, marlaOffer.Intent);
            WorldActionReceipt? answered = null;
            Exception? dispatch = Record.Exception(() => answered = world.Service.Submit(rewrapped, world.Delivery, CargoJourneyWorld.NewId()));
            Assert.True(dispatch is AuthorityDispatchException || answered?.Rejection == WorldActionRejection.NotAuthorized,
                $"A merchant answer under the player's binding must be refused: {dispatch?.GetType().Name ?? answered?.Rejection.ToString()}.");
            Assert.Equal(before, Snapshot.Of(world));
        }
        finally
        {
            await world.StopAsync();
        }
    }

    private static async Task<CargoLegAdvice> Sample(CargoJourneyWorld world, CargoLegCoordinator coordinator)
    {
        CargoLegAdvice advice = coordinator.Advise(world.State);
        world.Session.SetMovement(advice.DeltaX, advice.DeltaY);
        await world.Tick();
        return advice;
    }

    /// <summary>Balances, both counters' stock and the open quote count, compared before and after refusals.</summary>
    private sealed record Snapshot(ulong Player, ulong Marla, ulong Kegga, ulong MarlaStock, ulong KeggaStock, int OpenQuotes,
        int Lots)
    {
        internal static Snapshot Of(CargoJourneyWorld world)
        {
            WorldSnapshot state = world.State;
            var currency = state.Progression!.Currency;
            return new Snapshot(currency.GetActorAccount(world.Player).Balance,
                currency.GetActorAccount(world.Markets.Origin.MerchantActorId).Balance,
                currency.GetActorAccount(world.Markets.Destination.MerchantActorId).Balance,
                world.Markets.Origin.CountUsableStock(state.Inventory, state.Production!.Lots),
                world.Markets.Destination.CountUsableStock(state.Inventory, state.Production!.Lots),
                world.Session.ReadView().PaidQuotes.Count(value => value.Status == PaidQuoteStatus.Open),
                state.Inventory.Custody.Lots.Count);
        }
    }

    /// <summary>A disposable directory for one durable save; the session closes its authority before removal.</summary>
    private sealed class TemporarySave : IAsyncDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "openfantasy-cargo-refinement-" + Guid.NewGuid().ToString("N"));

        internal TemporarySave()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "world.sqlite");
        }

        internal string Path { get; }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
