using OpenFantasy.Core.Cognition;
using static OpenFantasy.Core.Tests.Cognition.CognitionFixture;

namespace OpenFantasy.Core.Tests.Cognition;

public sealed class CognitionTests
{
    [Fact]
    public void EvidenceNamesOnlyWhatItsKnowerReceived()
    {
        Assert.Throws<ArgumentException>(() => new CognitionEvidence(Delivered, Wren, EvidenceKind.QuestDeliveryReceived,
            DeliveryCommand, null, Quest, 4, At(0)));
        Assert.Throws<ArgumentException>(() => new CognitionEvidence(Delivered, Wren, EvidenceKind.QuestDeliveryReceived,
            DeliveryCommand, Wren, Quest, 4, At(0)));
        Assert.Throws<ArgumentException>(() => new CognitionEvidence(Delivered, Wren, EvidenceKind.QuestDeliveryReceived,
            DeliveryCommand, Player, Quest, 0, At(0)));
        Assert.Throws<ArgumentException>(() => new CognitionEvidence(Observed, Wren, EvidenceKind.SourceObserved,
            ObserveCommand, Player, Source, 3, At(0)));
        Assert.Throws<ArgumentException>(() => new CognitionEvidence(Wren, Wren, EvidenceKind.SourceObserved,
            ObserveCommand, null, Source, 3, At(0)));
        Assert.Throws<ArgumentException>(() => new CognitionEvidence(ObserveCommand, Wren, EvidenceKind.SourceObserved,
            ObserveCommand, null, Source, 3, At(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CognitionEvidence(Observed, Wren, (EvidenceKind)9,
            ObserveCommand, null, Source, 3, At(0)));
        Assert.Equal(0UL, Gathered(Source, 0, 0, Id(900)).Quantity);
    }

    [Fact]
    public void EvidenceIdentitiesAreDerivedPerCauseAndKnower()
    {
        EntityId first = CognitionEvidence.DeriveIdentity(DeliveryCommand, 0);
        Assert.Equal(first, CognitionEvidence.DeriveIdentity(DeliveryCommand, 0));
        Assert.NotEqual(first, CognitionEvidence.DeriveIdentity(DeliveryCommand, 1));
        Assert.NotEqual(first, CognitionEvidence.DeriveIdentity(GatherCommand, 0));
        Assert.NotEqual(first, EntityId.DeriveSpawned(DeliveryCommand, 0));
    }

    [Fact]
    public void CognitionHoldsOnlyItsOwnEvidenceInCanonicalOrder()
    {
        ActorCognition wren = ActorCognition.Empty(Wren).Acquire(Delivery(5000), 0).Acquire(Observation(Source, 3, 1000), 0);
        Assert.Equal([Observed, Delivered], wren.Evidence.Select(entry => entry.EvidenceId));
        Assert.Equal(2UL, wren.Revision);
        Assert.Throws<InvalidOperationException>(() => wren.Acquire(Delivery(5000), 0));
        var foreign = new CognitionEvidence(Id(803), Player, EvidenceKind.SourceObserved, ObserveCommand, null, Source, 3, At(0));
        Assert.Throws<ArgumentException>(() => new ActorCognition(Wren, 0, [foreign], null, []));
        var unsupported = new CognitionIntention(IntentionKind.WorkFundedProject, Project, At(0), [Delivered]);
        Assert.Throws<ArgumentException>(() => new ActorCognition(Wren, 0, [], unsupported, []));
        Assert.Throws<ArgumentException>(() => new CognitionIntention(IntentionKind.WorkFundedProject, Project, At(0), []));
        Assert.Throws<ArgumentException>(() => new CognitionIntention(IntentionKind.AwaitSupply, Project, At(0),
            [Delivered, Delivered]));
    }

    [Fact]
    public void BoundsRefuseRatherThanForget()
    {
        ActorCognition wren = ActorCognition.Empty(Wren);
        for (ulong index = 0; index < ActorCognition.MaximumEvidence; index++)
            wren = wren.Acquire(Observation(Id(5_000 + index), 1, (int)index, Id(10_000 + index)), 0);
        ActorCognition full = wren;
        Assert.Throws<CognitionCapacityException>(() => full.Acquire(Observation(Source, 1, 100, Id(99_999)), 0));
        Assert.Equal(ActorCognition.MaximumEvidence, full.Evidence.Count);
        Assert.Equal(Id(10_000), full.Evidence[0].EvidenceId);

        ActorCognition remembering = ActorCognition.Empty(Wren);
        var gather = new CognitionIntention(IntentionKind.GatherPersonalStock, Source, At(0), []);
        var wait = new CognitionIntention(IntentionKind.AwaitSupply, Project, At(0), []);
        remembering = remembering.Adopt(Decision(wait, CognitionDeferral.MissingTool), At(0));
        while (remembering.Memory.Count + 2 <= ActorCognition.MaximumMemory)
            remembering = remembering.Adopt(Decision(remembering.Intention!.Kind == IntentionKind.AwaitSupply
                ? gather : wait, CognitionDeferral.None), At(1));
        ActorCognition saturated = remembering;
        CognitionIntention other = saturated.Intention!.Kind == IntentionKind.AwaitSupply ? gather : wait;
        Assert.Throws<CognitionCapacityException>(() => saturated.Adopt(Decision(other, CognitionDeferral.None), At(2)));
        Assert.Equal(ActorCognition.MaximumMemory - 1, saturated.Memory.Count);
    }

    [Fact]
    public void BeliefsAreDerivedFromEvidenceAndCanBeStale()
    {
        CognitionBeliefs nothing = ActorCognition.Empty(Wren).Beliefs;
        Assert.Null(nothing.DeliveryEvidence(Quest));
        Assert.Empty(nothing.Sources);

        ActorCognition wren = ActorCognition.Empty(Wren).Acquire(Observation(Source, 3, 1000), 0)
            .Acquire(Gathered(Source, 2, 2000, Id(810)), 0).Acquire(Gathered(Source, 0, 3000, Id(811)), 0);
        KnownSource depleted = Assert.Single(wren.Beliefs.Sources);
        Assert.True(depleted.BelievedDepleted);
        Assert.Equal(0UL, depleted.LatestYield);
        // The later empty-handed attempt replaced the uncited earlier yield; the journal keeps the earlier one.
        Assert.Equal([Observed, Id(811)], wren.Evidence.Select(entry => entry.EvidenceId));

        ActorCognition refreshed = wren.Acquire(Observation(Source, 3, 4000, Id(812)), 0);
        KnownSource replenished = Assert.Single(refreshed.Beliefs.Sources);
        Assert.False(replenished.BelievedDepleted);
        Assert.Equal(Id(812), replenished.EvidenceId);
        Assert.DoesNotContain(refreshed.Evidence, entry => entry.EvidenceId == Observed);

        ActorCognition unobserved = ActorCognition.Empty(Wren).Acquire(Gathered(OtherSource, 0, 1000, Id(813)), 0);
        Assert.Empty(unobserved.Beliefs.Sources);
        Assert.Equal(Delivered, ActorCognition.Empty(Wren).Acquire(Delivery(), 0).Beliefs.DeliveryEvidence(Quest));
    }

    public static TheoryData<string, IntentionKind?, CognitionDeferral> Table => new()
    {
        { "hold", IntentionKind.AwaitSupply, CognitionDeferral.ReadingHold },
        { "delivered", IntentionKind.WorkFundedProject, CognitionDeferral.None },
        { "stocked", IntentionKind.AwaitSupply, CognitionDeferral.StockTargetMet },
        { "no-tool", IntentionKind.AwaitSupply, CognitionDeferral.MissingTool },
        { "no-source", IntentionKind.AwaitSupply, CognitionDeferral.NoKnownSource },
        { "depleted", IntentionKind.AwaitSupply, CognitionDeferral.SourceDepleted },
        { "unreachable", IntentionKind.AwaitSupply, CognitionDeferral.Unreachable },
        { "tired", IntentionKind.AwaitSupply, CognitionDeferral.InsufficientEffort },
        { "gather", IntentionKind.GatherPersonalStock, CognitionDeferral.AwaitingSupply },
        { "built", null, CognitionDeferral.None },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void PolicyFollowsTheDelayedSupplyDecisionTable(string row, IntentionKind? expected, CognitionDeferral reason)
    {
        var waiting = new CognitionIntention(IntentionKind.AwaitSupply, Project, At(0), []);
        var missingTool = new CurrentDeferral(CognitionDeferral.MissingTool, CognitionPolicy.Version, At(0));
        ActorCognition observed = new(Wren, 0, [Observation(Source, 3, 1000)], waiting, [], deferral: missingTool);
        ActorCognition depleted = observed.Acquire(Gathered(Source, 0, 2000, Id(810)), 0);
        ActorCognition blind = new(Wren, 0, [], waiting, [], deferral: missingTool);
        (ActorCognition cognition, CognitionSituation situation) = row switch
        {
            "hold" => (observed.Acquire(Delivery(), 0), Situation(hold: true, reachable: [Source])),
            "delivered" => (observed.Acquire(Delivery(), 0), Situation(tool: false)),
            "stocked" => (observed, Situation(stock: 2, reachable: [Source])),
            "no-tool" => (observed, Situation(tool: false, reachable: [Source])),
            "no-source" => (blind, Situation(reachable: [Source])),
            "depleted" => (depleted, Situation(reachable: [Source])),
            "unreachable" => (observed, Situation()),
            "tired" => (observed, Situation(effort: false, reachable: [Source])),
            "gather" => (observed, Situation(reachable: [Source])),
            "built" => (observed, Situation(needsWork: false, reachable: [Source])),
            _ => throw new ArgumentOutOfRangeException(nameof(row)),
        };
        CognitionDecision decision = CognitionPolicy.Decide(cognition, situation, At(6000));
        Assert.Equal(expected, decision.Intention?.Kind);
        Assert.Equal(reason, decision.Deferral);
        Assert.False(decision.CompletedCurrent);
        if (expected == IntentionKind.GatherPersonalStock) Assert.Equal([Observed], decision.Intention!.Basis);
        if (expected == IntentionKind.WorkFundedProject) Assert.Equal([Delivered], decision.Intention!.Basis);
    }

    [Fact]
    public void GatheringChoosesTheFirstReachableUndepletedSourceInIdentityOrder()
    {
        ActorCognition wren = ActorCognition.Empty(Wren).Acquire(Observation(OtherSource, 3, 500, Id(820)), 0)
            .Acquire(Observation(Source, 3, 1000), 0).Acquire(Gathered(Source, 0, 1500, Id(821)), 0);
        CognitionDecision decision = CognitionPolicy.Decide(wren, Situation(reachable: [Source, OtherSource]), At(2000));
        Assert.Equal((IntentionKind.GatherPersonalStock, OtherSource), (decision.Intention!.Kind, decision.Intention.SubjectId));
        Assert.Equal([Id(820)], decision.Intention.Basis);
    }

    [Fact]
    public void RedecidingKeepsTheOriginalAdoptionAndAdoptingNothingNewChangesNothing()
    {
        ActorCognition wren = ActorCognition.Empty(Wren).Acquire(Observation(Source, 3, 1000), 0);
        CognitionDecision first = CognitionPolicy.Decide(wren, Situation(tool: false, reachable: [Source]), At(2000));
        ActorCognition waiting = wren.Adopt(first, At(2000));
        CognitionDecision again = CognitionPolicy.Decide(waiting, Situation(tool: false, reachable: [Source]), At(9000));
        Assert.Same(waiting.Intention, again.Intention);
        Assert.Same(waiting, waiting.Adopt(again, At(9000)));
        Assert.Same(waiting, waiting.Adopt(CognitionPolicy.Decide(waiting, Situation(hold: true), At(9500)), At(9500)));
    }

    [Fact]
    public void GatheringEndsAsCompletedOnlyWhenTheStockTargetIsMetAndSurvivesSpentEffort()
    {
        ActorCognition gathering = ActorCognition.Empty(Wren).Acquire(Observation(Source, 3, 1000), 0);
        gathering = gathering.Adopt(CognitionPolicy.Decide(gathering, Situation(reachable: [Source]), At(1000)), At(1000));
        Assert.Equal(IntentionKind.GatherPersonalStock, gathering.Intention!.Kind);

        CognitionDecision stocked = CognitionPolicy.Decide(gathering, Situation(stock: 2, reachable: [Source]), At(3000));
        Assert.True(stocked.CompletedCurrent);
        Assert.Equal(MemoryOutcome.Completed, gathering.Adopt(stocked, At(3000)).Memory[^2].Outcome);

        // Spent effort defers the next swing but keeps the plan: the reason changes and is remembered once.
        CognitionDecision tired = CognitionPolicy.Decide(gathering, Situation(effort: false, reachable: [Source]), At(3000));
        Assert.False(tired.CompletedCurrent);
        Assert.Equal(CognitionDeferral.InsufficientEffort, tired.Deferral);
        Assert.Same(gathering.Intention, tired.Intention);
        ActorCognition resting = gathering.Adopt(tired, At(3000));
        Assert.Same(gathering.Intention, resting.Intention);
        Assert.Equal(gathering.Memory.Count + 1, resting.Memory.Count);
        Assert.Equal((MemoryOutcome.Reconsidered, CognitionDeferral.InsufficientEffort),
            (resting.Memory[^1].Outcome, resting.Memory[^1].Reason));
        Assert.Equal(new CurrentDeferral(CognitionDeferral.InsufficientEffort, CognitionPolicy.Version, At(3000)), resting.Deferral);

        // A source no longer reachable does end the plan, with that reason.
        CognitionDecision blocked = CognitionPolicy.Decide(gathering, Situation(effort: false), At(3000));
        CognitionMemory interrupted = gathering.Adopt(blocked, At(3000)).Memory[^2];
        Assert.Equal((MemoryOutcome.Superseded, CognitionDeferral.Unreachable), (interrupted.Outcome, interrupted.Reason));
    }

    [Fact]
    public void ANewerObservationOrYieldReplacesOnlyTheUncitedOlderOneOfItsSource()
    {
        ActorCognition wren = ActorCognition.Empty(Wren).Acquire(Observation(Source, 3, 1000), 0)
            .Acquire(Observation(OtherSource, 3, 1500, Id(840)), 0);
        wren = wren.Adopt(Decision(new CognitionIntention(IntentionKind.GatherPersonalStock, Source, At(1000), [Observed]),
            CognitionDeferral.AwaitingSupply), At(1000));
        // The cited observation stays; each newer uncited observation or yield of the same source replaces the last.
        for (ulong swing = 0; swing < 3 * ActorCognition.MaximumEvidence; swing++)
        {
            int at = 2000 + (int)swing * 10;
            wren = wren.Acquire(Gathered(Source, 1, at, Id(20_000 + swing)), swing)
                .Acquire(Observation(Source, 2, at, Id(30_000 + swing)), swing);
        }
        ulong last = 3 * ActorCognition.MaximumEvidence - 1;
        Assert.Equal([Observed, Id(840), Id(20_000 + last), Id(30_000 + last)],
            wren.Evidence.Select(entry => entry.EvidenceId));
        KnownSource source = wren.Beliefs.Sources.Single(known => known.SourceId == Source);
        Assert.Equal((Id(30_000 + last), 1UL), (source.EvidenceId, source.LatestYield!.Value));
        // Another source's observation is a different subject and is untouched.
        Assert.Contains(wren.Beliefs.Sources, known => known.SourceId == OtherSource && known.EvidenceId == Id(840));
        // Received items and deliveries are never replaced.
        ActorCognition gifted = wren.Acquire(new CognitionEvidence(Id(50_001), Wren, EvidenceKind.ItemReceived, Id(50_002),
            Player, Id(50_003), 1, At(9000)), 0).Acquire(new CognitionEvidence(Id(50_004), Wren, EvidenceKind.ItemReceived,
            Id(50_005), Player, Id(50_003), 1, At(9001)), 0);
        Assert.Equal(2, gifted.Evidence.Count(entry => entry.Kind == EvidenceKind.ItemReceived));
        // A state that keeps two uncited observations of one source is not canonical.
        Assert.Throws<ArgumentException>(() => new ActorCognition(Wren, 0,
            [Observation(Source, 3, 1000), Observation(Source, 2, 2000, Id(841))], null, []));
        Assert.Throws<ArgumentException>(() => ActorCognition.Empty(Wren).Acquire(
            new CognitionEvidence(Id(842), Player, EvidenceKind.SourceObserved, ObserveCommand, null, Source, 3, At(0)), 0));
    }

    [Fact]
    public void TheEvidenceBoundIsCheckedAfterReplacement()
    {
        ActorCognition wren = ActorCognition.Empty(Wren);
        for (ulong index = 0; index < ActorCognition.MaximumEvidence; index++)
            wren = wren.Acquire(Observation(Id(5_000 + index), 1, (int)index, Id(10_000 + index)), 0);
        ActorCognition refreshed = wren.Acquire(Observation(Id(5_000), 0, 100, Id(99_998)), 0);
        Assert.Equal(ActorCognition.MaximumEvidence, refreshed.Evidence.Count);
        Assert.True(refreshed.Beliefs.Sources.Single(known => known.SourceId == Id(5_000)).BelievedDepleted);
    }

    [Fact]
    public void AReasonChangeIsRememberedOnceAndARepeatedReasonChangesOnlyTheWake()
    {
        var waiting = new CognitionIntention(IntentionKind.AwaitSupply, Project, At(0), []);
        ActorCognition wren = ActorCognition.Empty(Wren).Adopt(Decision(waiting, CognitionDeferral.MissingTool), At(0));
        Assert.Equal(new CurrentDeferral(CognitionDeferral.MissingTool, CognitionPolicy.Version, At(0)), wren.Deferral);

        ActorCognition tired = wren.Adopt(Decision(waiting, CognitionDeferral.InsufficientEffort, step: 20), At(3000));
        Assert.Same(wren.Intention, tired.Intention);
        Assert.Equal(new CurrentDeferral(CognitionDeferral.InsufficientEffort, CognitionPolicy.Version, At(3000)), tired.Deferral);
        CognitionMemory changed = tired.Memory[^1];
        Assert.Equal((IntentionKind.AwaitSupply, MemoryOutcome.Reconsidered, CognitionDeferral.InsufficientEffort, At(3000)),
            (changed.Intention, changed.Outcome, changed.Reason, changed.At));
        Assert.Empty(changed.Basis);

        // The same reason again keeps its original "since" and adds no memory; an identical wake changes nothing.
        ActorCognition again = tired.Adopt(Decision(waiting, CognitionDeferral.InsufficientEffort, step: 40) with
            { PolicyVersion = "cognition.other-planner" }, At(4000));
        Assert.Equal(tired.Memory.Count, again.Memory.Count);
        Assert.Equal(tired.Deferral, again.Deferral);
        Assert.Same(again, again.Adopt(Decision(waiting, CognitionDeferral.InsufficientEffort, step: 40), At(4500)));

        // Without an intention nothing is deferred, and a held decision still services nothing.
        Assert.Throws<ArgumentException>(() => tired.Adopt(Decision(null, CognitionDeferral.MissingTool), At(5000)));
        Assert.Throws<ArgumentException>(() => new ActorCognition(Wren, 0, [], null, [],
            deferral: new CurrentDeferral(CognitionDeferral.MissingTool, CognitionPolicy.Version, At(0))));
        Assert.Throws<ArgumentException>(() => new CurrentDeferral(CognitionDeferral.ReadingHold, CognitionPolicy.Version, At(0)));
        Assert.Throws<ArgumentException>(() => new ActorCognition(Wren, 0, [], waiting, []));
        Assert.Throws<ArgumentException>(() => new ActorCognition(Wren, 0, [], waiting, [],
            deferral: new CurrentDeferral(CognitionDeferral.None, CognitionPolicy.Version, new WorldInstant(-1, 0))));

        // Reason changes count against the memory bound like any other remembered decision.
        ActorCognition flapping = wren;
        while (flapping.Memory.Count < ActorCognition.MaximumMemory)
            flapping = flapping.Adopt(Decision(waiting, flapping.Deferral!.Reason == CognitionDeferral.MissingTool
                ? CognitionDeferral.InsufficientEffort : CognitionDeferral.MissingTool), At(6000));
        ActorCognition saturated = flapping;
        CognitionDeferral other = saturated.Deferral!.Reason == CognitionDeferral.MissingTool
            ? CognitionDeferral.InsufficientEffort : CognitionDeferral.MissingTool;
        Assert.Throws<CognitionCapacityException>(() => saturated.Adopt(Decision(waiting, other), At(7000)));
    }

    [Fact]
    public void ReconsiderationWakesOnlyFromTheActorsOwnAcquisitionsAndPersistsItsCause()
    {
        ActorCognition wren = ActorCognition.Empty(Wren);
        Assert.Null(wren.Wake);
        wren = wren.Acquire(Observation(Source, 3, 1000), 40);
        Assert.Equal(new CognitionWake(40, WakeCause.EvidenceAcquired), wren.Wake);
        wren = wren.Acquire(Delivery(), 60);
        Assert.Equal(new CognitionWake(40, WakeCause.EvidenceAcquired), wren.Wake);
        Assert.Same(wren, wren.Schedule(new CognitionWake(50, WakeCause.ActionFinished)));
        Assert.Equal(new CognitionWake(30, WakeCause.OwnResourceChanged),
            wren.Schedule(new CognitionWake(30, WakeCause.OwnResourceChanged)).Wake);
        Assert.Throws<ArgumentException>(() => wren.Schedule(new CognitionWake(10, WakeCause.Fallback)));

        ActorCognition held = wren.Suppress(WakeSuppression.ReadingHold);
        Assert.Equal(new CognitionWake(40, WakeCause.EvidenceAcquired, WakeSuppression.ReadingHold), held.Wake);
        ActorCognition stillHeld = held.Acquire(Gathered(Source, 1, 2000, Id(830)), 10);
        Assert.Equal(new CognitionWake(10, WakeCause.EvidenceAcquired, WakeSuppression.ReadingHold), stillHeld.Wake);
        Assert.Equal(WakeSuppression.None, stillHeld.Suppress(WakeSuppression.None).Wake!.Suppression);

        ActorCognition decided = wren.Adopt(CognitionPolicy.Decide(wren, Situation(step: 60, tool: false), At(3000)), At(3000));
        Assert.Equal(new CognitionWake(60 + CognitionPolicy.FallbackSteps, WakeCause.Fallback), decided.Wake);
        Assert.Equal(new CognitionWake(40, WakeCause.EvidenceAcquired),
            CognitionCodecV2.Decode(CognitionCodecV2.Encode(new CognitionSnapshot(World, Branch, 0, [wren]))).For(Wren).Wake);
    }

    [Fact]
    public void AHeldDecisionKeepsTheEvidenceWakeForAfterTheHold()
    {
        ActorCognition waiting = ActorCognition.Empty(Wren).Acquire(Observation(Source, 3, 1000), 0);
        waiting = waiting.Adopt(CognitionPolicy.Decide(waiting, Situation(tool: false), At(1000)), At(1000));
        ActorCognition delivered = waiting.Acquire(Delivery(5000), 50).Suppress(WakeSuppression.ReadingHold);
        CognitionDecision held = CognitionPolicy.Decide(delivered, Situation(step: 60, tool: false, hold: true), At(5500));
        Assert.Same(delivered, delivered.Adopt(held, At(5500)));
        Assert.Equal(new CognitionWake(50, WakeCause.EvidenceAcquired, WakeSuppression.ReadingHold), delivered.Wake);
        Assert.Throws<ArgumentException>(() => delivered.Adopt(Decision(null, CognitionDeferral.ReadingHold), At(5500)));

        ActorCognition resumed = delivered.Suppress(WakeSuppression.None);
        ActorCognition working = resumed.Adopt(CognitionPolicy.Decide(resumed, Situation(step: 70, tool: false), At(6000)), At(6000));
        Assert.Equal(IntentionKind.WorkFundedProject, working.Intention!.Kind);
        Assert.Equal(new CognitionWake(70 + CognitionPolicy.FallbackSteps, WakeCause.Fallback), working.Wake);
    }

    [Fact]
    public void ServicingAnUnchangedDecisionMovesOnlyTheWake()
    {
        ActorCognition wren = ActorCognition.Empty(Wren).Acquire(Observation(Source, 3, 1000), 0);
        ActorCognition waiting = wren.Adopt(CognitionPolicy.Decide(wren, Situation(tool: false), At(1000)), At(1000));
        ActorCognition serviced = waiting.Adopt(CognitionPolicy.Decide(waiting, Situation(step: 300, tool: false), At(4000)), At(4000));
        Assert.Same(waiting.Intention, serviced.Intention);
        Assert.Equal(waiting.Memory.Count, serviced.Memory.Count);
        Assert.Equal(waiting.Revision + 1, serviced.Revision);
        Assert.Equal(300 + CognitionPolicy.FallbackSteps, serviced.Wake!.DueStep);
        Assert.Throws<ArgumentException>(() => waiting.Adopt(Decision(null, CognitionDeferral.None) with
            { NextWake = new CognitionWake(5, WakeCause.EvidenceAcquired) }, At(4000)));
        Assert.Throws<ArgumentException>(() => waiting.Adopt(Decision(null, CognitionDeferral.None) with { PolicyVersion = " " },
            At(4000)));
    }

    [Fact]
    public void ReplayingRecordedDecisionsNeedsNoPlanner()
    {
        // Live: the planner proposes, and each decision is what an admitted command would record.
        ActorCognition live = ActorCognition.Empty(Wren).Acquire(Observation(Source, 3, 1000), 20);
        List<CognitionDecision> recorded = [];
        foreach ((ulong step, CognitionSituation situation, int at) in new[]
        {
            (20UL, Situation(step: 20, tool: false, reachable: [Source]), 1000),
            (100UL, Situation(step: 100, tool: false, reachable: [Source]), 5000),
        })
        {
            if (step == 100) live = live.Acquire(Delivery(), 100);
            CognitionDecision decision = CognitionPolicy.Decide(live, situation, At(at));
            recorded.Add(decision);
            live = live.Adopt(decision, At(at));
        }
        // Replay: the same committed evidence and recorded decisions, with no planner call.
        ActorCognition replayed = ActorCognition.Empty(Wren).Acquire(Observation(Source, 3, 1000), 20)
            .Adopt(recorded[0], At(1000)).Acquire(Delivery(), 100).Adopt(recorded[1], At(5000));
        Assert.Equal(CognitionCodecV2.Encode(new CognitionSnapshot(World, Branch, 1, [live])),
            CognitionCodecV2.Encode(new CognitionSnapshot(World, Branch, 1, [replayed])));
    }

    [Fact]
    public void WrensDelayedSupplyEpisodeIsExplainableFromHerOwnMemory()
    {
        // Wren's joinery kit is not an axe: she waits, honestly, until the player's delivery reaches her.
        ActorCognition wren = ActorCognition.Empty(Wren).Acquire(Observation(Source, 3, 1000), 0);
        wren = wren.Adopt(CognitionPolicy.Decide(wren, Situation(tool: false, reachable: [Source]), At(1000)), At(1000));
        Assert.Equal(IntentionKind.AwaitSupply, wren.Intention!.Kind);

        wren = wren.Acquire(Delivery(5000), 0);
        wren = wren.Adopt(CognitionPolicy.Decide(wren, Situation(tool: false, reachable: [Source]), At(5000)), At(5000));
        Assert.Equal(IntentionKind.WorkFundedProject, wren.Intention!.Kind);

        CognitionDecision finished = CognitionPolicy.Decide(wren, Situation(needsWork: false, tool: false), At(9000));
        Assert.True(finished.CompletedCurrent);
        wren = wren.Adopt(finished, At(9000));
        Assert.Null(wren.Intention);
        Assert.Equal(
            [
                (IntentionKind.AwaitSupply, MemoryOutcome.Adopted, CognitionDeferral.MissingTool),
                (IntentionKind.AwaitSupply, MemoryOutcome.Superseded, CognitionDeferral.None),
                (IntentionKind.WorkFundedProject, MemoryOutcome.Adopted, CognitionDeferral.None),
                (IntentionKind.WorkFundedProject, MemoryOutcome.Completed, CognitionDeferral.None),
            ],
            wren.Memory.Select(memory => (memory.Intention, memory.Outcome, memory.Reason)));
        Assert.Throws<ArgumentException>(() => wren.Adopt(Decision(
            new CognitionIntention(IntentionKind.AwaitSupply, Project, At(1), []), CognitionDeferral.None), At(1)));
    }

    [Fact]
    public void TheFacetHoldsOneOwnerPerActorWithinItsBound()
    {
        var facet = new CognitionSnapshot(World, Branch, 0, []);
        Assert.Throws<ArgumentException>(() => new CognitionSnapshot(World, World, 0, []));
        Assert.Throws<ArgumentException>(() => new CognitionSnapshot(World, Branch, 0,
            [ActorCognition.Empty(Wren), ActorCognition.Empty(Wren)]));
        Assert.Throws<ArgumentException>(() => new CognitionSnapshot(World, Branch, 0,
            Enumerable.Range(0, CognitionSnapshot.MaximumActors + 1).Select(index => ActorCognition.Empty(Id(60_000 + (ulong)index)))));
        CognitionSnapshot updated = facet.With(ActorCognition.Empty(Player)).With(ActorCognition.Empty(Wren))
            .With(ActorCognition.Empty(Wren).Acquire(Delivery(), 0));
        Assert.Equal([Wren, Player], updated.Actors.Select(actor => actor.ActorId));
        Assert.Equal(3UL, updated.Revision);
        Assert.Single(updated.For(Wren).Evidence);
        Assert.Empty(updated.For(Id(77)).Evidence);
    }
}
