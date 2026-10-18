using System.Runtime.CompilerServices;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed partial class SeparatedTerrainStorageTests(ITestOutputHelper output)
{
    [Fact]
    public void Actual_gift_work_return_intervention_and_installation_share_history_without_resident_terrain()
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path);
        byte[] complete;
        using (var authority = ready.Authority)
        {
            Collected(ready.References);
            var state = ready.Locality;
            int reads = authority.TerrainArchiveReads;
            authority.BeforeTerrainRead = () => throw new InvalidOperationException("Terrain must remain absent during partial work.");
            long allocated = GC.GetAllocatedBytesForCurrentThread(), ticks = Stopwatch.GetTimestamp();
            for (int i = 0; i < 40; i++) state = Work(authority, ready.Fixture, state).Candidate;
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            ticks = Stopwatch.GetTimestamp() - ticks;
            Assert.Equal(reads, authority.TerrainArchiveReads);
            authority.BeforeTerrainRead = null;
            var returned = ReturnAndIntervene(authority, ready.Fixture, state);
            state = returned.Locality;
            Collected(returned.References);
            reads = authority.TerrainArchiveReads;
            authority.BeforeTerrainRead = () => throw new InvalidOperationException("Resumed work must not fetch terrain.");
            for (int i = 40; i < 199; i++) state = Work(authority, ready.Fixture, state).Candidate;
            Assert.Equal(reads, authority.TerrainArchiveReads);
            authority.BeforeTerrainRead = null;
            var completed = Work(authority, ready.Fixture, state);
            Assert.True(completed.Candidate.Construction.Projects.Single().IsCompleted);
            Assert.Equal(reads + state.Terrain.Count, authority.TerrainArchiveReads);
            var full = authority.ReadStateForHost();
            AssertCompleted(full);
            complete = WorldSnapshotCodec.Encode(full);
            Assert.Equal(ConstructionLocalityEncoding.Encode(full), ConstructionLocalityEncoding.Encode(completed.Candidate));
            Assert.Equal(ready.GenesisHash, authority.ReadRecordedStateForHost(0).State.ContentHash);
            Assert.Equal(complete, WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(full.Revision).State));
            output.WriteLine($"First40 actual SQLite work commits: {allocated} managed allocated bytes, {ticks * 1000.0 / Stopwatch.Frequency:F3} ms, zero terrain reads; original world/scene/index/terrain collected. Full ordinary recovery and final geometry admission remain required.");
        }
        long reopenBytes = GC.GetAllocatedBytesForCurrentThread(), reopenTicks = Stopwatch.GetTimestamp();
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        reopenBytes = GC.GetAllocatedBytesForCurrentThread() - reopenBytes;
        reopenTicks = Stopwatch.GetTimestamp() - reopenTicks;
        var restored = reopened.ReadStateForHost();
        Assert.Equal(complete, WorldSnapshotCodec.Encode(restored));
        output.WriteLine($"Complete original-history reopen: {reopenBytes} managed allocated bytes, {reopenTicks * 1000.0 / Stopwatch.Frequency:F3} ms; full root {complete.Length} bytes, nonterrain image {ConstructionLocalityEncoding.Encode(restored).Length} bytes, immutable terrain {restored.Space.Regions.Sum(region => OpenFantasy.Core.Terrain.TerrainRegionCodecV1.Encode(region).Length)} bytes. Managed warm-process observation; not cold-cache, RSS, platform or throughput acceptance.");
    }

    [Theory]
    [InlineData(1, (int)AuthorityCommitBoundary.BeforeCommit, false)]
    [InlineData(1, (int)AuthorityCommitBoundary.AfterCommit, false)]
    [InlineData(200, (int)AuthorityCommitBoundary.BeforeCommit, false)]
    [InlineData(200, (int)AuthorityCommitBoundary.AfterCommit, false)]
    [InlineData(1, (int)AuthorityCommitBoundary.BeforeCommit, true)]
    [InlineData(1, (int)AuthorityCommitBoundary.AfterCommit, true)]
    [InlineData(200, (int)AuthorityCommitBoundary.BeforeCommit, true)]
    [InlineData(200, (int)AuthorityCommitBoundary.AfterCommit, true)]
    public void Faulted_unloaded_publication_recovers_one_complete_old_or_new_boundary(int target, int boundaryValue, bool typed)
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path, typed: typed);
        WorldStepBatch selected;
        byte[] beforeImage, expectedImage, expectedReceipt;
        using (var authority = ready.Authority)
        {
            var state = ready.Locality;
            for (int i = 1; i < target; i++) state = Work(authority, ready.Fixture, state).Candidate;
            selected = Batch(state, ready.Fixture);
            beforeImage = ConstructionLocalityEncoding.Encode(state);
            var expected = state.PrepareStep(selected, state.Occupancy, state.Terrain, _ => false);
            expectedImage = ConstructionLocalityEncoding.Encode(expected.Candidate);
            expectedReceipt = WorldReceiptCodecV1.Encode(expected.Receipt);
            Assert.Throws<IOException>(() => authority.CommitWithoutTerrain(selected, at =>
            {
                if ((int)at == boundaryValue) throw new IOException("Interrupt separated terrain publication");
            }));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        var world = recovered.ReadStateForHost();
        if (boundaryValue == (int)AuthorityCommitBoundary.BeforeCommit)
        {
            Assert.Equal(beforeImage, ConstructionLocalityEncoding.Encode(world));
            recovered.EvictConstructionTerrain(world.Construction!.Projects.Single().ProjectId, 512);
            recovered.CommitWithoutTerrain(selected);
        }
        world = recovered.ReadStateForHost();
        Assert.Equal(expectedImage, ConstructionLocalityEncoding.Encode(world));
        var command = selected.Actions.Single();
        var receipt = recovered.FindReceipt(command, ConstructionUpgradeFixture.Delivery(world, command.ActorId));
        Assert.Equal(expectedReceipt, WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(receipt)));
        recovered.EvictConstructionTerrain(world.Construction!.Projects.Single().ProjectId, 512);
        Assert.Throws<AuthorityDispatchException>(() => recovered.CommitWithoutTerrain(selected));
        if (target == 200) AssertCompleted(recovered.ReadStateForHost());
    }

    [Fact]
    public void Missing_pinned_terrain_refuses_recovery_instead_of_becoming_empty_space()
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path);
        using (var authority = ready.Authority) Work(authority, ready.Fixture, ready.Locality);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = files.Path, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM world_terrain";
            Assert.True(command.ExecuteNonQuery() > 0);
        }
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_terrain_read_fences_return_or_final_installation_without_publishing(bool atInstallation)
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path);
        byte[] before;
        using (var authority = ready.Authority)
        {
            var state = ready.Locality;
            if (atInstallation)
                for (int i = 0; i < 199; i++) state = Work(authority, ready.Fixture, state).Candidate;
            before = ConstructionLocalityEncoding.Encode(state);
            authority.BeforeTerrainRead = () => throw new IOException("Terrain storage read unavailable");
            if (atInstallation) Assert.Throws<IOException>(() => Work(authority, ready.Fixture, state));
            else Assert.Throws<IOException>(() => authority.ReadStateForHost());
            authority.BeforeTerrainRead = null;
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(before, ConstructionLocalityEncoding.Encode(recovered.ReadStateForHost()));
    }

    [Fact]
    public void Richer_profiles_cannot_enter_the_legacy_storage_binding()
    {
        using var files = new StorageFiles();
        var world = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan"]);
        Assert.Throws<ArgumentException>(() => SqliteWorldAuthority.CreateWithSeparatedTerrain(files.Path, world));
        Assert.False(File.Exists(files.Path));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Ready Start(string path, bool separated = true, bool typed = false,
        Action<WorldSnapshot, WorldSnapshot, string>? observe = null)
    {
        var fixture = new ConstructionUpgradeFixture();
        var genesis = ConstructionFarmAdmission.CreateGenesis(fixture.QuestGenesis());
        var funded = fixture.CreateSource(path, completeQuest: true, genesis: genesis,
            create: typed ? SqliteWorldAuthority.CreateWithTypedOwners
                : separated ? SqliteWorldAuthority.CreateWithSeparatedTerrain : SqliteWorldAuthority.CreateNew);
        var authority = SqliteWorldAuthority.OpenExisting(path);
        try
        {
            var player = funded.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            var world = fixture.Act(authority, funded, player, new CloseDialogueIntent(funded.Dialogues.Single().DialogueId));
            var initialFeet = world.Space.GetActor(player).Feet;
            var inventory = AuthorityEncodingV1.Encode(world.Inventory);
            var beforeMovement = world;
            for (int i = 0; i < 8; i++) world = authority.Commit(fixture.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, player), player, -32, 0)])).Candidate;
            Assert.NotEqual(initialFeet.Cell, world.Space.GetActor(player).Feet.Cell);
            Assert.Equal(inventory, AuthorityEncodingV1.Encode(world.Inventory));
            observe?.Invoke(beforeMovement, world, "movement");
            for (int i = 0; i < 8; i++) world = authority.Commit(fixture.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, player), player, 32, 0)])).Candidate;
            Assert.Equal(initialFeet, world.Space.GetActor(player).Feet);
            var builder = world.Construction!.Projects.Single().BuilderActorId;
            var bag = world.GetResident(player).InventoryCustodianId;
            var wood = world.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == world.Quests!.Definitions.Single().Item);
            Assert.Equal(2UL, wood.Quantity);
            var beforeGift = world;
            world = fixture.Act(authority, world, player, new GiveItemIntent(builder, world.Space.GetActor(builder).Revision,
                wood.LotId, wood.Revision, bag, world.Inventory.Custody.GetStorage(bag).Revision, 1));
            Assert.Equal(1UL, world.Inventory.Custody.GetLot(wood.LotId).Quantity);
            observe?.Invoke(beforeGift, world, "gift");
            var controller = new LocalNpcController();
            for (int i = 0; ; i++)
            {
                Assert.True(i < 512, "Actual funded builder must reach work within the fixture bound.");
                var batch = fixture.Batch(world, controller.SelectActions(world, fixture.NextIdentity), controller.SelectMovement(world));
                if (batch.Actions.Any(value => value.Intent is WorkConstructionIntent)) break;
                world = authority.Commit(batch).Candidate;
            }
            var locality = separated
                ? authority.EvictConstructionTerrain(world.Construction!.Projects.Single().ProjectId, 512)
                : ConstructionLocalityState.Capture(world, world.Construction!.Projects.Single().ProjectId, 512);
            WeakReference[] references = [new(genesis), new(funded), new(world), new(world.Space), new(world.IdentityIndex),
                .. world.Space.Regions.Select(value => new WeakReference(value))];
            return new(authority, fixture, locality, references, genesis.ContentHash);
        }
        catch { authority.Dispose(); throw; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ConstructionLocalityState Locality, WeakReference[] References) ReturnAndIntervene(
        SqliteWorldAuthority authority, ConstructionUpgradeFixture fixture, ConstructionLocalityState state)
    {
        var world = authority.ReadStateForHost();
        Assert.Equal(40UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Equal(ConstructionLocalityEncoding.Encode(state), ConstructionLocalityEncoding.Encode(world));
        Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(world.Revision).State));
        var player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        world = authority.Commit(fixture.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, player), player, -32, 0)])).Candidate;
        var locality = authority.EvictConstructionTerrain(world.Construction!.Projects.Single().ProjectId, 512);
        return (locality, [new(world), new(world.Space), new(world.IdentityIndex), .. world.Space.Regions.Select(value => new WeakReference(value))]);
    }

    private static WorldStepBatch Batch(ConstructionLocalityState state, ConstructionUpgradeFixture fixture) =>
        Batch(state.Inventory, state.Actors, state.Construction, state.Revision, state.AdvancingSteps, fixture);

    private static WorldStepBatch Batch(CountedAuthorityState inventory, IEnumerable<ActorPresence> actors,
        ConstructionSnapshot construction, ulong revision, ulong steps, ConstructionUpgradeFixture fixture)
    {
        var project = construction.Projects.Single();
        var actor = actors.Single(value => value.ActorId == project.BuilderActorId);
        var control = inventory.Controls.Single(value => value.ActorId == actor.ActorId);
        var tool = inventory.Custody.Lots.Single(value => value.Definition == project.Definition.RequiredTool.Item);
        var command = new WorldActionCommand(fixture.NextIdentity(), control.PrincipalId, control.SessionId,
            actor.ActorId, inventory.WorldId, inventory.BranchId, actor.Revision,
            new WorkConstructionIntent(project.ProjectId, project.Revision,
                inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, tool.LotId, tool.Revision));
        return new(fixture.NextIdentity(), inventory.WorldId, inventory.BranchId, revision,
            steps, WorldBatchKind.AdvancingStep, [], [command], 5);
    }

    private static ConstructionLocalityStep Work(SqliteWorldAuthority authority, ConstructionUpgradeFixture fixture, ConstructionLocalityState state)
    {
        var step = authority.CommitWithoutTerrain(Batch(state, fixture));
        Assert.Equal(WorldActionRejection.None, step.Receipt.Rejection);
        return step;
    }

    private static void Collected(IEnumerable<WeakReference> references)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    private static void AssertCompleted(WorldSnapshot world)
    {
        Assert.True(world.Construction!.Projects.Single().IsCompleted);
        Assert.Single(world.Space.Structures);
        Assert.Single(world.Production!.Batches);
        Assert.Equal(1UL, Assert.Single(world.Production.Lots, value => value.ReservedBy is not null).Quantity);
        Assert.Equal(3UL, Assert.Single(world.Production.Lots, value => value.ReservedBy is null).Quantity);
    }

    private sealed record Ready(SqliteWorldAuthority Authority, ConstructionUpgradeFixture Fixture,
        ConstructionLocalityState Locality, WeakReference[] References, ContentHash GenesisHash);
}
