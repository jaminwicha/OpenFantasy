using System.Reflection;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;

namespace OpenFantasy.Hosting.Tests.Client.EncounterOutcome;

/// <summary>
/// Pure wording of each care and outcome reason (F3–F5). The public views are built directly at the exact states a
/// world can publish, so every stated reason is checked against the view field it comes from; the committed journeys
/// in the Worlds.EncounterOutcome family check the same wording against actual worlds. Only projected facts may be
/// stated: a missing care view is not an absent agreement, a pending request is not a helper's commitment, and every
/// consequence of accepting carries the whole completion condition.
/// </summary>
public sealed class EncounterOutcomeTextTests
{
    private static readonly ContentHash Hash = ContentHash.Compute(new byte[] { 4, 2 });

    [Fact]
    public void WheelConsequencesKeepIncapacityCompletionAndDeclinedCareExplicit()
    {
        foreach (LocalOutcomeStanding standing in new[] { LocalOutcomeStanding.OpponentStands, LocalOutcomeStanding.YourSideStands })
        {
            LocalEncounterOutcomeView outcome = Outcome(LocalOutcomeStage.AwaitingYourReading, standing,
                LocalOutcomeRestriction.Incapacitated, readable: true);
            string text = EncounterOutcomeText.ChoiceDetail(outcome, Care(LocalCareStatus.Available, Offer(true, WorldActionRejection.None)));
            Assert.Contains("all required readings, with no pending care", text, StringComparison.Ordinal);
            Assert.Contains("You stay incapacitated", text, StringComparison.Ordinal);
            Assert.Contains("Wren can help now; accepting declines that care", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Returns to exploration", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CareReasonComesFromTheCareViewNotFromAReachAssumption()
    {
        // A null view projects only that no care is offered here; it never establishes that no agreement exists.
        Assert.Equal("No care is offered to you", EncounterOutcomeText.CareReason(null));
        Assert.DoesNotContain("agree", EncounterOutcomeText.CareReason(null), StringComparison.Ordinal);
        Assert.Equal("Wren can help (care)", EncounterOutcomeText.CareReason(Care(LocalCareStatus.Available, Offer(true, WorldActionRejection.None))));
        Assert.Equal("Wren cannot treat you now", EncounterOutcomeText.CareReason(Care(LocalCareStatus.Unavailable, Offer(false, WorldActionRejection.Unavailable))));
        Assert.Equal("Wren is out of reach", EncounterOutcomeText.CareReason(Care(LocalCareStatus.Unavailable, Offer(false, WorldActionRejection.OutOfReach))));
        Assert.Equal("Wren's care offer is out of date",
            EncounterOutcomeText.CareReason(Care(LocalCareStatus.Unavailable, Offer(false, WorldActionRejection.RevisionConflict))));
        Assert.Equal("Wren's care does not cover you",
            EncounterOutcomeText.CareReason(Care(LocalCareStatus.Unavailable, Offer(false, WorldActionRejection.NotAuthorized))));
        // A pending request is the patient's own; it names the agreement's helper and claims no acceptance by them.
        Assert.Equal("Care requested from Wren", EncounterOutcomeText.CareReason(Care(LocalCareStatus.Unavailable, pendingId: Id(9))));
    }

    [Fact]
    public void DefeatWithAvailableCareSaysThatAcceptingDeclinesIt()
    {
        LocalEncounterOutcomeView defeat = Outcome(LocalOutcomeStage.AwaitingYourReading, LocalOutcomeStanding.OpponentStands,
            LocalOutcomeRestriction.Incapacitated, readable: true);
        LocalCareView available = Care(LocalCareStatus.Available, Offer(true, WorldActionRejection.None));
        Assert.Equal("Accept defeat", EncounterOutcomeText.ReadLabel(defeat));
        string detail = EncounterOutcomeText.ReadDetail(defeat, available);
        Assert.Contains("Wren can help now: ask for care first. Accepting declines that care", detail, StringComparison.Ordinal);
        // The consequence of accepting keeps its whole condition: every required reading and no pending care.
        Assert.EndsWith("Accepting declines that care; the world completes the encounter at its next ready boundary once every "
            + "required player has read it and no care is pending.", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("no rescue", detail, StringComparison.Ordinal);
        // E / A asks for care and V only opens the choices; what accepting declines is stated on the reading's own control,
        // wheel inspection and card (the detail above), never attributed to a key that does not commit it.
        Assert.Equal("Defeated\nE / A: care · V: choices", EncounterOutcomeText.Hint(defeat, available));
        Assert.DoesNotContain("V: accept", EncounterOutcomeText.Hint(defeat, available), StringComparison.Ordinal);
        Assert.Equal("Defeated\nE / A: accept defeat", EncounterOutcomeText.Hint(defeat, null));
        // Without an admitted helper the detail makes no care promise at all.
        Assert.DoesNotContain("can help", EncounterOutcomeText.ReadDetail(defeat, null), StringComparison.Ordinal);
        Assert.DoesNotContain("can help", EncounterOutcomeText.ReadDetail(defeat,
            Care(LocalCareStatus.Unavailable, Offer(false, WorldActionRejection.Unavailable))), StringComparison.Ordinal);
        Assert.Contains("once every required player has read it", EncounterOutcomeText.ReadDetail(defeat, null), StringComparison.Ordinal);
        // The card states the care view's reason only where no care is admitted.
        Assert.EndsWith("\nCare: No care is offered to you.", EncounterOutcomeText.Card(defeat, null), StringComparison.Ordinal);
        Assert.DoesNotContain("Care: ", EncounterOutcomeText.Card(defeat, available), StringComparison.Ordinal);
    }

    [Fact]
    public void PendingButUnavailableHelperIsNotDescribedAsPreparingCare()
    {
        LocalEncounterOutcomeView pending = Outcome(LocalOutcomeStage.CarePending, LocalOutcomeStanding.OpponentStands,
            LocalOutcomeRestriction.Incapacitated, readable: false);
        LocalCareView unavailable = Care(LocalCareStatus.Unavailable, pendingId: Id(9));
        string blocker = EncounterOutcomeText.ReadBlocker(pending, unavailable)!;
        Assert.Equal("You requested care from Wren; the requested care is unavailable now. The fight stays open, unfinished, "
            + "while that request is pending.", blocker);
        Assert.Contains("stays open", blocker, StringComparison.Ordinal);
        string status = EncounterOutcomeText.Status(pending, unavailable);
        Assert.Equal("Defeated · Care requested from Wren · The requested care is unavailable now · The fight stays open", status);
        Assert.DoesNotContain("preparing", status, StringComparison.Ordinal);
        // The patient's acknowledgment is not the helper's commitment: nothing says the helper accepted or agreed.
        foreach (string text in new[] { blocker, status })
        {
            Assert.DoesNotContain("accepted", text, StringComparison.Ordinal);
            Assert.DoesNotContain("agreed", text, StringComparison.Ordinal);
        }
        LocalCareView preparing = Care(LocalCareStatus.Pending, pendingId: Id(9));
        Assert.Equal("Defeated · Care requested from Wren · Treatment can proceed · World waiting", EncounterOutcomeText.Status(pending, preparing));
        Assert.DoesNotContain("preparing", EncounterOutcomeText.Status(pending, preparing), StringComparison.Ordinal);
        Assert.Contains("resumes this same fight", EncounterOutcomeText.ReadBlocker(pending, preparing)!, StringComparison.Ordinal);
        Assert.Equal("Defeated\nCare requested", EncounterOutcomeText.Hint(pending, unavailable));
        Assert.Equal("Defeated · Care requested from Wren · Treatment needs attention · World waiting",
            EncounterOutcomeText.Status(pending, Care(LocalCareStatus.NeedsAttention, pendingId: Id(9))));
    }

    [Fact]
    public void IncapacitatedWinnerIsNotPromisedExploration()
    {
        LocalEncounterOutcomeView winner = Outcome(LocalOutcomeStage.AwaitingYourReading, LocalOutcomeStanding.YourSideStands,
            LocalOutcomeRestriction.Incapacitated, readable: true);
        Assert.Equal("Victory", EncounterOutcomeText.Title(winner));
        Assert.Equal("Accept outcome", EncounterOutcomeText.ReadLabel(winner));
        string detail = EncounterOutcomeText.ReadDetail(winner);
        Assert.Contains("incapacitated (0 HP)", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("returns you to exploration", detail, StringComparison.Ordinal);
        Assert.Contains("once every required player has read it and no care is pending", detail, StringComparison.Ordinal);
        LocalEncounterOutcomeView standing = Outcome(LocalOutcomeStage.AwaitingYourReading, LocalOutcomeStanding.YourSideStands,
            LocalOutcomeRestriction.AwaitingCompletion, readable: true);
        Assert.Equal("Continue", EncounterOutcomeText.ReadLabel(standing));
        Assert.Contains("returns you to exploration once every required player has read it and no care is pending",
            EncounterOutcomeText.ReadDetail(standing), StringComparison.Ordinal);
    }

    [Fact]
    public void ObserverOwesNoReadingAndSeesOnlyObservedCondition()
    {
        LocalEncounterOutcomeView observer = Outcome(LocalOutcomeStage.AwaitingOthers, LocalOutcomeStanding.Observer,
            LocalOutcomeRestriction.AwaitingCompletion, readable: false);
        Assert.False(observer.ObserverIsParticipant);
        Assert.False(observer.IsTerminal);
        string blocker = EncounterOutcomeText.ReadBlocker(observer)!;
        Assert.Contains("between others", blocker, StringComparison.Ordinal);
        Assert.DoesNotContain("You have read", blocker, StringComparison.Ordinal);
        Assert.Contains("between others", EncounterOutcomeText.Status(observer, null), StringComparison.Ordinal);
        Assert.Equal("Encounter decided\nBetween others · nothing to read", EncounterOutcomeText.Hint(observer, null));
        Assert.Equal("Wolf · defeated side · condition not in view", EncounterOutcomeText.Participant(
            new LocalOutcomeParticipantView(Id(3), "Wolf", 2, false, false, null), EncounterOutcomeResult.Victory));
        Assert.Equal("You · standing side · HP 7/30", EncounterOutcomeText.Participant(
            new LocalOutcomeParticipantView(Id(1), "Squire", 1, true, true, new LocalOutcomeCondition(7, 30)), EncounterOutcomeResult.Victory));
    }

    [Fact]
    public void TerminalTextStatesTheCompletionPolicy()
    {
        LocalEncounterOutcomeView terminal = Outcome(LocalOutcomeStage.Completed, LocalOutcomeStanding.OpponentStands,
            LocalOutcomeRestriction.Incapacitated, readable: false);
        Assert.True(terminal.IsTerminal);
        string text = EncounterOutcomeText.Terminal(terminal);
        Assert.Contains(EncounterOutcomeText.CompletionPolicy, text, StringComparison.Ordinal);
        Assert.DoesNotContain("reach", text, StringComparison.Ordinal);
        // The card carries the full terminal explanation; the HUD keeps a compact status line and a two-line hint.
        Assert.StartsWith(text, EncounterOutcomeText.Card(terminal, null), StringComparison.Ordinal);
        Assert.Equal("Defeated · Incapacitated (0 HP) · Encounter complete", EncounterOutcomeText.TerminalStatus(terminal));
    }

    [Fact]
    public void HudFormsStayCompactWhileTheCardKeepsTheFullExplanation()
    {
        // The non-wrapping hint sits beside the exploration buttons: each terminal line is no longer than the ordinary
        // movement hint it replaces (the native route measures the actual drawn bounds).
        Assert.All(EncounterOutcomeText.TerminalHint.Split('\n'), line => Assert.True(line.Length <= "WASD / arrows".Length, line));
        Assert.Equal(2, EncounterOutcomeText.TerminalHint.Split('\n').Length);
        LocalEncounterOutcomeView waiting = Outcome(LocalOutcomeStage.AwaitingOthers, LocalOutcomeStanding.YourSideStands,
            LocalOutcomeRestriction.AwaitingCompletion, readable: false, remaining: 1);
        Assert.Equal("Victory\nRead · waiting for others", EncounterOutcomeText.Hint(waiting, null));
        Assert.Equal("You have read this outcome. Waiting for one other player to read it.", EncounterOutcomeText.Card(waiting, null));
        foreach (LocalOutcomeStage stage in new[] { LocalOutcomeStage.AwaitingYourReading, LocalOutcomeStage.AwaitingOthers,
            LocalOutcomeStage.CarePending, LocalOutcomeStage.Completing })
        {
            LocalEncounterOutcomeView outcome = Outcome(stage, LocalOutcomeStanding.OpponentStands, LocalOutcomeRestriction.Incapacitated,
                readable: stage == LocalOutcomeStage.AwaitingYourReading);
            string[] lines = EncounterOutcomeText.Hint(outcome, Care(LocalCareStatus.Available, Offer(true, WorldActionRejection.None))).Split('\n');
            Assert.Equal(2, lines.Length);
            Assert.All(lines, line => Assert.True(line.Length <= 48, line));
        }
        // The inline preview of an outcome choice is one line; the control's tooltip keeps the full detail.
        var scope = new FarmActionScope(Id(20), Id(21), Id(22), Id(23), Id(24), 5);
        var reading = new FarmActionOffer(FarmSemanticAction.ReleaseEncounter, scope, "Accept defeat", "Defeated", null,
            FarmActionOffers.NoCost, "", new AcknowledgeEncounterOutcomeIntent(Id(6), Hash), null,
            detailText: EncounterOutcomeText.ReadDetail(Outcome(LocalOutcomeStage.AwaitingYourReading,
                LocalOutcomeStanding.OpponentStands, LocalOutcomeRestriction.Incapacitated, readable: true)));
        Assert.Equal("Accept defeat: Defeated · No cost", EncounterOutcomeText.InlinePreview(reading));
        var blocked = new FarmActionOffer(FarmSemanticAction.ReleaseEncounter, scope, "Accept defeat", "Defeated", null,
            FarmActionOffers.NoCost, "", null, "You have read this outcome. Waiting for one other player to read it.");
        Assert.Equal("Accept defeat: Defeated · No cost · Unavailable: see the outcome card", EncounterOutcomeText.InlinePreview(blocked));
    }

    internal static EntityId Id(byte value)
    {
        byte[] bytes = new byte[EntityId.ByteLength];
        bytes[0] = 0x6B;
        bytes[^1] = value;
        return EntityId.FromBytes(bytes);
    }

    internal static LocalCareAcknowledgmentView Offer(bool can, WorldActionRejection rejection) =>
        new(Id(2), "Wren", new AcknowledgeCareIntent(Id(5), Id(6), 3, Hash), can, rejection);

    internal static LocalCareView Care(LocalCareStatus status, LocalCareAcknowledgmentView? offer = null, EntityId? pendingId = null) =>
        (LocalCareView)Activator.CreateInstance(typeof(LocalCareView), BindingFlags.NonPublic | BindingFlags.Instance, null,
            [offer is null ? Array.Empty<LocalCareAcknowledgmentView>() : new[] { offer }, status, pendingId,
                pendingId is null ? null : (EntityId?)Id(2), pendingId is null ? null : "Wren"], null)!;

    internal static LocalEncounterOutcomeView Outcome(LocalOutcomeStage stage, LocalOutcomeStanding standing,
        LocalOutcomeRestriction restriction, bool readable, int? remaining = null) =>
        (LocalEncounterOutcomeView)Activator.CreateInstance(typeof(LocalEncounterOutcomeView), BindingFlags.NonPublic | BindingFlags.Instance,
            null, [stage, Id(6), EncounterOutcomeResult.Victory, 2, standing, Array.Empty<LocalOutcomeParticipantView>(),
                readable ? new AcknowledgeEncounterOutcomeIntent(Id(6), Hash) : null, 1, remaining ?? (readable ? 1 : 0), false,
                restriction, 0UL, stage == LocalOutcomeStage.Completed ? (ulong?)9UL : null], null)!;
}
