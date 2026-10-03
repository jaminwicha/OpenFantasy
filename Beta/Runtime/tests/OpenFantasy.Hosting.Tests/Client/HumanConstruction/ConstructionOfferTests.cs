using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Client.HumanConstruction;

public sealed class ConstructionOfferTests
{
    [Fact]
    public void Human_builder_can_inspect_own_unfunded_project_without_a_supplier_participation()
    {
        WorldSnapshot world = CreateWorld();
        var project = world.Construction!.Projects.Single();
        var view = new LocalWorldView(world, project.BuilderActorId);
        Assert.Null(world.Quests!.FindParticipation(project.BuilderActorId, project.QuestId));
        var target = new FarmTarget(project.ProjectId, project.Definition.DisplayName,
            FarmTargetKind.Construction, project.SiteFeet);
        var offer = Offers(world, view).Create(view, target)[FarmSemanticAction.Build];
        Assert.Equal(project.ProjectId, offer.TargetId);
        Assert.Equal(project.BuilderActorId, offer.Scope.ActorId);
        Assert.False(offer.CanExecute);
        Assert.False(string.IsNullOrWhiteSpace(offer.BlockingReason));
        Assert.Equal(0UL, world.Construction.Projects.Single().Progress.CompletedUnits);
        Assert.Equal(0UL, world.Revision);
    }

    [Fact]
    public void Missing_or_wrong_project_selection_never_substitutes_an_owned_project()
    {
        WorldSnapshot world = CreateWorld();
        var project = world.Construction!.Projects.Single();
        var view = new LocalWorldView(world, project.BuilderActorId);
        var offers = Offers(world, view);
        foreach (FarmTarget? target in new FarmTarget?[]
        {
            null,
            new(new(0xc057, 1), "Missing project", FarmTargetKind.Construction, null),
            new(project.ProjectId, "Not a construction target", FarmTargetKind.Harvestable, project.SiteFeet)
        })
        {
            var candidate = offers.Create(view, target).GetValueOrDefault(FarmSemanticAction.Build);
            Assert.True(candidate is null || candidate.Intent is null);
        }
    }

    [Fact]
    public void Work_wheel_pins_every_command_term_even_for_the_unselected_home_interface()
    {
        WorldSnapshot world = CreateWorld();
        var project = world.Construction!.Projects.Single();
        var view = new LocalWorldView(world, project.BuilderActorId);
        var binding = world.Inventory.Controls.Single(row => row.ActorId == project.BuilderActorId);
        FarmActionScope scope = FarmActionScope.From(new(binding.PrincipalId, binding.SessionId), view);
        var tool = world.Inventory.Custody.Lots.Single(lot => lot.CustodianId == world.GetResident(project.BuilderActorId).InventoryCustodianId
            && lot.Definition == project.Definition.RequiredTool.Item);
        var intent = new WorkConstructionIntent(project.ProjectId, project.Revision,
            world.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, tool.LotId, tool.Revision);
        // Counterfactual UI candidates test review ownership, not authority admission or earned funding.
        FarmActionOffer Offer(FarmActionScope capture, WorkConstructionIntent terms) => new(FarmSemanticAction.Build,
            capture, "Build", project.Definition.DisplayName, project.ProjectId, "1 work", "Work completed", terms, null);
        var original = Offer(scope, intent);
        var alternatives = new[]
        {
            Offer(scope with { ExpectedActorRevision = scope.ExpectedActorRevision + 1 }, intent),
            Offer(scope with { ActorId = new(0xc057, 2) }, intent),
            Offer(scope with { SessionId = new(0xc057, 3) }, intent),
            Offer(scope, intent with { ProjectId = new(0xc057, 4) }),
            Offer(scope, intent with { ExpectedProjectRevision = intent.ExpectedProjectRevision + 1 }),
            Offer(scope, intent with { ExpectedProjectStorageRevision = intent.ExpectedProjectStorageRevision + 1 }),
            Offer(scope, intent with { ToolLotId = new(0xc057, 5) }),
            Offer(scope, intent with { ExpectedToolLotRevision = intent.ExpectedToolLotRevision + 1 })
        };
        foreach (var changed in alternatives)
        {
            var slot = new FarmWheelSlot(original);
            slot.Refresh(changed);
            Assert.False(slot.Offer.CanExecute);
            Assert.Null(slot.Offer.Intent);
            Assert.Equal(scope, slot.Offer.Scope);
            Assert.Equal(project.ProjectId, slot.Offer.TargetId);
        }
        var equal = new FarmWheelSlot(original);
        equal.Refresh(Offer(scope, intent with { }));
        Assert.True(equal.Offer.CanExecute);
        Assert.Equal(intent, equal.Offer.Intent);
        Assert.Equal(FarmTargetKind.Construction, equal.PinnedTarget!.Kind);
    }

    private static WorldSnapshot CreateWorld() => HumanConstructionWorldAdmission.CreateEmbedded(
        HumanConstructionWorldAdmission.SeedText, ["Amber", "Birch", "Cedar"]);

    private static FarmActionOffers Offers(WorldSnapshot world, LocalWorldView view)
    {
        var binding = world.Inventory.Controls.Single(row => row.ActorId == view.PlayerActorId);
        var recipe = world.Construction!.Projects.Single().Definition.MaterialTransform;
        return new(new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId), view.PlayerActorId,
            recipe.ItemFor(recipe.Inputs.Single()).DefinitionId, null);
    }
}
