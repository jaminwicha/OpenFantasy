using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;

namespace OpenFantasy.Hosting.Tests.Client.Retreat;

/// <summary>
/// The encounter wheel opened at the player's first ready boundary. The previous frame's offers (what the wheel once
/// opened with) read as changed against that boundary for Retreat, Strike and Wait alike; the capture the game now
/// takes at opening (<see cref="FarmClientSession.CaptureWheelView"/>) is current, executable and stays unchanged when
/// the open wheel refreshes. The retreat's end is announced once, never for a record that predates the opening.
/// </summary>
public sealed class RetreatWheelCaptureTests
{
    [Fact]
    public async Task The_first_wheel_opening_at_a_ready_boundary_captures_current_offers_not_the_previous_frame()
    {
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-wheel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded("retreat-wheel", "Traveler");
            await using var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false);
            var notice = new RetreatOutcomeNotice();
            Assert.Null(notice.Observe(world.Session.ReadView().Retreat));
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            LocalWorldView previous = world.Session.ReadView();
            for (int cutoffs = 0; world.Session.ReadView().Retreat?.Offer?.CanQueue != true; cutoffs++)
            {
                Assert.True(cutoffs < 400, "The player's first ready boundary was not reached.");
                previous = world.Session.ReadView();
                await world.Tick();
            }
            Assert.False(previous.Retreat?.Offer?.CanQueue == true, "The previous frame must predate the ready boundary.");

            LocalWorldView current = world.Client.CaptureWheelView();
            Assert.True(current.Retreat!.Offer!.CanQueue);
            IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> Refreshed(FarmTarget? _) => world.Client.CreateOffers(current);

            var stale = new FarmQuickWheelModel(world.Client.CreateOffers(previous));
            stale.Refresh(Refreshed);
            Assert.Contains(stale.Slots.Where(slot => slot is not null), slot => slot!.Offer.BlockingReason?.Contains("changed", StringComparison.Ordinal) == true);

            var opened = new FarmQuickWheelModel(world.Client.CreateOffers(current));
            opened.Refresh(Refreshed);
            Assert.Equal(new[] { FarmSemanticAction.Retreat, FarmSemanticAction.Strike, FarmSemanticAction.Wait },
                opened.Slots.Take(3).Select(slot => slot!.Action));
            FarmActionOffer retreat = opened.Slots[0]!.Offer;
            Assert.True(retreat.CanExecute, retreat.BlockingReason);
            var intent = Assert.IsType<QueueEncounterRetreatIntent>(retreat.Intent);
            Assert.Equal((current.Retreat.Offer.TimingRevision, current.Retreat.Offer.Destination), (intent.ExpectedTimingRevision, intent.Destination));
            Assert.All(opened.Slots.Where(slot => slot is not null), slot => Assert.DoesNotContain("changed", slot!.Offer.BlockingReason ?? ""));
            Assert.True(opened.Slots[2]!.Offer.CanExecute);
            // The inspected text names where, how long the walk takes and the recovery still owed afterwards.
            Assert.Equal(RetreatOffers.Steps(current.Retreat.Offer), retreat.CostText);
            Assert.Contains("s walk", retreat.CostText);
            Assert.Contains("recovery", retreat.CostText);
            Assert.Contains(RetreatOffers.Charges(current.Retreat.Offer), retreat.DetailText);

            // A published executable choice must survive real scheduled cutoffs, not just refresh against its own
            // original view. Automatic decisions settle before readiness is advertised; Full Wait holds afterwards.
            string boundaryHash = world.State.ContentHash.ToString();
            for (int cutoff = 0; cutoff < 12; cutoff++)
            {
                await world.Tick();
                LocalWorldView later = world.Session.ReadView();
                opened.Refresh(_ => world.Client.CreateOffers(later));
                Assert.Equal(boundaryHash, world.State.ContentHash.ToString());
                Assert.True(opened.Slots[0]!.Offer.CanExecute, opened.Slots[0]!.Offer.BlockingReason);
                Assert.Equal(intent, opened.Slots[0]!.Offer.Intent);
                Assert.True(opened.Slots[2]!.Offer.CanExecute);
            }

            // Committing that capture walks the route; its end is announced exactly once.
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.RetreatCommand(current.Retreat.Offer, RetreatJourneyWorld.NewId()))).Rejection);
            Assert.Null(notice.Observe(world.Session.ReadView().Retreat));
            await world.TickUntil(() => world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration, 2_000, "retreat");
            string? announced = notice.Observe(world.Session.ReadView().Retreat);
            Assert.NotNull(announced);
            Assert.Contains("broke away", announced);
            Assert.Null(notice.Observe(world.Session.ReadView().Retreat));
            // A record that predates an opening is not news.
            Assert.Null(new RetreatOutcomeNotice().Observe(world.Session.ReadView().Retreat));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
