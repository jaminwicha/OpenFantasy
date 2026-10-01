using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Cargo;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;

namespace OpenFantasy.Hosting.Tests.Simulation.RoadThreat;

/// <summary>
/// Ordinary-cutoff steps over a durable threatened cargo save: the retreat journey's session plumbing plus the camp
/// geometry. Nothing is injected: movement is one cardinal sample per cutoff and every action is an ordinary command.
/// </summary>
internal static class RoadThreatJourney
{
    internal const string PlayerName = "Traveler";

    internal static WorldSnapshot Genesis(string seed) => RoadThreatWorldAdmission.CreateEmbedded(seed, PlayerName);

    internal static RoadThreatBinding Threat(WorldSnapshot state) =>
        RoadThreatWorldAdmission.Find(state, CargoCircuitSources.LoadEmbedded(), RoadThreatSources.LoadEmbedded())!;

    internal static CargoCorridor Corridor(WorldSnapshot state, EntityId player)
    {
        CellCoordinate standing = state.Space.GetActor(player).Feet.Cell;
        return CargoCorridor.CreateCircuit(state.Space.Regions.Single(region => region.Bounds.Contains(standing)).Bounds,
            CargoCircuitSources.LoadEmbedded());
    }

    /// <summary>The farm start's standing cell (cell below the feet), as the corridor lanes address cells.</summary>
    internal static CellCoordinate Standing(WorldSnapshot state, EntityId actor)
    {
        CellCoordinate feet = state.Space.GetActor(actor).Feet.Cell;
        return new CellCoordinate(feet.X, feet.Y, feet.Z - 1);
    }

