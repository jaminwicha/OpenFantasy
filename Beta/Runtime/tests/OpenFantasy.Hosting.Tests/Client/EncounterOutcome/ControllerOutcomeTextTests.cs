using OpenFantasy.Client;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Tests.WorldStorage;
using static OpenFantasy.Hosting.Tests.Client.EncounterOutcome.EncounterOutcomeTextTests;

namespace OpenFantasy.Hosting.Tests.Client.EncounterOutcome;

public sealed class ControllerOutcomeTextTests
{
    [Fact]
    public void Captured_action_completion_notice_uses_character_acknowledgment()
    {
        LocalWorldView source = new(WorldServiceFixture.Genesis(), WorldServiceFixture.Id(10));
        LocalWorldView view = (WorldViewData.From(source) with
        {
            Encounter = new(Id(6), 1, 0, false, false, true),
            EncounterOutcome = ControllerOutcome(LocalOutcomeStage.AwaitingYourReading)
        }).ToView();
        var scope = new FarmActionScope(Id(20), Id(21), view.PlayerActorId, view.WorldId, view.BranchId, 0);
        FarmActionOffer offer = EncounterActionOffers.Create(view, scope, null)![FarmSemanticAction.ReleaseEncounter];
        Assert.Equal("Outcome accepted; completion waits for every required character's acknowledgment and no pending care.",
            offer.CompletionText);
        Assert.DoesNotContain("reader", offer.CompletionText, StringComparison.Ordinal);
        Assert.IsType<AcknowledgeEncounterOutcomeIntent>(offer.Intent);
    }

    [Theory]
    [InlineData(LocalOutcomeStage.AwaitingYourReading)]
    [InlineData(LocalOutcomeStage.AwaitingOthers)]
    [InlineData(LocalOutcomeStage.Completing)]
    [InlineData(LocalOutcomeStage.CarePending)]
    [InlineData(LocalOutcomeStage.Completed)]
    public void Every_presented_surface_avoids_claiming_the_human_read_or_requested_care(LocalOutcomeStage stage)
    {
        LocalEncounterOutcomeView outcome = ControllerOutcome(stage);
        LocalCareView care = stage == LocalOutcomeStage.CarePending
            ? EncounterOutcomeTextTests.Care(LocalCareStatus.Pending, pendingId: Id(9))
            : EncounterOutcomeTextTests.Care(LocalCareStatus.Available, Offer(true, WorldActionRejection.None));
        string[] surfaces = [EncounterOutcomeText.ReadBlocker(outcome, care) ?? "",
            EncounterOutcomeText.ChoiceDetail(outcome, care), EncounterOutcomeText.ReadDetail(outcome, care),
            EncounterOutcomeText.Status(outcome, care), EncounterOutcomeText.Hint(outcome, care),
            EncounterOutcomeText.Card(outcome, care)];
        foreach (string text in surfaces)
        {
            Assert.DoesNotContain("read the", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("reading", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("has read", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("have read", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("you requested", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("every player", text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.All(EncounterOutcomeText.Hint(outcome, care).Split('\n'), line => Assert.True(line.Length <= 48, line));
    }

    [Fact]
    public void Recorded_acceptance_is_attributed_to_the_character_without_identifying_its_submitter()
    {
        var waiting = ControllerOutcome(LocalOutcomeStage.AwaitingOthers);
        string text = EncounterOutcomeText.ReadBlocker(waiting)!;
        Assert.Contains("Your character's acknowledgment is recorded", text, StringComparison.Ordinal);
        Assert.Contains("one remaining character's acknowledgment", text, StringComparison.Ordinal);
        Assert.DoesNotContain("autonomous", text, StringComparison.OrdinalIgnoreCase);
        var complete = ControllerOutcome(LocalOutcomeStage.Completed);
        Assert.Contains("Your character's acknowledgment is recorded", EncounterOutcomeText.Card(complete, null), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LocalCareStatus.Pending, "if that treatment happens it resumes this same fight")]
    [InlineData(LocalCareStatus.Unavailable, "The fight stays open, unfinished")]
    [InlineData(LocalCareStatus.NeedsAttention, "the treatment needs attention")]
    public void Pending_care_reports_the_request_without_claiming_personal_input_or_helper_commitment(LocalCareStatus status,
        string expected)
    {
        var outcome = ControllerOutcome(LocalOutcomeStage.CarePending, LocalOutcomeRestriction.Incapacitated);
        var care = EncounterOutcomeTextTests.Care(status, pendingId: Id(9));
        string card = EncounterOutcomeText.Card(outcome, care);
        Assert.Contains("Care was requested from Wren", card, StringComparison.Ordinal);
        Assert.Contains(expected, card, StringComparison.Ordinal);
        Assert.DoesNotContain("You requested", card, StringComparison.Ordinal);
        Assert.DoesNotContain("has treated", card, StringComparison.Ordinal);
        Assert.DoesNotContain("will treat", card, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LocalOutcomeStanding.YourSideStands)]
    [InlineData(LocalOutcomeStanding.OpponentStands)]
    [InlineData(LocalOutcomeStanding.NoSideStands)]
    public void Acceptance_keeps_the_complete_condition_and_declined_care_explicit(LocalOutcomeStanding standing)
    {
        var outcome = ControllerOutcome(LocalOutcomeStage.AwaitingYourReading, LocalOutcomeRestriction.Incapacitated, standing);
        var care = EncounterOutcomeTextTests.Care(LocalCareStatus.Available, Offer(true, WorldActionRejection.None));
        string detail = EncounterOutcomeText.ReadDetail(outcome, care);
        Assert.Contains("every required character has acknowledged it and no care is pending", detail, StringComparison.Ordinal);
        Assert.Contains("Accepting declines that care", detail, StringComparison.Ordinal);
        Assert.Contains("incapacitated", detail, StringComparison.Ordinal);
        Assert.Contains("all required acknowledgments, with no pending care", EncounterOutcomeText.ChoiceDetail(outcome, care), StringComparison.Ordinal);
    }

    [Fact]
    public void Outside_observer_is_never_told_its_character_acknowledged()
    {
        var outcome = ControllerOutcome(LocalOutcomeStage.AwaitingOthers, standing: LocalOutcomeStanding.Observer);
        Assert.Contains("no acknowledgment to give", EncounterOutcomeText.ReadBlocker(outcome), StringComparison.Ordinal);
        Assert.Contains("no acknowledgment needed", EncounterOutcomeText.Hint(outcome, null), StringComparison.Ordinal);
        Assert.DoesNotContain("acknowledgment is recorded", EncounterOutcomeText.Card(outcome, null), StringComparison.Ordinal);
    }

    private static LocalEncounterOutcomeView ControllerOutcome(LocalOutcomeStage stage,
        LocalOutcomeRestriction restriction = LocalOutcomeRestriction.AwaitingCompletion,
        LocalOutcomeStanding standing = LocalOutcomeStanding.YourSideStands) =>
        new(stage, Id(6), EncounterOutcomeResult.Victory, 1, standing, [],
            stage == LocalOutcomeStage.AwaitingYourReading ? new AcknowledgeEncounterOutcomeIntent(Id(6), ContentHash.Compute("acceptance"u8)) : null,
            2, stage is LocalOutcomeStage.Completing or LocalOutcomeStage.Completed ? 0 : 1,
            stage is LocalOutcomeStage.AwaitingOthers or LocalOutcomeStage.Completing or LocalOutcomeStage.Completed,
            restriction, 0, stage == LocalOutcomeStage.Completed ? 9UL : null,
            LocalOutcomeAcknowledgmentMode.ControllerAcceptance);
}
