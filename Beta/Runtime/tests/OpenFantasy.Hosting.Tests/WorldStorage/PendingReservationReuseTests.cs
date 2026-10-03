using System.Diagnostics;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class PendingReservationReuseTests(ITestOutputHelper output)
{
    [Fact]
    public void A_real_forward_continuation_reuses_immutable_reservations_without_changing_replay_or_publication()
    {
        const int warmupCount = 2;
        const int measuredCount = 16;
        var fixture = new CoupledSettlementWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldStepBatch trigger = fixture.NextExcavation(before);
        WorldSnapshot state;
        WorldStepDecision committed;
        WorldStepDecision[] measured = new WorldStepDecision[measuredCount];
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepDecision accepted = authority.Commit(trigger);
            Assert.Empty(accepted.Receipts);
            state = accepted.Candidate;
            PendingExcavation pending = Assert.IsType<PendingExcavation>(state.PendingExcavation);
            Assert.Equal((ushort)17, state.ProfileVersion);
            Assert.False(pending.Resolution.IsComplete);
            Assert.Equal(0UL, pending.ContinuationCount);
            Assert.Equal(256, pending.Resolution.MaximumTotalWorkUnits);
            Assert.Equal(257UL, pending.MaximumContinuationCount);
            Assert.Equal(514, pending.ResultReservations.Count);
            Assert.Equal(514, pending.ResultReservations.Distinct().Count());

            byte[] originalBytes = WorldSnapshotCodec.Encode(state);
            EntityId[] originalReservations = pending.ResultReservations.ToArray();
            WorldStepBatch continuation = WorldStepCodec.Decode(WorldStepCodec.Encode(fixture.Continue(state)));
            var processor = new WorldStepProcessor();
            for (int index = 0; index < warmupCount; index++)
            {
                _ = processor.Decide(state, continuation);
            }

            // Only synchronous decisions over this exact immutable state and batch are measured.
            // Preparation, SQL, encoding, assertions and output are outside this allocation interval.
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long timestampBefore = Stopwatch.GetTimestamp();
            for (int index = 0; index < measured.Length; index++)
            {
                measured[index] = processor.Decide(state, continuation);
            }
            long timestampAfter = Stopwatch.GetTimestamp();
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            output.WriteLine($"PendingReservationMeasurement profile={state.ProfileVersion} "
                + $"warmups={warmupCount} decisions={measuredCount} reservations={pending.ResultReservations.Count} "
                + $"maximumWork={pending.Resolution.MaximumTotalWorkUnits} allocatedBytes={allocatedBytes} "
                + $"bytesPerDecision={allocatedBytes / measuredCount} "
                + $"elapsedMilliseconds={Stopwatch.GetElapsedTime(timestampBefore, timestampAfter).TotalMilliseconds:F3}");

            byte[] expectedBytes = WorldSnapshotCodec.Encode(measured[0].Candidate);
            output.WriteLine($"PendingReservationPins input={state.ContentHash} "
                + $"candidate={measured[0].Candidate.ContentHash} inputBytes={originalBytes.Length} "
                + $"candidateBytes={expectedBytes.Length}");
            foreach (WorldStepDecision decision in measured)
            {
                WorldSnapshot candidate = decision.Candidate;
                PendingExcavation next = Assert.IsType<PendingExcavation>(candidate.PendingExcavation);
                Assert.Equal(expectedBytes, WorldSnapshotCodec.Encode(candidate));
                Assert.Empty(decision.Receipts);
                Assert.Equal(new WorldCommittedIdentity(continuation.CauseId, WorldCommittedIdentityKind.CommitCause),
                    Assert.Single(decision.Identities));
                Assert.Equal(state.Revision + 1, candidate.Revision);
                Assert.Equal(1UL, next.ContinuationCount);
                Assert.Equal(1UL, next.Resolution.CompletedWorkUnits);
                Assert.False(next.Resolution.IsComplete);
                Assert.Equal(pending.MaximumContinuationCount, next.MaximumContinuationCount);
                Assert.Equal(pending.Resolution.MaximumTotalWorkUnits, next.Resolution.MaximumTotalWorkUnits);
                Assert.Equal(originalReservations, next.ResultReservations);
                AssertStableGameplay(state, candidate);
            }

            WorldSnapshot decoded = WorldSnapshotCodec.Decode(expectedBytes);
            PendingExcavation restored = Assert.IsType<PendingExcavation>(decoded.PendingExcavation);
            Assert.Equal(expectedBytes, WorldSnapshotCodec.Encode(decoded));
            Assert.Equal(originalReservations, restored.ResultReservations);
            Assert.NotSame(pending.ResultReservations, restored.ResultReservations);
            Assert.NotSame(measured[0].Candidate.PendingExcavation!.ResultReservations, restored.ResultReservations);
            IList<EntityId> exposed = measured[0].Candidate.PendingExcavation!.ResultReservations;
            Assert.True(exposed.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => exposed[0] = originalReservations[^1]);
            Assert.Throws<NotSupportedException>(() => exposed.Clear());
            Assert.Equal(originalReservations, pending.ResultReservations);
            Assert.Equal(expectedBytes, WorldSnapshotCodec.Encode(measured[0].Candidate));

            foreach (EntityId occupied in new[] { pending.Command.CommandId, pending.TriggerCauseId,
                pending.ReservedSourceId, originalReservations[0], originalReservations[^1] })
            {
                WorldStepBatch conflicting = fixture.Continue(state, occupied);
                Assert.Throws<InvalidOperationException>(() => processor.Decide(state, conflicting));
                Assert.Throws<AuthorityDispatchException>(() => authority.Commit(conflicting));
            }
            Assert.Equal(originalBytes, WorldSnapshotCodec.Encode(state));
            Assert.Equal(originalBytes, WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            Assert.Null(authority.FindReceipt(pending.Command, ConstructionUpgradeFixture.Delivery(state, fixture.Player)));

            committed = authority.Commit(continuation);
            Assert.Equal(expectedBytes, WorldSnapshotCodec.Encode(committed.Candidate));
            Assert.Empty(committed.Receipts);
            Assert.Null(authority.FindReceipt(pending.Command,
                ConstructionUpgradeFixture.Delivery(committed.Candidate, fixture.Player)));
        }

        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldSnapshot recovered = reopened.InspectForHost().State;
            Assert.Equal(WorldSnapshotCodec.Encode(committed.Candidate), WorldSnapshotCodec.Encode(recovered));
            Assert.Equal(state.PendingExcavation!.ResultReservations, recovered.PendingExcavation!.ResultReservations);
            Assert.NotSame(state.PendingExcavation.ResultReservations, recovered.PendingExcavation.ResultReservations);
            AssertStableGameplay(before, recovered);
            Assert.Null(reopened.FindReceipt(state.PendingExcavation.Command,
                ConstructionUpgradeFixture.Delivery(recovered, fixture.Player)));
        }

        // This is deliberately last so the baseline records the allocation measurement and proves
        // canonical, refusal and durable-replay behavior before exposing the redundant ownership.
        Assert.All(measured, decision => Assert.Same(state.PendingExcavation!.ResultReservations,
            decision.Candidate.PendingExcavation!.ResultReservations));
        Assert.Same(state.PendingExcavation!.ResultReservations, committed.Candidate.PendingExcavation!.ResultReservations);
    }

    private static void AssertStableGameplay(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Gathering!.ContentHash, after.Gathering!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.ContentHash, after.ConstructionPhysical!.ContentHash);
        Assert.Equal(before.Excavation!.ContentHash, after.Excavation!.ContentHash);
        Assert.Equal(before.Space.Regions.Select(region => region.ComputeContentHash()),
            after.Space.Regions.Select(region => region.ComputeContentHash()));
        Assert.Equal(before.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision, actor.Condition.ComputeContentHash())),
            after.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision, actor.Condition.ComputeContentHash())));
        Assert.Equal(before.Residents.Select(resident => (resident.ActorId, resident.ExperiencedTime)),
            after.Residents.Select(resident => (resident.ActorId, resident.ExperiencedTime)));
    }
}
