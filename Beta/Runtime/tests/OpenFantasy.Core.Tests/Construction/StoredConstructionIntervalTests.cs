using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionWorldFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class StoredConstructionIntervalTests
{
    [Fact]
    public void Consecutive_partial_steps_preserve_exact_ordinary_directory_due_owners_and_final_admission_boundary()
    {
        var world = Create();
        var interval = StoredConstructionInterval.Capture(world, Command(world));
        var payloads = ConstructionOwnerDirectory.Capture(world).Owners.ToDictionary(owner => owner.Hash, owner => owner.CopyBytes());
        var history = new HashSet<EntityId>();
        for (ulong i = 0; i < 200; i++)
        {
            var command = Command(world, 100 + i);
            var batch = Batch(world, command, i);
            var expected = new WorldStepProcessor().Decide(world, batch, history.Contains);
            var step = interval.Prepare(batch, interval.DirectoryHash, hash => payloads[hash], history.Contains);
            if (i == 199)
            {
                Assert.Null(step);
                Assert.True(expected.Candidate.Construction!.Projects.Single().IsCompleted);
                Assert.Equal(199UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
                break;
            }
            Assert.NotNull(step);
            var directory = ConstructionOwnerDirectory.Capture(expected.Candidate);
            Assert.Equal(directory.CopyBytes(), step.Candidate.CopyDirectory());
            Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts), WorldReceiptCodecV1.EncodeBatch([step.Receipt]));
            Assert.Equal(expected.Identities.OrderBy(x => x.Identity), step.Identities);
            foreach (var owner in step.ChangedOwners)
            {
                Assert.Equal(directory.Owners.Single(value => value.Key == owner.Key).CopyBytes(), owner.CopyBytes());
                payloads[owner.Hash] = owner.CopyBytes();
            }
            Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(
                ConstructionOwnerReconstruction.Decode(step.Candidate.CopyDirectory(), step.Candidate.DirectoryHash,
                    hash => payloads[hash], reference => world.Space.Regions.Single(region => TerrainRegionReference.Capture(region) == reference))));
            foreach (var identity in step.Identities) history.Add(identity.Identity);
            interval = step.Candidate;
            world = expected.Candidate;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Held_and_rejected_work_keep_ordinary_time_and_receipt_semantics(bool held)
    {
        var world = Create(held: held);
        var intent = (WorkConstructionIntent)Command(world).Intent;
        var command = Command(world, intent: intent with { ExpectedProjectRevision = 99 });
        var interval = StoredConstructionInterval.Capture(world, command);
        var payloads = ConstructionOwnerDirectory.Capture(world).Owners.ToDictionary(owner => owner.Hash, owner => owner.CopyBytes());
        var batch = Batch(world, command, 1);
        var step = interval.Prepare(batch, interval.DirectoryHash, hash => payloads[hash], _ => false)!;
        var expected = new WorldStepProcessor().Decide(world, batch);
        Assert.Equal(ConstructionOwnerDirectory.Capture(expected.Candidate).CopyBytes(), step.Candidate.CopyDirectory());
        Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single()), WorldReceiptCodecV1.Encode(step.Receipt));
        if (held) Assert.Empty(step.ChangedOwners);
        else Assert.DoesNotContain(step.ChangedOwners, owner => owner.Key.Kind == ConstructionOwnerKind.Project);
    }

    [Fact]
    public void Stale_directory_and_reused_historical_identity_cannot_prepare_a_publication()
    {
        var world = Create();
        var command = Command(world);
        var interval = StoredConstructionInterval.Capture(world, command);
        var payloads = ConstructionOwnerDirectory.Capture(world).Owners.ToDictionary(owner => owner.Hash, owner => owner.CopyBytes());
        var batch = Batch(world, command, 1);
        Assert.Throws<InvalidOperationException>(() => interval.Prepare(batch, world.ContentHash,
            _ => throw new Exception("Stale source must not read"), _ => false));
        Assert.Throws<InvalidOperationException>(() => interval.Prepare(batch, interval.DirectoryHash,
            hash => payloads[hash], id => id == batch.CauseId));
        Assert.Equal(ConstructionOwnerDirectory.Capture(world).CopyBytes(), interval.CopyDirectory());
    }

    [Fact]
    public void Another_action_refuses_as_dispatch_before_reading_any_dependencies()
    {
        var world = Create();
        var command = Command(world);
        var interval = StoredConstructionInterval.Capture(world, command);
        var other = StepFixture.Action(world, 11_000_001,
            new SpeakToActorIntent(Builder, world.Space.GetActor(Builder).Revision));
        Assert.Throws<InvalidOperationException>(() => interval.Prepare(Batch(world, other, 1), interval.DirectoryHash,
            _ => throw new Exception("Wrong action must not read"), _ => false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_or_corrupt_due_actor_payload_refuses_before_publication(bool corrupt)
    {
        var world = Create();
        var command = Command(world);
        var interval = StoredConstructionInterval.Capture(world, command);
        var directory = ConstructionOwnerDirectory.Capture(world);
        var payloads = directory.Owners.ToDictionary(owner => owner.Hash, owner => owner.CopyBytes());
        var other = directory.Owners.First(owner => owner.Key.Kind == ConstructionOwnerKind.Actor && owner.Key.SubjectId != command.ActorId);
        Assert.Throws<FormatException>(() => interval.Prepare(Batch(world, command, 1), interval.DirectoryHash, hash =>
        {
            if (hash != other.Hash) return payloads[hash];
            if (!corrupt) return null!;
            var changed = payloads[hash].ToArray(); changed[^1] ^= 1; return changed;
        }, _ => false));
    }

    [Fact]
    public void Real_depletion_regrows_during_selected_stored_work_with_all_residents_aging()
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
        var interval = StoredConstructionInterval.Capture(world, Command(world));
        var payloads = ConstructionOwnerDirectory.Capture(world).Owners.ToDictionary(owner => owner.Hash, owner => owner.CopyBytes());
        for (ulong i = 0; i < 8; i++)
        {
            var batch = Batch(world, Command(world, 600 + i), 600 + i);
            var step = interval.Prepare(batch, interval.DirectoryHash, hash => payloads[hash], _ => false)!;
            foreach (var owner in step.ChangedOwners) payloads[owner.Hash] = owner.CopyBytes();
            var expected = processor.Decide(world, batch).Candidate;
            var restored = ConstructionOwnerReconstruction.Decode(step.Candidate.CopyDirectory(), step.Candidate.DirectoryHash,
                hash => payloads[hash], reference => world.Space.Regions.Single(region => TerrainRegionReference.Capture(region) == reference));
            Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(restored));
            var node = restored.Gathering!.GetNode(GatheringFixture.Node);
            Assert.Equal(depleted.NextUnissuedUnit, node.NextUnissuedUnit);
            Assert.Equal(restored.AdvancingSteps < due ? 0 : node.Definition.MaxSwings, node.RemainingSwings);
            Assert.All(restored.Residents, resident => Assert.True(resident.ExperiencedTime.TotalMilliseconds
                > world.GetResident(resident.ActorId).ExperiencedTime.TotalMilliseconds));
            world = expected;
            interval = step.Candidate;
        }
    }

    private static WorldStepBatch Batch(WorldSnapshot world, WorldActionCommand command, ulong sequence) =>
        new(ConstructionFixture.Id(90_000 + sequence), world.WorldId, world.BranchId, world.Revision,
            world.AdvancingSteps, world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep, [], [command], 5);
}