    /// <summary>Earns Wren's reward by the dialogue offers, walks to Marla and buys units there in person.</summary>
    internal static async Task<(ulong Reward, ulong Paid, CountedSourceLot Lot)> EarnAndBuy(RetreatJourneyWorld world, ulong units)
    {
        WorldSnapshot genesis = world.State;
        CargoCorridor corridor = Corridor(genesis, world.Player);
        CellCoordinate start = Standing(genesis, world.Player);
        foreach (FarmSemanticAction action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give, FarmSemanticAction.Give,
            FarmSemanticAction.CloseDialogue })
        {
            Assert.Equal(WorldActionRejection.None, (await world.SubmitOffer(action)).Rejection);
        }
        ulong reward = world.Balance(world.Player);
        Assert.True(reward > 0, "Delivering Wren's quest must pay its reward.");
        await world.Walk([.. CargoCorridor.Lane([start, new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z),
            corridor.OriginApproach])], 8_000);
        var (bought, total) = await world.Buy(world.Markets.Origin.MarketId, units);
        Assert.Equal(WorldActionRejection.None, bought.Rejection);
        WorldSnapshot state = world.State;
        CountedSourceLot lot = Assert.Single(state.Inventory.Custody.Lots, value => value.Definition == world.Markets.Origin.Commodity
            && value.CustodianId == state.GetResident(world.Player).InventoryCustodianId);
        Assert.Equal(units, lot.Quantity);
        return (reward, total, lot);
    }

    /// <summary>
    /// Walks waypoints with the ordinary leg coordinator until the world leaves exploration (the chief's entry) or the
    /// walk arrives. Returns whether an encounter began; movement input is cleared either way.
    /// </summary>
    internal static async Task<bool> WalkUntilEngaged(RetreatJourneyWorld world, IReadOnlyList<CellCoordinate> waypoints, int maximumCutoffs)
    {
        var coordinator = new CargoLegCoordinator(world.Player, waypoints.Select(CargoCorridor.FeetAt));
        for (int cutoffs = 0; ; cutoffs++)
        {
            WorldSnapshot state = world.State;
            if (state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Exploration)
            {
                world.Session.SetMovement(0, 0);
                return true;
            }
            CargoLegAdvice advice = coordinator.Advise(state);
            if (advice.State == CargoLegState.Arrived)
            {
                world.Session.SetMovement(0, 0);
                return false;
            }
            Assert.True(advice.State == CargoLegState.Moving, $"The walk stopped with {advice.RouteStatus}.");
            Assert.True(cutoffs < maximumCutoffs, $"The walk exceeded {maximumCutoffs} cutoffs.");
            world.Session.SetMovement(advice.DeltaX, advice.DeltaY);
            await world.Tick();
        }
    }

    /// <summary>The outbound lane from Marla's approach toward Gearford.</summary>
    internal static IReadOnlyList<CellCoordinate> Outbound(RetreatJourneyWorld world) => Corridor(world.State, world.Player).Outbound;

    /// <summary>A paid detour west of the camp, outside the chief's reach, rejoining the lane north of it.</summary>
    internal static IReadOnlyList<CellCoordinate> Bypass(CellCoordinate from)
    {
        long z = from.Z;
        return [.. CargoCorridor.Lane([from, new CellCoordinate(343, from.Y, z), new CellCoordinate(343, 303, z),
            new CellCoordinate(346, 303, z)])];
    }

    /// <summary>Shows the carried lot at the far counter, asks in person and accepts the exact bid.</summary>
    internal static async Task<(WorldActionReceipt Receipt, ulong Total)> Sell(RetreatJourneyWorld world, EntityId lotId, ulong units)
    {
        EntityId market = world.Markets.Destination.MarketId;
        EntityId showId = RetreatJourneyWorld.NewId();
        WorldActionCommand show = world.Service.PrepareDisclosure(world.Delivery, world.Player, market, lotId, units,
            CargoCircuitPolicy.ShowingWindowSteps, showId)!;
        Assert.Equal(WorldActionRejection.None, (await world.Submit(show)).Rejection);
        var inquiry = new LocalTradeInquiry(market, world.Markets.Destination.Commodity, LocalTradeDirection.Sell, units, lotId);
        Assert.Equal(LocalTradeInquiryState.Waiting, (await world.Session.SubmitTradeInquiryAsync(inquiry)).State);
        await world.Tick();
        LocalTradeInquiryView answered = world.Session.ReadTradeInquiry()!;
        Assert.Equal(LocalTradeInquiryState.Offered, answered.State);
        PaidQuoteView quote = world.Session.ReadView().PaidQuotes.Single(value => value.QuoteId == answered.QuoteId!.Value);
        EntityId accept = RetreatJourneyWorld.NewId();
        WorldActionReceipt receipt = await world.Submit(world.PlayerCommand(quote.PrepareAcceptance(accept, world.Delivery, units), accept));
        return (receipt, quote.TotalFor(units));
    }

    /// <summary>The player's strike on the chief at the current boundary, through the ordinary ability intent.</summary>
    internal static WorldActionCommand Strike(RetreatJourneyWorld world, EntityId chief)
    {
        WorldSnapshot state = world.State;
        EncounterAbilityDefinition strike = state.EncounterLifecycle!.Abilities.Single(value => value.Key == "strike");
        var timing = state.Pacing!.Encounter!;
        return world.PlayerCommand(new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision, strike.DefinitionId, chief),
            RetreatJourneyWorld.NewId());
    }

    /// <summary>Ticks until the player is the awaited human at a due boundary with no queued choice.</summary>
    internal static Task AwaitPlayerChoice(RetreatJourneyWorld world, int maximumCutoffs = 400) =>
        world.TickUntil(() =>
        {
            WorldSnapshot state = world.State;
            return state.EncounterLifecycle?.Phase == EncounterLifecyclePhase.Active && state.Pacing?.Encounter is { } timing
                && WorldEncounterActions.HasRequiredDecisions(state)
                && timing.DueActors.Any(actor => actor.ActorId == world.Player)
                && !timing.Decisions.Any(decision => decision.ActorId == world.Player);
        }, maximumCutoffs, "player choice");

    /// <summary>
    /// The inclusive damage range of one chief attack on the actor's actual defense, by the physical formula:
    /// base = max(1, floor(attack·power/10) − floor(defense/2)); damage = floor(base·[900,1100]/1000).
    /// </summary>
    internal static (int Minimum, int Maximum) ChiefHit(WorldSnapshot state, EntityId target)
    {
        int defense = state.Progression!.GetActor(target).Stats.Defense;
        int damage = Math.Max(1, 27 * 15 / 10 - defense / 2);
        return (Math.Max(1, damage * 900 / 1000), Math.Max(1, damage * 1100 / 1000));
    }

    internal static long CentreDistance(WorldSnapshot state, EntityId a, EntityId b)
    {
        var first = state.Space.GetActor(a).Feet;
        var second = state.Space.GetActor(b).Feet;
        long dx = first.X - second.X;
        long dy = first.Y - second.Y;
        return (long)Math.Floor(Math.Sqrt((double)dx * dx + (double)dy * dy));
    }
}
