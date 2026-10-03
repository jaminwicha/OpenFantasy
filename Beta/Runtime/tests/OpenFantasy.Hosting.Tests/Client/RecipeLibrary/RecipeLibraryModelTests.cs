using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Client.SelectedProduction;
using OpenFantasy.Hosting.Tests.Simulation;

namespace OpenFantasy.Hosting.Tests.Client.RecipeLibrary;

public sealed class RecipeLibraryModelTests
{
    [Fact]
    public async Task Session_issues_exact_library_offer_and_retains_one_receipt_without_reselecting()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = SelectedProductionBindingTests.Client(fixture, player);
        await SelectedProductionBindingTests.GatherTwice(fixture, player, client);
        var observed = player.ReadSelectedObservation();
        var foreignModel = Read(fixture, observed);
        foreignModel.Select(Assert.Single(foreignModel.Entries, entry => entry.Key == "craft_rations").RecipeId);
        Assert.True(foreignModel.Prepare());
        Assert.Throws<InvalidOperationException>(() => { _ = client.SubmitOffer(foreignModel.PreparedOffer!); });
        var model = new FarmRecipeLibraryModel();
        client.RefreshRecipes(model, observed);
        model.Select(Assert.Single(model.Entries, entry => entry.Key == "craft_rations").RecipeId);
        Assert.True(model.Prepare());
        var prepared = model.PreparedOffer!;
        client.RefreshRecipes(model, observed);
        Assert.Same(prepared, model.PreparedOffer);
        player.ResumeSelected(observed);
        var pending = client.SubmitOffer(prepared);
        await SessionFixture.Until(() => player.Pending is not null || pending.IsCompleted);
        for (int step = 0; !pending.IsCompleted; step++)
        {
            Assert.True(step < 128, "The ordinary selected craft must settle within the existing managed fixture bound.");
            await fixture.Tick();
        }
        Assert.Equal(WorldActionRejection.None, (await pending).Rejection);
        Assert.Same(pending, client.SubmitOffer(prepared));
        player.PauseSelected(player.ReadSelectedObservation().Selection);
        client.RefreshRecipes(model, player.ReadSelectedObservation());
        Assert.Null(model.PreparedOffer);
        Assert.Equal(2UL, FarmCrafting.Quantity(new LocalWorldView(fixture.State, fixture.Owner), RecipeMaterialRole.Output));
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    [Fact]
    public async Task Production_only_library_discloses_both_blocked_recipes_without_borrowing_inventory_or_care()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var before = fixture.State;
        var model = Read(fixture, observed);
        Assert.Equal(new[] { "craft_potion", "craft_rations" }, model.Entries.Select(entry => entry.Key).Order());
        Assert.All(model.Entries, entry => Assert.False(entry.Offer.CanExecute));
        Assert.All(model.Entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Offer.BlockingReason)));
        var potion = Assert.Single(model.Entries, entry => entry.Key == "craft_potion");
        Assert.Contains("Berries", potion.InputsText);
        Assert.Contains("Potion", potion.OutputsText);
        Assert.True(model.Select(potion.RecipeId));
        Assert.False(model.Prepare());
        Assert.Null(model.PreparedOffer);
        Assert.False(model.Select(new(0xdace, 1)));
        Assert.Equal(potion.RecipeId, model.SelectedId);
        model.Clear();
        Assert.Empty(model.Entries);
        Assert.Null(observed.ControlledView!.Inventory);
        Assert.Null(observed.ControlledView.Recovery);
        Assert.Same(before, fixture.State);
    }

    [Fact]
    public async Task Preparation_pins_exact_recipe_and_materials_and_equal_wire_refresh_never_recaptures()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = SelectedProductionBindingTests.Client(fixture, player);
        await SelectedProductionBindingTests.GatherTwice(fixture, player, client);
        await RestoreGatheringEffort(fixture, player);
        await SelectedProductionBindingTests.GatherTwice(fixture, player, client);
        var observed = player.ReadSelectedObservation();
        var before = fixture.State;
        var model = Read(fixture, observed);
        var potion = Assert.Single(model.Entries, entry => entry.Key == "craft_potion");
        Assert.True(model.Select(potion.RecipeId));
        Assert.Null(model.PreparedOffer);
        Assert.True(model.Prepare(), model.Notice);
        var prepared = model.PreparedOffer!;
        var request = Assert.IsType<CraftIntent>(prepared.Intent).Request;
        Assert.Equal(potion.RecipeId, request.RecipeId);
        Assert.Equal(3UL, request.Ingredients.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Equal(observed.Selection, prepared.Scope.Selection);
        Assert.Equal(fixture.Owner, prepared.Scope.ActorId);
        Assert.Equal(fixture.Delivery(fixture.Grantee).PrincipalId, prepared.Scope.PrincipalId);
        var encoded = PlayerSelectedObservationCodec.EncodeWithMaterialDescriptions(new(new(0xdace, 2), 1, observed));
        var restored = PlayerSelectedObservationCodec.Decode(encoded).Observation;
        model.Refresh(FarmProductionPresentation.From(restored), FarmActionScope.From(fixture.Delivery(fixture.Grantee), restored));
        Assert.Same(prepared, model.PreparedOffer);
        Assert.True(model.Select(Assert.Single(model.Entries, entry => entry.Key == "craft_rations").RecipeId));
        Assert.Null(model.PreparedOffer);
        Assert.True(model.Prepare(), model.Notice);
        Assert.NotEqual(request.RecipeId, Assert.IsType<CraftIntent>(model.PreparedOffer!.Intent).Request.RecipeId);
        Assert.Same(before, fixture.State);
    }

    [Fact]
    public async Task New_stock_invalidates_prepared_batch_even_when_the_original_recipe_remains_possible()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = SelectedProductionBindingTests.Client(fixture, player);
        await SelectedProductionBindingTests.GatherTwice(fixture, player, client);
        var observed = player.ReadSelectedObservation();
        var model = Read(fixture, observed);
        model.Select(Assert.Single(model.Entries, entry => entry.Key == "craft_rations").RecipeId);
        Assert.True(model.Prepare(), model.Notice);
        var old = Assert.IsType<CraftIntent>(model.PreparedOffer!.Intent).Request;
        await RestoreGatheringEffort(fixture, player);
        await SelectedProductionBindingTests.GatherTwice(fixture, player, client);
        observed = player.ReadSelectedObservation();
        model.Refresh(FarmProductionPresentation.From(observed), FarmActionScope.From(fixture.Delivery(fixture.Grantee), observed));
        Assert.True(model.Selected!.Offer.CanExecute);
        Assert.Null(model.PreparedOffer);
        Assert.Contains("changed", model.Notice!);
        model.Refresh(FarmProductionPresentation.From(observed), FarmActionScope.From(fixture.Delivery(fixture.Grantee), observed));
        Assert.Null(model.PreparedOffer);
        Assert.True(model.Prepare());
        Assert.NotEqual(old.ExpectedStorageRevision, Assert.IsType<CraftIntent>(model.PreparedOffer!.Intent).Request.ExpectedStorageRevision);
    }

    [Fact]
    public async Task Home_generation_zero_and_selected_lifetimes_both_pin_actor_revision_and_identity()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = SelectedProductionBindingTests.Client(fixture, player);
        await SelectedProductionBindingTests.GatherTwice(fixture, player, client);
        var observed = player.ReadSelectedObservation();
        var home = new LocalWorldView(fixture.State, fixture.Owner);
        var homeScope = FarmActionScope.From(fixture.Delivery(fixture.Owner), home) with
        { Selection = new(new(0xdace, 3), fixture.Owner, fixture.Owner, 0, null) };
        var presentations = new[]
        {
            (FarmProductionPresentation.From(observed), FarmActionScope.From(fixture.Delivery(fixture.Grantee), observed)),
            (FarmProductionPresentation.From(home), homeScope)
        };
        foreach (var (presentation, scope) in presentations)
        {
            var changedScopes = new[]
            {
                scope with { ExpectedActorRevision = scope.ExpectedActorRevision + 1 },
                scope with { BranchId = new(0xdace, 4) },
                scope with { SessionId = new(0xdace, 5) },
                scope with { Selection = scope.Selection! with { Generation = scope.Selection!.Generation + 2 } }
            };
            foreach (var changed in changedScopes)
            {
                var model = new FarmRecipeLibraryModel();
                model.Refresh(presentation, scope);
                model.Select(Assert.Single(model.Entries, entry => entry.Key == "craft_rations").RecipeId);
                Assert.True(model.Prepare(), model.Notice);
                model.Refresh(presentation, changed);
                Assert.Null(model.PreparedOffer);
            }
        }
    }

    [Fact]
    public async Task Inventory_only_names_potion_and_exact_axe_without_exposing_treatment_or_production()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Inventory);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var view = FarmInventoryPresentation.From(observed);
        var model = new FarmInventoryModel(fixture.WoodDefinition, fixture.BerryDefinition);
        model.Refresh(view);
        var potion = Assert.Single(model.Entries, entry => entry.Name == "Potion");
        Assert.DoesNotContain("HP", potion.Details);
        Assert.DoesNotContain("Care wheel", potion.Details);
        Assert.Contains(model.Entries, entry => entry.Name == "Copper Axe");
        Assert.Null(observed.ControlledView!.Recovery);
        Assert.Null(observed.ControlledView.Production);
        Assert.Throws<InvalidOperationException>(() => FarmProductionPresentation.From(observed));
    }

    private static FarmRecipeLibraryModel Read(SelectedProductionClientFixture fixture, LocalSelectedPlayerObservation observation)
    {
        var model = new FarmRecipeLibraryModel();
        model.Refresh(FarmProductionPresentation.From(observation), FarmActionScope.From(fixture.Delivery(fixture.Grantee), observation));
        return model;
    }

    private static async Task RestoreGatheringEffort(SelectedProductionClientFixture fixture, LocalPlayerConnection player)
    {
        player.ResumeSelected(player.ReadSelectedObservation());
        for (int step = 0; fixture.State.Gathering!.GetActorWork(fixture.Owner).Effort < GatherRulesV1.MaximumEffort; step++)
        {
            Assert.True(step < 128, "Additional harvests must earn their effort through ordinary host steps.");
            await fixture.Tick();
        }
        player.PauseSelected(player.ReadSelectedObservation().Selection);
    }
}
