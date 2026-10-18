using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed partial class SeparatedTerrainStorageTests
{
    [Fact]
    public void Actual_stored_work_cohort_qualifies_after_unrelated_detail_is_collected()
    {
        using var files = new StorageFiles();
        var captured = CaptureStoredCohort(files.Path);
        Collected(captured.References);
        List<ContentHash> reads = [];
        long bytes = 0;
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = files.Path, Pooling = false, Mode = SqliteOpenMode.ReadOnly }.ToString());
        connection.Open();
        using var head = connection.CreateCommand();
        head.CommandText = "SELECT hash FROM world_head WHERE singleton=1";
        var hash = new ContentHash(Convert.ToHexString((byte[])head.ExecuteScalar()!).ToLowerInvariant());
        var assessment = captured.Cohort.Assess(hash, requested =>
        {
            reads.Add(requested);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT state FROM world_owners WHERE hash=$hash";
            command.Parameters.AddWithValue("$hash", requested.ToByteArray());
            byte[] payload = (byte[])command.ExecuteScalar()!;
            bytes += payload.Length;
            return payload;
        });
        Assert.Equal(WorldActionRejection.None, assessment.Rejection);
        Assert.Equal(1UL, assessment.ProposedProgress!.CompletedUnits);
        Assert.Equal(captured.Cohort.RequiredOwners.Select(value => value.Hash), reads);
        Assert.True(reads.Count < captured.Cohort.CompleteOwnerCount);
        Assert.False(assessment.RequiresCompletion);
        output.WriteLine($"Stored qualification: {reads.Count}/{captured.Cohort.CompleteOwnerCount} owner payloads, {bytes} payload bytes; zero terrain payload reads. Complete actor/structure geometry witness remains; no work publication or due-process update is claimed.");
    }

    [Fact]
    public void Host_serializes_read_only_assessment_and_rejects_it_after_real_work()
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path, typed: true);
        using var authority = ready.Authority;
        var batch = Batch(ready.Locality, ready.Fixture);
        var cohort = authority.CaptureStoredWorkCohort(batch.Actions.Single());
        var before = authority.ReadStateForHost();
        int terrainReads = authority.TerrainArchiveReads;
        var assessment = authority.AssessStoredWorkCohort(cohort);
        Assert.Equal(WorldActionRejection.None, assessment.Rejection);
        Assert.Equal(cohort.SourceWorldHash, authority.ReadStateForHost().ContentHash);
        Assert.Equal(terrainReads, authority.TerrainArchiveReads);
        var step = authority.Commit(batch);
        Assert.Equal(assessment.ProposedProgress, step.Candidate.Construction!.Projects.Single().Progress);
        Assert.True(step.Candidate.AdvancingSteps > before.AdvancingSteps);
        Assert.Throws<InvalidOperationException>(() => authority.AssessStoredWorkCohort(cohort));
        Assert.Equal(step.Candidate.ContentHash, authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public void Rich_current_profiles_cannot_be_reduced_to_stored_work_cohort()
    {
        var world = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan"]);
        using var files = new StorageFiles();
        var ready = Start(files.Path, typed: true);
        using var authority = ready.Authority;
        var command = Batch(ready.Locality, ready.Fixture).Actions.Single();
        Assert.Throws<ArgumentException>(() => ConstructionWorkCohort.Capture(world, command));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ConstructionWorkCohort Cohort, WeakReference[] References) CaptureStoredCohort(string path)
    {
        var ready = Start(path, typed: true);
        using var authority = ready.Authority;
        var world = authority.ReadStateForHost();
        var command = Batch(ready.Locality, ready.Fixture).Actions.Single();
        var cohort = authority.CaptureStoredWorkCohort(command);
        var holders = cohort.RequiredOwners.Where(value => value.Key.Kind == ConstructionOwnerKind.Custodian)
            .Select(value => value.Key.SubjectId).ToHashSet();
        var unrelated = world.Inventory.Custody.Lots.Where(value => !holders.Contains(value.CustodianId)).ToArray();
        Assert.NotEmpty(unrelated);
        return (cohort, [new(world), new(world.Inventory), new(world.Inventory.Custody), new(world.Production!),
            new(world.Space), new(world.IdentityIndex), .. world.Space.Regions.Select(value => new WeakReference(value)),
            .. unrelated.Select(value => new WeakReference(value))]);
    }
}
