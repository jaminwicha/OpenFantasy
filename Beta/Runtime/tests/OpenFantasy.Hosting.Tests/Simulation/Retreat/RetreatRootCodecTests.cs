using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Simulation.Retreat;

namespace OpenFantasy.Hosting.Tests.Simulation.Retreat;

/// <summary>
/// The profile-34 root codec over real roots reached by play: exact round trips, refusal across the V33/V34 boundary,
/// a malformed retreat field, and admission violations (feet off the walked path, a mismatched queued choice, a
/// choice left after the first sample) surfacing as format or admission failures. Variants are made by replacing the
/// trailing retreat field with an equal-length encoding of a different owner; nothing else is touched.
/// </summary>
public sealed class RetreatRootCodecTests
{
    [Fact]
    public async Task Retreat_roots_round_trip_and_refuse_other_versions_malformed_fields_and_inconsistent_owners()
    {
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-codec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded("retreat-codec", "Traveler");
            WorldSnapshot queued, started;
            await using (var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false))
            {
                Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
                LocalRetreatOffer offer = await world.PlayerRetreatOffer();
                Assert.True(offer.Samples >= 3);
                Assert.Equal(WorldActionRejection.None, (await world.Submit(world.RetreatCommand(offer, RetreatJourneyWorld.NewId()))).Rejection);
                queued = world.State;
                Assert.False(queued.Retreat!.Active!.HasStarted);
                await world.TickUntil(() => world.State.Retreat!.Active is { SamplesTaken: >= 1 } active && active.RemainingSamples >= 2, 400, "started");
                started = world.State;
            }

            // Exact round trips through the dispatcher and the profile's own codec.
            foreach (WorldSnapshot root in new[] { genesis, queued, started })
            {
                byte[] bytes = WorldSnapshotCodec.Encode(root);
                Assert.Equal(bytes, WorldSnapshotCodecV34.Encode(root));
                Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
                Assert.Equal(root.ContentHash, WorldSnapshotCodecV34.Decode(bytes).ContentHash);
            }
            // Neither neighbouring interpretation reads or writes the other's root.
            WorldSnapshot market = CargoWorldAdmission.CreateEmbedded("retreat-codec", "Traveler");
            byte[] v33 = WorldSnapshotCodec.Encode(market);
            byte[] v34 = WorldSnapshotCodec.Encode(started);
            Assert.Throws<FormatException>(() => WorldSnapshotCodecV34.Decode(v33));
            Assert.Throws<FormatException>(() => WorldSnapshotCodecV33.Decode(v34));
            Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV33.Encode(started));
            Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV34.Encode(market));

            // A malformed retreat field: a changed pin byte, a truncated root or a trailing byte.
            byte[] retreatField = EncounterRetreatCodecV1.Encode(started.Retreat!);
            Assert.True(v34.AsSpan().EndsWith(retreatField));
            byte[] wrongPin = v34.ToArray();
            wrongPin[v34.AsSpan().LastIndexOf(EncounterRetreatRules.Pin.ToByteArray())] ^= 1;
            Assert.ThrowsAny<FormatException>(() => WorldSnapshotCodec.Decode(wrongPin));
            Assert.ThrowsAny<FormatException>(() => WorldSnapshotCodec.Decode(v34[..^1]));
            Assert.ThrowsAny<FormatException>(() => WorldSnapshotCodec.Decode([.. v34, 0]));

            // Feet off the walked path: the continuation claims one more sample than the body has walked.
            EncounterRetreatContinuation active = started.Retreat!.Active!;
            ulong delay = EncounterRetreatRules.SampleDelay(started.Pacing!.Encounter!.GetActor(active.ActorId).NormalizedSpeed);
            var ahead = new EncounterRetreatState(started.Retreat.RulesPin, started.Retreat.AcceptedCount,
                new EncounterRetreatContinuation(active.Command, active.Origin, active.SamplesTaken + 1, active.ReadinessCharged + delay),
                started.Retreat.Last);
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(Splice(v34, retreatField, EncounterRetreatCodecV1.Encode(ahead))));

            // A queued retreat whose timing choice pins another command.
            byte[] queuedBytes = WorldSnapshotCodec.Encode(queued);
            EncounterRetreatContinuation pending = queued.Retreat!.Active!;
            WorldActionCommand other = new(RetreatJourneyWorld.NewId(), pending.Command.PrincipalId, pending.Command.OriginSessionId,
                pending.Command.ActorId, pending.Command.WorldId, pending.Command.BranchId, pending.Command.ExpectedActorRevision, pending.Intent);
            var mismatched = new EncounterRetreatState(queued.Retreat.RulesPin, queued.Retreat.AcceptedCount,
                new EncounterRetreatContinuation(other, pending.Origin, 0, 0), queued.Retreat.Last);
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(Splice(queuedBytes, EncounterRetreatCodecV1.Encode(queued.Retreat), EncounterRetreatCodecV1.Encode(mismatched))));

            // A started retreat never keeps a queued choice: its actor is not due between samples, and the clock refuses
            // a choice for an actor that is not due, so that inconsistent root cannot even be expressed.
            Assert.False(started.Pacing.Encounter!.GetActor(active.ActorId).IsDue);
            Assert.DoesNotContain(started.Pacing.Encounter.Decisions, decision => decision.ActorId == active.ActorId);
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
}
