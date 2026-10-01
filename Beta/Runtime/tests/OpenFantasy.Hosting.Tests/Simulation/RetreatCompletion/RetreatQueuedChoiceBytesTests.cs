using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;

namespace OpenFantasy.Hosting.Tests.Simulation.RetreatCompletion;

/// <summary>
/// A timing choice left queued after a retreat's first sample cannot be produced by the authority, so it is proved as
/// MALFORMED V34 BYTES rather than an impossible typed snapshot: a real queued root reached by ordinary play keeps its
/// pinned Action choice while only its trailing retreat field is replaced by an equal-length continuation claiming
/// one walked sample (origin one sample behind, so the body stands exactly at the walked prefix's end, and that
/// sample's readiness charged). Decoding must refuse it.
/// </summary>
public sealed class RetreatQueuedChoiceBytesTests
{
    // Once the continuation has started, the leftover Action choice is no longer the exact queued retreat choice, so the
    // encounter-action binding (WorldEncounterActionAdmission) reads it as an ability request with no retained request.
    private const string LeftoverChoiceRefusal = "Each pending action requires exactly one complete retained request.";

    [Fact]
    public async Task Root_bytes_keeping_the_queued_choice_after_the_first_sample_are_refused_by_decode()
    {
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-queued-bytes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded("retreat-queued-choice-bytes", "Traveler");
            WorldSnapshot queued;
            await using (var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false))
            {
                Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
                LocalRetreatOffer offer = await world.PlayerRetreatOffer();
                Assert.True(offer.CanQueue && offer.Samples >= 2, $"A multi-sample retreat must be offered: {offer.BlockedReason}.");
                Assert.Equal(WorldActionRejection.None, (await world.Submit(world.RetreatCommand(offer, RetreatJourneyWorld.NewId()))).Rejection);
                queued = world.State;
                world.Session.SetReady(false);
                await world.StopAsync();
            }

            // The ordinary queued root: the continuation has walked nothing and its exact Action choice is pinned.
            EncounterRetreatContinuation pending = queued.Retreat!.Active!;
            Assert.False(pending.HasStarted);
            EncounterDecision choice = queued.Pacing!.Encounter!.Decisions.Single(decision => decision.ActorId == pending.ActorId);
            Assert.Equal((EncounterDecisionKind.Action, pending.Command.CommandId, pending.Command.ContentHash),
                (choice.Kind, choice.DecisionId, choice.ActionPin));
            SubunitPosition feet = queued.Space.GetActor(pending.ActorId).Feet;
            Assert.Equal(pending.Origin, feet);
            byte[] bytes = WorldSnapshotCodec.Encode(queued);
            Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));

            // The malformed field: one sample walked, the body at that walked prefix's end, one sample's readiness charged.
            (long x, long y) = QueueEncounterRetreatIntent.Displacement(pending.Intent.Route[0]);
            var origin = new SubunitPosition(feet.X - x, feet.Y - y, feet.Z);
            WorldActionCommand command = pending.Command;
            var walked = new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId, command.ActorId,
                command.WorldId, command.BranchId, command.ExpectedActorRevision, new QueueEncounterRetreatIntent(pending.Intent.EncounterId,
                    pending.Intent.ExpectedTimingRevision, QueueEncounterRetreatIntent.Endpoint(origin, pending.Intent.Route), pending.Intent.Route));
            ulong delay = EncounterRetreatRules.SampleDelay(queued.Pacing.Encounter.GetActor(pending.ActorId).NormalizedSpeed);
            var started = new EncounterRetreatState(queued.Retreat.RulesPin, queued.Retreat.AcceptedCount,
                new EncounterRetreatContinuation(walked, origin, 1, delay), queued.Retreat.Last);
            Assert.True(started.Active!.HasStarted);
            Assert.Equal(feet, started.Active.ExpectedFeet);

            byte[] malformed = Splice(bytes, EncounterRetreatCodecV1.Encode(queued.Retreat), EncounterRetreatCodecV1.Encode(started));
            // The retreat field itself is well formed; only its combination with the retained queued choice is refused.
            Assert.Equal(EncounterRetreatCodecV1.Encode(started), EncounterRetreatCodecV1.Encode(EncounterRetreatCodecV1.Decode(EncounterRetreatCodecV1.Encode(started))));
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(malformed));
            Assert.Throws<FormatException>(() => WorldSnapshotCodecV34.Decode(malformed));
            // The complete root constructor names the first violated rule for the same owners: the retained choice.
            ArgumentException typed = Assert.ThrowsAny<ArgumentException>(() => WithRetreat(queued, started));
            Assert.Equal(LeftoverChoiceRefusal, typed.Message);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Replaces the root's trailing retreat field with an equal-length encoding; the frame itself is unchanged.</summary>
    private static byte[] Splice(byte[] root, byte[] field, byte[] replacement)
    {
        Assert.True(root.AsSpan().EndsWith(field));
        Assert.Equal(field.Length, replacement.Length);
        Assert.NotEqual(field, replacement);
        byte[] changed = root.ToArray();
        replacement.CopyTo(changed, root.Length - field.Length);
        return changed;
    }

    private static WorldSnapshot WithRetreat(WorldSnapshot state, EncounterRetreatState retreat) =>
        new(state.Inventory, state.Space, state.Residents, state.AdmissionVersion, state.Calendar, state.Revision,
            state.AdvancingSteps, state.Dialogues, state.Gathering, state.Production, state.Progression, state.Quests,
            state.Construction, reservoirs: state.Reservoirs, reservoirSeals: state.ReservoirSeals, liquidCells: state.LiquidCells,
            excavation: state.Excavation, pacing: state.Pacing, encounterLifecycle: state.EncounterLifecycle,
            care: state.Care, resourceHistory: state.ResourceHistory, coordination: state.Coordination, retreat: retreat);
}
