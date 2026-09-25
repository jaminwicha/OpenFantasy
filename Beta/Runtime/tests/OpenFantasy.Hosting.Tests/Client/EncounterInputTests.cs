using OpenFantasy.Client;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class EncounterInputTests
{
    private static readonly FarmInputSituation World = new(true, false, false);

    [Fact]
    public void Existing_context_keys_enter_strike_wait_and_release_without_routing_farm_actions()
    {
        var router = new FarmInputRouter();
        var entry = World with { HasEncounterEntry = true };
        var active = World with { IsInEncounter = true };
        Assert.Equal(FarmSemanticAction.EnterEncounter, Press(router, entry, FarmInputActions.Interact));
        Assert.Equal(FarmInputLayer.Encounter, router.Layer(active));
        Assert.Equal(FarmSemanticAction.Strike, Press(router, active, FarmInputActions.Interact));
        Assert.Equal(FarmSemanticAction.Wait, Press(router, active, FarmInputActions.Give));
        Assert.Equal(FarmSemanticAction.None, Press(router, active, FarmInputActions.Gather));
        Assert.Equal(FarmSemanticAction.None, Press(router, active, FarmInputActions.Craft));
        foreach (FarmSemanticAction ordinary in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give,
            FarmSemanticAction.Gather, FarmSemanticAction.Craft, FarmSemanticAction.Excavate, FarmSemanticAction.Traverse })
            Assert.False(router.Permits(active, ordinary));
        Assert.Equal(FarmSemanticAction.ReleaseEncounter, Press(router,
            active with { IsEncounterOutcome = true }, FarmInputActions.Interact));
        Assert.Equal(FarmSemanticAction.None, Press(router, active with { ActionPending = true }, FarmInputActions.Interact));
        Assert.Equal(FarmSemanticAction.NextTarget, Press(router, active, FarmInputActions.NextTarget));
        Assert.Equal(FarmSemanticAction.OpenWheel, Press(router, active, FarmInputActions.QuickWheel));
        Assert.Equal(FarmSemanticAction.Speak, Press(router, World, FarmInputActions.Interact));
    }

    [Fact]
    public void Held_movement_and_held_choice_do_not_escape_encounter_or_repeat_after_release()
    {
        var router = new FarmInputRouter();
        var active = World with { IsInEncounter = true };
        var right = new FarmDirectionalInput(0, 1, 0, 0);
        Assert.False(router.Movement(World, right).IsNeutral);
        Assert.True(router.Movement(active, right).IsNeutral);
        Assert.True(router.Movement(World, right).IsNeutral);
        Assert.True(router.Movement(World, FarmDirectionalInput.Neutral).IsNeutral);
        Assert.False(router.Movement(World, right).IsNeutral);
        Assert.Equal(FarmSemanticAction.Strike, router.Route(active, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
        Assert.Equal(FarmSemanticAction.None, router.Route(active, FarmInputSignal.Repeat(FarmInputActions.Interact)).Action);
        Assert.Equal(FarmSemanticAction.None, router.Route(World, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
        router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact));
        Assert.Equal(FarmSemanticAction.Speak, router.Route(World, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
    }

    private static FarmSemanticAction Press(FarmInputRouter router, FarmInputSituation situation, string action)
    {
        var decision = router.Route(situation, FarmInputSignal.Press(action));
        Assert.True(decision.Consumed);
        router.Route(situation, FarmInputSignal.Release(action));
        return decision.Action;
    }
}
