using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionWorldFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class StoredWorkCohortTests
{
    [Fact]
    public void Selected_stored_owners_match_ordinary_qualification_without_mutating_the_source()
    {
        var world = Create();
        var command = Command(world);
        var cohort = ConstructionWorkCohort.Capture(world, command);
        var directory = ConstructionOwnerDirectory.Capture(world);
        var payloads = directory.Owners.ToDictionary(value => value.Hash, value => value.CopyBytes());
        List<ContentHash> reads = [];
        var assessment = cohort.Assess(directory.Hash, hash => { reads.Add(hash); return payloads[hash]; });
        var ordinary = new ConstructionPlanner().Plan(world, world.Inventory, world.Production!, world.Construction!,
            world.Space, command, true, false);
        Assert.Equal(ordinary.Rejection, assessment.Rejection);
        Assert.Equal(ordinary.CandidateConstruction.Projects.Single().Progress, assessment.ProposedProgress);
        Assert.Equal(cohort.RequiredOwners.Select(value => value.Hash), reads);
        Assert.True(reads.Count < directory.Owners.Count);
        Assert.DoesNotContain(cohort.RequiredOwners, value => value.Key.Kind is ConstructionOwnerKind.ProductionHistory
            or ConstructionOwnerKind.ProductionBatch or ConstructionOwnerKind.Gathering or ConstructionOwnerKind.Timing);
        Assert.Equal(cohort.SourceWorldHash, world.ContentHash);
        Assert.False(assessment.RequiresCompletion);
        Assert.Equal(0UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
    }

    [Fact]
    public void Changed_directory_refuses_before_any_selected_read()
    {
        var world = Create();
        var cohort = ConstructionWorkCohort.Capture(world, Command(world));
        var changed = Step(world, 101).Candidate;
        int reads = 0;
        Assert.Throws<InvalidOperationException>(() => cohort.Assess(ConstructionOwnerDirectory.Capture(changed).Hash,
            _ => { reads++; throw new InvalidOperationException("No stale reads"); }));
        Assert.Equal(0, reads);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("foreign")]
    public void Selected_payload_must_be_exactly_the_captured_owner(string mutation)
    {
        var world = Create();
        var cohort = ConstructionWorkCohort.Capture(world, Command(world));
        var directory = ConstructionOwnerDirectory.Capture(world);
        var payloads = directory.Owners.ToDictionary(value => value.Hash, value => value.CopyBytes());
        var first = cohort.RequiredOwners[0];
        byte[] foreign = payloads[cohort.RequiredOwners[1].Hash];
        Assert.ThrowsAny<FormatException>(() => cohort.Assess(directory.Hash, hash =>
        {
            if (hash != first.Hash) return payloads[hash];
            if (mutation == "missing") return null!;
            if (mutation == "foreign") return foreign;
            byte[] bytes = payloads[hash].ToArray(); bytes[^1] ^= 1; return bytes;
        }));
    }

    [Fact]
    public void Completion_qualification_requires_larger_cohort_and_does_not_transform_material()
    {
        var world = Create();
        for (ulong i = 0; i < 199; i++) world = Step(world, 100 + i).Candidate;
        var cohort = ConstructionWorkCohort.Capture(world, Command(world, 700));
        var payloads = ConstructionOwnerDirectory.Capture(world).Owners.ToDictionary(value => value.Hash, value => value.CopyBytes());
        var assessment = cohort.Assess(cohort.DirectoryHash, hash => payloads[hash]);
        Assert.Equal(WorldActionRejection.None, assessment.Rejection);
        Assert.True(assessment.RequiresCompletion);
        Assert.Equal(200UL, assessment.ProposedProgress!.CompletedUnits);
        Assert.Equal(199UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Empty(world.Production!.Batches);
        Assert.Empty(world.Space.Structures);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Stored_qualification_preserves_reading_and_stale_project_rejections(bool held)
    {
        var world = Create(held: held);
        var intent = (WorkConstructionIntent)Command(world).Intent;
        var command = Command(world, intent: intent with { ExpectedProjectRevision = 99 });
        var cohort = ConstructionWorkCohort.Capture(world, command);
        var payloads = ConstructionOwnerDirectory.Capture(world).Owners.ToDictionary(value => value.Hash, value => value.CopyBytes());
        Assert.Equal(held ? WorldActionRejection.Unavailable : WorldActionRejection.RevisionConflict,
            cohort.Assess(cohort.DirectoryHash, hash => payloads[hash]).Rejection);
    }
}
