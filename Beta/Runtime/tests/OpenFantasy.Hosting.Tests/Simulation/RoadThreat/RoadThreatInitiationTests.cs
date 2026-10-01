using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Simulation.RoadThreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;

namespace OpenFantasy.Hosting.Tests.Simulation.RoadThreat;

/// <summary>
/// Automatic initiation against the actual reach, control and session holds, and the consequence of staying to fight:
/// a player cannot forge the chief's entry, single samples just outside reach start nothing, a paused session holds an
/// in-reach player, the next ready cutoff enters under the chief's own control, an exact retry adds nothing, and two
/// real hits incapacitate the Squire without any reward, loot, release or relocation being fabricated.
/// </summary>
public sealed class RoadThreatInitiationTests
{
    [Fact]
    public async Task The_chief_enters_only_at_actual_reach_when_ready_and_staying_to_fight_incapacitates()
    {
        WorldSnapshot genesis = RoadThreatJourney.Genesis("road-threat-initiation");
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-road-threat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false);
            EntityId player = world.Player;
            EntityId chief = threat.ChiefActorId;
            CargoCorridor corridor = RoadThreatJourney.Corridor(genesis, player);
            CellCoordinate start = RoadThreatJourney.Standing(genesis, player);
            CellCoordinate approach = corridor.OriginApproach;
            await world.Walk([.. CargoCorridor.Lane([start, new CellCoordinate(start.X, approach.Y, start.Z), approach])], 8_000);
            Assert.False(await RoadThreatJourney.WalkUntilEngaged(world,
                [.. CargoCorridor.Lane([approach, corridor.Corner, new CellCoordinate(346, 314, approach.Z)])], 8_000));
            Assert.Equal(0UL, world.State.EncounterLifecycle!.EncounterCount);
            // At (346,314) the chief's centre is 809 subunits away, beyond the 768-subunit observation bound.
            Assert.NotEqual(SpatialInteraction.Reachable, world.State.Space.QueryInteraction(player, chief));
            Assert.Empty(world.Session.ReadView().RoadThreats!);

            // The player cannot begin the chief's scenario: the warning grants no authority and the refusal starts nothing.
            EncounterLifecycleSnapshot lifecycle = world.State.EncounterLifecycle!;
            WorldActionReceipt forged = await world.Submit(world.PlayerCommand(new EnterEncounterIntent(threat.ScenarioId,
                EncounterEpisodeIdentity.Derive(lifecycle, threat.ScenarioId), lifecycle.EncounterCount,
                world.State.Space.GetActor(player).Revision), RetreatJourneyWorld.NewId()));
            Assert.Equal(WorldActionRejection.NotAuthorized, forged.Rejection);
            Assert.Equal(EncounterLifecyclePhase.Exploration, world.State.EncounterLifecycle!.Phase);
            Assert.Equal(0UL, world.State.EncounterLifecycle.EncounterCount);

