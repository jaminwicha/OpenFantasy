using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Client.SelectedGifts;
using OpenFantasy.Hosting.Tests.Simulation;

namespace OpenFantasy.Hosting.Tests.Client.SelectedProduction;

/// <summary>Managed adapter proofs over actual admitted ports and recorded authority; not TLS or rendered input evidence.</summary>
public sealed class SelectedProductionBindingTests
{
    [Fact]
    public async Task Production_only_gathers_twice_and_crafts_owned_material_with_real_grantee_delivery()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = Client(fixture, player);
        var controlled = observed.ControlledView!;
        Assert.Equal(WorldControlCapabilities.Production, controlled.Capabilities);
        Assert.Null(controlled.Inventory);
        Assert.Null(controlled.Combat);
        Assert.Null(controlled.Recovery);
        Assert.Null(controlled.CareDisposition);
        Assert.Null(controlled.Trade);
        Assert.Null(controlled.Conversation);
        var before = fixture.State;
        var homeBefore = new LocalWorldView(before, fixture.Grantee);
        var target = Berry(observed, fixture);
        var emissions = new List<FarmSubmission>();
        client.Submitted += emissions.Add;
        Assert.False(client.CreateOffers(observed, target)[FarmSemanticAction.Craft].CanExecute);
        await GatherTwice(fixture, player, client);
        observed = player.ReadSelectedObservation();
        var craft = client.CreateOffers(observed, target)[FarmSemanticAction.Craft];
        Assert.True(craft.CanExecute, craft.BlockingReason);
        var request = Assert.IsType<CraftIntent>(craft.Intent).Request;
        var owned = new LocalWorldView(fixture.State, fixture.Owner).PlayerInventory;
        Assert.Equal(owned.CustodianId, request.CustodianId);
        Assert.Equal(owned.Revision, request.ExpectedStorageRevision);
        foreach (var ingredient in request.Ingredients)
            Assert.Contains(owned.Lots, lot => lot.LotId == ingredient.LotId && lot.Revision == ingredient.ExpectedRevision
                && lot.Provenance == ingredient.Kind && lot.Quantity >= ingredient.Quantity);
        Assert.Equal(WorldActionRejection.None, (await Execute(fixture, player, client, craft)).Rejection);
        Assert.Equal(3, emissions.Count);
        foreach (var emitted in emissions)
        {
            Assert.Equal(fixture.Owner, emitted.Command.ActorId);
            Assert.Equal(fixture.Delivery(fixture.Grantee).PrincipalId, emitted.Command.PrincipalId);
            Assert.Equal(fixture.Delivery(fixture.Grantee).SessionId, emitted.Command.OriginSessionId);
            Assert.Equal(acquired.Witness, emitted.Command.ControlWitness);
            var receipt = await emitted.Receipt;
            Assert.Equal(emitted.Command.CommandId, receipt.CommandId);
            Assert.Equal(emitted.Command.ContentHash, receipt.CommandHash);
            Assert.Equal(fixture.Genesis.WorldId, receipt.WorldId);
            Assert.Equal(fixture.Genesis.BranchId, receipt.BranchId);
        }
        foreach (var emitted in emissions.Take(2))
        {
            var intent = Assert.IsType<GatherIntent>(emitted.Command.Intent);
            Assert.Equal(fixture.BerryNode, intent.NodeId);
            Assert.Contains(controlled.Production!.GatheringTools,
                tool => tool.LotId == intent.ToolLotId);
        }
        var after = fixture.State;
        var ownerAfter = new LocalWorldView(after, fixture.Owner);
        var recipe = observed.ControlledView!.Production!.CraftPreparation!.Recipes.Single(value => value.Key == "craft_rations");
        var output = Assert.Single(recipe.Terms, term => term.Role == RecipeMaterialRole.Output);
        Assert.Equal(output.Quantity, ownerAfter.PlayerInventory.Lots.Where(lot => lot.DefinitionId == output.DefinitionId)
            .Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity)));
        Assert.Equal(before.Gathering!.GetNode(fixture.BerryNode).RemainingSwings - 2,
            after.Gathering!.GetNode(fixture.BerryNode).RemainingSwings);
        Assert.Equal(homeBefore.PlayerInventory.Lots, new LocalWorldView(after, fixture.Grantee).PlayerInventory.Lots);
        Assert.Equal(before.Space.GetActor(fixture.Grantee).Feet, after.Space.GetActor(fixture.Grantee).Feet);
        Assert.Equal(before.Care!.ContentHash, after.Care!.ContentHash);
        foreach (var actor in fixture.Actors)
        {
            Assert.Equal(before.Progression!.Currency.GetActorAccount(actor).Balance,
                after.Progression!.Currency.GetActorAccount(actor).Balance);
            Assert.Equal(before.Progression.GetActor(actor).CurrentHp, after.Progression.GetActor(actor).CurrentHp);
        }
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    [Fact]
    public async Task Inventory_permission_does_not_supply_production_or_home_work_choices()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Inventory);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        Assert.Null(observed.ControlledView!.Production);
        Assert.Throws<InvalidOperationException>(() => FarmProductionPresentation.From(observed));
        var offers = Client(fixture, player).CreateOffers(observed);
        foreach (var action in new[] { FarmSemanticAction.Gather, FarmSemanticAction.Craft, FarmSemanticAction.Excavate, FarmSemanticAction.Traverse })
            Assert.False(offers.ContainsKey(action));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_wrong_or_missing_plant_never_falls_back_to_the_reachable_berry(bool missing)
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = Client(fixture, player);
        Assert.True(client.CreateOffers(observed, Berry(observed, fixture))[FarmSemanticAction.Gather].CanExecute);
        var target = missing ? new FarmTarget(new(0xda70726f, 1), "Earlier plant", FarmTargetKind.Harvestable, null)
            : new FarmTarget(fixture.Third, "Another person", FarmTargetKind.Person,
                observed.ControlledView!.Actors.Single(actor => actor.ActorId == fixture.Third).Feet);
        var offer = client.CreateOffers(observed, target)[FarmSemanticAction.Gather];
        Assert.Equal(target.Id, offer.TargetId);
        Assert.False(offer.CanExecute);
        Assert.Null(offer.Intent);
    }

    [Fact]
    public async Task Returning_to_the_same_actor_does_not_reauthorize_the_old_production_offer()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var original = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = Client(fixture, player);
        var old = client.CreateOffers(original, Berry(original, fixture))[FarmSemanticAction.Gather];
        Assert.True(old.CanExecute, old.BlockingReason);
        var before = fixture.State;
        await player.SelectActorAsync(original.Selection, fixture.Grantee);
        var returned = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        Assert.Equal(original.Selection.Generation + 2, returned.Selection.Generation);
        Assert.Throws<InvalidOperationException>(() => { _ = client.SubmitOffer(old); });
        Assert.True(client.CreateOffers(returned, Berry(returned, fixture))[FarmSemanticAction.Gather].CanExecute);
        Assert.Same(before, fixture.State);
    }

    [Fact]
    public async Task Revocation_refuses_an_unsubmitted_work_offer_and_returns_the_port_home_paused()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = Client(fixture, player);
        var old = client.CreateOffers(observed, Berry(observed, fixture))[FarmSemanticAction.Gather];
        Assert.True(old.CanExecute, old.BlockingReason);
        var stock = fixture.State.Inventory.ContentHash;
        fixture.Revoke();
        await fixture.Tick();
        Assert.Equal(fixture.Grantee, player.ReadSelectedObservation().Selection.ActorId);
        Assert.False(player.ReadSelectedObservation().IsReady);
        Assert.Throws<InvalidOperationException>(() => { _ = client.SubmitOffer(old); });
        Assert.Equal(stock, fixture.State.Inventory.ContentHash);
    }

    [Theory]
    [InlineData(WorldControlCapabilities.Production)]
    [InlineData(WorldControlCapabilities.Production | WorldControlCapabilities.Traversal)]
    public async Task Actual_owned_mining_uses_exact_work_pins_and_profile40_does_not_offer_climbing(WorldControlCapabilities scopes)
    {
        await using var fixture = await SelectedProductionClientFixture.Open(scopes, excavation: true);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var worksite = observed.ControlledView!.Production!.Worksite!;
        var site = Assert.Single(worksite.ExcavationTargets);
        var client = Client(fixture, player);
        var target = new FarmTarget(site.SiteId, "Bank face", FarmTargetKind.Excavation, site.Stance);
        var offers = client.CreateOffers(observed, target);
        Assert.False(worksite.SupportsAssistedTraversal);
        Assert.False(offers.ContainsKey(FarmSemanticAction.Traverse));
        var offer = offers[FarmSemanticAction.Excavate];
        Assert.True(offer.CanExecute, offer.BlockingReason);
        var intent = Assert.IsType<ExcavateIntent>(offer.Intent);
        Assert.Equal((site.SiteId, site.SiteRevision, site.TerrainRevision, site.DestinationRevision),
            (intent.SiteId, intent.ExpectedSiteRevision, intent.ExpectedTerrainRevision, intent.ExpectedDestinationRevision));
        Assert.Contains(worksite.Tools, tool => tool.LotId == intent.ToolLotId && tool.Revision == intent.ExpectedToolLotRevision
            && tool.DefinitionId == site.RequiredToolDefinitionId);
        Assert.Equal(WorldActionRejection.None, (await Execute(fixture, player, client, offer)).Rejection);
        var progressed = Assert.Single(player.ReadSelectedObservation().ControlledView!.Production!.Worksite!.ExcavationTargets);
        Assert.True(progressed.CompletedWork > site.CompletedWork);
        Assert.Null(player.ReadSelectedObservation().ControlledView!.Inventory);
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    internal static FarmClientSession Client(SelectedProductionClientFixture fixture, LocalPlayerConnection player) =>
        new(new SelectedGiftPort(player), fixture.Delivery(fixture.Grantee), fixture.Third,
            fixture.WoodDefinition, fixture.BerryDefinition);

    internal static FarmTarget Berry(LocalSelectedPlayerObservation observed, SelectedProductionClientFixture fixture)
    {
        var node = observed.ControlledView!.Production!.GatheringNodes.Single(value => value.NodeId == fixture.BerryNode);
        return new(node.NodeId, node.DisplayName, FarmTargetKind.Harvestable, node.Feet);
    }

    internal static async Task GatherTwice(SelectedProductionClientFixture fixture, LocalPlayerConnection player, FarmClientSession client)
    {
        for (int swing = 0; swing < 2; swing++)
        {
            await Ready(fixture, player);
            var observed = player.ReadSelectedObservation();
            var offer = client.CreateOffers(observed, Berry(observed, fixture))[FarmSemanticAction.Gather];
            Assert.True(offer.CanExecute, offer.BlockingReason);
            var intent = Assert.IsType<GatherIntent>(offer.Intent);
            Assert.Contains(observed.ControlledView!.Production!.GatheringTools,
                tool => tool.LotId == intent.ToolLotId && tool.Revision == intent.ExpectedToolLotRevision);
            Assert.Equal(WorldActionRejection.None, (await Execute(fixture, player, client, offer)).Rejection);
        }
        await Ready(fixture, player);
    }

    private static async Task Ready(SelectedProductionClientFixture fixture, LocalPlayerConnection player)
    {
        player.ResumeSelected(player.ReadSelectedObservation());
        for (int step = 0; !WorldActorAvailability.CanAct(fixture.State, fixture.Owner); step++)
        {
            Assert.True(step < 128, "An actual recovery debt must retire through ordinary host steps.");
            await fixture.Tick();
        }
        player.PauseSelected(player.ReadSelectedObservation().Selection);
    }

    private static async Task<WorldActionReceipt> Execute(SelectedProductionClientFixture fixture, LocalPlayerConnection player,
        FarmClientSession client, FarmActionOffer offer)
    {
        player.ResumeSelected(player.ReadSelectedObservation());
        var pending = client.SubmitOffer(offer);
        await SessionFixture.Until(() => player.Pending is not null || pending.IsCompleted);
        for (int step = 0; !pending.IsCompleted; step++)
        {
            Assert.True(step < 128, "The selected paid action did not settle within the finite managed fixture bound.");
            await fixture.Tick();
        }
        var receipt = await pending;
        player.PauseSelected(player.ReadSelectedObservation().Selection);
        return receipt;
    }
}
