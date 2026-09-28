using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class RestorativeActionOfferTests
{
    [Fact]
    public void Known_own_medicine_has_an_inspectable_blocker_and_separate_page_without_rearranging_farm_slots()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var state = fixture.Genesis;
        var view = new LocalWorldView(state, fixture.Player);
        var factory = Factory(fixture);
        var offers = factory.Create(view);
        var use = offers[FarmSemanticAction.UseRestorative];
        Assert.False(use.CanExecute);
        Assert.Null(use.Intent);
        Assert.Contains("already full", use.BlockingReason);
        Assert.Contains("1 Potion", use.CostText);
        Assert.Contains("40 HP", use.DetailText);
        Assert.Equal(fixture.Player, use.TargetId);
        var wheel = new FarmQuickWheelModel(offers);
        Assert.Equal(2, wheel.PageCount);
        Assert.Equal("Actions", wheel.PageName);
        Assert.Equal(new[] { FarmSemanticAction.Speak, FarmSemanticAction.Gather, FarmSemanticAction.Craft, FarmSemanticAction.Give },
            wheel.Slots.Select(slot => slot!.Action));
        Assert.Null(wheel.Selected);
        wheel.NextPage();
        Assert.Equal("Care", wheel.PageName);
        Assert.Null(wheel.Selected);
        wheel.Select(0);
        Assert.Same(use, wheel.Selected!.Offer);
        wheel.NextPage();
        Assert.Null(wheel.Selected);
        var pack = new FarmInventoryModel(Wood(state), null);
        pack.Refresh(view);
        var entry = Assert.Single(pack.Entries, entry => entry.Name == "Potion");
        Assert.Equal(1UL, entry.Quantity);
        Assert.Contains("40 HP", entry.Description);
        Assert.Equal(state.ContentHash, fixture.Genesis.ContentHash);
    }

    [Fact]
    public void Earned_self_use_captures_the_exact_dose_and_open_wheel_cannot_silently_select_a_replacement()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var world = new CareWorldSession(fixture.Genesis);
        var before = fixture.ReachInjuredExploration(world);
        var factory = Factory(fixture);
        var view = new LocalWorldView(before, fixture.Player);
        var offers = factory.Create(view);
        var use = offers[FarmSemanticAction.UseRestorative];
        var intent = Assert.IsType<UseRestorativeItemIntent>(use.Intent);
        Assert.True(use.CanExecute);
        Assert.Equal(fixture.Intent(before, self: true), intent);
        var wheel = new FarmQuickWheelModel(offers);
        wheel.NextPage();
        wheel.Select(0);
        var command = use.ToCommand(fixture.Journey.Command(before, fixture.Player, intent).CommandId);
        var after = CareRewardJourney.Commit(world, fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [command])).Candidate;
        Assert.Single(after.Care!.Ordinary!.Uses);
        var current = new LocalWorldView(after, fixture.Player);
        wheel.Refresh(target => factory.Create(current, target));
        Assert.False(wheel.Selected!.Offer.CanExecute);
        Assert.Null(wheel.Selected.Offer.Intent);
        Assert.Same(intent, use.Intent);
        Assert.Equal(fixture.Player, command.ActorId);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
    }

    [Fact]
    public void Withdrawal_captures_only_patient_authority_and_does_not_create_a_helper_item_command()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var state = fixture.Genesis;
        var offers = Factory(fixture).Create(new LocalWorldView(state, fixture.Player));
        var withdraw = offers[FarmSemanticAction.RevokeCarePermission];
        Assert.True(withdraw.CanExecute);
        Assert.Equal(fixture.Helper, withdraw.TargetId);
        Assert.Contains("permanent", withdraw.DetailText);
        var intent = Assert.IsType<RevokeCarePermissionIntent>(withdraw.Intent);
        var permission = state.Care!.Ordinary!.Permissions.Single();
        Assert.Equal(permission.PermissionId, intent.PermissionId);
        var command = withdraw.ToCommand(fixture.Journey.Command(state, fixture.Player, intent).CommandId);
        var result = new WorldStepProcessor().Decide(state, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep, [command]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(result.Receipts).Status);
        Assert.Equal(fixture.Player, command.ActorId);
        Assert.False(result.Candidate.Care!.Ordinary!.Permissions.Single().IsActive);
        Assert.Empty(result.Candidate.Care.Ordinary.Uses);
        Assert.Equal(state.Progression!.ContentHash, result.Candidate.Progression!.ContentHash);
        Assert.Equal(state.Production!.ContentHash, result.Candidate.Production!.ContentHash);
        Assert.False(Factory(fixture).Create(new LocalWorldView(result.Candidate, fixture.Player))
            [FarmSemanticAction.RevokeCarePermission].CanExecute);
    }

    [Fact]
    public void Page_control_is_inspection_only_and_cannot_leak_target_cycle_or_movement()
    {
        var router = new FarmInputRouter();
        var situation = new FarmInputSituation(true, false, false) { HasWheelPages = true };
        router.SetWheelOpen(true);
        router.TogglePause();
        Assert.True(router.Permits(situation, FarmSemanticAction.NextWheelPage));
        Assert.False(router.Permits(situation, FarmSemanticAction.ConfirmWheel));
        Assert.Equal(FarmSemanticAction.NextWheelPage, router.Route(situation,
            FarmInputSignal.Press(FarmInputActions.NextTarget)).Action);
        Assert.False(router.Permits(situation, FarmSemanticAction.UseRestorative));
        Assert.True(router.Movement(situation, new(0, 1, 0, 0)).IsNeutral);
        router.SetFocused(false);
        Assert.False(router.Permits(situation, FarmSemanticAction.NextWheelPage));
    }

    [Fact]
    public async Task Explicit_fresh_selection_never_reinterprets_an_old_care_save()
    {
        using var files = new SessionFiles();
        var old = CareJourneyAdmission.CreateEmbedded(EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName);
        using (SqliteWorldAuthority.CreateNew(files.Path, old)) { }
        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenRestorativeAsync(files.Path,
            EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName));
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(old.ContentHash, reopened.ReadStateForHost().ContentHash);
        Assert.Null(new LocalWorldView(old, old.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId).Restorative);
    }

    private static EntityId Wood(WorldSnapshot state) => state.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Oak)
        .Definition.YieldDefinition.DefinitionId;

    private static FarmActionOffers Factory(OrdinaryCareWorldFixture fixture)
    {
        var control = fixture.Genesis.Inventory.Controls.Single(value => value.ActorId == fixture.Player);
        return new(new(control.PrincipalId, control.SessionId), fixture.Helper, Wood(fixture.Genesis), null);
    }
}
