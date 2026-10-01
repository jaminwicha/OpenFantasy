using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Retreat;

/// <summary>
/// The authority's ordinary journal checkpoint (written once 6,000 advancing steps have passed) lands inside a
/// committed retreat reached by ordinary play: idle exploration, the real wolf, Wait at the player's own boundaries
/// until the checkpoint is near, then the advised retreat. The retreat completes after the checkpoint. The checkpoint
/// root holds the active continuation; rows retained before it never change; full verified recovery reproduces the
/// live root with its debt, feet, body, material and history; the request retries to its original receipt.
/// </summary>
public sealed class RetreatCheckpointTests
{
    private const ulong CheckpointSteps = 6_000;

    [Fact]
    public async Task A_journal_checkpoint_inside_an_active_retreat_keeps_every_retained_row_and_replays_exactly()
    {
        WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded("retreat-checkpoint", "Traveler");
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string save = Path.Combine(directory, "world.sqlite");
            string prior = Path.Combine(directory, "prior.sqlite");
            WorldActionCommand retreat;
            WorldSnapshot live;
            await using (var world = new RetreatJourneyWorld(save, genesis, reopen: false))
            {
                EntityId player = world.Player;
                // Ordinary idle exploration towards the checkpoint boundary; the wolf may amble, so approach it again.
                while (world.State.AdvancingSteps < CheckpointSteps - 120)
                {
                    ulong target = Math.Min(world.State.AdvancingSteps + 400, CheckpointSteps - 120);
                    await world.TickUntil(() => world.State.AdvancingSteps >= target, 1_000, "idle");
                    await world.ApproachFirstWolf();
                }
                Assert.True(world.State.AdvancingSteps < CheckpointSteps - 60, "Approaching the wolf used up the checkpoint margin.");
                Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
                // At each of the player's own boundaries wait until the checkpoint falls within the coming walk.
                LocalRetreatOffer offer;
                for (int boundaries = 0; ; boundaries++)
                {
                    Assert.True(boundaries < 8, "The checkpoint window was not reached by ordinary Wait boundaries.");
                    await world.TickUntil(() => world.Session.ReadView().EncounterActions?.CanWait == true, 400, "player boundary");
                    ulong remaining = CheckpointSteps - world.State.AdvancingSteps;
                    Assert.True(world.State.AdvancingSteps < CheckpointSteps, "The checkpoint was written before the retreat.");
                    offer = world.Session.ReadView().Retreat!.Offer!;
                    Assert.True(offer.CanQueue, $"The retreat must be offered: {offer.BlockedReason}.");
                    ulong walkTicks = offer.WalkingUnits / EncounterTimingSnapshot.UnitsPerTick;
                    if (remaining + 3 <= walkTicks) break;
                    var actions = world.Session.ReadView().EncounterActions!;
                    Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(
                        new QueueEncounterWaitIntent(actions.EncounterId, actions.TimingRevision), RetreatJourneyWorld.NewId()))).Rejection);
                }
                Assert.True(world.State.Progression!.GetActor(player).CurrentHp > 0);
                world.Authority.BackupForHost(prior);
                retreat = world.RetreatCommand(offer, RetreatJourneyWorld.NewId());
                Assert.Equal(WorldActionRejection.None, (await world.Submit(retreat)).Rejection);
                await world.TickUntil(() => world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration, 2_000, "retreat");
                live = world.State;
                Assert.Equal(EncounterRetreatStatus.Disengaged, live.Retreat!.Last!.Status);
                Assert.True(live.AdvancingSteps >= CheckpointSteps);
                world.Session.SetReady(false);
                await world.StopAsync();
            }

            // The checkpoint the authority wrote by its own interval holds the retreat mid-route.
            StoredWorldCheckpoint checkpoint;
            using (WorldDatabase database = WorldDatabase.Open(save, create: false))
            {
                checkpoint = database.ReadCheckpoints().Single(value => value.Sequence != 0);
                WorldSnapshot cut = WorldSnapshotCodec.Decode(checkpoint.State);
                Assert.Equal(checkpoint.Sequence, cut.Revision);
                Assert.True(cut.AdvancingSteps >= CheckpointSteps);
                EncounterRetreatContinuation active = cut.Retreat!.Active!;
                Assert.Equal(retreat.CommandId, active.Command.CommandId);
                Assert.InRange(active.SamplesTaken, 1, active.Intent.Route.Count - 1);
                Assert.Equal(active.ExpectedFeet, cut.Space.GetActor(retreat.ActorId).Feet);
                Assert.True(live.Retreat!.Last!.WorldSequence > checkpoint.Sequence);
                // Every row retained before the retreat and the checkpoint is unchanged: journal, receipts and identities.
                using WorldDatabase before = WorldDatabase.Open(prior, create: false);
                ulong head = before.ReadHead().Sequence;
                Assert.True(head < checkpoint.Sequence);
                Assert.Equal(before.ReadJournal().Select(Row), database.ReadJournal().Where(value => value.Sequence <= head).Select(Row));
                Assert.Equal(before.ReadCommandOutcomes().Select(value => (value.CommandId, value.Sequence, Convert.ToHexString(value.Receipt))),
                    database.ReadCommandOutcomes().Where(value => value.Sequence <= head)
                        .Select(value => (value.CommandId, value.Sequence, Convert.ToHexString(value.Receipt))));
                var retained = before.ReadIdentities().ToHashSet();
                Assert.Subset(database.ReadIdentities().ToHashSet(), retained);
                Assert.Equal(before.ReadCheckpoints().Select(value => Convert.ToHexString(value.State)),
                    database.ReadCheckpoints().Where(value => value.Sequence <= head).Select(value => Convert.ToHexString(value.State)));
            }

            // The checkpoint's bytes are exactly the recorded root at that sequence under full verified replay.
            Assert.Equal(checkpoint.State, WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, checkpoint.Sequence).State));
            // A separate opening recovers the complete journal across the checkpoint to the identical live root.
            await using var reopened = new RetreatJourneyWorld(save, genesis, reopen: true);
            WorldSnapshot recovered = reopened.State;
            EntityId actor = retreat.ActorId;
            Assert.Equal(live.ContentHash, recovered.ContentHash);
            Assert.Equal(live.EncounterLifecycle!.GetDebt(actor), recovered.EncounterLifecycle!.GetDebt(actor));
            Assert.Equal(live.Space.GetActor(actor).Feet, recovered.Space.GetActor(actor).Feet);
            Assert.Equal(live.Space.GetActor(actor).Revision, recovered.Space.GetActor(actor).Revision);
            Assert.True(recovered.Space.GetActor(actor).Condition.CanLocomote(OpenFantasy.Core.Actors.LocomotionMode.Ground));
            Assert.Equal(live.Progression!.GetActor(actor).CurrentHp, recovered.Progression!.GetActor(actor).CurrentHp);
            Assert.Equal(live.Inventory.Custody.Lots.Select(lot => (lot.LotId, lot.Quantity, lot.CustodianId)),
                recovered.Inventory.Custody.Lots.Select(lot => (lot.LotId, lot.Quantity, lot.CustodianId)));
            Assert.Equal(live.ResourceHistory!.History.Batches.Count, recovered.ResourceHistory!.History.Batches.Count);
            Assert.Equal(EncounterRetreatCodecV1.Encode(live.Retreat!), EncounterRetreatCodecV1.Encode(recovered.Retreat!));
            // The request committed before the checkpoint retries to its original receipt without another effect.
            reopened.Session.SetReady(false);
            WorldActionReceipt again = await reopened.Session.SubmitAction(retreat);
            Assert.Equal((retreat.CommandId, WorldActionRejection.None), (again.CommandId, again.Rejection));
            Assert.Equal(recovered.ContentHash, reopened.State.ContentHash);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static (ulong, string, string) Row(StoredWorldCommit commit) =>
        (commit.Sequence, Convert.ToHexString(commit.AcceptedBatch), Convert.ToHexString(commit.Outcomes));
}
