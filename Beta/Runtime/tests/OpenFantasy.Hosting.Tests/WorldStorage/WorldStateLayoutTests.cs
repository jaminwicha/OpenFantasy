using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class WorldStateLayoutTests
{
    [Fact]
    public void Layout_covers_all_bytes_and_does_not_treat_framing_as_payload()
    {
        var world = WorldServiceFixture.Genesis();
        var layout = new WorldStateLayout(world);
        Assert.Equal(WorldSnapshotCodec.Encode(world).Length, layout.ByteLength);
        Assert.Equal(world.ContentHash, layout.ContentHash);
        Assert.Equal(layout.ByteLength, layout.FramingBytes + layout.Fields.Sum(field => field.PayloadBytes));
        var equal = new WorldStateLayout(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world)));
        var reuse = equal.CompareTo(layout);
        Assert.Empty(reuse.ChangedFields);
        Assert.Equal(0, reuse.ChangedPayloadBytes);
        Assert.Equal(layout.ByteLength - layout.FramingBytes, reuse.IdenticalPayloadBytes);
    }

    [Fact]
    public void Actual_movement_changes_space_and_time_but_preserves_custody_bytes()
    {
        var world = WorldServiceFixture.Genesis();
        var moved = new WorldStepProcessor().Decide(world, WorldServiceFixture.Step(world,
            movement: [new(WorldServiceFixture.Delivery, WorldServiceFixture.Id(10), 32, 0)])).Candidate;
        Assert.Equal(32, moved.Space.GetActor(WorldServiceFixture.Id(10)).Feet.X);
        var before = new WorldStateLayout(world);
        var after = new WorldStateLayout(moved);
        var reuse = after.CompareTo(before);
        Assert.Contains(3, reuse.ChangedFields); // Spatial field in this actual profile-one encoder.
        Assert.DoesNotContain(2, reuse.ChangedFields); // Complete counted custody/control remains unchanged.
        Assert.NotEqual(before.ContentHash, after.ContentHash);
        Assert.Equal(after.ByteLength, after.FramingBytes + reuse.ChangedPayloadBytes + reuse.IdenticalPayloadBytes);
        Assert.Equal(AuthorityEncodingV1.Encode(world.Inventory), AuthorityEncodingV1.Encode(moved.Inventory));
    }

    [Fact]
    public void Comparing_unrelated_worlds_or_interpretations_refuses_positional_reuse_claims()
    {
        var one = AdventureWorldAdmission.CreateEmbedded("layout-one", ["Rowan", "Mira"]);
        var other = AdventureWorldAdmission.CreateEmbedded("layout-other", ["Rowan", "Mira"]);
        Assert.Throws<ArgumentException>(() => new WorldStateLayout(one).CompareTo(new WorldStateLayout(other)));
        var upgraded = WorldPartyWithdrawalAdmission.CreateFresh(one);
        Assert.Throws<ArgumentException>(() => new WorldStateLayout(upgraded).CompareTo(new WorldStateLayout(one)));
    }

    [Fact]
    public void Measures_real_movement_and_funded_construction_without_claiming_partition_feasibility()
    {
        string? output = Environment.GetEnvironmentVariable("OPENFANTASY_STATE_LAYOUT_EVIDENCE");
        var watch = Stopwatch.StartNew();
        var rows = new List<object>();
        var adventure = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan", "Mira"]);
        EntityId player = adventure.Excavation!.Sites.Single().OwnerActorId;
        var binding = adventure.Inventory.Controls.Single(row => row.ActorId == player);
        var batch = new WorldStepBatch(new(0, 8_800_001), adventure.WorldId, adventure.BranchId,
            adventure.Revision, adventure.AdvancingSteps, WorldBatchKind.AdvancingStep,
            [new(new(binding.PrincipalId, binding.SessionId), player, 32, 0)], [], adventure.ProfileVersion);
        var moved = new WorldStepProcessor().Decide(adventure, batch).Candidate;
        Assert.NotEqual(adventure.Space.GetActor(player).Feet, moved.Space.GetActor(player).Feet);
        rows.Add(Measure("two-player-adventure-movement", adventure, moved));

        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        var original = fixture.CreateSource(source.Path, completeQuest: true);
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.UpgradeQuestsToConstruction(
            source.Path, destination.Path, fixture.Request(original)))
        {
            var world = authority.ReadStateForHost();
            player = world.Residents.Single(row => row.ControllerKind == ResidentControllerKind.Player).ActorId;
            world = fixture.Act(authority, world, player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
            var controller = new LocalNpcController();
            WorldSnapshot? beforeWork = null;
            WorldSnapshot? partial = null;
            for (int step = 0; !world.Construction!.Projects.Single().IsCompleted; step++)
            {
                Assert.True(step < 512);
                var before = world;
                var decision = authority.Commit(fixture.Batch(world,
                    controller.SelectActions(world, fixture.NextIdentity), controller.SelectMovement(world)));
                Assert.All(decision.Receipts, row => Assert.Equal(CommandReceiptStatus.Applied, row.Status));
                world = decision.Candidate;
                if (world.Construction!.Projects.Single().Progress.CompletedUnits == 1 && beforeWork is null)
                {
                    beforeWork = before;
                    rows.Add(Measure("first-funded-work-step", before, world));
                }
                if (world.Construction!.Projects.Single().Progress.CompletedUnits == 40 && partial is null)
                {
                    partial = world;
                    rows.Add(Measure("forty-funded-work-steps", Assert.IsType<WorldSnapshot>(beforeWork), world));
                }
                if (world.Construction!.Projects.Single().IsCompleted)
                    rows.Add(Measure("funded-object-completion", before, world));
            }
            Assert.NotNull(partial);
            Assert.Single(world.Space.Structures);
            final = world;
        }
        var restore = Stopwatch.StartNew();
        using var recovered = SqliteWorldAuthority.OpenExisting(destination.Path);
        Assert.Equal(final.ContentHash, recovered.ReadStateForHost().ContentHash);
        double restoreMilliseconds = restore.Elapsed.TotalMilliseconds;
        var currentConstruction = MeasureCurrentConstruction(rows);
        Assert.True(watch.Elapsed.TotalSeconds < 120);
        if (output is not null)
        {
            File.WriteAllText(output, JsonSerializer.Serialize(new
            {
                Kind = "complete-root-layout-baseline", Runtime = RuntimeInformation.FrameworkDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(), RuntimeInformation.OSDescription,
                Processors = Environment.ProcessorCount, Seconds = watch.Elapsed.TotalSeconds,
                RestoredRevision = final.Revision, RestoreMilliseconds = restoreMilliseconds,
                CurrentConstruction = currentConstruction, Rows = rows,
                Limits = "Complete resident roots. No partial residency, partitioned atomicity, offscreen equivalence, manifest format or production scale is proved."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static object MeasureCurrentConstruction(List<object> rows)
    {
        using var files = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        var genesis = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan"]);
        var supplied = fixture.CreateSource(files.Path, completeQuest: true, genesis: genesis);
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var player = supplied.Residents.Single(row => row.ControllerKind == ResidentControllerKind.Player).ActorId;
            var world = fixture.Act(authority, supplied, player,
                new CloseDialogueIntent(supplied.Dialogues.Single().DialogueId));
            var controller = new LocalNpcController();
            WorldSnapshot? first = null;
            bool measuredPartial = false;
            for (int step = 0; !world.Construction!.Projects.Single().IsCompleted; step++)
            {
                Assert.True(step < 512);
                var before = world;
                var decision = authority.Commit(fixture.Batch(world,
                    controller.SelectActions(world, fixture.NextIdentity), controller.SelectMovement(world)));
                Assert.All(decision.Receipts, row => Assert.Equal(CommandReceiptStatus.Applied, row.Status));
                world = decision.Candidate;
                ulong work = world.Construction!.Projects.Single().Progress.CompletedUnits;
                if (work == 1 && first is null)
                {
                    first = before;
                    rows.Add(Measure("adventure-first-funded-work-step", before, world));
                }
                if (work == 40 && !measuredPartial)
                {
                    measuredPartial = true;
                    rows.Add(Measure("adventure-forty-funded-work-steps", Assert.IsType<WorldSnapshot>(first), world));
                }
                if (world.Construction.Projects.Single().IsCompleted)
                    rows.Add(Measure("adventure-funded-object-completion", before, world));
            }
            Assert.True(measuredPartial);
            Assert.Single(world.Space.Structures);
            Assert.Equal(supplied.Quests!.ContentHash, world.Quests!.ContentHash);
            Assert.Equal(supplied.Progression!.ContentHash, world.Progression!.ContentHash);
            final = world;
        }
        var restore = Stopwatch.StartNew();
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(final.ContentHash, recovered.ReadStateForHost().ContentHash);
        return new { final.ProfileVersion, final.Revision, RestoreMilliseconds = restore.Elapsed.TotalMilliseconds };
    }

    private static object Measure(string name, WorldSnapshot before, WorldSnapshot after)
    {
        var previous = new WorldStateLayout(before);
        var current = new WorldStateLayout(after);
        var reuse = current.CompareTo(previous);
        for (int warmup = 0; warmup < 3; warmup++) WorldSnapshotCodec.Encode(after);
        const int iterations = 25;
        long startBytes = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        for (int iteration = 0; iteration < iterations; iteration++) WorldSnapshotCodec.Encode(after);
        double encodeMilliseconds = clock.Elapsed.TotalMilliseconds;
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - startBytes;
        return new
        {
            Name = name, after.ProfileVersion, BeforeRevision = before.Revision, after.Revision,
            Actors = after.Residents.Count, TerrainRegions = after.Space.Regions.Count,
            CountedLots = after.Inventory.Custody.Lots.Count, MaterialLots = after.Production?.Lots.Count,
            CompletedWork = after.Construction?.Projects.Single().Progress.CompletedUnits,
            BeforeBytes = previous.ByteLength, AfterBytes = current.ByteLength, current.FramingBytes,
            Fields = current.Fields, Reuse = reuse, Iterations = iterations, EncodeMilliseconds = encodeMilliseconds,
            AllocatedBytes = allocatedBytes,
            Measurement = "Warmed encoder only; layout/hash inspection and world preparation excluded. Stopwatch allocation included. Payload reuse excludes all reference/index/transaction costs."
        };
    }
}
