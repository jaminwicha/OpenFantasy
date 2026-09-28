using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class CoordinatedConcurrentConstructionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_actual_builders_cross_the_aggregate_step_bound_only_in_the_coordinated_profile(bool coordinated)
    {
        var journey = new CoordinatedConstructionJourney(coordinated);
        Assert.Equal(coordinated ? (ushort)27 : (ushort)26, journey.World.ProfileVersion);
        journey.SupplyAndPositionBuilders();
        var supplied = journey.World;
        Assert.Equal(2, supplied.Quests!.Participations.Count);
        Assert.Equal(2, supplied.Construction!.Projects.Select(project => project.BuilderActorId).Distinct().Count());
        Assert.All(supplied.Construction.Projects, project =>
        {
            Assert.Equal(200UL, project.Definition.RequiredWork.Value);
            Assert.Equal(project.WorkFeet, supplied.Space.GetActor(project.BuilderActorId).Feet);
            var stock = Assert.Single(supplied.Inventory.Custody.Lots, lot => lot.ReservedBy == project.ReservationId);
            Assert.Equal(project.ProjectCustodianId, stock.CustodianId);
            Assert.Equal(6UL, stock.Quantity);
        });
        // Two units per world step overcome only the actual setup slack. Neither project's
        // original work target is enlarged to manufacture this boundary.
        ulong pairedSteps = checked(supplied.Revision + 1);
        Assert.True(pairedSteps + 4 < 200, $"The actual setup used {supplied.Revision} steps; it must leave partial work after the cut.");
        WorldActionCommand? firstWork = null;
        for (ulong step = 1; step <= pairedSteps; step++)
        {
            var before = journey.World;
            var commands = journey.WorkCommands();
            firstWork ??= commands[0];
            var decision = journey.Commit(commands);
            Assert.Equal(2, decision.Receipts.Count);
            AssertApplied(decision, commands[0]);
            bool refusedByLegacyBound = !coordinated && step == pairedSteps;
            var second = Assert.Single(decision.Receipts, receipt => receipt.CommandId == commands[1].CommandId);
            Assert.Equal(refusedByLegacyBound ? CommandReceiptStatus.Rejected : CommandReceiptStatus.Applied, second.Status);
            Assert.Equal(refusedByLegacyBound ? WorldActionRejection.Unavailable : WorldActionRejection.None, second.Rejection);
            Assert.Equal(commands[1].ContentHash, second.CommandHash);
            Assert.Equal(before.Revision + 1, journey.World.Revision);
            Assert.All(decision.Receipts, receipt => Assert.Equal(journey.World.Revision, receipt.CommitSequence));
            Assert.Equal(before.AdvancingSteps + 1, journey.World.AdvancingSteps);
            Assert.Equal(before.Construction!.Revision + (refusedByLegacyBound ? 1UL : 2UL), journey.World.Construction!.Revision);
            for (int index = 0; index < commands.Length; index++)
            {
                var projectId = ((WorkConstructionIntent)commands[index].Intent).ProjectId;
                Assert.Equal(before.Construction.FindProject(projectId)!.Revision
                    + (refusedByLegacyBound && index == 1 ? 0UL : 1UL), journey.World.Construction.FindProject(projectId)!.Revision);
            }
            AssertPartialOwners(supplied, journey.World);
        }

        if (!coordinated)
        {
            Assert.Equal(journey.World.Revision, journey.World.Construction!.Revision);
            Assert.Null(journey.World.Coordination);
            return;
        }

        var closing = journey.World;
        Assert.Equal(closing.Revision + 1, closing.Construction!.Revision);
        Assert.All(closing.Construction.Projects, project => Assert.Equal(pairedSteps, project.Revision));
        var segment = journey.CutAndReopen();
        Assert.Equal(closing.Revision, segment.Reference.BoundarySequence);
        Assert.Equal(closing.ContentHash, segment.Reference.BoundaryHash);
        Assert.Equal(closing.Construction.ContentHash, journey.World.Construction!.ContentHash);
        Assert.Equal(closing.Inventory.ContentHash, journey.World.Inventory.ContentHash);
        Assert.Equal(closing.Quests!.ContentHash, journey.World.Quests!.ContentHash);
        Assert.Equal(closing.Progression!.ContentHash, journey.World.Progression!.ContentHash);
        Assert.Equal(closing.AdvancingSteps, journey.World.AdvancingSteps);
        Assert.Equal(closing.Calendar, journey.World.Calendar);
        Assert.Equal(closing.LiquidCells!.Portions, journey.World.LiquidCells!.Portions);
        Assert.Equal(closing.LiquidCells.Revision, journey.World.LiquidCells.Revision);
        Assert.Equal(closing.LiquidCells.Boundary, journey.World.LiquidCells.Boundary);
        Assert.Contains(firstWork!.CommandId, journey.OccupiedIdentities);
        AssertPartialOwners(supplied, journey.World);

        for (int step = 0; step < 4; step++)
        {
            var commands = journey.WorkCommands();
            var decision = journey.Commit(commands);
            Assert.All(commands, command => AssertApplied(decision, command));
            AssertPartialOwners(supplied, journey.World);
        }
        Assert.All(journey.World.Construction!.Projects, project => Assert.Equal(pairedSteps + 4, project.Revision));
        Assert.True(journey.World.Construction.Revision > journey.World.Revision);
        byte[] finalBytes = WorldSnapshotCodec.Encode(journey.World);
        Assert.Throws<InvalidOperationException>(() => journey.Commit(firstWork!));
        Assert.Equal(finalBytes, WorldSnapshotCodec.Encode(journey.World));
    }

    private static void AssertApplied(WorldStepDecision decision, WorldActionCommand command)
    {
        var receipt = Assert.Single(decision.Receipts, value => value.CommandId == command.CommandId);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
    }

    private static void AssertPartialOwners(WorldSnapshot supplied, WorldSnapshot current)
    {
        Assert.Equal(supplied.Inventory.ContentHash, current.Inventory.ContentHash);
        Assert.Equal(supplied.Production!.Revision, current.Production!.Revision);
        Assert.Empty(current.Production.Batches);
        Assert.Empty(current.Production.Lots);
        Assert.Equal(supplied.Quests!.ContentHash, current.Quests!.ContentHash);
        Assert.Equal(supplied.Progression!.ContentHash, current.Progression!.ContentHash);
        Assert.Equal(supplied.Excavation!.ContentHash, current.Excavation!.ContentHash);
        Assert.Equal(supplied.Space.Regions.Select(region => region.ComputeContentHash()),
            current.Space.Regions.Select(region => region.ComputeContentHash()));
        Assert.Equal(supplied.LiquidCells!.Portions, current.LiquidCells!.Portions);
        Assert.Equal(current.Inventory.ContentHash, current.Reservoirs!.Capacity.Inventory.ContentHash);
        Assert.Equal(current.Production.ContentHash, current.Reservoirs.Capacity.Production!.ContentHash);
        Assert.Equal(current.Reservoirs.ContentHash, current.LiquidCells.ReservoirHash);
        Assert.Equal(current.ProcessIntervals, current.Gathering!.AdvancingStep);
        Assert.NotNull(current.Care!.Ordinary);
        Assert.Equal(supplied.Care!.Revision, current.Care.Revision);
        Assert.Equal(supplied.Care.Pending, current.Care.Pending);
        Assert.Equal(supplied.Care.Ordinary!.Definitions.Select(value => (value.DefinitionId, value.Version)),
            current.Care.Ordinary.Definitions.Select(value => (value.DefinitionId, value.Version)));
        Assert.Equal(supplied.Care.Ordinary.Permissions.Select(value => (value.PermissionId, value.GrantVersion, value.Revision)),
            current.Care.Ordinary.Permissions.Select(value => (value.PermissionId, value.GrantVersion, value.Revision)));
        Assert.Equal(supplied.Care.Ordinary.Uses, current.Care.Ordinary.Uses);
        Assert.NotNull(current.EncounterLifecycle);
        Assert.NotNull(current.ResourceHistory);
        Assert.All(current.Construction!.Projects, project =>
        {
            Assert.False(project.IsCompleted);
            Assert.Null(project.Installed);
            Assert.Equal(project.Revision, project.Progress.CompletedUnits);
            Assert.True(project.Revision <= current.AdvancingSteps);
            Assert.True(project.Progress.Elapsed.TotalMilliseconds
                <= current.GetResident(project.BuilderActorId).ExperiencedTime.TotalMilliseconds);
        });
    }
}
