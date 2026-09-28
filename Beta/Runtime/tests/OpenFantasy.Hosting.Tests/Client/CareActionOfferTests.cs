using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class CareActionOfferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_defeat_offers_acknowledgment_and_only_a_later_helper_action_spends_one_dose(bool crafted)
    {
        var fixture = new CareWorldFixture(crafted);
        using var files = new SessionFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        WorldSnapshot defeated = fixture.PrepareDefeat(new(authority));
        var clock = new ManualSessionTimeProvider();
        var world = new LocalWorldSession(authority, fixture.Player, fixture.DeliveryFor(fixture.Player), clock);
        await using var client = Client(fixture, world);
        LocalWorldView view = world.ReadView();
        var publicOffer = Assert.Single(view.Care!.Offers);
        var selectedOpponent = Target(view, fixture.Wolf, FarmTargetKind.Encounter);
        var offers = client.CreateOffers(view, selectedOpponent);
        FarmActionOffer care = Assert.Single(offers).Value;
        Assert.Equal(FarmSemanticAction.AcknowledgeCare, care.Action);
        Assert.True(care.CanExecute);
        Assert.False(care.SpendsSomething);
        Assert.Equal(fixture.Helper, care.TargetId);
        Assert.Equal(publicOffer.HelperName, care.TargetName);
        Assert.Same(publicOffer.Intent, care.Intent);
        var intent = Assert.IsType<AcknowledgeCareIntent>(care.Intent);
        Assert.Equal((fixture.Agreement, defeated.EncounterActions!.Scope.EncounterId,
            defeated.EncounterLifecycle!.Revision, defeated.EncounterActions.ContentHash),
            (intent.AgreementId, intent.EncounterId, intent.ExpectedLifecycleRevision, intent.ExpectedActionHash));
        Assert.Contains("separate action", care.DetailText!, StringComparison.Ordinal);
        Assert.Contains("not yet", care.CompletionText, StringComparison.Ordinal);
        Assert.Equal(fixture.Player, care.Scope.ActorId);
        Assert.Equal(defeated.Space.GetActor(fixture.Player).Revision, care.Scope.ExpectedActorRevision);

        var selection = new FarmTargetSelection(fixture.Helper);
        selection.Refresh(view);
        for (int index = 0; selection.Selected?.Id != fixture.Helper; index++)
        {
            Assert.True(index < view.Actors.Count, "The known care helper must be explicitly selectable.");
            selection.Cycle();
        }
        Assert.Equal(FarmTargetKind.Person, selection.Selected!.Kind);
        Assert.Equal(intent, client.CreateOffers(view, selection.Selected)[care.Action].Intent);
        FarmActionOffer wrongPerson = client.CreateOffers(view,
            Target(view, fixture.Player, FarmTargetKind.Person))[care.Action];
        Assert.False(wrongPerson.CanExecute);
        Assert.Equal(fixture.Player, wrongPerson.TargetId);
        Assert.Null(wrongPerson.Intent);

        // Opening, inspecting and cancelling the wheel neither acknowledges care nor advances time.
        var wheel = new FarmQuickWheelModel(offers);
        Assert.All(wheel.Slots.Take(3), Assert.Null);
        wheel.Select(3);
        Assert.Equal(fixture.Helper, wheel.Selected!.PinnedTarget!.Id);
        Assert.Equal(FarmTargetKind.Person, wheel.Selected.PinnedTarget.Kind);
        var router = new FarmInputRouter();
        router.SetWheelOpen(true);
        Assert.Equal(FarmSemanticAction.CloseWheel,
            router.Route(CareSituation(), FarmInputSignal.Press(FarmInputActions.Cancel)).Action);
        router.SetWheelOpen(false);
        Assert.Equal(defeated.ContentHash, authority.ReadStateForHost().ContentHash);

        // A real second client with the same persisted control identities cannot submit this
        // lifetime's offer. Registration is stricter than simply matching those identities.
        using (var foreignFiles = new SessionFiles())
        {
            var foreignAuthority = SqliteWorldAuthority.CreateNew(foreignFiles.Path, fixture.Genesis);
            var foreignWorld = new LocalWorldSession(foreignAuthority, fixture.Player,
                fixture.DeliveryFor(fixture.Player), new ManualSessionTimeProvider());
            await using var foreignClient = Client(fixture, foreignWorld);
            Assert.Throws<InvalidOperationException>(() => { _ = foreignClient.SubmitOffer(care); });
            Assert.Equal(fixture.Genesis.ContentHash, foreignAuthority.ReadStateForHost().ContentHash);
        }

        FarmActionOffer stale = client.CreateOffers(view, selection.Selected)[care.Action];
        world.SetReady(true);
        Task<WorldActionReceipt> acknowledged = client.SubmitOffer(care);
        Assert.Same(acknowledged, client.SubmitOffer(care));
        await Tick(clock);
        await Until(() => acknowledged.IsCompleted);
        Assert.Equal(CommandReceiptStatus.Applied, (await acknowledged).Status);
        WorldSnapshot pending = authority.ReadStateForHost();
        Assert.Equal(defeated.Revision + 1, pending.Revision);
        Assert.Equal(defeated.Inventory.ContentHash, pending.Inventory.ContentHash);
        Assert.Equal(defeated.Progression!.ContentHash, pending.Progression!.ContentHash);
        Assert.Empty(pending.Production!.ConsumedItems);
        Assert.Equal(defeated.Calendar, pending.Calendar);
        FarmActionOffer pendingOffer = Assert.Single(client.CreateOffers(world.ReadView())).Value;
        Assert.Equal(care.Action, pendingOffer.Action);
        Assert.False(pendingOffer.CanExecute);
        Assert.Null(pendingOffer.Intent);
        Assert.Contains("Waiting", pendingOffer.BlockingReason!, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => { _ = client.SubmitOffer(pendingOffer); });
        wheel.Refresh(target => client.CreateOffers(world.ReadView(), target));
        Assert.False(wheel.Selected.Offer.CanExecute);
        Assert.Equal(fixture.Helper, wheel.Selected.PinnedTarget.Id);

        // A different already-presented offer remains its original command, even while the
        // first acknowledgment is pending. Its rejection does not perform the helper's action.
        Task<WorldActionReceipt> rejected = client.SubmitOffer(stale);
        await Tick(clock);
        await Until(() => rejected.IsCompleted);
        Assert.Equal(WorldActionRejection.Unavailable, (await rejected).Rejection);
        Assert.Same(rejected, client.SubmitOffer(stale));
        Assert.Equal(pending.Care!.ContentHash, authority.ReadStateForHost().Care!.ContentHash);
        Assert.Empty(authority.ReadStateForHost().Production!.ConsumedItems);
        world.SetReady(false);
        Assert.Same(acknowledged, client.SubmitOffer(care));
        await Tick(clock, TimeSpan.FromHours(1));
        Assert.Empty(authority.ReadStateForHost().Production!.ConsumedItems);

        LocalWorldView needsAttention = new(authority.ReadStateForHost(), fixture.Player,
            careHelperSelectionOccupied: true);
        var attentionOffer = Assert.Single(client.CreateOffers(needsAttention)).Value;
        Assert.Equal(care.Action, attentionOffer.Action);
        Assert.False(attentionOffer.CanExecute);
        Assert.Null(attentionOffer.Intent);
        Assert.Contains("attention", attentionOffer.BlockingReason!, StringComparison.Ordinal);

        world.SetReady(true);
        await Tick(clock);
        WorldSnapshot treated = authority.ReadStateForHost();
        Assert.Null(treated.Care!.Pending);
        Assert.Single(treated.Care.Treatments);
        var dose = Assert.Single(treated.Production!.ConsumedItems);
        Assert.Equal(1UL, dose.Ingredient.Quantity);
        Assert.Equal(crafted ? IngredientLotKind.Transformed : IngredientLotKind.Source, dose.Ingredient.Kind);
        Assert.Equal(40, treated.Progression!.GetActor(fixture.Player).CurrentHp);
        Assert.Equal(defeated.Calendar, treated.Calendar);
        Assert.Null(world.ReadView().Care);
        selection.Refresh(world.ReadView());
        Assert.Equal(fixture.Helper, selection.Selected!.Id);
        Assert.Equal(treated.Space.GetActor(fixture.Helper).Feet, selection.Selected.Feet);
        Assert.DoesNotContain(care.Action, client.CreateOffers(world.ReadView(), selectedOpponent).Keys);
        wheel.Refresh(target => client.CreateOffers(world.ReadView(), target));
        Assert.Equal(care.Action, wheel.Selected.Action);
        Assert.False(wheel.Selected.Offer.CanExecute);
        Assert.Null(wheel.Selected.Offer.Intent);
        Assert.All(wheel.Slots.Take(3), Assert.Null);
        Assert.Same(acknowledged, client.SubmitOffer(care));
    }

    [Fact]
    public void Multiple_prior_agreements_never_silently_choose_an_agreement_even_for_the_same_helper()
    {
        var fixture = new CareWorldFixture();
        var original = fixture.Genesis.Care!;
        var agreement = Assert.Single(original.Agreements);
        var care = new CareSnapshot(original.WorldId, original.BranchId, 0, original.Remedies,
            original.Agreements.Append(new CareAgreement(new EntityId(0xcafe, 21), agreement.PatientActorId,
                agreement.HelperActorId, agreement.RemedyDefinitionId, agreement.RemedyVersion)), [], []);
        // This is fresh authored calibration before any command, not fabricated earned history.
        WorldSnapshot genesis = WithCare(fixture.Genesis, care);
        WorldSnapshot defeated = fixture.PrepareDefeat(new(genesis));
        var view = new LocalWorldView(defeated, fixture.Player);
        Assert.Equal(2, view.Care!.Offers.Count);
        Assert.All(view.Care.Offers, offer => Assert.True(offer.CanAcknowledge));
        var factory = Offers(fixture);
        foreach (FarmTarget? target in new FarmTarget?[] { null, Target(view, fixture.Wolf, FarmTargetKind.Encounter),
            Target(view, fixture.Helper, FarmTargetKind.Person) })
        {
            FarmActionOffer offer = Assert.Single(factory.Create(view, target)).Value;
            Assert.Equal(FarmSemanticAction.AcknowledgeCare, offer.Action);
            Assert.False(offer.CanExecute);
            Assert.Null(offer.Intent);
            Assert.Contains("unambiguous", offer.BlockingReason!, StringComparison.Ordinal);
        }
        Assert.Null(defeated.Care!.Pending);
    }

    [Fact]
    public void Wheel_rejects_each_changed_care_boundary_or_helper_without_changing_the_inspected_slot()
    {
        var fixture = new CareWorldFixture();
        var view = new LocalWorldView(fixture.PrepareDefeat(new(fixture.Genesis)), fixture.Player);
        FarmActionOffer original = Assert.Single(Offers(fixture).Create(view)).Value;
        var intent = Assert.IsType<AcknowledgeCareIntent>(original.Intent);
        var other = new EntityId(0xcafe, 22);
        AcknowledgeCareIntent[] changed =
        [
            new(other, intent.EncounterId, intent.ExpectedLifecycleRevision, intent.ExpectedActionHash),
            new(intent.AgreementId, other, intent.ExpectedLifecycleRevision, intent.ExpectedActionHash),
            new(intent.AgreementId, intent.EncounterId, intent.ExpectedLifecycleRevision + 1, intent.ExpectedActionHash),
            new(intent.AgreementId, intent.EncounterId, intent.ExpectedLifecycleRevision, ContentHash.Compute("other boundary"u8))
        ];
        foreach (var nextIntent in changed)
        {
            AssertBlockedAfterRefresh(original, new(original.Action, original.Scope, original.Label,
                original.TargetName, original.TargetId, original.CostText, original.CompletionText, nextIntent, null));
        }
        AssertBlockedAfterRefresh(original, new(original.Action, original.Scope, original.Label,
            "Another helper", other, original.CostText, original.CompletionText, intent, null));
    }

    [Fact]
    public void An_actual_unreachable_helper_remains_a_named_blocked_offer_without_a_release_or_free_heal()
    {
        var fixture = new CareWorldFixture();
        var original = fixture.Genesis.Care!;
        var remedy = Assert.Single(original.Remedies);
        var shorterReach = new RestorativeRemedyDefinition(remedy.DefinitionId, remedy.Item, remedy.RestoredHp, 128);
        var agreement = Assert.Single(original.Agreements);
        var care = new CareSnapshot(original.WorldId, original.BranchId, 0, [shorterReach],
            [new CareAgreement(agreement.AgreementId, agreement.PatientActorId, agreement.HelperActorId,
                shorterReach.DefinitionId, shorterReach.Version)], [], []);
        var session = new CareWorldSession(WithCare(fixture.Genesis, care));
        WorldSnapshot defeated = fixture.PrepareDefeat(session);
        var view = new LocalWorldView(defeated, fixture.Player);
        Assert.Equal(LocalCareStatus.Unavailable, view.Care!.Status);
        Assert.Equal(WorldActionRejection.OutOfReach, Assert.Single(view.Care.Offers).Rejection);
        FarmActionOffer offer = Assert.Single(Offers(fixture).Create(view)).Value;
        Assert.Equal(FarmSemanticAction.AcknowledgeCare, offer.Action);
        Assert.Equal(fixture.Helper, offer.TargetId);
        Assert.False(offer.CanExecute);
        Assert.Null(offer.Intent);
        Assert.False(offer.SpendsSomething);
        Assert.Equal(defeated.ContentHash, session.ReadStateForHost().ContentHash);
        Assert.Empty(defeated.Production!.ConsumedItems);
    }

    [Fact]
    public void Contextual_interact_acknowledges_but_never_strikes_waits_or_releases_while_care_is_present()
    {
        var router = new FarmInputRouter();
        var care = CareSituation();
        Assert.Equal(FarmInputLayer.Encounter, router.Layer(care));
        Assert.Equal(FarmSemanticAction.AcknowledgeCare,
            router.Route(care, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
        Assert.Equal(FarmSemanticAction.None,
            router.Route(care, FarmInputSignal.Repeat(FarmInputActions.Interact)).Action);
        router.Route(care, FarmInputSignal.Release(FarmInputActions.Interact));
        Assert.Equal(FarmSemanticAction.None,
            router.Route(care, FarmInputSignal.Press(FarmInputActions.Give)).Action);
        Assert.False(router.Permits(care, FarmSemanticAction.Strike));
        Assert.False(router.Permits(care, FarmSemanticAction.Wait));
        Assert.False(router.Permits(care, FarmSemanticAction.ReleaseEncounter));
        Assert.Equal(FarmMovementIntent.Neutral, router.Movement(care, new(0, 1, 0, 0)));
        Assert.False(router.Permits(care with { ActionPending = true }, FarmSemanticAction.AcknowledgeCare));
        router.SetFocused(false);
        Assert.False(router.Permits(care, FarmSemanticAction.AcknowledgeCare));
        router.SetFocused(true);
        router.TogglePause();
        Assert.False(router.Permits(care, FarmSemanticAction.AcknowledgeCare));
        router.TogglePause();
        Assert.False(router.Permits(care with { HasCareAcknowledgment = false }, FarmSemanticAction.AcknowledgeCare));
        Assert.Equal(FarmSemanticAction.ReleaseEncounter,
            router.Route(care with { HasCareAcknowledgment = false }, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
    }

    [Fact]
    public async Task Explicit_care_selection_reopens_itself_and_refuses_the_original_encounter_save_in_both_directions()
    {
        using var careFiles = new SessionFiles();
        const string playerName = "Traveler";
        const string seed = "care-client-selection";
        EntityId helper;
        await using (var client = await FarmClientSession.OpenCareAsync(careFiles.Path, seed, playerName))
        {
            helper = client.WrenActorId;
            Assert.Equal("Wren", client.World.ReadView().Actors.Single(actor => actor.ActorId == helper).DisplayName);
            Assert.Null(client.World.ReadView().Care); // No incapacitation, so no inert medical menu.
        }
        WorldSnapshot original;
        using (var owner = SqliteWorldAuthority.OpenExisting(careFiles.Path))
        {
            original = owner.ReadStateForHost();
            Assert.Equal((ushort)25, original.ProfileVersion);
            Assert.Single(original.Care!.Agreements);
        }
        await using (var reopened = await FarmClientSession.OpenCareAsync(careFiles.Path, seed, playerName))
            Assert.Equal(helper, reopened.WrenActorId);
        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenEncounterAsync(careFiles.Path, seed, playerName));
        using (var unchanged = SqliteWorldAuthority.OpenExisting(careFiles.Path))
            Assert.Equal(original.ContentHash, unchanged.ReadStateForHost().ContentHash);

        using var encounterFiles = new SessionFiles();
        WorldSnapshot encounter = EncounterJourneyAdmission.CreateEmbedded(seed, playerName);
        using (SqliteWorldAuthority.CreateNew(encounterFiles.Path, encounter)) { }
        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenCareAsync(encounterFiles.Path, seed, playerName));
        using var prior = SqliteWorldAuthority.OpenExisting(encounterFiles.Path);
        Assert.Equal(encounter.ContentHash, prior.ReadStateForHost().ContentHash);
    }

    private static void AssertBlockedAfterRefresh(FarmActionOffer original, FarmActionOffer changed)
    {
        var wheel = new FarmQuickWheelModel(new Dictionary<FarmSemanticAction, FarmActionOffer> { [original.Action] = original });
        wheel.Select(3);
        wheel.Refresh(_ => new Dictionary<FarmSemanticAction, FarmActionOffer> { [changed.Action] = changed });
        Assert.Equal(original.Action, wheel.Selected!.Action);
        Assert.Equal(original.TargetId, wheel.Selected.PinnedTarget!.Id);
        Assert.False(wheel.Selected.Offer.CanExecute);
        Assert.Null(wheel.Selected.Offer.Intent);
    }

    private static FarmInputSituation CareSituation() => new(true, false, false)
        { IsInEncounter = true, IsEncounterOutcome = true, HasCareAcknowledgment = true };
    private static FarmClientSession Client(CareWorldFixture fixture, LocalWorldSession world) => new(world,
        fixture.DeliveryFor(fixture.Player), fixture.Helper, Wood(fixture.Genesis), null);
    private static FarmActionOffers Offers(CareWorldFixture fixture) => new(fixture.DeliveryFor(fixture.Player),
        fixture.Helper, Wood(fixture.Genesis), null);
    private static EntityId Wood(WorldSnapshot state) => state.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak)
        .Definition.YieldDefinition.DefinitionId;
    private static FarmTarget Target(LocalWorldView view, EntityId actorId, FarmTargetKind kind)
    {
        var actor = view.Actors.Single(value => value.ActorId == actorId);
        return new(actor.ActorId, actor.DisplayName, kind, actor.Feet);
    }
    private static WorldSnapshot WithCare(WorldSnapshot world, CareSnapshot care) => new(world.Inventory,
        world.Space, world.Residents, world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps,
        world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests, world.Construction,
        reservoirs: world.Reservoirs, reservoirSeals: world.ReservoirSeals, liquidCells: world.LiquidCells,
        excavation: world.Excavation, pacing: world.Pacing, encounterLifecycle: world.EncounterLifecycle,
        care: care, resourceHistory: world.ResourceHistory);
}
