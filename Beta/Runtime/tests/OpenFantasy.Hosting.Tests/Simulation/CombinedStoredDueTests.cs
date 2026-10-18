using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed partial class CombinedStoredDueTests
{
    private static readonly Lazy<DueFixture> Fixture = new(Prepare);
    private ulong _identity = 96_000_000;
    private EntityId NextIdentity() => new(0x6375746f66666475, _identity++);

    [Fact]
    public void Actual_resident_roster_harvest_and_work_prefix_reaches_the_unshortened_due_boundary()
    {
        var fixture = Fixture.Value;
        var world = fixture.World;
        var node = world.Gathering!.GetNode(fixture.NodeId);
        Assert.Equal(5, world.Residents.Count);
        var original = OpenFantasy.Hosting.Content.RestorativeJourneyAdmission.CreateEmbedded("coordinated-concurrent-construction", "Traveler");
        Assert.Equal(original.Residents.Select(resident => resident.ActorId).Append(fixture.Observer).Order(),
            world.Residents.Select(resident => resident.ActorId).Order());
        Assert.Equal(2, Select(world).Count);
        Assert.DoesNotContain(world.Construction!.Projects, project => project.BuilderActorId == fixture.Observer);
        Assert.Equal(0, node.RemainingSwings);
        Assert.Equal(world.AdvancingSteps + 1, node.RespawnAtAdvancingStep);
        Assert.Equal(node.Definition.RegrowthDelaySteps, fixture.Due - fixture.DepletedAt);
        Assert.True(fixture.IssuedUnits > 0);
        Assert.Equal(fixture.IssuedUnits, HarvestedUnits(world, fixture));
        Assert.All(world.Construction.Projects, project =>
        {
            Assert.True(project.Progress.CompletedUnits > 0);
            Assert.True(project.Progress.CompletedUnits + 8 < project.Definition.RequiredWork.Value);
            Assert.Equal(project.WorkFeet, world.Space.GetActor(project.BuilderActorId).Feet);
        });
        Assert.All(fixture.Batches.SelectMany(batch => batch.Movement), input =>
            Assert.Equal(32L, Math.Abs(input.DeltaX) + Math.Abs(input.DeltaY)));
    }

    [Fact]
    public void Sliced_multi_actor_work_regrows_the_depleted_node_and_ages_the_unselected_resident()
    {
        var fixture = Fixture.Value;
        var world = fixture.World;
        var interval = StoredConstructionInterval.Capture(world, Select(world));
        var directory = ConstructionOwnerDirectory.Capture(world);
        var payloads = directory.Owners.ToDictionary(owner => owner.Hash, owner => owner.CopyBytes());
        var observerBag = directory.Owners.Single(owner => owner.Key.Kind == ConstructionOwnerKind.Custodian
            && owner.Key.SubjectId == world.GetResident(fixture.Observer).InventoryCustodianId).Hash;
        var history = fixture.Identities.Concat(world.IdentityIndex.CopyEntityReservations()).ToHashSet();
        for (int step = 0; step < 8; step++)
        {
            var batch = Batch(world, Select(world));
            var expected = new WorldStepProcessor().Decide(world, batch, history.Contains);
            var preparation = interval.BeginPreparation(batch, interval.DirectoryHash, history.Contains);
            int reads = 0;
            while (!preparation.IsReady)
            {
                Assert.Throws<InvalidOperationException>(() => preparation.Complete(interval.DirectoryHash));
                preparation.ReadNext(interval.DirectoryHash, 1, hash =>
                {
                    Assert.NotEqual(observerBag, hash);
                    reads++;
                    return payloads[hash];
                });
                Assert.Equal(world.AdvancingSteps, interval.AdvancingSteps);
            }
            Assert.Equal(interval.RequiredOwnerCount, reads);
            Assert.True(reads < interval.CompleteOwnerCount);
            var stored = Assert.IsType<StoredConstructionStep>(preparation.Complete(interval.DirectoryHash));
            Assert.Equal(ConstructionOwnerDirectory.Capture(expected.Candidate).CopyBytes(), stored.Candidate.CopyDirectory());
            Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts), WorldReceiptCodecV1.EncodeBatch(stored.Receipts));
            Assert.Equal(expected.Identities.OrderBy(identity => identity.Identity), stored.Identities);
            foreach (var owner in stored.ChangedOwners) payloads[owner.Hash] = owner.CopyBytes();
            var reconstructed = ConstructionOwnerReconstruction.Decode(stored.Candidate.CopyDirectory(), stored.Candidate.DirectoryHash,
                hash => payloads[hash], reference => world.Space.Regions.Single(region => TerrainRegionReference.Capture(region) == reference));
            Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(reconstructed));
            AssertCombinedBoundary(world, reconstructed, fixture);
            history.UnionWith(stored.Identities.Select(identity => identity.Identity));
            interval = stored.Candidate;
            world = reconstructed;
        }
    }

    [Fact]
    public void An_unselected_residents_missing_payload_prevents_the_whole_due_boundary()
    {
        var fixture = Fixture.Value;
        var world = fixture.World;
        var commands = Select(world);
        var interval = StoredConstructionInterval.Capture(world, commands);
        var preparation = interval.BeginPreparation(Batch(world, commands), interval.DirectoryHash, _ => false);
        var directory = ConstructionOwnerDirectory.Capture(world);
        var missing = directory.Owners.Single(owner => owner.Key == new ConstructionOwnerKey(ConstructionOwnerKind.Actor, fixture.Observer)).Hash;
        bool requested = false;
        Assert.Throws<FormatException>(() => preparation.ReadNext(interval.DirectoryHash, interval.RequiredOwnerCount, hash =>
        {
            if (hash == missing) { requested = true; return null!; }
            return directory.Owners.Single(owner => owner.Hash == hash).CopyBytes();
        }));
        Assert.True(requested);
        Assert.False(preparation.IsReady);
        Assert.Throws<InvalidOperationException>(() => preparation.Complete(interval.DirectoryHash));
        Assert.Equal(directory.CopyBytes(), interval.CopyDirectory());
        Assert.Equal(0, world.Gathering!.GetNode(fixture.NodeId).RemainingSwings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void Regrowth_bystander_exposure_and_both_work_receipts_share_one_saved_transaction(int? faultValue)
    {
        var fixture = Fixture.Value;
        var world = fixture.World;
        var commands = Select(world);
        var batch = Batch(world, commands);
        var expected = new WorldStepProcessor().Decide(world, batch, fixture.Identities.Contains);
        AuthorityCommitBoundary? fault = faultValue is { } value ? (AuthorityCommitBoundary)value : null;
        using var files = new RetainedFiles(fault?.ToString() ?? "ordinary");
        int readCount;
        int completeCount;
        using (var authority = SqliteWorldAuthority.CreateWithTypedOwners(files.Path, fixture.Genesis))
        {
            foreach (var prior in fixture.Batches) authority.Commit(prior);
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            var interval = authority.EvictForStoredConstruction(commands);
            var observerBag = ConstructionOwnerDirectory.Capture(world).Owners.Single(owner =>
                owner.Key == new ConstructionOwnerKey(ConstructionOwnerKind.Custodian,
                    world.GetResident(fixture.Observer).InventoryCustodianId)).Hash;
            int reads = authority.OwnerArchiveReads;
            authority.BeforeTerrainRead = () => throw new IOException("Due partial work must not fetch terrain");
            authority.BeforeOwnerRead = hash => Assert.NotEqual(observerBag, hash);
            if (fault is null)
                Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts), WorldReceiptCodecV1.EncodeBatch(authority.CommitStoredConstructionBatch(batch)));
            else
            {
                Assert.Throws<IOException>(() => authority.CommitStoredConstructionBatch(batch, at =>
                {
                    if (at == fault) throw new IOException("Combined due-boundary publication fault");
                }));
                Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
            }
            readCount = authority.OwnerArchiveReads - reads;
            completeCount = interval.CompleteOwnerCount;
            Assert.Equal(interval.RequiredOwnerCount, readCount);
            Assert.True(readCount < completeCount);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        bool published = fault != AuthorityCommitBoundary.BeforeCommit;
        Assert.Equal(WorldSnapshotCodec.Encode(published ? expected.Candidate : world), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Genesis), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(0).State));
        Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(world.Revision).State));
        foreach (var command in commands)
        {
            var receipt = reopened.FindReceipt(command, ConstructionUpgradeFixture.Delivery(world, command.ActorId));
            if (!published) Assert.Null(receipt);
            else Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single(result => result.CommandId == command.CommandId)),
                WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(receipt)));
        }
        if (!published)
        {
            reopened.EvictForStoredConstruction(commands);
            reopened.CommitStoredConstructionBatch(batch);
        }
        var after = reopened.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(after));
        AssertCombinedBoundary(world, after, fixture);
        File.WriteAllBytes(System.IO.Path.Combine(files.Directory, "genesis.ofc"), WorldSnapshotCodec.Encode(fixture.Genesis));
        File.WriteAllBytes(System.IO.Path.Combine(files.Directory, "before.ofc"), WorldSnapshotCodec.Encode(world));
        File.WriteAllBytes(System.IO.Path.Combine(files.Directory, "after.ofc"), WorldSnapshotCodec.Encode(after));
        File.WriteAllText(System.IO.Path.Combine(files.Directory, "measurement.json"), JsonSerializer.Serialize(new
        {
            Residents = world.Residents.Count, Builders = commands.Count, PrefixCommits = fixture.Batches.Length,
            DueStep = fixture.Due, DepletedAtStep = fixture.DepletedAt, RegrowthDelay = fixture.Due - fixture.DepletedAt,
            IssuedUnits = fixture.IssuedUnits, SelectedOwnerReads = readCount, CompleteOwnerCount = completeCount,
            TerrainReadsDuringPartialWork = 0, SourceSequence = world.Revision, ResultSequence = after.Revision,
            SourceHash = world.ContentHash.ToString(), ResultHash = after.ContentHash.ToString()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static DueFixture Prepare()
    {
        var journey = new CoordinatedConstructionJourney(coordinated: false, hostBuilders: true,
            constructionOnly: true, withBystander: true);
        var genesis = journey.Genesis;
        EntityId observer = genesis.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        var node = genesis.Gathering!.Nodes.First(node => node.Definition.Kind == GatherNodeKind.Berry);
        var candidates = new[] { node.Feet.Offset(-256, 0, 0), node.Feet.Offset(256, 0, 0),
            node.Feet.Offset(0, -256, 0), node.Feet.Offset(0, 256, 0) };
        var approach = candidates.First(feet => genesis.Space.QueryGroundRoute(observer, feet).Status == GroundRouteStatus.Reachable);
        journey.Walk(observer, approach);
        var tool = genesis.Gathering.Tools.Single(tool => tool.Kind == node.Definition.RequiredToolKind);
        var toolLot = genesis.Inventory.Custody.Lots.Single(lot => lot.CustodianId == genesis.GetResident(observer).InventoryCustodianId
            && lot.Definition == tool.Definition);
        for (int i = 0; journey.World.Gathering!.GetNode(node.NodeId).RemainingSwings != 0; i++)
        {
            Assert.True(i < 512, "Real harvesting must finish inside the retained finite effort budget.");
            var world = journey.World;
            var liveNode = world.Gathering.GetNode(node.NodeId);
            var actions = world.Gathering.GetActorWork(observer).Effort >= GatherRulesV1.EffortPerSwing
                ? new[] { journey.Command(observer, new GatherIntent(node.NodeId, liveNode.Revision,
                    toolLot.LotId, world.Inventory.Custody.GetLot(toolLot.LotId).Revision)) } : [];
            Assert.All(journey.Commit(actions).Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        }
        var depleted = journey.World.Gathering!.GetNode(node.NodeId);
        ulong due = depleted.RespawnAtAdvancingStep!.Value;
        journey.Walk(observer, genesis.GetResident(observer).HomeFeet);
        while (journey.World.AdvancingSteps < due - 200) journey.Commit();
        journey.SupplyAndPositionBuilders();
        Assert.True(journey.World.AdvancingSteps < due - 8, "The real funding/routes must leave a nonfinal work interval across regrowth.");
        var controller = new LocalNpcController();
        while (journey.World.AdvancingSteps < due - 1)
        {
            var actions = controller.SelectActions(journey.World, journey.NextIdentity);
            Assert.Equal(2, actions.Count);
            Assert.All(journey.Commit(actions.ToArray()).Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        }
        return new(genesis, journey.World, journey.Batches.ToArray(), journey.OccupiedIdentities.ToHashSet(),
            observer, node.NodeId, node.SourceId, due, depleted.DepletedAtStep!.Value,
            depleted.NextUnissuedUnit - node.NextUnissuedUnit);
    }

    private IReadOnlyList<WorldActionCommand> Select(WorldSnapshot world)
    {
        var commands = new LocalNpcController().SelectActions(world, NextIdentity);
        Assert.Equal(2, commands.Count);
        Assert.DoesNotContain(commands, command => command.ActorId == Fixture.Value.Observer);
        return commands;
    }

    private WorldStepBatch Batch(WorldSnapshot world, IEnumerable<WorldActionCommand> commands) => new(NextIdentity(),
        world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], commands, world.ProfileVersion);

    private static ulong HarvestedUnits(WorldSnapshot world, DueFixture fixture) => world.Inventory.Custody.Lots
        .Where(lot => lot.CustodianId == world.GetResident(fixture.Observer).InventoryCustodianId && lot.Source.SourceId == fixture.SourceId)
        .Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));

    private static void AssertCombinedBoundary(WorldSnapshot before, WorldSnapshot after, DueFixture fixture)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.Equal(before.Residents.Select(resident => resident.ActorId), after.Residents.Select(resident => resident.ActorId));
        Assert.All(before.Residents, resident => Assert.Equal(resident.ExperiencedTime.TotalMilliseconds + 1800,
            after.GetResident(resident.ActorId).ExperiencedTime.TotalMilliseconds));
        Assert.All(after.Construction!.Projects, project => Assert.Equal(before.Construction!.FindProject(project.ProjectId)!.Progress.CompletedUnits + 1,
            project.Progress.CompletedUnits));
        var node = after.Gathering!.GetNode(fixture.NodeId);
        Assert.Equal(node.Definition.MaxSwings, node.RemainingSwings);
        Assert.Equal(before.Gathering!.GetNode(fixture.NodeId).NextUnissuedUnit, node.NextUnissuedUnit);
        Assert.Equal(fixture.IssuedUnits, HarvestedUnits(after, fixture));
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Quests!.ContentHash, after.Quests!.ContentHash);
    }

    private sealed record DueFixture(WorldSnapshot Genesis, WorldSnapshot World, WorldStepBatch[] Batches,
        HashSet<EntityId> Identities, EntityId Observer, EntityId NodeId, EntityId SourceId,
        ulong Due, ulong DepletedAt, ulong IssuedUnits);

    private sealed class RetainedFiles : IDisposable
    {
        private readonly bool _retained;
        internal RetainedFiles(string name)
        {
            string? root = Environment.GetEnvironmentVariable("OPENFANTASY_COMBINED_DUE_ARTIFACTS");
            _retained = !string.IsNullOrWhiteSpace(root);
            Directory = _retained ? System.IO.Path.Combine(root!, name)
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openfantasy-combined-due-" + Guid.NewGuid().ToString("N"));
            Assert.False(System.IO.Directory.Exists(Directory), "Combined boundary evidence needs a fresh directory.");
            System.IO.Directory.CreateDirectory(Directory);
            Path = System.IO.Path.Combine(Directory, "world.sqlite3");
        }
        internal string Directory { get; }
        internal string Path { get; }
        public void Dispose() { if (!_retained) System.IO.Directory.Delete(Directory, recursive: true); }
    }
}
