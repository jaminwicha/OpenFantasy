using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Simulation.HumanConstruction;

public sealed class ConstructionViewTests
{
    [Fact]
    public void Actual_human_builder_sees_own_unfunded_project_without_being_a_supplier()
    {
        using var fixture = new HumanConstructionViewFixture();
        var state = fixture.State;
        Assert.Empty(state.Quests!.Participations);
        var view = new LocalWorldView(state, fixture.Owner);
        var work = Assert.Single(view.ConstructionWork);
        Assert.Equal(fixture.Owner, work.Project.BuilderActorId);
        Assert.Contains(work.Project, view.Construction);
        Assert.False(work.IsFunded);
        Assert.Equal(LocalConstructionWorkBlocker.NotFunded, work.Blocker);
        var choice = Assert.Single(work.Choices);
        Assert.Null(work.Prepare(choice.ToolLotId));
        Assert.Equal(1UL, state.Inventory.Custody.GetLot(choice.ToolLotId).Quantity);
        Assert.Empty(new LocalWorldView(state, fixture.Helper).ConstructionWork);
        Assert.Empty(view.Structures!);
        Assert.Same(state, fixture.State);
    }

    [Fact]
    public void Actual_funding_and_stance_prepare_true_delegated_work_without_inventory_or_quest_disclosure()
    {
        using var fixture = new HumanConstructionViewFixture();
        fixture.SupplyAndPosition();
        var witness = fixture.Delegate(WorldControlCapabilities.Production);
        var before = fixture.State;
        var view = LocalControlledActorView.Project(before, fixture.Helper, fixture.Delivery(fixture.Helper), witness);
        Assert.Null(view.Inventory);
        Assert.Null(view.Conversation);
        Assert.Null(view.Recovery);
        var work = Assert.Single(view.Production!.ConstructionWorkViews);
        Assert.True(work.IsFunded);
        Assert.True(work.CanWork, work.Blocker.ToString());
        Assert.Equal(0UL, work.ProjectRevision);
        Assert.Equal(1UL, work.WorkPerAction);
        var choice = Assert.Single(work.Choices);
        Assert.Same(choice, work.Prepare(choice.ToolLotId));
        Assert.Equal(before.Construction!.Projects.Single().Revision, choice.ExpectedProjectRevision);
        Assert.Equal(before.Inventory.Custody.GetStorage(before.Construction.Projects.Single().ProjectCustodianId).Revision,
            choice.ExpectedProjectStorageRevision);
        var command = fixture.Command(fixture.Owner, choice, witness);
        fixture.Apply(command);
        Assert.Equal(fixture.Delivery(fixture.Helper).PrincipalId, command.PrincipalId);
        Assert.Equal(1UL, fixture.State.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Equal(before.Inventory.ContentHash, fixture.State.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, fixture.State.Production!.ContentHash);
    }

    [Fact]
    public void Completed_geometry_is_public_without_production_or_history_and_omits_private_backing()
    {
        using var fixture = new HumanConstructionViewFixture();
        fixture.SupplyAndPosition();
        fixture.Complete();
        var project = fixture.State.Construction!.Projects.Single();
        var witness = fixture.Delegate(WorldControlCapabilities.Traversal);
        var state = fixture.State;
        var controlled = LocalControlledActorView.Project(state, fixture.Helper, fixture.Delivery(fixture.Helper), witness);
        Assert.Null(controlled.Production);
        Assert.Null(controlled.Inventory);
        Assert.Null(controlled.Conversation);
        var visible = Assert.Single(controlled.Structures!);
        Assert.Equal(project.Installed!.StructureId, visible.StructureId);
        Assert.Equal(project.Definition.Members.Count, visible.Members.Count);
        var collision = state.Space.Structures.Single(structure => structure.StructureId == visible.StructureId);
        Assert.All(visible.Members, member => Assert.Contains(member.Collision, collision.Boxes));
        Assert.Equal(visible.Members.Select(member => member.Geometry), project.Definition.Members);
        Assert.Equal(visible.StructureId, Assert.Single(new LocalWorldView(state, fixture.Helper).Structures!).StructureId);
        string publicJson = System.Text.Json.JsonSerializer.Serialize(visible);
        foreach (var privateId in new[] { project.ProjectId, project.ReservationId, project.ProjectCustodianId,
                     project.Installed.GuardLotId, project.Installed.ProductionBatchId, project.Installed.CompletionCommandId })
            Assert.DoesNotContain(privateId.ToString(), publicJson);
        Assert.DoesNotContain("Condition", publicJson);
        Assert.DoesNotContain("Funded", publicJson);
        Assert.DoesNotContain("CompletedWork", publicJson);
        Assert.Empty(new LocalWorldView(state, fixture.Owner).ConstructionWork);
        Assert.Equal(state.ContentHash, fixture.Authority.ReadRecordedStateForHost(state.Revision).StateHash);
    }

    [Theory]
    [InlineData(1UL, LotProtection.None, false, true)]
    [InlineData(2UL, LotProtection.None, false, false)]
    [InlineData(1UL, LotProtection.Favorite, false, false)]
    [InlineData(1UL, LotProtection.None, true, false)]
    public void Tool_eligibility_requires_one_owned_unprotected_unreserved_item(ulong quantity,
        LotProtection protection, bool reserved, bool expected)
    {
        using var fixture = new HumanConstructionViewFixture();
        var project = fixture.State.Construction!.Projects.Single();
        var original = fixture.State.Inventory.Custody.Lots.Single(lot => lot.Definition == project.Definition.RequiredTool.Item);
        var lot = new CountedSourceLot(original.LotId, original.CustodianId, original.Definition,
            new SourceInterval(original.Source.SourceId, 0, quantity), 0, protection, reserved ? project.ReservationId : null);
        Assert.Equal(expected, LocalConstructionWorkProjection.IsEligibleTool(lot, original.CustodianId, project.Definition.RequiredTool.Item));
        Assert.False(LocalConstructionWorkProjection.IsEligibleTool(lot, new EntityId(0xc057, 99), project.Definition.RequiredTool.Item));
    }

    [Fact]
    public void Work_capture_uses_explicit_revision_rather_than_displayed_work_units()
    {
        var actor = new EntityId(0xc057, 1);
        var project = new LocalConstructionView(new(0xc057, 2), actor, "Qualified project", default, default, 3, 200, false);
        var request = new WorkConstructionIntent(project.ProjectId, 17, 4, new(0xc057, 3), 2);
        var view = new LocalConstructionWorkView(actor, project, 17, 1, true, true, true, true, true,
            LocalConstructionWorkBlocker.None, [request]);
        Assert.Same(request, view.Prepare(request.ToolLotId));
        Assert.Throws<ArgumentException>(() => new LocalConstructionWorkView(actor, project, 3, 1,
            true, true, true, true, true, LocalConstructionWorkBlocker.None, [request]));
        // Current Core content fixes one work unit per ordinary step; this is a DTO contrast, not a new work rule.
    }
}
