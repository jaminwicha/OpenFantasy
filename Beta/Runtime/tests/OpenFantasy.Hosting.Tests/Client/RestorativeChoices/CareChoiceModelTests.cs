using OpenFantasy.Client;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;

namespace OpenFantasy.Hosting.Tests.Client.RestorativeChoices;

public sealed class CareChoiceModelTests
{
    [Fact]
    public void Two_medicines_and_two_same_helper_permissions_are_explicit_distinct_unspent_choices()
    {
        var fixture = CareChoiceAdmission.Create();
        var session = new CareWorldSession(fixture.Genesis);
        var state = fixture.ReachInjuredExploration(session);
        var model = Read(state, fixture);
        Assert.Equal(4, model.Entries.Count);
        Assert.Null(model.SelectedId);
        Assert.Null(model.PreparedOffer);
        Assert.False(model.Prepare());
        var uses = model.Entries.Where(entry => entry.Key.Kind == FarmCareChoiceKind.UseRemedy).ToArray();
        Assert.Equal(2, uses.Select(entry => entry.Label).Distinct().Count());
        foreach (var entry in uses)
        {
            Assert.True(entry.Offer.CanExecute, entry.Offer.BlockingReason);
            Assert.True(model.Select(entry.Key));
            Assert.True(model.Prepare());
            var intent = Assert.IsType<UseRestorativeItemIntent>(model.PreparedOffer!.Intent);
            Assert.Equal(entry.Key.Id, intent.UseDefinitionId);
            Assert.Same(state, session.ReadStateForHost());
        }
        var permissions = model.Entries.Where(entry => entry.Key.Kind == FarmCareChoiceKind.WithdrawPermission).ToArray();
        Assert.Equal(2, permissions.Select(entry => entry.Label).Distinct().Count());
        foreach (var entry in permissions)
        {
            Assert.True(model.Select(entry.Key));
            Assert.Null(model.PreparedOffer);
            Assert.True(model.Prepare());
            Assert.Equal(entry.Key.Id, Assert.IsType<RevokeCarePermissionIntent>(model.PreparedOffer!.Intent).PermissionId);
            Assert.Contains("permanent", entry.Offer.DetailText);
        }
        Assert.Same(state, session.ReadStateForHost());
    }

    [Fact]
    public void A_consumed_shared_dose_invalidates_review_without_switching_to_another_definition()
    {
        var fixture = CareChoiceAdmission.Create();
        var session = new CareWorldSession(fixture.Genesis);
        var state = fixture.ReachInjuredExploration(session);
        var model = Read(state, fixture);
        var chosen = model.Entries.First(entry => entry.Key.Kind == FarmCareChoiceKind.UseRemedy);
        model.Select(chosen.Key);
        Assert.True(model.Prepare());
        var prepared = model.PreparedOffer!;
        Refresh(model, state, fixture);
        Assert.Same(prepared, model.PreparedOffer);
        var committed = CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(state, fixture.Player, prepared.Intent!)])).Candidate;
        Refresh(model, committed, fixture);
        Assert.Equal(chosen.Key, model.SelectedId);
        Assert.Null(model.PreparedOffer);
        Assert.False(model.Selected!.Offer.CanExecute);
        Assert.Contains("changed", model.Notice!);
        Assert.Single(committed.Care!.Ordinary!.Uses);
    }

    [Fact]
    public void Withdrawal_targets_one_agreement_and_keeps_other_same_helper_permission_active()
    {
        var fixture = CareChoiceAdmission.Create();
        var session = new CareWorldSession(fixture.Genesis);
        var state = fixture.ReachInjuredExploration(session);
        var model = Read(state, fixture);
        var chosen = model.Entries.First(entry => entry.Key.Kind == FarmCareChoiceKind.WithdrawPermission);
        model.Select(chosen.Key);
        Assert.True(model.Prepare());
        var prepared = model.PreparedOffer!;
        var after = CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(state, fixture.Player, prepared.Intent!)])).Candidate;
        Assert.False(after.Care!.Ordinary!.Permissions.Single(value => value.PermissionId == chosen.Key.Id).IsActive);
        Assert.True(after.Care.Ordinary.Permissions.Single(value => value.PermissionId != chosen.Key.Id).IsActive);
        Assert.Equal(state.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(state.Production!.ContentHash, after.Production!.ContentHash);
        Refresh(model, after, fixture);
        Assert.Equal(chosen.Key, model.SelectedId);
        Assert.Null(model.PreparedOffer);
        Assert.Contains("already been withdrawn", model.Selected!.Offer.BlockingReason!);
    }

    [Fact]
    public void Missing_description_remains_explicit_without_inventing_agreement_terms()
    {
        var fixture = CareChoiceAdmission.Create();
        var view = new LocalWorldView(fixture.Genesis, fixture.Player);
        var row = view.Restorative!.PermissionOffers[0];
        Assert.NotNull(row.Description);
        var legacy = row with { Description = null };
        var scope = Scope(fixture.Genesis, fixture);
        var offer = RestorativeActionOffers.Withdraw(scope, legacy);
        Assert.Contains("details unavailable", RestorativeActionOffers.PermissionLabel(legacy));
        Assert.Contains("description of this exact agreement is unavailable", offer.DetailText);
        Assert.DoesNotContain("40 HP", offer.DetailText);
        Assert.Equal(row.Intent, offer.Intent);
    }

    private static FarmCareLibraryModel Read(WorldSnapshot state, OrdinaryCareWorldFixture fixture)
    {
        var model = new FarmCareLibraryModel();
        Refresh(model, state, fixture);
        return model;
    }

    private static void Refresh(FarmCareLibraryModel model, WorldSnapshot state, OrdinaryCareWorldFixture fixture) =>
        model.Refresh(FarmCarePresentation.From(new LocalWorldView(state, fixture.Player)), Scope(state, fixture));

    private static FarmActionScope Scope(WorldSnapshot state, OrdinaryCareWorldFixture fixture)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == fixture.Player);
        return FarmActionScope.From(new(control.PrincipalId, control.SessionId), new LocalWorldView(state, fixture.Player));
    }
}
