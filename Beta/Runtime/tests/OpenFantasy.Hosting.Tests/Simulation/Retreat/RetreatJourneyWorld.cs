using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Retreat;

/// <summary>
/// One opening of a durable retreat-capable cargo save through an actual local session: the cargo trading advice,
/// optional opt-in NPC retreat preferences and the ordinary client session. Every step is a session cutoff.
/// </summary>
internal sealed class RetreatJourneyWorld : IAsyncDisposable
{
    private bool _stopped;

    internal RetreatJourneyWorld(string path, WorldSnapshot genesis, bool reopen, LocalRetreatConfiguration? retreat = null)
    {
        Authority = reopen ? SqliteWorldAuthority.OpenExisting(path, WorldSnapshotCodec.Encode(genesis))
            : SqliteWorldAuthority.CreateNew(path, genesis);
        WorldSnapshot state = Authority.ReadStateForHost();
        Sources = CargoCircuitSources.LoadEmbedded();
        Player = state.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        Wren = state.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
        Markets = CargoWorldAdmission.Find(state, Sources)!;
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == Player);
        Delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        Session = new LocalWorldSession(Authority, Player, Delivery, Time, null, CargoWorldAdmission.Configuration(state, Sources),
            null, retreat);
        EntityId wood = state.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak).Definition.YieldDefinition.DefinitionId;
        EntityId berries = state.Gathering.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry).Definition.YieldDefinition.DefinitionId;
        Client = new FarmClientSession(Session, Delivery, Wren, wood, berries);
        Service = new MarketTradingService(Authority);
        Session.SetReady(true);
    }

    internal SqliteWorldAuthority Authority { get; }
    internal CargoCircuitSources Sources { get; }
    internal ManualSessionTimeProvider Time { get; } = new();
    internal LocalWorldSession Session { get; }
    internal FarmClientSession Client { get; }
    internal MarketTradingService Service { get; }
    internal TrustedCommandDelivery Delivery { get; }
    internal EntityId Player { get; }
    internal EntityId Wren { get; }
    internal CargoCircuitMarkets Markets { get; }
    internal WorldSnapshot State => Authority.ReadStateForHost();

    internal ulong Balance(EntityId actor) => State.Progression!.Currency.GetActorAccount(actor).Balance;

    internal Task Tick() => SessionFixture.Tick(Time);

    internal async Task<WorldActionReceipt> Submit(WorldActionCommand command)
    {
        Task<WorldActionReceipt> pending = Session.SubmitAction(command);
        await Tick();
        await SessionFixture.Until(() => pending.IsCompleted);
        return await pending;
    }

    internal async Task<WorldActionReceipt> SubmitOffer(FarmSemanticAction action)
    {
        FarmActionOffer offer = Client.CreateOffers(Session.ReadView())[action];
        Task<WorldActionReceipt> pending = Client.SubmitOffer(offer);
        await Tick();
        await SessionFixture.Until(() => pending.IsCompleted);
        return await pending;
    }

    internal WorldActionCommand PlayerCommand(WorldActionIntent intent, EntityId commandId)
    {
        WorldSnapshot state = State;
        return new WorldActionCommand(commandId, Delivery.PrincipalId, Delivery.SessionId, Player, state.WorldId,
            state.BranchId, state.Space.GetActor(Player).Revision, intent);
    }

    internal static EntityId NewId() => EntityId.FromBytes(System.Security.Cryptography.RandomNumberGenerator.GetBytes(EntityId.ByteLength));

    internal EncounterScenarioDefinition FirstScenario =>
        State.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);

    /// <summary>Enters the inherited first wolf scenario through the player's own entry row.</summary>
    internal async Task<WorldActionReceipt> EnterFirstWolf()
    {
        EncounterScenarioDefinition scenario = FirstScenario;
        LocalEncounterEntryView entry = Session.ReadView().EncounterLifecycle!.Entries.Single(value => value.ScenarioId == scenario.DefinitionId);
        return await Submit(PlayerCommand(new EnterEncounterIntent(scenario.DefinitionId, entry.EncounterId,
            State.EncounterLifecycle!.EncounterCount, entry.TargetRevision), NewId()));
    }

    /// <summary>Ordinary cutoffs until the predicate holds; the world never receives an injected outcome.</summary>
    internal async Task<int> TickUntil(Func<bool> predicate, int maximumCutoffs, string label)
    {
        int cutoffs = 0;
        while (!predicate())
        {
            Assert.True(cutoffs++ < maximumCutoffs, $"{label} exceeded {maximumCutoffs} cutoffs.");
            await Tick();
        }
        return cutoffs;
    }

    /// <summary>The player's offered retreat at its current due boundary, waiting through ordinary recovery first.</summary>
    internal async Task<LocalRetreatOffer> PlayerRetreatOffer(int maximumCutoffs = 400)
    {
        await TickUntil(() => Session.ReadView().Retreat?.Offer?.BlockedReason is LocalRetreatBlockedReason.None
            or LocalRetreatBlockedReason.NoClearGround or LocalRetreatBlockedReason.Outcome, maximumCutoffs, "retreat offer");
        return Session.ReadView().Retreat!.Offer!;
    }

    internal WorldActionCommand RetreatCommand(LocalRetreatOffer offer, EntityId commandId) =>
        new(commandId, Delivery.PrincipalId, Delivery.SessionId, Player, State.WorldId, State.BranchId, offer.ActorRevision,
            new QueueEncounterRetreatIntent(offer.EncounterId, offer.TimingRevision, offer.Destination, offer.Route));

    internal SubunitPosition Feet(EntityId actor) => State.Space.GetActor(actor).Feet;

    /// <summary>Walks authored waypoints with one ordinary movement sample per cutoff; the world decides each step.</summary>
    internal async Task<int> Walk(IReadOnlyList<OpenFantasy.Core.Terrain.CellCoordinate> waypoints, int maximumCutoffs)
    {
        var coordinator = new OpenFantasy.Hosting.Simulation.Cargo.CargoLegCoordinator(Player, waypoints.Select(CargoCorridor.FeetAt));
        int samples = 0;
        for (int cutoffs = 0; ; cutoffs++)
        {
            WorldSnapshot state = State;
            var advice = coordinator.Advise(state);
            if (advice.State == OpenFantasy.Hosting.Simulation.Cargo.CargoLegState.Arrived) break;
            Assert.True(advice.State == OpenFantasy.Hosting.Simulation.Cargo.CargoLegState.Moving, $"The walk stopped with {advice.RouteStatus}.");
            Assert.True(cutoffs < maximumCutoffs, $"The walk exceeded {maximumCutoffs} cutoffs.");
            SubunitPosition before = state.Space.GetActor(Player).Feet;
            Session.SetMovement(advice.DeltaX, advice.DeltaY);
            await Tick();
            SubunitPosition after = Feet(Player);
            if (after == before) continue;
            Assert.Equal(WorldMovementInput.DisplacementPerStep, Math.Abs(after.X - before.X) + Math.Abs(after.Y - before.Y));
            samples++;
        }
        Session.SetMovement(0, 0);
        return samples;
    }

    /// <summary>
    /// Walks back beside the first wolf (one cell west of it, as admitted) until its entry row is offered; the wolf may
    /// have ambled while the player travelled. Ordinary samples only.
    /// </summary>
    internal async Task ApproachFirstWolf(int attempts = 4)
    {
        EntityId wolf = FirstScenario.TargetActorId;
        for (int attempt = 0; ; attempt++)
        {
            await TickUntil(() => WorldActorAvailability.CanAct(State, Player), 400, "settle");
            if (Session.ReadView().EncounterLifecycle!.Entries.Single(value => value.ScenarioId == FirstScenario.DefinitionId).CanEnter) return;
            Assert.True(attempt < attempts, "The first wolf could not be approached through ordinary walking.");
            var here = Feet(Player).Cell;
            var near = Feet(wolf).Cell;
            var from = new OpenFantasy.Core.Terrain.CellCoordinate(here.X, here.Y, here.Z - 1);
            var target = new OpenFantasy.Core.Terrain.CellCoordinate(near.X - 1, near.Y, here.Z - 1);
            await Walk([.. CargoCorridor.Lane([from, new OpenFantasy.Core.Terrain.CellCoordinate(target.X, from.Y, from.Z), target])], 2_000);
        }
    }

    /// <summary>Asks the merchant in person exactly as the stall panel does, then accepts the exact offered terms.</summary>
    internal async Task<(WorldActionReceipt Receipt, ulong Total)> Buy(EntityId marketId, ulong units)
    {
        var inquiry = new LocalTradeInquiry(marketId, Markets.Origin.Commodity, LocalTradeDirection.Buy, units, null);
        Assert.Equal(LocalTradeInquiryState.Waiting, (await Session.SubmitTradeInquiryAsync(inquiry)).State);
        await Tick();
        LocalTradeInquiryView answered = Session.ReadTradeInquiry()!;
        Assert.Equal(LocalTradeInquiryState.Offered, answered.State);
        PaidQuoteView quote = Session.ReadView().PaidQuotes.Single(value => value.QuoteId == answered.QuoteId!.Value);
        EntityId accept = NewId();
        WorldActionReceipt receipt = await Submit(PlayerCommand(quote.PrepareAcceptance(accept, Delivery, units), accept));
        return (receipt, quote.TotalFor(units));
    }

    internal async Task StopAsync()
    {
        if (_stopped) return;
        _stopped = true;
        await Session.StopAsync();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
