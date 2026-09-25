using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class BreachActionOfferTests
{
    [Theory]
    [InlineData(10, false)]
    [InlineData(11, true)]
    [InlineData(12, true)]
    public void Legacy_excavation_and_assisted_traversal_are_distinct_capabilities(int profile, bool climb)
    {
        var fixture = new ExcavationWorldFixture();
        WorldSnapshot world = fixture.Genesis;
        if (profile >= 11) world = WorldConstructedSupportAdmission.CreateGenesis(world);
        if (profile == 12) world = WorldSteppedSupportAdmission.CreateGenesis(world);
        var view = new LocalWorldView(world, fixture.Player);
        Assert.False(view.Worksite!.HasPhysicalConstruction);
        Assert.Equal(climb, view.Worksite.SupportsAssistedTraversal);
        Assert.Equal(ExcavationReachMode.SameLevelSide, Assert.Single(view.Worksite.ExcavationTargets).ReachMode);
        var target = new FarmTarget(fixture.Site.SiteId, "Selected face", FarmTargetKind.Excavation, fixture.Site.Stance);
        var offers = Offers(world).Create(view, target);
        Assert.Contains(FarmSemanticAction.Excavate, offers.Keys);
        Assert.Equal(climb, offers.ContainsKey(FarmSemanticAction.Traverse));
        Assert.False(offers[FarmSemanticAction.Excavate].CanExecute);
        Assert.Contains("tired", offers[FarmSemanticAction.Excavate].BlockingReason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Ordinary_bank_walk_exposes_one_exact_mining_offer_without_climbing_and_stale_confirmation_cannot_retarget()
    {
        WorldSnapshot world = BreachFarmAdmission.CreateEmbedded("breach-captured-offer", "Traveler");
        var actions = new ConstructionUpgradeFixture();
        var processor = new WorldStepProcessor();
        EntityId player = Player(world);
        var selection = new FarmTargetSelection(Speaker(world));
        var initialView = new LocalWorldView(world, player);
        selection.Refresh(initialView);
        for (int count = 0; selection.Selected?.Kind != FarmTargetKind.Excavation; count++)
        {
            Assert.True(count < initialView.Actors.Count + initialView.GatheringNodes.Count + initialView.Worksite!.ExcavationTargets.Count);
            selection.Cycle();
        }
        FarmTarget selected = Assert.IsType<FarmTarget>(selection.Selected);
        Assert.Equal(world.Excavation!.Sites.Single().SiteId, selected.Id);
        Assert.Equal(BreachFarmLayout.MiningStance, selected.Feet);
        var factory = Offers(world);
        Assert.DoesNotContain(FarmSemanticAction.Excavate, factory.Create(initialView).Keys);
        var initialOffers = factory.Create(initialView, selected);
        Assert.False(initialOffers[FarmSemanticAction.Excavate].CanExecute);
        Assert.DoesNotContain(FarmSemanticAction.Traverse, initialOffers.Keys);
        Assert.DoesNotContain(initialOffers.Values, offer => offer.Intent is ClimbIntent);

        // Reach the real bank through ordinary committed movement; no actor pose or effort is supplied.
        SubunitPosition start = world.Space.GetActor(player).Feet;
        foreach (var target in new[] { new SubunitPosition(start.X, BreachFarmLayout.MiningStance.Y, start.Z), BreachFarmLayout.MiningStance })
        {
            for (int step = 0; world.Space.GetActor(player).Feet != target; step++)
            {
                Assert.True(step < 256);
                SubunitPosition feet = world.Space.GetActor(player).Feet;
                long dx = Math.Clamp(target.X - feet.X, -32, 32);
                long dy = dx == 0 ? Math.Clamp(target.Y - feet.Y, -32, 32) : 0;
                world = processor.Decide(world, actions.Batch(world, [],
                    [new(ConstructionUpgradeFixture.Delivery(world, player), player, dx, dy)])).Candidate;
                Assert.Equal(feet.Offset(dx, dy, 0), world.Space.GetActor(player).Feet);
            }
        }
        Assert.True(world.Gathering!.GetActorWork(player).Effort >= GatherRulesV1.EffortPerSwing);
        var readyView = new LocalWorldView(world, player);
        selection.Refresh(readyView);
        Assert.Equal(selected.Id, selection.Selected!.Id);
        byte[] readyBytes = WorldSnapshotCodec.Encode(world);
        var offers = factory.Create(readyView, selection.Selected);
        FarmActionOffer captured = offers[FarmSemanticAction.Excavate];
        FarmActionOffer unusedStale = factory.Create(readyView, selection.Selected)[FarmSemanticAction.Excavate];
        var intent = Assert.IsType<ExcavateIntent>(captured.Intent);
        LocalExcavationView site = Assert.Single(readyView.Worksite!.ExcavationTargets);
        LocalExcavationToolView tool = Assert.Single(readyView.Worksite.Tools);
        Assert.True(captured.CanExecute);
        Assert.Equal("400 effort", captured.CostText);
        Assert.Contains("water", captured.DetailText!, StringComparison.Ordinal);
        Assert.DoesNotContain("settle", captured.DetailText!, StringComparison.Ordinal);
        Assert.Equal(new ExcavateIntent(site.SiteId, site.SiteRevision, site.TerrainRevision,
            tool.LotId, tool.Revision, site.DestinationRevision), intent);
        Assert.Equal(world.Space.GetActor(player).Revision, captured.Scope.ExpectedActorRevision);
        Assert.DoesNotContain(FarmSemanticAction.Traverse, offers.Keys);
        var wheel = new FarmQuickWheelModel(offers);
        Assert.Equal(FarmSemanticAction.Excavate, wheel.Slots[1]!.Action);
        Assert.DoesNotContain(wheel.Slots, slot => slot?.Action == FarmSemanticAction.Traverse);
        Assert.Equal(readyBytes, WorldSnapshotCodec.Encode(world));

        WorldStepDecision applied = processor.Decide(world, actions.Batch(world, [captured.ToCommand(actions.NextIdentity())], []));
        Assert.Equal(WorldActionRejection.None, Assert.Single(applied.Receipts).Rejection);
        WorldSnapshot worked = applied.Candidate;
        Assert.Equal(1UL, worked.Excavation!.Sites.Single().CompletedWork);
        Assert.Equal(world.Inventory.ContentHash, worked.Inventory.ContentHash);
        Assert.Equal(world.LiquidCells!.Topology!.ContentHash, worked.LiquidCells!.Topology!.ContentHash);
        Assert.True(new LocalWorldView(worked, player).Worksite!.Terrain.Single(region => region.Bounds.Contains(site.Cell)).IsSolid(site.Cell));
        Assert.Equal(0UL, intent.ExpectedSiteRevision);
        WorldStepDecision rejected = processor.Decide(worked, actions.Batch(worked, [unusedStale.ToCommand(actions.NextIdentity())], []));
        Assert.Equal(WorldActionRejection.RevisionConflict, Assert.Single(rejected.Receipts).Rejection);
        Assert.Equal(worked.Excavation.ContentHash, rejected.Candidate.Excavation!.ContentHash);
        Assert.Equal(worked.Inventory.ContentHash, rejected.Candidate.Inventory.ContentHash);
        Assert.Equal(worked.LiquidCells.Topology.ContentHash, rejected.Candidate.LiquidCells!.Topology!.ContentHash);
        Assert.Equal(site.SiteId, Assert.IsType<ExcavateIntent>(unusedStale.Intent).SiteId);

        var missing = new FarmTarget(new(0x627265616368, 999), "Missing face", FarmTargetKind.Excavation, null);
        FarmActionOffer blocked = factory.Create(new LocalWorldView(worked, player), missing)[FarmSemanticAction.Excavate];
        Assert.False(blocked.CanExecute);
        Assert.Equal(missing.Id, blocked.TargetId);
        Assert.Null(blocked.Intent);
        Assert.Contains("no longer visible", blocked.BlockingReason!, StringComparison.Ordinal);
    }

    private static EntityId Player(WorldSnapshot world) => world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
    private static EntityId Speaker(WorldSnapshot world) => world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Host).ActorId;
    private static FarmActionOffers Offers(WorldSnapshot world) => new(ConstructionUpgradeFixture.Delivery(world, Player(world)),
        Speaker(world), world.Quests!.Definitions[0].Item.DefinitionId, null);
}
