using System.Text.Json;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Cargo;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Content.Cargo;

/// <summary>
/// One opening of the durable cargo save through an actual local session with the cargo trading configuration.
/// Every step is an ordinary session cutoff; setup injects no stock, money, quote or position.
/// </summary>
internal sealed class CargoJourneyWorld : IAsyncDisposable
{
    private bool _stopped;

    internal CargoJourneyWorld(string path, WorldSnapshot genesis, CargoCircuitSources sources, bool reopen)
    {
        Authority = reopen ? SqliteWorldAuthority.OpenExisting(path, WorldSnapshotCodec.Encode(genesis))
            : SqliteWorldAuthority.CreateNew(path, genesis);
        WorldSnapshot state = Authority.ReadStateForHost();
        Player = state.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        Wren = state.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
        Markets = CargoWorldAdmission.Find(state, sources)!;
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == Player);
        Delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        Session = new LocalWorldSession(Authority, Player, Delivery, Time, null, CargoWorldAdmission.Configuration(state, sources));
        EntityId wood = state.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak).Definition.YieldDefinition.DefinitionId;
        EntityId berries = state.Gathering.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry).Definition.YieldDefinition.DefinitionId;
        Client = new FarmClientSession(Session, Delivery, Wren, wood, berries);
        Service = new MarketTradingService(Authority);
        Session.SetReady(true);
    }

    internal SqliteWorldAuthority Authority { get; }
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

    /// <summary>Asks exactly as the stall panel does, then lets one cutoff answer.</summary>
    internal async Task<LocalTradeInquiryView> Ask(EntityId marketId, LocalTradeDirection direction, ulong units,
        EntityId? shownLot = null)
    {
        var inquiry = new LocalTradeInquiry(marketId, Markets.Origin.Commodity, direction, units, shownLot);
        Assert.Equal(LocalTradeInquiryState.Waiting, (await Session.SubmitTradeInquiryAsync(inquiry)).State);
        await Tick();
        return Session.ReadTradeInquiry()!;
    }

    internal PaidQuoteView Quote(EntityId quoteId) => Session.ReadView().PaidQuotes.Single(value => value.QuoteId == quoteId);

    internal WorldActionCommand PlayerCommand(WorldActionIntent intent, EntityId commandId)
    {
        WorldSnapshot state = State;
        return new WorldActionCommand(commandId, Delivery.PrincipalId, Delivery.SessionId, Player, state.WorldId,
            state.BranchId, state.Space.GetActor(Player).Revision, intent);
    }

    internal static EntityId NewId() => EntityId.FromBytes(System.Security.Cryptography.RandomNumberGenerator.GetBytes(EntityId.ByteLength));

    /// <summary>
    /// Enters the inherited first wolf scenario through the player's own entry row and fights it with ordinary
    /// Strike choices, one session cutoff at a time, until the world reaches its outcome. The wolf's choices are the
    /// session's own automatic NPC decisions; nothing is injected.
    /// </summary>
    internal async Task<CargoEncounterRecord> FightFirstWolfToOutcome(int maximumCutoffs)
    {
        WorldSnapshot state = State;
        EncounterScenarioDefinition scenario = state.EncounterLifecycle!.Scenarios.Single(value =>
            value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        EntityId strike = state.EncounterLifecycle.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey).DefinitionId;
        LocalEncounterEntryView entry = Session.ReadView().EncounterLifecycle!.Entries.Single(value => value.ScenarioId == scenario.DefinitionId);
        Assert.True(entry.CanEnter, "The player starts within reach of the first inherited wolf.");
        WorldActionReceipt entered = await Submit(PlayerCommand(new EnterEncounterIntent(scenario.DefinitionId, entry.EncounterId,
            state.EncounterLifecycle.EncounterCount, entry.TargetRevision), NewId()));
        Assert.Equal(WorldActionRejection.None, entered.Rejection);
        int cutoffs = 0;
        int strikes = 0;
        while (State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome)
        {
            Assert.True(cutoffs++ < maximumCutoffs, $"The wolf encounter exceeded {maximumCutoffs} cutoffs.");
            LocalEncounterActionView? encounter = Session.ReadView().EncounterActions;
            LocalEncounterAbilityOffer? offer = encounter?.AbilityOffers.FirstOrDefault(value =>
                value.AbilityDefinitionId == strike && value.TargetActorId == scenario.TargetActorId && value.CanQueue);
            if (offer is null)
            {
                await Tick();
                continue;
            }
            WorldActionReceipt queued = await Submit(PlayerCommand(new QueueEncounterAbilityIntent(encounter!.EncounterId,
                encounter.TimingRevision, strike, scenario.TargetActorId), NewId()));
            Assert.Equal(WorldActionRejection.None, queued.Rejection);
            strikes++;
        }
        WorldSnapshot outcome = State;
        return new CargoEncounterRecord(scenario.Key, entry.EncounterId.ToString(), strikes, cutoffs,
            outcome.Progression!.GetActor(Player).CurrentHp, outcome.Progression.GetActor(scenario.TargetActorId).CurrentHp);
    }

    /// <summary>Releases the outcome through the player's own release row, as the native control does.</summary>
    internal async Task<WorldActionReceipt> ReleaseEncounter()
    {
        WorldSnapshot state = State;
        ContentHash hash = Session.ReadView().EncounterLifecycle!.ReleaseActionHash
            ?? throw new InvalidOperationException("The player may not release this outcome.");
        return await Submit(PlayerCommand(new ReleaseEncounterIntent(state.EncounterActions!.Scope.EncounterId, hash), NewId()));
    }

    /// <summary>Ordinary exploration cutoffs pay the player's retained readiness debt; nothing waives it.</summary>
    internal async Task<int> UntilPlayerCanAct(int maximumCutoffs)
    {
        int cutoffs = 0;
        while (!WorldActorAvailability.CanAct(State, Player))
        {
            Assert.True(cutoffs++ < maximumCutoffs, "The player's readiness debt outlasted its bound.");
            await Tick();
        }
        return cutoffs;
    }

    /// <summary>
    /// Walks the player along authored waypoints: each cutoff takes the coordinator's one ordinary movement sample,
    /// and the session's own world step decides whether it moves. Region transitions are read from actual feet.
    /// </summary>
    internal async Task<CargoWalkRecord> Walk(string label, CargoCorridor corridor, IReadOnlyList<CellCoordinate> waypoints,
        int maximumCutoffs)
    {
        var coordinator = new CargoLegCoordinator(Player, waypoints.Select(CargoCorridor.FeetAt));
        WorldSnapshot state = State;
        SubunitPosition start = state.Space.GetActor(Player).Feet;
        ulong startStep = state.AdvancingSteps;
        int region = corridor.RegionIndexOf(start.Cell);
        List<int> regions = [region];
        int samples = 0;
        int cutoffs = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            state = State;
            CargoLegAdvice advice = coordinator.Advise(state);
            if (advice.State == CargoLegState.Arrived) break;
            Assert.True(advice.State == CargoLegState.Moving, $"{label}: leg {advice.LegIndex} stopped with {advice.RouteStatus}.");
            Assert.True(cutoffs < maximumCutoffs, $"{label}: the walk exceeded {maximumCutoffs} cutoffs.");
            SubunitPosition before = state.Space.GetActor(Player).Feet;
            Session.SetMovement(advice.DeltaX, advice.DeltaY);
            await Tick();
            cutoffs++;
            SubunitPosition after = State.Space.GetActor(Player).Feet;
            if (after == before) continue;
            Assert.Equal(WorldMovementInput.DisplacementPerStep, Math.Abs(after.X - before.X) + Math.Abs(after.Y - before.Y));
            samples++;
            int next = corridor.RegionIndexOf(after.Cell);
            Assert.True(next >= 0, $"{label}: the player left the admitted corridor.");
            if (next != region)
            {
                regions.Add(next);
                region = next;
            }
        }
        Session.SetMovement(0, 0);
        WorldSnapshot end = State;
        return new CargoWalkRecord(label, CellText(start.Cell), CellText(end.Space.GetActor(Player).Feet.Cell), waypoints.Count,
            cutoffs, samples, regions.Count - 1, regions, coordinator.PlannedLegs, coordinator.Replans,
            end.AdvancingSteps - startStep, Math.Round(watch.Elapsed.TotalSeconds, 3));
    }

    internal static string CellText(CellCoordinate cell) => $"({cell.X},{cell.Y},{cell.Z})";

    internal async Task StopAsync()
    {
        if (_stopped) return;
        _stopped = true;
        await Session.StopAsync();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

internal sealed record CargoWalkRecord(string Label, string StartCell, string EndCell, int Waypoints, int Cutoffs,
    int AppliedSamples, int RegionTransitions, IReadOnlyList<int> RegionSequence, int PlannedLegs, int Replans,
    ulong AdvancingSteps, double ElapsedSeconds);

internal sealed record CargoEncounterRecord(string Scenario, string EncounterId, int Strikes, int Cutoffs, int PlayerHp,
    int WolfHp);

/// <summary>Closed journey exports under the qualification-supplied directory, or a disposable directory otherwise.</summary>
internal sealed class CargoJourneyEvidence
{
    private readonly Dictionary<string, object?> _facts = [];

    internal CargoJourneyEvidence()
    {
        string? supplied = Environment.GetEnvironmentVariable("OPENFANTASY_CARGO_CIRCUIT_EVIDENCE");
        Directory = supplied is { Length: > 0 } && Path.IsPathRooted(supplied) ? supplied
            : Path.Combine(Path.GetTempPath(), "openfantasy-cargo-journey-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
    }

    internal string Directory { get; }

    internal void Fact(string name, object? value) => _facts[name] = value;

    internal void Snapshot(string name, WorldSnapshot world)
    {
        File.WriteAllBytes(Path.Combine(Directory, name + ".snapshot"), WorldSnapshotCodec.Encode(world));
        _facts["snapshot." + name] = new { world.Revision, world.AdvancingSteps, StateHash = world.ContentHash.Value };
    }

    /// <summary>Copies a closed save (its owning authority already disposed) with its SQLite side files.</summary>
    internal void Save(string name, string path)
    {
        File.Copy(path, Path.Combine(Directory, name + ".sqlite"));
        foreach (string suffix in new[] { "-wal", "-shm" })
        {
            if (File.Exists(path + suffix)) File.Copy(path + suffix, Path.Combine(Directory, name + ".sqlite" + suffix));
        }
    }

    internal void Write() => File.WriteAllText(Path.Combine(Directory, "results.json"),
        JsonSerializer.Serialize(_facts, new JsonSerializerOptions { WriteIndented = true }));
}
