using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Simulation.RoadThreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.RoadThreat;

/// <summary>
/// The durable road journey through ordinary session cutoffs: earned money buys real lots at Marla, the lane brings
/// the player into the Bandit Chief's actual reach, the chief enters under its own control, its attack does real
/// damage, a committed retreat breaks contact, a paid detour avoids the camp, the carried lots sell at Kegga, and a
/// complete journal recovery reopens the same world where returning within reach starts a second confrontation.
/// </summary>
public sealed class RoadThreatJourneyTests
{
    [Fact]
    public async Task Bought_cargo_meets_the_chief_on_the_lane_escapes_detours_sells_and_reopens()
    {
        WorldSnapshot genesis = RoadThreatJourney.Genesis("road-threat-journey");
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-road-threat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "world.sqlite");
        try
        {
            var world = new RetreatJourneyWorld(path, genesis, reopen: false);
            EntityId player = world.Player;
            EntityId chief = threat.ChiefActorId;
            SubunitPosition chiefFeet = genesis.Space.GetActor(chief).Feet;
            var (reward, paid, lot) = await RoadThreatJourney.EarnAndBuy(world, 1);
            Assert.Equal(reward - paid, world.Balance(player));
            // At Marla, about 250 cells from the camp, the chief is unobserved: nothing about it is published.
            Assert.Empty(world.Session.ReadView().RoadThreats!);

            // The lane itself brings the carried lot into the chief's reach; the chief enters under its own control.
            bool engaged = await RoadThreatJourney.WalkUntilEngaged(world, RoadThreatJourney.Outbound(world), 8_000);
            Assert.True(engaged, "Walking the lane must meet the camp's chief.");
            WorldSnapshot entered = world.State;
            EncounterLifecycleSnapshot lifecycle = entered.EncounterLifecycle!;
            Assert.Equal(1UL, lifecycle.EncounterCount);
            Assert.Equal(threat.ScenarioId, lifecycle.ScenarioId);
            Assert.Equal(chiefFeet, entered.Space.GetActor(chief).Feet);
            Assert.Equal(346L, entered.Space.GetActor(player).Feet.Cell.X);
            long contact = RoadThreatJourney.CentreDistance(entered, player, chief);
            Assert.InRange(contact, 256, threat.Scenario.EntryRangeSubunits);
            // Wren's reward experience levelled the Squire before the road; it arrives at its full levelled HP.
            int fullHp = entered.Progression!.GetActor(player).Stats.MaxHp;
            Assert.True(fullHp > genesis.Progression!.GetActor(player).Stats.MaxHp);
            Assert.Equal(fullHp, entered.Progression.GetActor(player).CurrentHp);
            var (minimumHit, maximumHit) = RoadThreatJourney.ChiefHit(entered, player);
            Assert.Equal(lot, entered.Inventory.Custody.Lots.Single(value => value.LotId == lot.LotId));

            // Full Wait holds the human; striking an 800-HP level-7 chief invites its real attack.
            await RoadThreatJourney.AwaitPlayerChoice(world);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(RoadThreatJourney.Strike(world, chief))).Rejection);
            await RoadThreatJourney.AwaitPlayerChoice(world);
            WorldSnapshot struck = world.State;
            int hurt = struck.Progression!.GetActor(player).CurrentHp;
            int chiefHp = struck.Progression.GetActor(chief).CurrentHp;
            Assert.InRange(hurt, fullHp - maximumHit, fullHp - minimumHit);
            Assert.InRange(chiefHp, 770, 799);
            Assert.Equal(chiefFeet, struck.Space.GetActor(chief).Feet);

            // A committed retreat walks out of contact; the chief stays where it stood, with its own HP.
            LocalRetreatOffer offer = await world.PlayerRetreatOffer();
            Assert.Equal(LocalRetreatBlockedReason.None, offer.BlockedReason);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.RetreatCommand(offer, RetreatJourneyWorld.NewId()))).Rejection);
            await world.TickUntil(() => world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration, 2_000, "disengage");
            WorldSnapshot escaped = world.State;
            Assert.Equal(EncounterRetreatStatus.Disengaged, escaped.Retreat!.Last!.Status);
            Assert.Equal(offer.Destination, escaped.Space.GetActor(player).Feet);
            Assert.Equal(hurt, escaped.Progression!.GetActor(player).CurrentHp);
            Assert.Equal(chiefHp, escaped.Progression.GetActor(chief).CurrentHp);
            Assert.Equal(chiefFeet, escaped.Space.GetActor(chief).Feet);
            Assert.True(escaped.EncounterLifecycle!.GetDebt(player).RemainingDelay > 0, "The retreat's exit recovery is retained.");
            Assert.True(RoadThreatJourney.CentreDistance(escaped, player, chief) > threat.Scenario.EntryRangeSubunits + 256);
            Assert.Equal(lot, escaped.Inventory.Custody.Lots.Single(value => value.LotId == lot.LotId));
            Assert.Equal(reward - paid, world.Balance(player));

            // No automatic reentry: ordinary cutoffs without movement keep one confrontation.
            for (int index = 0; index < 40; index++) await world.Tick();
            Assert.Equal(1UL, world.State.EncounterLifecycle!.EncounterCount);
            Assert.Equal(LocalRoadThreatState.Watching, Assert.Single(world.Session.ReadView().RoadThreats!).State);

            // A paid detour west of the camp passes outside the chief's reach and rejoins the lane.
            await world.TickUntil(() => WorldActorAvailability.CanAct(world.State, player), 400, "settle");
            ulong stepsBeforeDetour = world.State.AdvancingSteps;
            int detour = await world.Walk(RoadThreatJourney.Bypass(RoadThreatJourney.Standing(world.State, player)), 4_000);
            Assert.True(detour > 0);
            Assert.Equal(1UL, world.State.EncounterLifecycle!.EncounterCount);
            Assert.True(world.State.AdvancingSteps > stepsBeforeDetour);
            bool engagedAgain = await RoadThreatJourney.WalkUntilEngaged(world,
                [.. CargoCorridorLane(RoadThreatJourney.Standing(world.State, player), RoadThreatJourney.Corridor(genesis, player).DestinationApproach)],
                8_000);
            Assert.False(engagedAgain, "North of the camp the lane is clear of the chief.");

            // The same carried lot sells at Kegga; funds move exactly between the named parties.
            ulong keggaBefore = world.Balance(world.Markets.Destination.MerchantActorId);
            var (sold, total) = await RoadThreatJourney.Sell(world, lot.LotId, 1);
            Assert.Equal(WorldActionRejection.None, sold.Rejection);
            Assert.Equal(reward - paid + total, world.Balance(player));
            Assert.Equal(keggaBefore - total, world.Balance(world.Markets.Destination.MerchantActorId));
            WorldSnapshot head = world.State;
            CountedSourceLot delivered = Assert.Single(head.Inventory.Custody.Lots, value =>
                value.CustodianId == head.GetResident(world.Markets.Destination.MerchantActorId).InventoryCustodianId
                && value.Definition == world.Markets.Destination.Commodity && value.Source.SourceId == lot.Source.SourceId);
            Assert.Equal(lot.Source, delivered.Source);
            await world.StopAsync();

            // Complete journal recovery reopens the exact head; returning within reach meets the chief again.
            var reopened = new RetreatJourneyWorld(path, genesis, reopen: true);
            Assert.Equal(WorldSnapshotCodec.Encode(head), WorldSnapshotCodec.Encode(reopened.State));
            Assert.Equal(hurt, reopened.State.Progression!.GetActor(player).CurrentHp);
            Assert.Equal(chiefFeet, reopened.State.Space.GetActor(chief).Feet);
            CellCoordinate here = RoadThreatJourney.Standing(reopened.State, player);
            bool returned = await RoadThreatJourney.WalkUntilEngaged(reopened,
                [.. CargoCorridorLane(here, new CellCoordinate(346, 320, here.Z))], 8_000);
            Assert.True(returned, "Returning along the lane within reach starts a new confrontation from recorded state.");
            Assert.Equal(2UL, reopened.State.EncounterLifecycle!.EncounterCount);
            Assert.Equal(threat.ScenarioId, reopened.State.EncounterLifecycle.ScenarioId);
            await reopened.StopAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IEnumerable<CellCoordinate> CargoCorridorLane(CellCoordinate from, CellCoordinate to) =>
        OpenFantasy.Hosting.Content.Cargo.CargoCorridor.Lane([from, new CellCoordinate(to.X, from.Y, from.Z), to]);
}
