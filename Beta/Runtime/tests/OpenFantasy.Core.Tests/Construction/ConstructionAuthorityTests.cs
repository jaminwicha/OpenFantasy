using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionWorldFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionAuthorityTests(ITestOutputHelper output)
{
    [Fact]
    public void Cached_authority_matches_uncached_complete_world_receipts_time_and_material_for_200_steps()
    {
        var cachedWorld = Create();
        var ordinaryWorld = cachedWorld;
        var sites = new ConstructionSiteCache();
        var cached = new WorldStepProcessor(sites);
        var ordinary = new WorldStepProcessor(null);
        long cachedBytes = 0, ordinaryBytes = 0, cachedTicks = 0, ordinaryTicks = 0;
        for (ulong step = 0; step < 200; step++)
        {
            var command = Command(cachedWorld, 30_000 + step);
            var batch = Batch(cachedWorld, step, [command], [StepFixture.Move(cachedWorld, 11)]);
            WorldStepDecision actual, expected;
            // Alternate measurement order; numbers are bounded diagnostics, not timing assertions.
            if (step % 2 == 0)
            {
                expected = Measure(ordinary, ordinaryWorld, batch, ref ordinaryBytes, ref ordinaryTicks);
                actual = Measure(cached, cachedWorld, batch, ref cachedBytes, ref cachedTicks);
            }
            else
            {
                actual = Measure(cached, cachedWorld, batch, ref cachedBytes, ref cachedTicks);
                expected = Measure(ordinary, ordinaryWorld, batch, ref ordinaryBytes, ref ordinaryTicks);
            }
            EqualDecision(expected, actual);
            Assert.Equal(WorldActionRejection.None, Assert.Single(actual.Receipts).Rejection);
            Assert.Equal(cachedWorld.Space.GetActor(Builder).Feet, actual.Candidate.Space.GetActor(Builder).Feet);
            Assert.Equal(cachedWorld.AdvancingSteps + 1, actual.Candidate.AdvancingSteps);
            Assert.All(actual.Candidate.Residents, resident => Assert.Equal(
                cachedWorld.GetResident(resident.ActorId).ExperiencedTime.TotalMilliseconds + 1800,
                resident.ExperiencedTime.TotalMilliseconds));
            cachedWorld = actual.Candidate;
            ordinaryWorld = expected.Candidate;
        }
        Assert.Equal(1UL, sites.Captures);
        Assert.Equal(199UL, sites.Reuses);
        Assert.True(Assert.Single(cachedWorld.Construction!.Projects).IsCompleted);
        Assert.Single(cachedWorld.Space.Structures);
        Assert.Single(cachedWorld.Production!.Batches);
        Assert.Equal(1UL, Assert.Single(cachedWorld.Production.Lots, lot => lot.ReservedBy is not null).Quantity);
        Assert.Equal(3UL, Assert.Single(cachedWorld.Production.Lots, lot => lot.ReservedBy is null).Quantity);
        output.WriteLine($"200 complete decisions: uncached {ordinaryBytes} bytes/{ordinaryTicks * 1000.0 / Stopwatch.Frequency:F3} ms; cached {cachedBytes} bytes/{cachedTicks * 1000.0 / Stopwatch.Frequency:F3} ms; captures {sites.Captures}, reuses {sites.Reuses}.");
    }

    [Fact]
    public void Earlier_dialogue_holds_work_but_selected_step_still_charges_time_and_closing_does_not_earn_work()
    {
        var sites = new ConstructionSiteCache();
        var processor = new WorldStepProcessor(sites);
        var world = Create();
        world = processor.Decide(world, Batch(world, 300, [Command(world, 30300)])).Candidate;
        var speak = StepFixture.Action(world, 3_000_301,
            new SpeakToActorIntent(Builder, world.Space.GetActor(Builder).Revision));
        var batch = Batch(world, 301, [speak, Command(world, 30301)]);
        var held = processor.Decide(world, batch);
        EqualDecision(new WorldStepProcessor(null).Decide(world, batch), held);
        Assert.Equal(WorldActionRejection.None, held.Receipts.Single(receipt => receipt.CommandId == speak.CommandId).Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, held.Receipts.Single(receipt => receipt.CommandId != speak.CommandId).Rejection);
        Assert.Equal(world.AdvancingSteps + 1, held.Candidate.AdvancingSteps);
        Assert.Equal(world.Construction!.ContentHash, held.Candidate.Construction!.ContentHash);
        Assert.True(held.Candidate.HasReadingHold);
        var close = StepFixture.Action(held.Candidate, 3_000_302, new CloseDialogueIntent(held.Candidate.Dialogues.Single().DialogueId));
        var closing = Batch(held.Candidate, 302, [close, Command(held.Candidate, 30302)]);
        var closed = processor.Decide(held.Candidate, closing);
        EqualDecision(new WorldStepProcessor(null).Decide(held.Candidate, closing), closed);
        Assert.False(closed.Candidate.HasReadingHold);
        Assert.Equal(held.Candidate.Calendar, closed.Candidate.Calendar);
        Assert.Equal(world.Construction.ContentHash, closed.Candidate.Construction!.ContentHash);
        var resumed = processor.Decide(closed.Candidate, Batch(closed.Candidate, 303, [Command(closed.Candidate, 30303)]));
        Assert.Equal(2UL, resumed.Candidate.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Equal(1UL, sites.Captures);
        Assert.Equal(1UL, sites.Reuses);
    }

    [Fact]
    public void Movement_and_excavation_requalify_the_actual_current_scene_before_more_work()
    {
        var sites = new ConstructionSiteCache();
        var processor = new WorldStepProcessor(sites);
        var world = Create();
        var batch = Batch(world, 400, [Command(world, 30400)], [StepFixture.Move(world, 10, -32)]);
        var moved = processor.Decide(world, batch);
        EqualDecision(new WorldStepProcessor(null).Decide(world, batch), moved);
        Assert.NotEqual(world.Space.GetActor(AdmissionFixture.Id(10)).Feet, moved.Candidate.Space.GetActor(AdmissionFixture.Id(10)).Feet);
        world = moved.Candidate;
        var next = Batch(world, 401, [Command(world, 30401)]);
        var worked = processor.Decide(world, next);
        EqualDecision(new WorldStepProcessor(null).Decide(world, next), worked);
        Assert.Equal(2UL, sites.Captures);
        world = worked.Candidate;
        var region = world.Space.Regions.Single();
        var space = new LocalSpatialSnapshot(world.Space.Scope, [region.Excavate(region.Revision, [new(5, 5, 0)])], world.Space.Actors);
        world = Copy(world, space: space);
        var changed = Batch(world, 402, [Command(world, 30402)]);
        EqualDecision(new WorldStepProcessor(null).Decide(world, changed), processor.Decide(world, changed));
        Assert.Equal(3UL, sites.Captures);
    }

    [Fact]
    public void Arriving_blocker_invalidates_success_and_departure_invalidates_cached_refusal()
    {
        var sites = new ConstructionSiteCache();
        var processor = new WorldStepProcessor(sites);
        var world = Create();
        world = processor.Decide(world, Batch(world, 450, [Command(world, 30450)])).Candidate;
        var other = world.Space.GetActor(AdmissionFixture.Id(10));
        var blockedActor = new ActorPresence(other.Binding, other.Condition, other.Scope,
            world.Construction!.Projects.Single().SiteFeet, other.Revision + 1, other.HomePlaceId);
        var blocked = Copy(world, space: world.Space.WithActors(world.Space.Actors.Select(actor =>
            actor.ActorId == other.ActorId ? blockedActor : actor)));
        for (ulong index = 0; index < 2; index++)
        {
            var batch = Batch(blocked, 451 + index, [Command(blocked, 30451 + index)]);
            var result = processor.Decide(blocked, batch);
            EqualDecision(new WorldStepProcessor(null).Decide(blocked, batch), result);
            Assert.Equal(WorldActionRejection.Unavailable, result.Receipts.Single().Rejection);
            Assert.Equal(world.Construction.ContentHash, result.Candidate.Construction!.ContentHash);
            blocked = result.Candidate;
        }
        Assert.Equal(2UL, sites.Captures);
        Assert.Equal(1UL, sites.Reuses);
        var returnedActor = new ActorPresence(other.Binding, other.Condition, other.Scope,
            other.Feet, blockedActor.Revision + 1, other.HomePlaceId);
        var clear = Copy(blocked, space: blocked.Space.WithActors(blocked.Space.Actors.Select(actor =>
            actor.ActorId == other.ActorId ? returnedActor : actor)));
        var resumed = Batch(clear, 453, [Command(clear, 30453)]);
        var worked = processor.Decide(clear, resumed);
        EqualDecision(new WorldStepProcessor(null).Decide(clear, resumed), worked);
        Assert.Equal(WorldActionRejection.None, worked.Receipts.Single().Rejection);
        Assert.Equal(2UL, worked.Candidate.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Equal(3UL, sites.Captures);
    }

    [Fact]
    public void Reused_geometry_does_not_waive_tool_revision_control_or_batch_freshness()
    {
        var sites = new ConstructionSiteCache();
        var processor = new WorldStepProcessor(sites);
        var source = Create();
        var originalBatch = Batch(source, 500, [Command(source, 30500)]);
        var world = processor.Decide(source, originalBatch).Candidate;
        Assert.Throws<InvalidOperationException>(() => processor.Decide(world, originalBatch));
        var valid = Command(world, 30501);
        var intent = (WorkConstructionIntent)valid.Intent;
        var stale = Command(world, 30501, intent with { ExpectedToolLotRevision = intent.ExpectedToolLotRevision + 1 });
        var foreign = new WorldActionCommand(valid.CommandId, AdmissionFixture.Id(7_700_001), valid.OriginSessionId,
            valid.ActorId, valid.WorldId, valid.BranchId, valid.ExpectedActorRevision, valid.Intent);
        foreach (var command in new[] { stale, foreign })
        {
            var batch = Batch(world, 501, [command]);
            var actual = processor.Decide(world, batch);
            EqualDecision(new WorldStepProcessor(null).Decide(world, batch), actual);
            Assert.NotEqual(WorldActionRejection.None, actual.Receipts.Single().Rejection);
            Assert.Equal(world.Construction!.ContentHash, actual.Candidate.Construction!.ContentHash);
        }
        Assert.Equal(1UL, sites.Captures);
        Assert.Equal(0UL, sites.Reuses);
    }

    [Fact]
    public void Warm_geometry_still_refuses_a_final_historical_identity_collision()
    {
        var sites = new ConstructionSiteCache();
        var processor = new WorldStepProcessor(sites);
        var world = Create();
        for (ulong step = 0; step < 199; step++)
            world = processor.Decide(world, Batch(world, 600 + step, [Command(world, 30600 + step)])).Candidate;
        var command = Command(world, 30900);
        var batch = Batch(world, 900, [command]);
        var collision = EntityId.DeriveSpawned(command.CommandId, 0);
        var result = processor.Decide(world, batch, id => id == collision);
        EqualDecision(new WorldStepProcessor(null).Decide(world, batch, id => id == collision), result);
        Assert.Equal(WorldActionRejection.Unavailable, result.Receipts.Single().Rejection);
        Assert.Equal(world.Construction!.ContentHash, result.Candidate.Construction!.ContentHash);
        Assert.Equal(world.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Empty(result.Candidate.Space.Structures);
        Assert.Equal(1UL, sites.Captures);
        Assert.Equal(199UL, sites.Reuses);
    }

    [Fact]
    public void Long_lived_processor_cache_does_not_retain_world_scene_index_or_terrain()
    {
        var (processor, sites, references) = PrepareAndRelease();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(references, reference => Assert.False(reference.IsAlive));
        var world = Create();
        var batch = Batch(world, 1000, [Command(world, 31000)]);
        EqualDecision(new WorldStepProcessor(null).Decide(world, batch), processor.Decide(world, batch));
        Assert.Equal(1UL, sites.Captures);
        Assert.Equal(1UL, sites.Reuses);
        GC.KeepAlive(processor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WorldStepProcessor, ConstructionSiteCache, WeakReference[]) PrepareAndRelease()
    {
        var sites = new ConstructionSiteCache();
        var processor = new WorldStepProcessor(sites);
        var world = Create();
        var decision = processor.Decide(world, Batch(world, 1000, [Command(world, 31000)]));
        WeakReference[] references = [new(world), new(world.Space), new(world.IdentityIndex), new(decision.Candidate),
            .. world.Space.Regions.Select(region => new WeakReference(region))];
        return (processor, sites, references);
    }

    private static WorldStepBatch Batch(WorldSnapshot world, ulong sequence, WorldActionCommand[] actions,
        WorldMovementInput[]? movement = null) => new(AdmissionFixture.Id(8_000_000 + sequence), world.WorldId,
            world.BranchId, world.Revision, world.AdvancingSteps,
            world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            movement ?? [], actions, world.ProfileVersion);

    private static void EqualDecision(WorldStepDecision expected, WorldStepDecision actual)
    {
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(actual.Candidate));
        Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts), WorldReceiptCodecV1.EncodeBatch(actual.Receipts));
        Assert.Equal(expected.Identities, actual.Identities);
    }

    private static WorldStepDecision Measure(WorldStepProcessor processor, WorldSnapshot world, WorldStepBatch batch,
        ref long bytes, ref long ticks)
    {
        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        long beforeTicks = Stopwatch.GetTimestamp();
        var decision = processor.Decide(world, batch);
        ticks += Stopwatch.GetTimestamp() - beforeTicks;
        bytes += GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
        return decision;
    }
}
