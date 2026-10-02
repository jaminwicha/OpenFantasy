using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using static OpenFantasy.Hosting.Tests.Client.EncounterOutcome.EncounterOutcomeTextTests;

namespace OpenFantasy.Hosting.Tests.Client.EncounterOutcome;

/// <summary>
/// Independent care and outcome choices at the input and wheel models: E / A keeps requesting care whenever care is
/// offered, the profile-35 reading beside care stays a separate deliberate choice (pointer, focus or the wheel's right
/// sector, worded Accept defeat / Accept outcome / Continue), its control names no key it does not have, and every earlier
/// layout and the legacy release refusal beside care are unchanged.
/// </summary>
public sealed class EncounterOutcomeInputTests
{
    private static readonly ContentHash Hash = ContentHash.Compute(new byte[] { 7, 1 });
    private static readonly FarmActionScope Scope = new(Id(30), Id(31), Id(32), Id(33), Id(34), 9);

    [Fact]
    public void InteractKeepsChoosingCareWhileTheProfile35ReadingStaysASeparatePermittedChoice()
    {
        var router = new FarmInputRouter();
        FarmInputSituation outcome = BesideCare(readingBesideCare: true);
        Assert.Equal(FarmInputLayer.Encounter, router.Layer(outcome));
        Assert.Equal(FarmSemanticAction.AcknowledgeCare, router.Route(outcome, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
        router.Route(outcome, FarmInputSignal.Release(FarmInputActions.Interact));
        Assert.True(router.Permits(outcome, FarmSemanticAction.ReleaseEncounter));
        Assert.True(router.Permits(outcome, FarmSemanticAction.AcknowledgeCare));
        Assert.False(router.Permits(outcome, FarmSemanticAction.Strike));
        Assert.False(router.Permits(outcome, FarmSemanticAction.Wait));
        Assert.False(router.Permits(outcome with { ActionPending = true }, FarmSemanticAction.ReleaseEncounter));
        // The wheel route: opening and confirming are permitted in running time; paused time only inspects.
        Assert.True(router.Permits(outcome, FarmSemanticAction.OpenWheel));
        router.SetWheelOpen(true);
        Assert.True(router.Permits(outcome, FarmSemanticAction.ConfirmWheel));
        // Interact inside the open wheel is left to the focused wheel control; it never becomes care or a reading there.
        Assert.Equal(FarmSemanticAction.None, router.Route(outcome, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
        router.Route(outcome, FarmInputSignal.Release(FarmInputActions.Interact));
        router.SetWheelOpen(false);
        router.TogglePause();
        Assert.False(router.Permits(outcome, FarmSemanticAction.ReleaseEncounter));
        router.SetWheelOpen(true);
        Assert.False(router.Permits(outcome, FarmSemanticAction.ConfirmWheel));
    }

    [Fact]
    public void LegacyCareStillRefusesTheReleaseThatTheProfile35ReadingIsDistinguishedFrom()
    {
        var router = new FarmInputRouter();
        FarmInputSituation legacy = BesideCare(readingBesideCare: false);
        Assert.Equal(FarmSemanticAction.AcknowledgeCare, router.Route(legacy, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
        Assert.False(router.Permits(legacy, FarmSemanticAction.ReleaseEncounter));
        // Without care E / A reads the outcome, under either law.
        Assert.Equal(FarmSemanticAction.ReleaseEncounter, new FarmInputRouter().Route(legacy with { HasCareAcknowledgment = false },
            FarmInputSignal.Press(FarmInputActions.Interact)).Action);
    }

    [Fact]
    public void TheWheelExposesTheReadingBesideCareInTheUnusedRightSectorWithItsOwnWording()
    {
        FarmActionOffer care = CareOffer();
        FarmActionOffer reading = Reading("Accept defeat");
        var offers = Offers(care, reading);
        Assert.True(EncounterOutcomeText.ReadingBesideCare(offers));
        var wheel = new FarmQuickWheelModel(offers);
        Assert.Equal(1, wheel.PageCount);
        Assert.Null(wheel.Slots[0]);
        Assert.Null(wheel.Slots[2]);
        Assert.Same(reading, wheel.Slots[1]!.Offer);
        Assert.Same(care, wheel.Slots[3]!.Offer);
        Assert.Equal("Accept defeat", FarmQuickWheelModel.SectorLabel(wheel.Slots[1]!));
        Assert.Equal("Care", FarmQuickWheelModel.SectorLabel(wheel.Slots[3]!));

        // Choosing the right sector selects exactly the reading: an outcome acknowledgment, never a care request.
        wheel.Select(1);
        Assert.Equal(FarmSemanticAction.ReleaseEncounter, wheel.Selected!.Action);
        Assert.IsType<AcknowledgeEncounterOutcomeIntent>(wheel.Selected.Offer.Intent);
        Assert.IsNotType<AcknowledgeCareIntent>(wheel.Selected.Offer.Intent);
        // Refreshing with an equal presentation keeps the reading; nothing reselects care in its place.
        FarmActionOffer refreshed = Reading("Accept defeat");
        wheel.Refresh(_ => Offers(CareOffer(), refreshed));
        Assert.Same(refreshed, wheel.Selected.Offer);
        Assert.True(wheel.Selected.Offer.CanExecute);
        wheel.Select(3);
        Assert.IsType<AcknowledgeCareIntent>(wheel.Selected!.Offer.Intent);

        Assert.Equal("Accept outcome", FarmQuickWheelModel.SectorLabel(new FarmQuickWheelModel(Offers(care, Reading("Accept outcome"))).Slots[1]!));
        Assert.Equal("Continue", FarmQuickWheelModel.SectorLabel(new FarmQuickWheelModel(Offers(care, Reading("Continue"))).Slots[1]!));
    }

    [Fact]
    public void EarlierWheelLayoutsAndWordingAreUnchanged()
    {
        // Care alone keeps the left sector; a reading or legacy release alone keeps the left sector too.
        var careOnly = new FarmQuickWheelModel(Offers(CareOffer()));
        Assert.All(new[] { careOnly.Slots[0], careOnly.Slots[1], careOnly.Slots[2] }, Assert.Null);
        Assert.Equal(FarmSemanticAction.AcknowledgeCare, careOnly.Slots[3]!.Action);
        var readingOnly = new FarmQuickWheelModel(Offers(Reading("Continue")));
        Assert.Equal(FarmSemanticAction.ReleaseEncounter, readingOnly.Slots[3]!.Action);
        Assert.Null(readingOnly.Slots[1]);
        FarmActionOffer release = new(FarmSemanticAction.ReleaseEncounter, Scope, "Continue", "Exploration", null, FarmActionOffers.NoCost,
            "", new ReleaseEncounterIntent(Id(40), Hash), null);
        var legacy = new FarmQuickWheelModel(Offers(release));
        Assert.Same(release, legacy.Slots[3]!.Offer);
        Assert.Equal("Continue", FarmQuickWheelModel.SectorLabel(legacy.Slots[3]!));
        Assert.False(EncounterOutcomeText.ReadingBesideCare(Offers(release)));
        Assert.False(EncounterOutcomeText.ReadingBesideCare(Offers(CareOffer())));
        // An active fight keeps Strike right and Wait below.
        FarmActionOffer strike = new(FarmSemanticAction.Strike, Scope, "Strike", "Wolf", Id(41), "0 MP", "",
            new QueueEncounterAbilityIntent(Id(40), 3, Id(42), Id(41)), null);
        FarmActionOffer wait = new(FarmSemanticAction.Wait, Scope, "Wait", "Your readiness", null, "1 s recovery", "",
            new QueueEncounterWaitIntent(Id(40), 3), null);
        var active = new FarmQuickWheelModel(Offers(strike, wait));
        Assert.Same(strike, active.Slots[1]!.Offer);
        Assert.Same(wait, active.Slots[2]!.Offer);
        Assert.Equal(("Strike", "Wait"), (FarmQuickWheelModel.SectorLabel(active.Slots[1]!), FarmQuickWheelModel.SectorLabel(active.Slots[2]!)));
    }

    private static FarmInputSituation BesideCare(bool readingBesideCare) => new(SessionOpen: true, ReadingHold: false, ActionPending: false)
    {
        IsInEncounter = true, IsEncounterOutcome = true, HasCareAcknowledgment = true, HasOutcomeReadingBesideCare = readingBesideCare
    };

    private static FarmActionOffer CareOffer() => new(FarmSemanticAction.AcknowledgeCare, Scope, "Acknowledge care", "Wren", Id(2),
        FarmActionOffers.NoCost, "", new AcknowledgeCareIntent(Id(5), Id(40), 3, Hash), null);

    private static FarmActionOffer Reading(string label) => new(FarmSemanticAction.ReleaseEncounter, Scope, label, "Defeated", null,
        FarmActionOffers.NoCost, "", new AcknowledgeEncounterOutcomeIntent(Id(40), Hash), null);

    private static IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> Offers(params FarmActionOffer[] offers) =>
        offers.ToDictionary(offer => offer.Action);
}
