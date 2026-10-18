using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class StoredBatchConstructionTests
{
    [Fact]
    public void Legacy_two_builder_funding_and_routes_are_valid_before_storage_execution()
    {
        var journey = Ready();
        Assert.Equal((ushort)5, journey.World.ProfileVersion);
        Assert.Equal(2, Select(journey, journey.World).Count);
        Assert.All(journey.World.Construction!.Projects, project =>
        {
            Assert.Equal(0UL, project.Progress.CompletedUnits);
            Assert.Equal(200UL, project.Definition.RequiredWork.Value);
            Assert.Equal(project.WorkFeet, journey.World.Space.GetActor(project.BuilderActorId).Feet);
            Assert.Equal(6UL, Assert.Single(journey.World.Inventory.Custody.Lots,
                lot => lot.ReservedBy == project.ReservationId).Quantity);
        });
        Assert.All(journey.Batches.SelectMany(batch => batch.Movement), input =>
            Assert.Equal(32L, Math.Abs(input.DeltaX) + Math.Abs(input.DeltaY)));
    }

    [Fact]
    public void One_owner_slices_prepare_both_actions_and_all_due_effects_without_earning_intermediate_time()
    {
        var journey = Ready();
        var world = journey.World;
        var commands = Select(journey, world);
        var interval = StoredConstructionInterval.Capture(world, commands);
        var payloads = Payloads(world);
        var history = journey.OccupiedIdentities.Concat(world.IdentityIndex.CopyEntityReservations()).ToHashSet();
        for (int i = 0; i < 12; i++)
        {
            commands = Select(journey, world);
            var batch = Batch(journey, world, commands.Reverse());
            var ordinary = new WorldStepProcessor().Decide(world, batch, history.Contains);
            var preparation = interval.BeginPreparation(batch, interval.DirectoryHash, history.Contains);
            byte[] before = interval.CopyDirectory();
            List<ContentHash> reads = [];
            while (!preparation.IsReady)
            {
                Assert.Throws<InvalidOperationException>(() => preparation.Complete(interval.DirectoryHash));
                int count = reads.Count;
                preparation.ReadNext(interval.DirectoryHash, 1, hash => { reads.Add(hash); return payloads[hash]; });
                Assert.Equal(count + 1, reads.Count);
                Assert.Equal(before, interval.CopyDirectory());
                Assert.Equal(world.AdvancingSteps, interval.AdvancingSteps);
            }
            Assert.Equal(interval.RequiredOwnerCount, reads.Count);
            Assert.Equal(reads.Count, reads.Distinct().Count());
            Assert.True(reads.Count < interval.CompleteOwnerCount);
            var stored = Assert.IsType<StoredConstructionStep>(preparation.Complete(interval.DirectoryHash));
            Assert.Throws<InvalidOperationException>(() => preparation.Complete(interval.DirectoryHash));
            AssertEquivalent(world, ordinary, stored, payloads);
            Assert.All(ordinary.Candidate.Construction!.Projects, project => Assert.Equal((ulong)i + 1, project.Progress.CompletedUnits));
            Assert.Equal(world.AdvancingSteps + 1, stored.Candidate.AdvancingSteps);
            history.UnionWith(stored.Identities.Select(identity => identity.Identity));
            interval = stored.Candidate;
            world = ordinary.Candidate;
        }
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("held")]
    [InlineData("refused")]
    public void Empty_held_and_refused_batches_keep_the_complete_ordinary_boundary(string mode)
    {
        var journey = Ready();
        if (mode == "held")
        {
            var speak = journey.Command(journey.Player, new SpeakToActorIntent(journey.Helper,
                journey.World.Space.GetActor(journey.Helper).Revision));
            Assert.Equal(WorldActionRejection.None, Assert.Single(journey.Commit(speak).Receipts).Rejection);
        }
        var world = journey.World;
        var commands = journey.WorkCommands();
        var interval = StoredConstructionInterval.Capture(world, commands);
        if (mode == "refused")
        {
            var old = commands[1];
            commands[1] = new WorldActionCommand(old.CommandId, old.PrincipalId, old.OriginSessionId,
                old.ActorId, old.WorldId, old.BranchId, old.ExpectedActorRevision,
                ((WorkConstructionIntent)old.Intent) with { ExpectedProjectRevision = 99 });
        }
        var batch = Batch(journey, world, mode == "empty" ? [] : commands);
        var payloads = Payloads(world);
        var expected = new WorldStepProcessor().Decide(world, batch);
        var step = Assert.IsType<StoredConstructionStep>(interval.Prepare(batch, interval.DirectoryHash, hash => payloads[hash], _ => false));
        AssertEquivalent(world, expected, step, payloads);
        if (mode == "empty") Assert.Empty(step.Receipts);
        if (mode == "held") Assert.Empty(step.ChangedOwners);
        if (mode == "refused") Assert.Contains(step.Receipts, receipt => receipt.Rejection == WorldActionRejection.RevisionConflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_changed_directory_invalidates_loaded_slices_before_read_or_completion(bool atCompletion)
    {
        var journey = Ready();
        var world = journey.World;
        var commands = Select(journey, world);
        var interval = StoredConstructionInterval.Capture(world, commands);
        var batch = Batch(journey, world, commands);
        var preparation = interval.BeginPreparation(batch, interval.DirectoryHash, _ => false);
        var payloads = Payloads(world);
        preparation.ReadNext(interval.DirectoryHash, atCompletion ? interval.RequiredOwnerCount : 1, hash => payloads[hash]);
        var changedWorld = new WorldStepProcessor().Decide(world, Batch(journey, world, []));
        var changedHash = ConstructionOwnerDirectory.Capture(changedWorld.Candidate).Hash;
        if (atCompletion) Assert.Throws<InvalidOperationException>(() => preparation.Complete(changedHash));
        else Assert.Throws<InvalidOperationException>(() => preparation.ReadNext(changedHash, 1,
            _ => throw new Exception("A stale directory must not read another owner")));
        Assert.Throws<InvalidOperationException>(() => preparation.Complete(interval.DirectoryHash));
        Assert.Equal(ConstructionOwnerDirectory.Capture(world).CopyBytes(), interval.CopyDirectory());
    }

    [Fact]
    public void Failed_owner_read_cannot_resume_or_complete_a_boundary()
    {
        var journey = Ready();
        var world = journey.World;
        var commands = Select(journey, world);
        var interval = StoredConstructionInterval.Capture(world, commands);
        var preparation = interval.BeginPreparation(Batch(journey, world, commands), interval.DirectoryHash, _ => false);
        Assert.Throws<IOException>(() => preparation.ReadNext(interval.DirectoryHash, 1,
            _ => throw new IOException("Required owner unavailable")));
        Assert.Throws<InvalidOperationException>(() => preparation.ReadNext(interval.DirectoryHash, 1,
            _ => throw new Exception("A failed preparation cannot retry its reads")));
        Assert.Throws<InvalidOperationException>(() => preparation.Complete(interval.DirectoryHash));
        Assert.False(preparation.IsReady);
    }

    [Fact]
    public void Legacy_aggregate_limit_returns_the_whole_batch_to_ordinary_admission()
    {
        var journey = Ready();
        var world = journey.World;
        var interval = StoredConstructionInterval.Capture(world, Select(journey, world));
        var payloads = Payloads(world);
        bool reached = false;
        for (int i = 0; i < 200; i++)
        {
            var commands = Select(journey, world);
            var batch = Batch(journey, world, commands);
            var stored = interval.Prepare(batch, interval.DirectoryHash, hash => payloads[hash], _ => false);
            if (world.Construction!.Revision + 2 > world.Revision + 1)
            {
                Assert.Null(stored);
                Assert.Throws<ArgumentException>(() => new WorldStepProcessor().Decide(world, batch));
                reached = true;
                break;
            }
            var expected = new WorldStepProcessor().Decide(world, batch);
            AssertEquivalent(world, expected, Assert.IsType<StoredConstructionStep>(stored), payloads);
            interval = stored!.Candidate;
            world = expected.Candidate;
        }
        Assert.True(reached, "The original aggregate bound must be witnessed, not relaxed for the test.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void Stored_two_actor_publication_reopens_all_original_history_and_atomic_receipts(int? faultValue)
    {
        var journey = Ready();
        var world = journey.World;
        var commands = Select(journey, world);
        var batch = Batch(journey, world, commands);
        var expected = new WorldStepProcessor().Decide(world, batch, journey.OccupiedIdentities.Contains);
        AuthorityCommitBoundary? fault = faultValue is { } selectedBoundary ? (AuthorityCommitBoundary)selectedBoundary : null;
        using var files = new RetainedFiles(fault?.ToString() ?? "ordinary");
        using (var authority = SqliteWorldAuthority.CreateWithTypedOwners(files.Path, journey.Genesis))
        {
            foreach (var prior in journey.Batches) authority.Commit(prior);
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            var interval = authority.EvictForStoredConstruction(commands);
            int reads = authority.OwnerArchiveReads;
            authority.BeforeTerrainRead = () => throw new IOException("Partial work must not read terrain");
            var excluded = ConstructionOwnerDirectory.Capture(world).Owners.Where(owner =>
                owner.Key.Kind is ConstructionOwnerKind.InventoryBasis or ConstructionOwnerKind.InventoryLedger
                    or ConstructionOwnerKind.ProductionHistory or ConstructionOwnerKind.Recipe).Select(owner => owner.Hash).ToHashSet();
            authority.BeforeOwnerRead = hash => Assert.DoesNotContain(hash, excluded);
            if (fault is null)
            {
                var receipts = authority.CommitStoredConstructionBatch(batch);
                Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts), WorldReceiptCodecV1.EncodeBatch(receipts));
            }
            else
            {
                Assert.Throws<IOException>(() => authority.CommitStoredConstructionBatch(batch, at =>
                {
                    if (at == fault) throw new IOException("Stored batch publication fault");
                }));
                Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
            }
            Assert.Equal(interval.RequiredOwnerCount, authority.OwnerArchiveReads - reads);
            Assert.True(interval.RequiredOwnerCount < interval.CompleteOwnerCount);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        bool published = fault != AuthorityCommitBoundary.BeforeCommit;
        Assert.Equal(WorldSnapshotCodec.Encode(published ? expected.Candidate : world), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(WorldSnapshotCodec.Encode(journey.Genesis), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(0).State));
        Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(world.Revision).State));
        foreach (var command in commands)
        {
            var receipt = reopened.FindReceipt(command, ConstructionUpgradeFixture.Delivery(world, command.ActorId));
            if (!published) Assert.Null(receipt);
            else Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single(value => value.CommandId == command.CommandId)),
                WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(receipt)));
        }
        if (!published)
        {
            reopened.EvictForStoredConstruction(commands);
            Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts),
                WorldReceiptCodecV1.EncodeBatch(reopened.CommitStoredConstructionBatch(batch)));
        }
        Assert.Throws<AuthorityDispatchException>(() => reopened.CommitStoredConstructionBatch(batch));
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        File.WriteAllBytes(System.IO.Path.Combine(files.Directory, "genesis.ofc"), WorldSnapshotCodec.Encode(journey.Genesis));
        File.WriteAllBytes(System.IO.Path.Combine(files.Directory, "before.ofc"), WorldSnapshotCodec.Encode(world));
        File.WriteAllBytes(System.IO.Path.Combine(files.Directory, "after.ofc"), WorldSnapshotCodec.Encode(expected.Candidate));
    }

    private static CoordinatedConstructionJourney Ready()
    {
        var journey = new CoordinatedConstructionJourney(coordinated: false, hostBuilders: true, constructionOnly: true);
        journey.SupplyAndPositionBuilders();
        return journey;
    }

    private static IReadOnlyList<WorldActionCommand> Select(CoordinatedConstructionJourney journey, WorldSnapshot world)
    {
        var commands = new LocalNpcController().SelectActions(world, journey.NextIdentity);
        Assert.Equal(2, commands.Count);
        return commands;
    }

    private static WorldStepBatch Batch(CoordinatedConstructionJourney journey, WorldSnapshot world,
        IEnumerable<WorldActionCommand> commands) => new(journey.NextIdentity(), world.WorldId, world.BranchId,
        world.Revision, world.AdvancingSteps, world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
        [], commands, world.ProfileVersion);

    private static Dictionary<ContentHash, byte[]> Payloads(WorldSnapshot world) =>
        ConstructionOwnerDirectory.Capture(world).Owners.ToDictionary(owner => owner.Hash, owner => owner.CopyBytes());

    private static void AssertEquivalent(WorldSnapshot before, WorldStepDecision expected, StoredConstructionStep actual,
        Dictionary<ContentHash, byte[]> payloads)
    {
        var directory = ConstructionOwnerDirectory.Capture(expected.Candidate);
        Assert.Equal(directory.CopyBytes(), actual.Candidate.CopyDirectory());
        Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts), WorldReceiptCodecV1.EncodeBatch(actual.Receipts));
        Assert.Equal(expected.Identities.OrderBy(identity => identity.Identity), actual.Identities);
        foreach (var owner in actual.ChangedOwners)
        {
            Assert.Equal(directory.Owners.Single(value => value.Key == owner.Key).CopyBytes(), owner.CopyBytes());
            payloads[owner.Hash] = owner.CopyBytes();
        }
        var reconstructed = ConstructionOwnerReconstruction.Decode(actual.Candidate.CopyDirectory(), actual.Candidate.DirectoryHash,
            hash => payloads[hash], reference => before.Space.Regions.Single(region => TerrainRegionReference.Capture(region) == reference));
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(reconstructed));
    }

    private sealed class RetainedFiles : IDisposable
    {
        private readonly bool _retained;
        internal RetainedFiles(string name)
        {
            string? root = Environment.GetEnvironmentVariable("OPENFANTASY_STORED_BATCH_ARTIFACTS");
            _retained = !string.IsNullOrWhiteSpace(root);
            Directory = _retained ? System.IO.Path.Combine(root!, name)
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openfantasy-stored-batch-" + Guid.NewGuid().ToString("N"));
            Assert.False(System.IO.Directory.Exists(Directory), "Stored batch evidence must use a fresh directory.");
            System.IO.Directory.CreateDirectory(Directory);
            Path = System.IO.Path.Combine(Directory, "world.sqlite3");
        }
        internal string Directory { get; }
        internal string Path { get; }
        public void Dispose()
        {
            if (!_retained) System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