            // Single northward samples: every cutoff outside reach is a near miss; the first in-reach position is held
            // by a pause, then entered at the next ready cutoff under the chief's own control binding.
            int nearMisses = 0;
            int observedBeforeContact = 0;
            for (int sample = 0; ; sample++)
            {
                Assert.True(sample < 64, "The lane must reach the chief within two cells of samples.");
                SubunitPosition before = world.Feet(player);
                world.Session.SetMovement(0, -WorldMovementInput.DisplacementPerStep);
                await world.Tick();
                world.Session.SetMovement(0, 0);
                Assert.Equal(before.Offset(0, -WorldMovementInput.DisplacementPerStep, 0), world.Feet(player));
                bool inReach = world.State.Space.QueryInteraction(chief, player, threat.Scenario.EntryRangeSubunits)
                    == OpenFantasy.Core.Spatial.SpatialInteraction.Reachable;
                if (!inReach)
                {
                    await world.Tick();
                    Assert.Equal(0UL, world.State.EncounterLifecycle!.EncounterCount);
                    // The warning appears exactly while the chief is observed, before its reach: a safe pre-contact approach.
                    IReadOnlyList<LocalRoadThreatView> rows = world.Session.ReadView().RoadThreats!;
                    if (world.State.Space.QueryInteraction(player, chief) == SpatialInteraction.Reachable)
                    {
                        LocalRoadThreatView watching = Assert.Single(rows);
                        Assert.Equal((chief, LocalRoadThreatState.Watching, 7, 800), (watching.ActorId, watching.State, watching.Level, watching.CurrentHp));
                        Assert.Equal(400L, watching.ReachSubunits);
                        Assert.InRange(watching.DistanceSubunits, 401, 768);
                        observedBeforeContact++;
                    }
                    else Assert.Empty(rows);
                    nearMisses++;
                    continue;
                }
                Assert.True(observedBeforeContact > 0, "The chief must be observed before the player comes within its reach.");
                Assert.Equal(LocalRoadThreatState.InReach, Assert.Single(world.Session.ReadView().RoadThreats!).State);
                world.Session.SetReady(false);
                for (int held = 0; held < 5; held++) await world.Tick();
                Assert.Equal(0UL, world.State.EncounterLifecycle!.EncounterCount);
                world.Session.SetReady(true);
                await world.Tick();
                break;
            }
            Assert.True(nearMisses > 0);
            WorldSnapshot entered = world.State;
            Assert.Equal(1UL, entered.EncounterLifecycle!.EncounterCount);
            Assert.Equal(threat.ScenarioId, entered.EncounterLifecycle.ScenarioId);
            Assert.InRange(RoadThreatJourney.CentreDistance(entered, player, chief), 256, 400);

            // Staying to fight: an exact retry adds nothing, and the chief's real hits incapacitate the Squire.
            int fullHp = genesis.Progression!.GetActor(player).Stats.MaxHp;
            var (minimumHit, maximumHit) = RoadThreatJourney.ChiefHit(entered, player);
            await RoadThreatJourney.AwaitPlayerChoice(world);
            WorldActionCommand strike = RoadThreatJourney.Strike(world, chief);
            WorldActionReceipt first = await world.Submit(strike);
            Assert.Equal(WorldActionRejection.None, first.Rejection);
            WorldActionReceipt retried = await world.Submit(strike);
            Assert.Equal((first.CommandId, first.CommandHash, first.CommitSequence, first.ResultId, first.Rejection),
                (retried.CommandId, retried.CommandHash, retried.CommitSequence, retried.ResultId, retried.Rejection));
            await world.TickUntil(() => world.State.Progression!.GetActor(player).CurrentHp < fullHp, 400, "first hit");
            Assert.InRange(world.State.Progression!.GetActor(player).CurrentHp, fullHp - maximumHit, fullHp - minimumHit);
            int strikes = 1;
            while (!WorldEncounterActions.IsOutcome(world.State))
            {
                Assert.True(strikes++ < 8, "A level-7 chief must incapacitate an early Squire who stays to fight.");
                await world.TickUntil(() => WorldEncounterActions.IsOutcome(world.State)
                    || world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active
                    && WorldEncounterActions.HasRequiredDecisions(world.State), 400, "next choice");
                if (WorldEncounterActions.IsOutcome(world.State)) break;
                Assert.Equal(WorldActionRejection.None, (await world.Submit(RoadThreatJourney.Strike(world, chief))).Rejection);
            }
            WorldSnapshot outcome = world.State;
            Assert.Equal(0, outcome.Progression!.GetActor(player).CurrentHp);
            Assert.True(outcome.Progression.GetActor(chief).CurrentHp is > 700 and < 800, "Squire strikes barely mark an 800-HP chief.");
            Assert.Equal(genesis.Space.GetActor(chief).Feet, outcome.Space.GetActor(chief).Feet);
            Assert.Equal(0UL, outcome.Progression.Currency.GetActorAccount(chief).Balance);
            Assert.Equal(LocalRetreatBlockedReason.Outcome, world.Session.ReadView().Retreat?.Offer?.BlockedReason
                ?? LocalRetreatBlockedReason.Outcome);
            for (int index = 0; index < 20; index++) await world.Tick();
            Assert.True(WorldEncounterActions.IsOutcome(world.State), "No automatic release, rescue or reward follows incapacity.");
            Assert.Equal(1UL, world.State.EncounterLifecycle!.EncounterCount);
            await world.StopAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
