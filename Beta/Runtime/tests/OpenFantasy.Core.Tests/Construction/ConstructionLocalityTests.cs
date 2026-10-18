using System.Runtime.CompilerServices;
using System.Diagnostics;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionWorldFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionLocalityTests(ITestOutputHelper output)
{
    [Fact]
    public void All_200_selected_steps_complete_after_source_world_and_terrain_collection_then_reconstruct_exact_history()
    {
        var (state, references) = CaptureWithoutSource();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(references, reference => Assert.False(reference.IsAlive));
        var initialHash = state.SourceWorldHash;
        int initialIdentities = state.RetainedIdentityCount;
        long preparedBytes = 0, preparedTicks = 0, ordinaryBytes = 0, ordinaryTicks = 0;
        List<(WorldStepBatch Batch, ContentHash Owners, byte[] Receipt, WorldCommittedIdentity[] Identities)> observed = [];
        for (ulong step = 0; step < 200; step++)
        {
            var batch = Batch(state, step);
            long startBytes = GC.GetAllocatedBytesForCurrentThread();
            long startTicks = Stopwatch.GetTimestamp();
            var prepared = state.PrepareStep(batch, state.Occupancy, state.Terrain, _ => false);
            preparedTicks += Stopwatch.GetTimestamp() - startTicks;
            preparedBytes += GC.GetAllocatedBytesForCurrentThread() - startBytes;
            Assert.Same(state, prepared.Source);
            Assert.Equal(WorldActionRejection.None, prepared.Receipt.Rejection);
            Assert.Equal(step + 1, prepared.Candidate.Construction.Projects.Single().Progress.CompletedUnits);
            observed.Add((batch, OwnerHash(prepared.Candidate), WorldReceiptCodecV1.Encode(prepared.Receipt), prepared.Identities.ToArray()));
            state = prepared.Candidate;
        }
        Assert.All(references, reference => Assert.False(reference.IsAlive));
        Assert.Equal(initialIdentities + 604, state.RetainedIdentityCount);
        Assert.Single(state.Production.Batches);
        Assert.Equal(1UL, Assert.Single(state.Production.Lots, lot => lot.ReservedBy is not null).Quantity);
        Assert.Equal(3UL, Assert.Single(state.Production.Lots, lot => lot.ReservedBy is null).Quantity);

        // Build the complete-world oracle only after the entire terrain-free interval has finished.
        var ordinary = Create();
        Assert.Equal(initialHash, ordinary.ContentHash);
        var terrain = ordinary.Space.Regions;
        var processor = new WorldStepProcessor();
        HashSet<EntityId> history = [];
        foreach (var item in observed)
        {
            long startBytes = GC.GetAllocatedBytesForCurrentThread();
            long startTicks = Stopwatch.GetTimestamp();
            var next = processor.Decide(ordinary, item.Batch, history.Contains);
            ordinaryTicks += Stopwatch.GetTimestamp() - startTicks;
            ordinaryBytes += GC.GetAllocatedBytesForCurrentThread() - startBytes;
            Assert.Equal(item.Owners, OwnerHash(next.Candidate));
            Assert.Equal(item.Receipt, WorldReceiptCodecV1.Encode(Assert.Single(next.Receipts)));
            Assert.Equal(item.Identities, next.Identities);
            history.UnionWith(next.Identities.Select(value => value.Identity));
            ordinary = next.Candidate;
        }
        Assert.Equal(WorldSnapshotCodec.Encode(ordinary), WorldSnapshotCodec.Encode(state.Rehydrate(terrain)));
        output.WriteLine($"Terrain-free interval: {observed.Count} selected steps, {state.Actors.Count} actors, {state.Terrain.Count} terrain references, {state.Occupancy.RetainedPageCount} occupancy pages, {initialIdentities}->{state.RetainedIdentityCount} retained identities; no source/scene/index/terrain references alive before or after execution.");
        output.WriteLine($"Sequential diagnostic (continuation runs first; excludes capture, hashing assertions and reconstruction): continuation {preparedBytes} bytes/{preparedTicks * 1000.0 / Stopwatch.Frequency:F3} ms; ordinary {ordinaryBytes} bytes/{ordinaryTicks * 1000.0 / Stopwatch.Frequency:F3} ms. No persistence or speed qualification.");
        GC.KeepAlive(state);
    }

    [Fact]
    public void Preparation_is_repeatable_but_old_batches_and_reused_command_identities_cannot_earn_more_work()
    {
        var world = Create();
        var state = Capture(world);
        var batch = Batch(state, 300);
        var first = state.PrepareStep(batch, state.Occupancy, state.Terrain, _ => false);
        var second = state.PrepareStep(batch, state.Occupancy, state.Terrain, _ => false);
        Assert.Equal(OwnerHash(first.Candidate), OwnerHash(second.Candidate));
        Assert.Equal(0UL, state.Construction.Projects.Single().Progress.CompletedUnits);
        var next = first.Candidate;
        Assert.Throws<InvalidOperationException>(() => next.PrepareStep(batch, next.Occupancy, next.Terrain, _ => false));
        var reused = new WorldStepBatch(AdmissionFixture.Id(9_100_301), world.WorldId, world.BranchId,
            next.Revision, next.AdvancingSteps, WorldBatchKind.AdvancingStep, [], batch.Actions, 5);
        Assert.Throws<InvalidOperationException>(() => next.PrepareStep(reused, next.Occupancy, next.Terrain, _ => false));
        Assert.Equal(1UL, next.Construction.Projects.Single().Progress.CompletedUnits);
    }

    [Fact]
    public void Changed_population_or_terrain_requires_actual_readmission_before_continuing()
    {
        var world = Create();
        var state = Capture(world);
        var batch = Batch(state, 400);
        var arrival = state.Occupancy.Apply(0, [new(null, SpatialOccupancyReference.Capture(AdmissionFixture.Presence(2)))]);
        Assert.Throws<InvalidOperationException>(() => state.PrepareStep(batch, arrival, state.Terrain, _ => false));
        var region = world.Space.Regions.Single();
        var altered = region.Excavate(region.Revision, [new(5, 5, 0)]);
        Assert.Throws<InvalidOperationException>(() => state.PrepareStep(batch, state.Occupancy,
            [OpenFantasy.Core.Terrain.TerrainRegionReference.Capture(altered)], _ => false));
        Assert.Throws<InvalidOperationException>(() => state.Rehydrate([]));
        Assert.Throws<InvalidOperationException>(() => state.Rehydrate([altered]));
        Assert.Equal(world.ContentHash, state.Rehydrate(world.Space.Regions).ContentHash);

        var current = Copy(world, space: new LocalSpatialSnapshot(world.Space.Scope, [altered], world.Space.Actors));
        var refreshed = Capture(current);
        var selected = Batch(refreshed, 401);
        var resumed = refreshed.PrepareStep(selected, refreshed.Occupancy, refreshed.Terrain, _ => false);
        var expected = new WorldStepProcessor().Decide(current, selected);
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(resumed.Candidate.Rehydrate([altered])));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Holds_and_unfunded_work_preserve_ordinary_clock_and_refusal_semantics(bool funded, bool held)
    {
        var world = Create(completed: funded, held: held);
        var state = Capture(world);
        var batch = Batch(state, 500);
        var result = state.PrepareStep(batch, state.Occupancy, state.Terrain, _ => false);
        var expected = new WorldStepProcessor().Decide(world, batch);
        Assert.Equal(WorldActionRejection.Unavailable, result.Receipt.Rejection);
        Assert.Equal(world.Construction!.ContentHash, result.Candidate.Construction.ContentHash);
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(result.Candidate.Rehydrate(world.Space.Regions)));
        Assert.Equal(held ? world.AdvancingSteps : world.AdvancingSteps + 1, result.Candidate.AdvancingSteps);
    }

    [Fact]
    public void Current_control_and_tool_revisions_are_checked_from_retained_actual_owners()
    {
        var world = Create();
        var state = Capture(world);
        var original = Batch(state, 550);
        var command = original.Actions.Single();
        var intent = (WorkConstructionIntent)command.Intent;
        foreach (var altered in new[]
        {
            new WorldActionCommand(command.CommandId, AdmissionFixture.Id(12_000_001), command.OriginSessionId,
                command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision, intent),
            new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
                command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision,
                intent with { ExpectedToolLotRevision = intent.ExpectedToolLotRevision + 1 })
        })
        {
            var batch = new WorldStepBatch(original.CauseId, original.WorldId, original.BranchId, original.ExpectedRevision,
                original.ExpectedAdvancingSteps, original.Kind, [], [altered], 5);
            var actual = state.PrepareStep(batch, state.Occupancy, state.Terrain, _ => false);
            var expected = new WorldStepProcessor().Decide(world, batch);
            Assert.NotEqual(WorldActionRejection.None, actual.Receipt.Rejection);
            Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single()), WorldReceiptCodecV1.Encode(actual.Receipt));
            Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(actual.Candidate.Rehydrate(world.Space.Regions)));
        }
    }

    [Fact]
    public void A_genuinely_depleted_node_regrows_on_its_due_step_during_the_same_work_interval()
    {
        var world = Create();
        var processor = new WorldStepProcessor();
        ulong identity = 10_000_000;
        int harvestingSteps = 0;
        while (world.Gathering!.GetNode(GatheringFixture.Node).RemainingSwings != 0)
        {
            Assert.True(harvestingSteps++ < 512, "The real gathering prerequisite must deplete within its existing finite effort budget.");
            var actions = world.Gathering.GetActorWork(AdmissionFixture.Id(10)).Effort >= GatherRulesV1.EffortPerSwing
                ? new[] { GatheringFixture.Command(world, identity++) } : [];
            var harvested = processor.Decide(world, new WorldStepBatch(AdmissionFixture.Id(identity++), world.WorldId, world.BranchId,
                world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], actions, 5));
            Assert.All(harvested.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            world = harvested.Candidate;
        }
        var depleted = world.Gathering.GetNode(GatheringFixture.Node);
        ulong due = depleted.RespawnAtAdvancingStep!.Value;
        while (world.AdvancingSteps < due - 5)
            world = processor.Decide(world, new WorldStepBatch(AdmissionFixture.Id(identity++), world.WorldId, world.BranchId,
                world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], 5)).Candidate;
        var state = Capture(world);
        var terrain = world.Space.Regions;
        for (ulong step = 0; step < 8; step++)
        {
            var batch = Batch(state, 600 + step);
            state = state.PrepareStep(batch, state.Occupancy, state.Terrain, _ => false).Candidate;
            world = processor.Decide(world, batch).Candidate;
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(state.Rehydrate(terrain)));
            var node = state.Gathering!.GetNode(GatheringFixture.Node);
            Assert.Equal(depleted.NextUnissuedUnit, node.NextUnissuedUnit);
            Assert.Equal(state.AdvancingSteps < due ? 0 : node.Definition.MaxSwings, node.RemainingSwings);
        }
    }

    [Fact]
    public void Unsupported_source_profiles_movement_and_other_actions_are_explicit_preparation_refusals()
    {
        Assert.Throws<ArgumentException>(() => Capture(Basis()));
        var world = Create();
        var state = Capture(world);
        var command = StepFixture.Action(world, 11_000_001,
            new SpeakToActorIntent(Builder, world.Space.GetActor(Builder).Revision));
        var action = new WorldStepBatch(AdmissionFixture.Id(11_000_002), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [command], 5);
        var movement = new WorldStepBatch(AdmissionFixture.Id(11_000_003), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [StepFixture.Move(world, 10)], [], 5);
        Assert.Throws<InvalidOperationException>(() => state.PrepareStep(action, state.Occupancy, state.Terrain, _ => false));
        Assert.Throws<InvalidOperationException>(() => state.PrepareStep(movement, state.Occupancy, state.Terrain, _ => false));
        Assert.Equal(world.ContentHash, state.Rehydrate(world.Space.Regions).ContentHash);
    }

    [Fact]
    public void Final_identity_conflict_keeps_work_and_material_then_a_new_selection_completes_once()
    {
        var world = Create();
        var state = Capture(world);
        for (ulong step = 0; step < 199; step++)
        {
            var selected = Batch(state, 1000 + step);
            state = state.PrepareStep(selected, state.Occupancy, state.Terrain, _ => false).Candidate;
        }
        var batch = Batch(state, 1200);
        var occupied = EntityId.DeriveSpawned(batch.Actions.Single().CommandId, 0);
        var refused = state.PrepareStep(batch, state.Occupancy, state.Terrain, id => id == occupied);
        Assert.Equal(WorldActionRejection.Unavailable, refused.Receipt.Rejection);
        Assert.Equal(state.Inventory.ContentHash, refused.Candidate.Inventory.ContentHash);
        Assert.Equal(state.Construction.ContentHash, refused.Candidate.Construction.ContentHash);
        state = refused.Candidate;
        var final = Batch(state, 1201);
        state = state.PrepareStep(final, state.Occupancy, state.Terrain, _ => false).Candidate;
        var repeated = Batch(state, 1202);
        var after = state.PrepareStep(repeated, state.Occupancy, state.Terrain, _ => false);
        Assert.Equal(WorldActionRejection.Unavailable, after.Receipt.Rejection);
        Assert.Equal(state.Production.ContentHash, after.Candidate.Production.ContentHash);
        Assert.Equal(200UL, after.Candidate.Construction.Projects.Single().Progress.CompletedUnits);
        Assert.Single(after.Candidate.Rehydrate(world.Space.Regions).Space.Structures);
    }

    private static ConstructionLocalityState Capture(WorldSnapshot world) =>
        ConstructionLocalityState.Capture(world, ConstructionFixture.Id(70), 128);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ConstructionLocalityState, WeakReference[]) CaptureWithoutSource()
    {
        var world = Create();
        return (Capture(world), [new(world), new(world.Space), new(world.IdentityIndex),
            .. world.Space.Regions.Select(region => new WeakReference(region))]);
    }

    private static WorldStepBatch Batch(ConstructionLocalityState state, ulong sequence)
    {
        var actor = state.Actors.Single(value => value.ActorId == Builder);
        var control = state.Inventory.Controls.Single(value => value.ActorId == Builder);
        var project = state.Construction.Projects.Single(value => value.ProjectId == state.ProjectId);
        var command = new WorldActionCommand(AdmissionFixture.Id(9_000_000 + sequence), control.PrincipalId, control.SessionId,
            Builder, state.Inventory.WorldId, state.Inventory.BranchId, actor.Revision,
            new WorkConstructionIntent(project.ProjectId, project.Revision,
                state.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, KitLot,
                state.Inventory.Custody.GetLot(KitLot).Revision));
        return new(AdmissionFixture.Id(9_100_000 + sequence), state.Inventory.WorldId, state.Inventory.BranchId,
            state.Revision, state.AdvancingSteps, state.Dialogues.Count == 0 ? WorldBatchKind.AdvancingStep : WorldBatchKind.HeldInteraction,
            [], [command], 5);
    }

    private static ContentHash OwnerHash(ConstructionLocalityState state) => OwnerHash(state.Inventory.ContentHash,
        state.Production.ContentHash, state.Construction.ContentHash, state.Progression.ContentHash, state.Quests.ContentHash,
        state.Gathering!.ContentHash, state.Residents, state.Calendar, state.Revision, state.AdvancingSteps);

    private static ContentHash OwnerHash(WorldSnapshot world) => OwnerHash(world.Inventory.ContentHash,
        world.Production!.ContentHash, world.Construction!.ContentHash, world.Progression!.ContentHash, world.Quests!.ContentHash,
        world.Gathering!.ContentHash, world.Residents, world.Calendar, world.Revision, world.AdvancingSteps);

    private static ContentHash OwnerHash(ContentHash inventory, ContentHash production, ContentHash construction,
        ContentHash progression, ContentHash quests, ContentHash gathering, IEnumerable<WorldResident> residents,
        WorldInstant calendar, ulong revision, ulong steps) => ContentHash.Compute(CanonicalEncodingV1.Frame("test.locality-owners",
            inventory.ToByteArray(), production.ToByteArray(), construction.ToByteArray(), progression.ToByteArray(),
            quests.ToByteArray(), gathering.ToByteArray(),
            CanonicalEncodingV1.Frame("test.residents", residents.Select(value => (ReadOnlyMemory<byte>)WorldResidentCodecV1.Encode(value)).ToArray()),
            CanonicalEncodingV1.Unsigned64(unchecked((ulong)calendar.Days)), CanonicalEncodingV1.Unsigned64((ulong)calendar.MillisecondsWithinDay),
            CanonicalEncodingV1.Unsigned64(revision), CanonicalEncodingV1.Unsigned64(steps)));
}
