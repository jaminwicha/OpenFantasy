using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterTimingTests
{
    private static EntityId Id(ulong value) => new(0, value);
    private static EncounterTimingScope Scope => new(Id(1), Id(2), Id(3));
    private static EncounterActorTiming Actor(ulong id, uint speed = 100, ulong delay = 0,
        EncounterActorControl control = EncounterActorControl.RequiredHuman) => new(Id(id), speed, delay, control);
    private static EncounterDecision Choice(ulong actor, ulong decision, EncounterDecisionKind kind = EncounterDecisionKind.Action) =>
        new(Id(actor), Id(decision), kind, ContentHash.Compute("an externally validated action"u8));
    private static EncounterTimingSnapshot State(params EncounterActorTiming[] actors) => new(Scope, 0, 0, actors, []);

    [Fact]
    public void Every_required_human_choice_precedes_automatic_resolution_and_identity_orders_the_boundary()
    {
        var state = State(Actor(30, 300), Actor(10, control: EncounterActorControl.Automatic), Actor(20, 200));
        Assert.Equal(new[] { Id(10), Id(20), Id(30) }, state.DueActors.Select(actor => actor.ActorId));
        state = state.QueueDecision(state.Revision, Choice(10, 110));
        state = state.QueueDecision(state.Revision, Choice(20, 120));
        byte[] awaitingHuman = EncounterTimingCodecV1.Encode(state);
        for (int ignoredWallClockPoll = 0; ignoredWallClockPoll < 20; ignoredWallClockPoll++)
        {
            var blocked = state.TryConfirmNext(state.Revision);
            Assert.Equal(EncounterTimingStatus.RequiredDecisionMissing, blocked.Status);
            Assert.Same(state, blocked.State);
            Assert.Equal(LocalExposureDuration.Zero, blocked.Experienced);
            Assert.Equal(EncounterTimingStatus.RequiredDecisionMissing, state.TryAdvance(state.Revision).Status);
        }
        Assert.Equal(awaitingHuman, EncounterTimingCodecV1.Encode(state));
        state = state.QueueDecision(state.Revision, Choice(30, 130));
        Assert.Empty(state.RequiredDecisions);
        Assert.Equal(EncounterTimingStatus.ActionBoundaryPending, state.TryAdvance(state.Revision).Status);

        foreach (ulong actorId in new ulong[] { 10, 20, 30 })
        {
            var confirmed = state.TryConfirmNext(state.Revision);
            Assert.True(confirmed.IsAccepted);
            Assert.Equal(EncounterTimingStatus.Confirmed, confirmed.Status);
            Assert.Equal(Id(actorId), confirmed.ConfirmedDecision!.ActorId);
            Assert.Same(state, confirmed.Basis);
            Assert.Equal(LocalExposureDuration.Zero, confirmed.Experienced);
            state = confirmed.State;
        }
        Assert.Empty(state.Decisions);
        Assert.Equal(100UL, state.GetActor(Id(10)).RemainingDelay);
        Assert.Equal(50UL, state.GetActor(Id(20)).RemainingDelay);
        Assert.Equal(34UL, state.GetActor(Id(30)).RemainingDelay);
        Assert.Equal(0UL, state.ElapsedTacticalUnits);

        LocalExposureDuration experienced = LocalExposureDuration.Zero;
        for (int tick = 0; tick < 7; tick++)
        {
            var advance = state.TryAdvance(state.Revision);
            Assert.Equal(EncounterTimingStatus.Advanced, advance.Status);
            experienced = experienced.Add(advance.Experienced);
            state = advance.State;
        }
        Assert.Equal(35UL, state.ElapsedTacticalUnits);
        Assert.Equal(LocalExposureDuration.FromMilliseconds(350), experienced);
        Assert.Equal(Id(30), Assert.Single(state.DueActors).ActorId);
        Assert.Equal(65UL, state.GetActor(Id(10)).RemainingDelay);
        Assert.Equal(15UL, state.GetActor(Id(20)).RemainingDelay);
        Assert.Equal(EncounterTimingStatus.RequiredDecisionMissing, state.TryAdvance(state.Revision).Status);
    }

    [Theory]
    [InlineData(1U, 10_000UL)]
    [InlineData(3U, 3_334UL)]
    [InlineData(100U, 100UL)]
    [InlineData(101U, 100UL)]
    [InlineData(150U, 67UL)]
    [InlineData(10_001U, 1UL)]
    [InlineData(uint.MaxValue, 1UL)]
    public void Wait_and_action_charge_exact_positive_standard_delay(uint speed, ulong expectedDelay)
    {
        foreach (EncounterDecisionKind kind in Enum.GetValues<EncounterDecisionKind>())
        {
            var start = State(Actor(10, speed));
            var chosen = start.QueueDecision(0, Choice(10, 110, kind));
            var confirmed = chosen.TryConfirmNext(chosen.Revision);
            Assert.Equal(expectedDelay, confirmed.State.GetActor(Id(10)).RemainingDelay);
            Assert.Equal(kind, confirmed.ConfirmedDecision!.Kind);
            Assert.Equal(0UL, confirmed.State.ElapsedTacticalUnits);
            Assert.Equal(LocalExposureDuration.Zero, confirmed.Experienced);
            Assert.Equal(2UL, confirmed.State.Revision);
            Assert.Equal(EncounterTimingStatus.NoDueActors,
                confirmed.State.TryConfirmNext(confirmed.State.Revision).Status);
        }
    }

    [Fact]
    public void Restored_incurred_debt_is_not_reset_and_held_polls_do_not_create_catch_up()
    {
        var state = new EncounterTimingSnapshot(Scope, 9, 40,
            [Actor(10, delay: 1), Actor(20, delay: 101, control: EncounterActorControl.Automatic)], []);
        state = EncounterTimingCodecV1.Decode(EncounterTimingCodecV1.Encode(state), Scope,
            TestContext.Current.CancellationToken);
        Assert.Equal(1UL, state.GetActor(Id(10)).RemainingDelay);
        Assert.Equal(101UL, state.GetActor(Id(20)).RemainingDelay);
        var next = state.TryAdvance(9);
        Assert.Equal(45UL, next.State.ElapsedTacticalUnits);
        Assert.Equal(96UL, next.State.GetActor(Id(20)).RemainingDelay);
        Assert.Equal(LocalExposureDuration.FromMilliseconds(50), next.Experienced);
        for (int held = 0; held < 100; held++)
        {
            Assert.Same(next.State, next.State.TryAdvance(next.State.Revision).State);
        }
        var chosen = next.State.QueueDecision(next.State.Revision, Choice(10, 110, EncounterDecisionKind.Wait));
        var resumed = EncounterTimingCodecV1.Decode(EncounterTimingCodecV1.Encode(chosen), Scope,
            TestContext.Current.CancellationToken);
        resumed = resumed.TryConfirmNext(resumed.Revision).State;
        resumed = resumed.TryAdvance(resumed.Revision).State;
        Assert.Equal(50UL, resumed.ElapsedTacticalUnits);
        Assert.Equal(95UL, resumed.GetActor(Id(10)).RemainingDelay);
        Assert.Equal(91UL, resumed.GetActor(Id(20)).RemainingDelay);
    }

    [Fact]
    public void Automatic_choice_is_explicit_and_cannot_skip_an_earlier_due_actor()
    {
        var state = State(Actor(10, control: EncounterActorControl.Automatic), Actor(20));
        state = state.QueueDecision(state.Revision, Choice(20, 120));
        var blocked = state.TryConfirmNext(state.Revision);
        Assert.Equal(EncounterTimingStatus.AutomaticDecisionMissing, blocked.Status);
        Assert.Same(state, blocked.State);
        Assert.Null(blocked.ConfirmedDecision);
        Assert.Equal(EncounterTimingStatus.AutomaticDecisionMissing, state.TryAdvance(state.Revision).Status);
        state = state.QueueDecision(state.Revision, Choice(10, 110));
        Assert.Equal(Id(10), state.TryConfirmNext(state.Revision).ConfirmedDecision!.ActorId);
    }

    [Fact]
    public void Stale_requests_duplicate_decisions_and_non_due_choices_leave_the_original_unchanged()
    {
        var state = State(Actor(10), Actor(20), Actor(30, delay: 1));
        state = state.QueueDecision(0, Choice(10, 110));
        byte[] original = EncounterTimingCodecV1.Encode(state);
        Assert.Throws<InvalidOperationException>(() => state.TryAdvance(0));
        Assert.Throws<InvalidOperationException>(() => state.TryConfirmNext(0));
        Assert.Throws<InvalidOperationException>(() => state.QueueDecision(0, Choice(20, 120)));
        Assert.Throws<ArgumentException>(() => state.QueueDecision(1, Choice(10, 111)));
        Assert.Throws<ArgumentException>(() => state.QueueDecision(1, Choice(20, 110)));
        Assert.Throws<ArgumentException>(() => state.QueueDecision(1, Choice(30, 130)));
        Assert.Throws<ArgumentException>(() => state.QueueDecision(1, Choice(40, 140)));
        Assert.Throws<ArgumentException>(() => state.QueueDecision(1, Choice(20, 3)));
        Assert.Throws<ArgumentException>(() => state.QueueDecision(1, Choice(20, 30)));
        Assert.Equal(original, EncounterTimingCodecV1.Encode(state));
    }

    [Fact]
    public void Overflow_refuses_without_publishing_time_or_consuming_a_choice()
    {
        ulong elapsedLimitRevision = ulong.MaxValue / EncounterTimingSnapshot.UnitsPerTick;
        var elapsedLimit = new EncounterTimingSnapshot(Scope, elapsedLimitRevision, ulong.MaxValue, [Actor(10, delay: 1)], []);
        byte[] elapsedBytes = EncounterTimingCodecV1.Encode(elapsedLimit);
        Assert.Throws<OverflowException>(() => elapsedLimit.TryAdvance(elapsedLimitRevision));
        Assert.Equal(elapsedBytes, EncounterTimingCodecV1.Encode(elapsedLimit));
        var revisionLimit = new EncounterTimingSnapshot(Scope, ulong.MaxValue, 0, [Actor(10)], [Choice(10, 110)]);
        byte[] revisionBytes = EncounterTimingCodecV1.Encode(revisionLimit);
        Assert.Throws<OverflowException>(() => revisionLimit.TryConfirmNext(ulong.MaxValue));
        Assert.Equal(revisionBytes, EncounterTimingCodecV1.Encode(revisionLimit));
        var queuedLimit = new EncounterTimingSnapshot(Scope, ulong.MaxValue, 0, [Actor(10)], []);
        Assert.Throws<OverflowException>(() => queuedLimit.QueueDecision(ulong.MaxValue, Choice(10, 110)));
    }

    [Fact]
    public void Complete_actor_bound_is_enforced_before_unbounded_enumeration()
    {
        int yielded = 0;
        IEnumerable<EncounterActorTiming> Excessive()
        {
            for (ulong index = 0; ; index++)
            {
                yielded++;
                yield return Actor(100 + index);
            }
        }
        Assert.Throws<ArgumentException>(() => new EncounterTimingSnapshot(Scope, 0, 0, Excessive(), []));
        Assert.Equal(129, yielded);
        Assert.Throws<ArgumentException>(() => State());
        Assert.Throws<ArgumentException>(() => State(Actor(10), Actor(10)));
        Assert.Throws<ArgumentException>(() => State(Actor(1)));
        Assert.Throws<ArgumentException>(() => new EncounterTimingScope(Id(1), Id(1), Id(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Actor(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Actor(10, control: (EncounterActorControl)3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EncounterTimingSnapshot(Scope, 0, 1, [Actor(10)], []));
        Assert.Throws<ArgumentException>(() => new EncounterTimingSnapshot(Scope, 0, 5, [Actor(10)], []));
    }
}
