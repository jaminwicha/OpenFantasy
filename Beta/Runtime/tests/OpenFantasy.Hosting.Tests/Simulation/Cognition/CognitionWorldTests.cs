using OpenFantasy.Core;
using OpenFantasy.Core.Cognition;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Simulation.Cognition;

/// <summary>
/// The first profile-42 world binding, in the acceptance order of the PLAN-STATE exit decision: evidence only from
/// committed outcomes, references rather than copies, replay without the policy, restart with a pending wake,
/// suppression and overflow. Every effect uses the adventure's original people, tools, quest and project.
/// </summary>
public sealed class CognitionWorldTests
{
    [Fact]
    public void Fresh_cognition_world_strictly_succeeds_collective_withdrawal_with_the_original_pins()
    {
        WorldSnapshot adventure = AdventureWorldAdmission.CreateEmbedded("cognition-world-contract", ["Rowan"]);
        WorldSnapshot withdrawal = WorldPartyWithdrawalAdmission.CreateFresh(adventure);
        WorldSnapshot genesis = CognitionWorldFixture.FreshGenesis;

        Assert.Equal((ushort)42, genesis.ProfileVersion);
        Assert.Equal(genesis.Residents.Select(resident => resident.ActorId), genesis.Cognition!.Actors.Select(actor => actor.ActorId));
        Assert.All(genesis.Cognition.Actors, actor =>
        {
            Assert.Empty(actor.Evidence);
            Assert.Null(actor.Intention);
            Assert.Empty(actor.Memory);
            Assert.Null(actor.Wake);
        });
        // The six wood, 420 XP, 200 gil and 200 work units are the unchanged authored content of the basis.
        Assert.Equal(withdrawal.Inventory.ContentHash, genesis.Inventory.ContentHash);
        Assert.Equal(withdrawal.Quests!.ContentHash, genesis.Quests!.ContentHash);
        Assert.Equal(withdrawal.Progression!.ContentHash, genesis.Progression!.ContentHash);
        Assert.Equal(withdrawal.Construction!.ContentHash, genesis.Construction!.ContentHash);
        DeliveryQuestDefinition quest = genesis.Quests.Definitions.Single();
        Assert.Equal(6UL, quest.Quantity);
        Assert.Equal(0UL, genesis.Construction.Projects.Single().Progress.CompletedUnits);

        byte[] bytes = WorldSnapshotCodec.Encode(genesis);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldCognitionAdmission.CreateFresh(withdrawal)));
        // Decoders for profiles up to 41 refuse cognition bytes, and profile 41 cannot encode a cognition root.
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(Relabel(bytes, 42, 41)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV41.Decode(bytes));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV41.Encode(genesis));
        // Fresh selection never upgrades a lived save or skips an interpretation.
        Assert.Throws<ArgumentException>(() => WorldCognitionAdmission.CreateFresh(adventure));
        Assert.Throws<ArgumentException>(() => WorldCognitionAdmission.CreateFresh(genesis));
        WorldSnapshot lived = new WorldStepProcessor().Decide(withdrawal, new WorldStepBatch(new EntityId(9, 9), withdrawal.WorldId,
            withdrawal.BranchId, 0, 0, WorldBatchKind.AdvancingStep, [], [], withdrawal.ProfileVersion)).Candidate;
        Assert.Throws<ArgumentException>(() => WorldCognitionAdmission.CreateFresh(lived));
        // Cognition actions belong to profile 42's advancing steps only.
        var fixture = new CognitionWorldFixture();
        WorldActionCommand inspect = fixture.Command(fixture.Wren, fixture.InspectionOf(fixture.Oak));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(new EntityId(9, 10), withdrawal.WorldId, withdrawal.BranchId, 0, 0,
            WorldBatchKind.AdvancingStep, [], [inspect], withdrawal.ProfileVersion));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(new EntityId(9, 11), genesis.WorldId, genesis.BranchId, 0, 0,
            WorldBatchKind.HeldInteraction, [], [inspect], genesis.ProfileVersion));
    }

    [Fact]
    public void Evidence_comes_only_from_committed_outcomes_in_the_same_boundary()
    {
        var fixture = new CognitionWorldFixture();
        var axe = fixture.Owned(fixture.Player, fixture.Axe);
        byte[] untouched = CognitionCodecV2.Encode(fixture.State.Cognition!);

        // A refused gift writes nothing for anyone.
        GiveItemIntent current = fixture.GiftOf(fixture.Player, fixture.Wren, axe, 1);
        var stale = new GiveItemIntent(current.TargetActorId, current.ExpectedTargetRevision, current.LotId,
            current.ExpectedLotRevision + 1, current.SourceCustodianId, current.ExpectedSourceRevision, 1);
        Assert.NotEqual(CommandReceiptStatus.Applied, fixture.Act(fixture.Player, stale).Status);
        Assert.Equal(untouched, CognitionCodecV2.Encode(fixture.State.Cognition!));

        // The committed gift gives the recipient one fact about its cause, counterpart, lot and time, and nobody else.
        WorldInstant at = fixture.State.Calendar;
        ulong steps = fixture.State.AdvancingSteps;
        WorldActionCommand gift = fixture.Command(fixture.Player, fixture.GiftOf(fixture.Player, fixture.Wren, axe, 1));
        WorldStepDecision result = fixture.Step(gift);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(result.Receipts).Status);
        CognitionEvidence received = Assert.Single(fixture.CognitionOf(fixture.Wren).Evidence);
        Assert.Equal(EvidenceKind.ItemReceived, received.Kind);
        Assert.Equal(gift.CommandId, received.SourceCommandId);
        Assert.Equal(fixture.Player, received.CounterpartActorId);
        Assert.Equal(axe.LotId, received.SubjectId);
        Assert.Equal(1UL, received.Quantity);
        Assert.Equal(at, received.AcquiredAt);
        Assert.Equal(new CognitionWake(steps + 1, WakeCause.EvidenceAcquired), fixture.CognitionOf(fixture.Wren).Wake);
        Assert.Contains(result.Identities, identity => identity.Identity == received.EvidenceId);
        Assert.All(fixture.State.Cognition!.Actors.Where(actor => actor.ActorId != fixture.Wren), actor => Assert.Empty(actor.Evidence));
        Assert.Equal(fixture.State.GetResident(fixture.Wren).InventoryCustodianId, fixture.State.Inventory.Custody.GetLot(axe.LotId).CustodianId);
    }

    [Fact]
    public void A_change_elsewhere_leaves_beliefs_stale_until_new_evidence_is_acquired()
    {
        var fixture = new CognitionWorldFixture();
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        KnownSource seen = Assert.Single(fixture.CognitionOf(fixture.Wren).Beliefs.Sources);
        Assert.Equal(fixture.Oak, seen.SourceId);
        int swings = fixture.State.Gathering!.Nodes.Single(node => node.NodeId == fixture.Oak).RemainingSwings;
        Assert.Equal((ulong)swings, fixture.CognitionOf(fixture.Wren).Evidence.Single().Quantity);
        byte[] wrenBefore = CognitionCodecV2.Encode(new CognitionSnapshot(fixture.State.WorldId, fixture.State.BranchId, 0,
            [fixture.CognitionOf(fixture.Wren)]));

        // The player harvests the same oak. Only the player learns the yield; Wren's belief stays as observed.
        fixture.Walk(fixture.Player, fixture.StandingNear(fixture.Player, fixture.Oak)!.Value);
        fixture.RecoverEffort(fixture.Player);
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Player, fixture.GatherOf(fixture.Player, fixture.Oak)).Status);
        Assert.Equal(swings - 1, fixture.State.Gathering!.Nodes.Single(node => node.NodeId == fixture.Oak).RemainingSwings);
        Assert.Equal([EvidenceKind.GatherOutcome, EvidenceKind.SourceObserved],
            fixture.CognitionOf(fixture.Player).Evidence.Select(entry => entry.Kind).Order());
        Assert.Equal(wrenBefore, CognitionCodecV2.Encode(new CognitionSnapshot(fixture.State.WorldId, fixture.State.BranchId, 0,
            [fixture.CognitionOf(fixture.Wren)])));
        Assert.Equal(seen, Assert.Single(fixture.CognitionOf(fixture.Wren).Beliefs.Sources));

        // A new look replaces the stale observation.
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        Assert.Equal((ulong)(swings - 1), fixture.CognitionOf(fixture.Wren).Evidence[^1].Quantity);
    }

    [Fact]
    public void Wren_waits_honestly_gathers_with_a_real_gift_and_resumes_the_funded_guard_after_delivery()
    {
        var fixture = RunEpisode(policyVersion: null);
        ActorCognition wren = fixture.CognitionOf(fixture.Wren);
        Assert.Equal(IntentionKind.WorkFundedProject, wren.Intention!.Kind);
        Assert.Equal(fixture.ProjectId, wren.Intention.SubjectId);
        CognitionEvidence delivery = Assert.Single(wren.Evidence, entry => entry.Kind == EvidenceKind.QuestDeliveryReceived);
        Assert.Equal([delivery.EvidenceId], wren.Intention.Basis);
        Assert.Equal(fixture.Quest.DefinitionId, delivery.SubjectId);
        Assert.Equal(fixture.Player, delivery.CounterpartActorId);
        Assert.Equal(fixture.Quest.Quantity, delivery.Quantity);
        // The causal sequence is remembered with reasons: waiting without a tool, gathering, stock met, then the guard.
        Assert.Equal(new (IntentionKind, MemoryOutcome, CognitionDeferral)[]
        {
            (IntentionKind.AwaitSupply, MemoryOutcome.Adopted, CognitionDeferral.MissingTool),
            (IntentionKind.AwaitSupply, MemoryOutcome.Reconsidered, CognitionDeferral.InsufficientEffort),
            (IntentionKind.AwaitSupply, MemoryOutcome.Superseded, CognitionDeferral.AwaitingSupply),
            (IntentionKind.GatherPersonalStock, MemoryOutcome.Adopted, CognitionDeferral.AwaitingSupply),
            (IntentionKind.GatherPersonalStock, MemoryOutcome.Completed, CognitionDeferral.StockTargetMet),
            (IntentionKind.AwaitSupply, MemoryOutcome.Adopted, CognitionDeferral.StockTargetMet),
            (IntentionKind.AwaitSupply, MemoryOutcome.Superseded, CognitionDeferral.None),
            (IntentionKind.WorkFundedProject, MemoryOutcome.Adopted, CognitionDeferral.None),
        }, wren.Memory.Select(entry => (entry.Intention, entry.Outcome, entry.Reason)));
        Assert.All(wren.Memory, entry => Assert.Equal(CognitionPolicy.Version, entry.PolicyVersion));
        // Personal stock is Wren's own gathered wood, distinct from the project's reserved delivery.
        Assert.Equal(CognitionWorldFixture.PersonalStockTarget, fixture.Holding(fixture.Wren, fixture.Quest.Item));
        // Only the latest uncited yield and observation of the oak remain; the journal keeps the earlier ones.
        Assert.Single(wren.Evidence, entry => entry.Kind == EvidenceKind.GatherOutcome);
        Assert.Equal(1UL, Assert.Single(wren.Beliefs.Sources).LatestYield);
        // References, not copies: the participation and project stay with their owners.
        ActorQuestState participation = fixture.State.Quests!.FindParticipation(fixture.Player, fixture.Quest.DefinitionId)!;
        Assert.Equal(ActorQuestStatus.Completed, participation.Status);
        Assert.Equal(0UL, fixture.State.Construction!.Projects.Single().Progress.CompletedUnits);
    }

    [Fact]
    public void Replay_applies_recorded_decisions_and_never_runs_the_policy()
    {
        var lived = RunEpisode(policyVersion: "cognition.delayed-supply.v1-variant");
        Assert.All(lived.CognitionOf(lived.Wren).Memory,
            entry => Assert.Equal("cognition.delayed-supply.v1-variant", entry.PolicyVersion));

        // Replay from genesis with only the recorded batch bytes: no policy, no fixture decisions.
        WorldSnapshot state = CognitionWorldFixture.FreshGenesis;
        HashSet<EntityId> occupied = [];
        var processor = new WorldStepProcessor();
        foreach (byte[] encoded in lived.Batches)
        {
            WorldStepDecision decided = processor.Decide(state, WorldStepCodec.Decode(encoded), occupied.Contains);
            occupied.UnionWith(decided.Identities.Select(identity => identity.Identity));
            state = decided.Candidate;
        }
        Assert.Equal(WorldSnapshotCodec.Encode(lived.State), WorldSnapshotCodec.Encode(state));
    }

    [Fact]
    public void Restart_midway_through_gathering_keeps_the_pending_wake_and_matches_the_uninterrupted_run()
    {
        var uninterrupted = RunEpisode(policyVersion: null);
        var restarted = RunEpisode(policyVersion: null, afterFirstGather: fixture =>
        {
            CognitionWake pending = fixture.CognitionOf(fixture.Wren).Wake!;
            Assert.Equal(WakeCause.EvidenceAcquired, pending.Cause);
            fixture.Restore();
            Assert.Equal(pending, fixture.CognitionOf(fixture.Wren).Wake);
        });
        Assert.Equal(WorldSnapshotCodec.Encode(uninterrupted.State), WorldSnapshotCodec.Encode(restarted.State));
    }

    [Fact]
    public void A_reading_hold_suppresses_reconsideration_with_its_reason_and_writes_no_commit_of_its_own()
    {
        var fixture = new CognitionWorldFixture();
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        CognitionWake pending = fixture.CognitionOf(fixture.Wren).Wake!;
        ulong revision = fixture.State.Revision;

        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Player,
            new SpeakToActorIntent(fixture.Wren, fixture.State.Space.GetActor(fixture.Wren).Revision)).Status);
        Assert.Equal(revision + 1, fixture.State.Revision);
        Assert.Equal(pending with { Suppression = WakeSuppression.ReadingHold }, fixture.CognitionOf(fixture.Wren).Wake);
        Assert.Null(fixture.Decision(fixture.Wren));

        Assert.Equal(CommandReceiptStatus.Applied, fixture.CloseDialogue().Status);
        Assert.Equal(pending, fixture.CognitionOf(fixture.Wren).Wake);
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Decide(fixture.Wren).Status);
        Assert.Equal(CognitionDeferral.MissingTool, fixture.CognitionOf(fixture.Wren).Memory.Single().Reason);
    }

    [Fact]
    public void An_encounter_suppresses_reconsideration_in_the_boundary_that_enters_it()
    {
        var fixture = new CognitionWorldFixture();
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        CognitionWake pending = fixture.CognitionOf(fixture.Wren).Wake!;
        var scenario = fixture.State.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        EntityId lead = scenario.InitiatingActorId;
        Assert.Equal(fixture.Player, lead);
        SubunitPosition target = fixture.State.Space.GetActor(scenario.TargetActorId).Feet;
        SubunitPosition approach = new[] { target.Offset(256, 0, 0), target.Offset(-256, 0, 0), target.Offset(0, 256, 0), target.Offset(0, -256, 0) }
            .Select(feet => (Feet: feet, Route: fixture.State.Space.QueryGroundRoute(lead, feet)))
            .Where(value => value.Route.Status == GroundRouteStatus.Reachable).OrderBy(value => value.Route.Waypoints.Count).First().Feet;
        fixture.Walk(lead, approach);
        // Walking is not reconsideration: the wake stays due and unserviced, and its due age is visible.
        Assert.Equal(pending, fixture.CognitionOf(fixture.Wren).Wake);

        WorldActionCommand entry = fixture.Command(lead, new EnterEncounterIntent(scenario.DefinitionId,
            EncounterEpisodeIdentity.Derive(fixture.State.EncounterLifecycle!, scenario.DefinitionId),
            fixture.State.EncounterLifecycle!.EncounterCount, fixture.State.Space.GetActor(scenario.TargetActorId).Revision));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(fixture.Apply(WorldBatchKind.EncounterLifecycle, [entry]).Receipts).Status);
        Assert.Equal(pending with { Suppression = WakeSuppression.Encounter }, fixture.CognitionOf(fixture.Wren).Wake);
        fixture.Apply(WorldBatchKind.EncounterLifecycle);
        fixture.Apply(WorldBatchKind.EncounterLifecycle);
        Assert.Equal(EncounterLifecyclePhase.Active, fixture.State.EncounterLifecycle!.Phase);
        Assert.Equal(pending with { Suppression = WakeSuppression.Encounter }, fixture.CognitionOf(fixture.Wren).Wake);
        Assert.Null(fixture.Decision(fixture.Wren));
        fixture.Restore();
        Assert.Equal(WakeSuppression.Encounter, fixture.CognitionOf(fixture.Wren).Wake!.Suppression);
    }

    [Fact]
    public void Decision_admission_refuses_stale_undue_and_repeated_decisions()
    {
        var fixture = new CognitionWorldFixture();
        // No pending wake: nothing is due.
        var unsolicited = new CognitionDecision(CognitionPolicy.Version, null, CognitionDeferral.None,
            new CognitionWake(fixture.State.AdvancingSteps + CognitionPolicy.FallbackSteps, WakeCause.Fallback));
        Assert.Equal(WorldActionRejection.Unavailable,
            fixture.Act(fixture.Wren, new RecordCognitionDecisionIntent(0, unsolicited)).Rejection);

        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        fixture.AdvanceUntilDue(fixture.Wren);
        WorldActionCommand decision = fixture.Decision(fixture.Wren)!;
        var intent = (RecordCognitionDecisionIntent)decision.Intent;
        Assert.Equal(WorldActionRejection.RevisionConflict, fixture.Act(fixture.Wren,
            new RecordCognitionDecisionIntent(intent.ExpectedCognitionRevision + 1, intent.Decision)).Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, fixture.Act(fixture.Wren, new RecordCognitionDecisionIntent(
            intent.ExpectedCognitionRevision, intent.Decision with { NextWake = new(fixture.State.AdvancingSteps, WakeCause.Fallback) })).Rejection);
        // Another person's credentials cannot decide for Wren.
        var player = fixture.Delivery(fixture.Player);
        var impostor = new WorldActionCommand(fixture.NextId(), player.PrincipalId, player.SessionId, fixture.Wren,
            fixture.State.WorldId, fixture.State.BranchId, fixture.State.Space.GetActor(fixture.Wren).Revision, intent);
        Assert.Equal(WorldActionRejection.NotAuthorized, fixture.Step(impostor).Receipts.Single().Rejection);
        Assert.Empty(fixture.CognitionOf(fixture.Wren).Memory);

        // A new intention is adopted now: the decision computed several steps ago is no longer admissible.
        Assert.Equal(WorldActionRejection.Unavailable, fixture.Act(fixture.Wren, intent).Rejection);
        intent = (RecordCognitionDecisionIntent)fixture.Decision(fixture.Wren)!.Intent;
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Step(fixture.Command(fixture.Wren, intent)).Receipts.Single().Status);
        Assert.Single(fixture.CognitionOf(fixture.Wren).Memory);
        // Reload creates no duplicate adoption: the same recorded decision is now stale.
        fixture.Restore();
        Assert.Equal(WorldActionRejection.RevisionConflict, fixture.Act(fixture.Wren, intent).Rejection);
        Assert.Single(fixture.CognitionOf(fixture.Wren).Memory);
    }

    [Fact]
    public void A_decision_cannot_remember_what_its_own_evidence_and_project_do_not_support()
    {
        var fixture = new CognitionWorldFixture();
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        fixture.AdvanceUntilDue(fixture.Wren);
        ActorCognition wren = fixture.CognitionOf(fixture.Wren);
        EntityId seen = wren.Evidence.Single().EvidenceId;
        WorldInstant now = fixture.State.Calendar;
        ulong steps = fixture.State.AdvancingSteps;
        var fallback = new CognitionWake(steps + CognitionPolicy.FallbackSteps, WakeCause.Fallback);
        CognitionDecision Decision(CognitionIntention? intention, CognitionDeferral deferral = CognitionDeferral.None,
            CognitionWake? wake = null, bool completed = false) =>
            new(CognitionPolicy.Version, intention, deferral, wake ?? fallback, completed);
        WorldActionRejection Try(CognitionDecision decision) => fixture.Step(fixture.Command(fixture.Wren,
            new RecordCognitionDecisionIntent(wren.Revision, decision with
            {
                // Each refusal is a separate boundary; keep the attempted adoption at the current instant.
                Intention = decision.Intention is { } held && held.AdoptedAt == now
                    ? new CognitionIntention(held.Kind, held.SubjectId, fixture.State.Calendar, held.Basis) : decision.Intention,
                NextWake = decision.NextWake == fallback
                    ? new CognitionWake(fixture.State.AdvancingSteps + CognitionPolicy.FallbackSteps, WakeCause.Fallback) : decision.NextWake,
            }))).Receipts.Single().Rejection;

        // Working the guard needs the received delivery for its quest, not merely a sighting.
        Assert.Equal(WorldActionRejection.Unavailable,
            Try(Decision(new CognitionIntention(IntentionKind.WorkFundedProject, fixture.ProjectId, now, [seen]))));
        // Gathering must cite an observation of its own source.
        Assert.Equal(WorldActionRejection.Unavailable,
            Try(Decision(new CognitionIntention(IntentionKind.GatherPersonalStock, fixture.Oak, now, []), CognitionDeferral.AwaitingSupply)));
        // Waiting names the actor's own project, never a source or someone else's work.
        Assert.Equal(WorldActionRejection.Unavailable,
            Try(Decision(new CognitionIntention(IntentionKind.AwaitSupply, fixture.Oak, now, []), CognitionDeferral.MissingTool)));
        // A new intention is adopted now, not backdated.
        Assert.Equal(WorldActionRejection.Unavailable, Try(Decision(new CognitionIntention(IntentionKind.AwaitSupply,
            fixture.ProjectId, new WorldInstant(now.Days, 0), []), CognitionDeferral.MissingTool)));
        // Nothing completed, so nothing is remembered as completed.
        Assert.Equal(WorldActionRejection.Unavailable, Try(Decision(null, completed: true)));
        // The fallback stays within the declared cadence.
        Assert.Equal(WorldActionRejection.Unavailable, Try(Decision(
            new CognitionIntention(IntentionKind.AwaitSupply, fixture.ProjectId, now, []), CognitionDeferral.MissingTool,
            new CognitionWake(ulong.MaxValue, WakeCause.Fallback))));
        Assert.Empty(fixture.CognitionOf(fixture.Wren).Memory);
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Decide(fixture.Wren).Status);
    }

    [Fact]
    public void A_hold_opened_earlier_in_the_same_batch_refuses_a_due_decision()
    {
        // Actions run in actor-identity order. A second traveler whose identity sorts before Wren's speaks to her in
        // the very batch in which her decision is due.
        // Identities derive from the seed, so the same authored adventure under another seed supplies that ordering.
        CognitionWorldFixture fixture = Enumerable.Range(0, 32)
            .Select(attempt => new CognitionWorldFixture(["Rowan"], $"cognition-hold-order-{attempt}"))
            .First(candidate => candidate.Player.CompareTo(candidate.Wren) < 0);
        EntityId earlier = fixture.Player;
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        if (fixture.State.Space.QueryInteraction(earlier, fixture.Wren) != SpatialInteraction.Reachable)
            fixture.WalkFar(earlier, fixture.StandingNear(earlier, fixture.Oak)!.Value);
        fixture.AdvanceUntilDue(fixture.Wren);
        WorldActionCommand decision = fixture.Decision(fixture.Wren)!;
        WorldActionCommand speech = fixture.Command(earlier, new SpeakToActorIntent(fixture.Wren,
            fixture.State.Space.GetActor(fixture.Wren).Revision));
        WorldStepDecision result = fixture.Step(speech, decision);
        Assert.Equal(WorldActionRejection.None, result.Receipts.Single(receipt => receipt.CommandId == speech.CommandId).Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, result.Receipts.Single(receipt => receipt.CommandId == decision.CommandId).Rejection);
        Assert.Empty(fixture.CognitionOf(fixture.Wren).Memory);
        Assert.Equal(WakeSuppression.ReadingHold, fixture.CognitionOf(fixture.Wren).Wake!.Suppression);
    }

    [Fact]
    public void An_inspection_refusal_reveals_nothing_about_a_source_out_of_sight()
    {
        var fixture = new CognitionWorldFixture();
        var berries = fixture.State.Gathering!.Nodes.Single(node => node.NodeId != fixture.Oak);
        // An absent source and a stale revision of a source out of sight give the same refusal.
        Assert.Equal(WorldActionRejection.OutOfReach,
            fixture.Act(fixture.Wren, new InspectGatherSourceIntent(new EntityId(0x0BAD, 0x50C), 0)).Rejection);
        Assert.Equal(WorldActionRejection.OutOfReach,
            fixture.Act(fixture.Wren, new InspectGatherSourceIntent(berries.NodeId, berries.Revision + 1)).Rejection);
        Assert.Equal(WorldActionRejection.OutOfReach,
            fixture.Act(fixture.Wren, new InspectGatherSourceIntent(berries.NodeId, berries.Revision)).Rejection);
        // Only for a source in sight is a stale revision a conflict.
        var oak = fixture.InspectionOf(fixture.Oak);
        Assert.Equal(WorldActionRejection.RevisionConflict,
            fixture.Act(fixture.Wren, oak with { ExpectedNodeRevision = oak.ExpectedNodeRevision + 1 }).Rejection);
        Assert.All(fixture.State.Cognition!.Actors, actor => Assert.Empty(actor.Evidence));
    }

    [Fact]
    public void A_cognition_root_names_its_exact_laws()
    {
        byte[] bytes = WorldSnapshotCodec.Encode(CognitionWorldFixture.FreshGenesis);
        byte[] pin = WorldCognitionAdmission.RulesPin.ToByteArray();
        int at = bytes.AsSpan().IndexOf(pin);
        Assert.True(at > 0);
        byte[] altered = bytes.ToArray();
        altered[at] ^= 0x01;
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(altered));
    }

    [Fact]
    public void Repeated_looks_replace_the_uncited_observation_instead_of_reaching_the_bound()
    {
        var fixture = new CognitionWorldFixture();
        EntityId first = EntityId.FromBytes(new byte[16]);
        for (int look = 0; look < 2 * ActorCognition.MaximumEvidence + 1; look++)
        {
            Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
            if (look == 0) first = Assert.Single(fixture.CognitionOf(fixture.Wren).Evidence).EvidenceId;
        }
        CognitionEvidence latest = Assert.Single(fixture.CognitionOf(fixture.Wren).Evidence);
        Assert.NotEqual(first, latest.EvidenceId);
        Assert.Equal(EvidenceKind.SourceObserved, latest.Kind);
        // The replaced observation is still in the recorded history.
        Assert.Contains(fixture.Batches, batch => WorldStepCodec.Decode(batch).Actions.Any(action =>
            CognitionEvidence.DeriveIdentity(action.CommandId, 0) == first));
        fixture.Restore();
        Assert.Equal(latest.EvidenceId, Assert.Single(fixture.CognitionOf(fixture.Wren).Evidence).EvidenceId);
    }

    [Fact]
    public void A_root_whose_current_deferral_postdates_the_world_does_not_decode()
    {
        var fixture = new CognitionWorldFixture();
        fixture.Apply(WorldBatchKind.AdvancingStep);
        WorldInstant now = fixture.State.Calendar;
        var waiting = new CognitionIntention(IntentionKind.AwaitSupply, fixture.ProjectId, now, []);
        var adopted = new CognitionMemory(IntentionKind.AwaitSupply, fixture.ProjectId, MemoryOutcome.Adopted, now,
            CognitionDeferral.MissingTool, CognitionPolicy.Version);
        ActorCognition Wren(WorldInstant since) => new(fixture.Wren, 1, [], waiting, [adopted], null,
            new CurrentDeferral(CognitionDeferral.MissingTool, CognitionPolicy.Version, since));
        Assert.Equal(now, fixture.WithWrenCognition(Wren(now)).Cognition!.For(fixture.Wren).Deferral!.Since);
        var later = new WorldInstant(now.Days, now.MillisecondsWithinDay + 1);
        FormatException refused = Assert.Throws<FormatException>(() => fixture.WithWrenCognition(Wren(later)));
        Assert.Contains("before this instant", refused.InnerException!.Message);
    }

    [Fact]
    public void An_evidence_overflow_stops_the_boundary_and_never_refuses_the_player_action()
    {
        var fixture = new CognitionWorldFixture();
        fixture.Apply(WorldBatchKind.AdvancingStep);
        WorldSnapshot head = fixture.WithWrenHoldingReceivedItems(ActorCognition.MaximumEvidence);
        Assert.Equal(ActorCognition.MaximumEvidence, head.Cognition!.For(fixture.Wren).Evidence.Count);
        WorldActionCommand gift = fixture.Command(fixture.Player,
            fixture.GiftOf(fixture.Player, fixture.Wren, fixture.Owned(fixture.Player, fixture.Axe), 1));
        var batch = new WorldStepBatch(fixture.NextId(), head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps,
            WorldBatchKind.AdvancingStep, [], [gift], head.ProfileVersion);
        Assert.Throws<CognitionCapacityException>(() => new WorldStepProcessor().Decide(head, batch));
    }

    /// <summary>
    /// Wren's whole first episode through ordinary commands. A non-null policy version stands for a changed planner;
    /// replay must reproduce whichever decisions were recorded.
    /// </summary>
    private static CognitionWorldFixture RunEpisode(string? policyVersion, Action<CognitionWorldFixture>? afterFirstGather = null)
    {
        var fixture = new CognitionWorldFixture();
        WorldActionReceipt Decide()
        {
            fixture.AdvanceUntilDue(fixture.Wren);
            WorldActionCommand command = fixture.Decision(fixture.Wren, policyVersion)!;
            return fixture.Step(command).Receipts.Single(receipt => receipt.CommandId == command.CommandId);
        }

        // Wren sees the oak from home, but without a tool waiting is the honest outcome.
        Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.InspectionOf(fixture.Oak)).Status);
        Assert.Equal(CommandReceiptStatus.Applied, Decide().Status);
        Assert.Equal(CognitionDeferral.MissingTool, fixture.CognitionOf(fixture.Wren).Memory[^1].Reason);

        // The player gives the axe outright. Wren has not yet earned the effort for a swing, so she keeps waiting, now
        // for effort rather than a tool: the new reason becomes her current deferral and one remembered entry.
        Assert.Equal(CommandReceiptStatus.Applied, fixture.GiveAxeToWren().Status);
        Assert.Equal(CommandReceiptStatus.Applied, Decide().Status);
        ActorCognition waiting = fixture.CognitionOf(fixture.Wren);
        Assert.Equal(IntentionKind.AwaitSupply, waiting.Intention!.Kind);
        Assert.Equal((MemoryOutcome.Reconsidered, CognitionDeferral.InsufficientEffort),
            (waiting.Memory[^1].Outcome, waiting.Memory[^1].Reason));
        Assert.Equal(CognitionDeferral.InsufficientEffort, waiting.Deferral!.Reason);
        // At the fallback, with effort earned, she chooses to gather a small personal stock.
        fixture.RecoverEffort(fixture.Wren);
        Assert.Equal(CommandReceiptStatus.Applied, Decide().Status);
        Assert.Equal(IntentionKind.GatherPersonalStock, fixture.CognitionOf(fixture.Wren).Intention!.Kind);

        fixture.Walk(fixture.Wren, fixture.StandingNear(fixture.Wren, fixture.Oak)!.Value);
        void RecoverEffort() => fixture.RecoverEffort(fixture.Wren);
        for (ulong gathered = 0; gathered < CognitionWorldFixture.PersonalStockTarget; gathered++)
        {
            RecoverEffort();
            Assert.Equal(CommandReceiptStatus.Applied, fixture.Act(fixture.Wren, fixture.GatherOf(fixture.Wren, fixture.Oak)).Status);
            if (gathered == 0) afterFirstGather?.Invoke(fixture);
            // Waiting for effort keeps the plan; deciding while spent would honestly defer it instead.
            RecoverEffort();
            Assert.Equal(CommandReceiptStatus.Applied, Decide().Status);
        }
        Assert.Equal(IntentionKind.AwaitSupply, fixture.CognitionOf(fixture.Wren).Intention!.Kind);
        Assert.Equal(CognitionDeferral.StockTargetMet, fixture.CognitionOf(fixture.Wren).Memory[^1].Reason);

        // The real delivery reaches Wren in person, inside a reading hold that suppresses reconsideration.
        Assert.Equal(CommandReceiptStatus.Applied, fixture.DeliverToWren().Status);
        Assert.Equal(WakeSuppression.ReadingHold, fixture.CognitionOf(fixture.Wren).Wake!.Suppression);
        Assert.Equal(CommandReceiptStatus.Applied, fixture.CloseDialogue().Status);
        Assert.Equal(CommandReceiptStatus.Applied, Decide().Status);
        return fixture;
    }

    /// <summary>Rewrites the root's leading version number, leaving every other byte as encoded.</summary>
    private static byte[] Relabel(byte[] root, byte from, byte to)
    {
        byte[] copy = root.ToArray();
        byte[] version = [0, 0, 0, 0, 0, 0, 0, from];
        int index = copy.AsSpan().IndexOf(version);
        Assert.InRange(index, 0, 64);
        copy[index + 7] = to;
        return copy;
    }
}
