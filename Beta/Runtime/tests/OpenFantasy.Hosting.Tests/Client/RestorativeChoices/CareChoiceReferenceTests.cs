using OpenFantasy.Client;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;

namespace OpenFantasy.Hosting.Tests.Client.RestorativeChoices;

public sealed class CareChoiceReferenceTests
{
    [Fact]
    public void Identical_terms_keep_distinct_stable_visible_references_and_withdraw_only_the_reviewed_grant()
    {
        var fixture = CareChoiceAdmission.Create(identicalTerms: true);
        var session = new CareWorldSession(fixture.Genesis);
        var state = fixture.ReachInjuredExploration(session);
        var permissions = state.Care!.Ordinary!.Permissions;
        Assert.Equal(2, permissions.Count);
        Assert.Equal(permissions[0].UseDefinitionId, permissions[1].UseDefinitionId);
        Assert.Equal(permissions[0].HelperActorId, permissions[1].HelperActorId);
        var library = new FarmCareLibraryModel();
        Refresh(library, state, fixture);
        Assert.Null(library.SelectedId);
        var entries = library.Entries.ToArray();
        Assert.Equal(4, entries.Select(entry => entry.Label).Distinct().Count());
        foreach (var entry in entries)
        {
            Assert.StartsWith("Ref ", entry.Label);
            Assert.Contains(entry.Key.Id.ToString(), entry.Offer.DetailText!);
            Assert.True(library.Select(entry.Key));
            Assert.True(library.Prepare(), library.Notice);
            var exact = library.PreparedOffer!;
            Assert.StartsWith("Ref ", exact.Label);
            Assert.Contains(entry.Key.Id.ToString(), exact.DetailText!);
            Refresh(library, state, fixture);
            Assert.Same(exact, library.PreparedOffer);
            Assert.Equal(entry.Label, library.Selected!.Label);
            Assert.Same(state, session.ReadStateForHost());
        }
        var chosen = entries.First(entry => entry.Key.Kind == FarmCareChoiceKind.WithdrawPermission);
        var remaining = entries.Single(entry => entry.Key.Kind == FarmCareChoiceKind.WithdrawPermission
            && entry.Key != chosen.Key);
        library.Select(chosen.Key);
        Assert.True(library.Prepare());
        var captured = Assert.IsType<RevokeCarePermissionIntent>(library.PreparedOffer!.Intent);
        Assert.Equal(chosen.Key.Id, captured.PermissionId);
        state = CareRewardJourney.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(state, fixture.Player, captured)])).Candidate;
        Assert.False(state.Care!.Ordinary!.Permissions.Single(value => value.PermissionId == chosen.Key.Id).IsActive);
        Assert.True(state.Care.Ordinary.Permissions.Single(value => value.PermissionId == remaining.Key.Id).IsActive);
        Refresh(library, state, fixture);
        Assert.Equal(chosen.Key, library.SelectedId);
        Assert.Null(library.PreparedOffer);
        Assert.False(library.Selected!.Offer.CanExecute);
        Assert.Equal(chosen.Label, library.Selected.Label);
        Assert.Equal(remaining.Label, library.Entries.Single(entry => entry.Key == remaining.Key).Label);
        Assert.EndsWith(remaining.Key.Id.ToString(),
            library.Entries.Single(entry => entry.Key == remaining.Key).Offer.DetailText!);
        Assert.Empty(state.Care.Ordinary.Uses);
    }

    [Fact]
    public void Noncolliding_choices_keep_the_concise_usual_labels_and_details()
    {
        var fixture = CareChoiceAdmission.Create();
        var library = new FarmCareLibraryModel();
        Refresh(library, fixture.Genesis, fixture);
        Assert.All(library.Entries, entry =>
        {
            Assert.DoesNotContain("Ref ", entry.Label);
            Assert.DoesNotContain("Choice reference", entry.Offer.DetailText!);
        });
    }

    private static void Refresh(FarmCareLibraryModel library, WorldSnapshot state, OrdinaryCareWorldFixture fixture)
    {
        var view = new LocalWorldView(state, fixture.Player);
        var control = state.Inventory.Controls.Single(value => value.ActorId == fixture.Player);
        library.Refresh(FarmCarePresentation.From(view), FarmActionScope.From(new(control.PrincipalId, control.SessionId), view));
    }
}
