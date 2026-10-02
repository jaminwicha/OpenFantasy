using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Simulation.RoadThreat;
using OpenFantasy.Hosting.Tests.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;
using OpenFantasy.Hosting.Tests.Simulation.RoadThreat;
using static OpenFantasy.Hosting.Tests.Worlds.EncounterOutcome.EncounterOutcomeJourneyTests;

namespace OpenFantasy.Hosting.Tests.Worlds.EncounterOutcome;

/// <summary>
/// Regressions for the independent critic's findings (F1–F6) and the remaining managed outcome proofs, all over real
/// committed sequences: complete-root decode refusals of impossible owner contents (structural admission, distinct from
/// replay authentication), the bystander and bounded-condition projections, a two-human care contrast with Wren's real
/// agreement, the deliberate decline of available care at an ordinary unqueued cutoff, and two completed episodes in one
/// world. Declared calibrations are fresh fixture parameters pinned in their admission identities, never lived history.
/// </summary>
public sealed class EncounterOutcomeCorrectionTests
{
    private static readonly ContentHash OtherHash = ContentHash.Compute(new byte[] { 8, 8, 8 });

    [Fact]
    public async Task CompleteRootDecodeRefusesForeignPinsAndImpossibleOwnerContents()
    {
        string path = NewSave("root");
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateTwoHumanWolves("outcome-root", RoadThreatJourney.PlayerName, 1);
        try
        {
            await using var world = new TwoHumanOutcomeWorld(path, genesis);
            WorldSnapshot atOutcome = await FightFirstWolf(world, wolfStrikes: false);
            EncounterOutcomeDetermination current = WorldEncounterOutcomes.Determine(atOutcome)!;
            EntityId wren = atOutcome.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
            ulong sequence = atOutcome.Revision;

            // The harness itself: re-framing the actual owner reproduces the exact root, which decodes.
            AssertDecodes(atOutcome, atOutcome.EncounterOutcome!);
            // A well-formed reading by the required player is structurally admissible even though no journal committed it:
            // structural admission authenticates no history; replay of the journal does.
            AssertDecodes(atOutcome, Owner(0, [Reading(world.Player, current.EncounterId, current.ActionHash, sequence)], null));

            // F1 at the full root: the 32 pin bytes of the outcome owner replaced by another law's pin.
            AssertRefused(Repinned(atOutcome), atOutcome);
            AssertRefused(Repinned(genesis), genesis);
            // Wrong participant or control kind: the side-zero human bystander and a host-controlled non-participant.
            AssertRefused(atOutcome, Owner(0, [Reading(world.SecondWolf, current.EncounterId, current.ActionHash, sequence)], null));
            AssertRefused(atOutcome, Owner(0, [Reading(wren, current.EncounterId, current.ActionHash, sequence)], null));
            // Control kind isolated from participation: this human-controlled first wolf is a positive-side participant and a
            // required reader, so its reading decodes. The decline proof refuses the same role under host control.
            Assert.True(atOutcome.EncounterActions!.GetCombatant(world.FirstWolf).Side > 0);
            Assert.Equal(ResidentControllerKind.Player, atOutcome.GetResident(world.FirstWolf).ControllerKind);
            Assert.Contains(world.FirstWolf, current.RequiredHumans);
            AssertDecodes(atOutcome, Owner(0, [Reading(world.FirstWolf, current.EncounterId, current.ActionHash, sequence)], null));
            // A stale final-archive hash, another episode, or a reading from beyond committed time.
            AssertRefused(atOutcome, Owner(0, [Reading(world.Player, current.EncounterId, OtherHash, sequence)], null));
            AssertRefused(atOutcome, Owner(0, [Reading(world.Player, world.Player, current.ActionHash, sequence)], null));
            AssertRefused(atOutcome, Owner(0, [Reading(world.Player, current.EncounterId, current.ActionHash, sequence + 1)], null));
            // A completion record naming the current, unfinalized episode, and a count beyond the episodes ever begun.
            AssertRefused(atOutcome, Owner(1, [], new EncounterOutcomeRecord(current.EncounterId, current.ActionHash,
                EncounterOutcomeResult.Victory, current.StandingSide, sequence, [world.Player])));
            AssertRefused(atOutcome, Owner(2, [], new EncounterOutcomeRecord(world.FirstWolf, current.ActionHash,
                EncounterOutcomeResult.Victory, current.StandingSide, sequence, [world.Player])));

            // Complete the episode through both required humans, then refuse impossible contents of the completed root.
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(world.Player,
                new AcknowledgeEncounterOutcomeIntent(current.EncounterId, current.ActionHash)))).Rejection);
            Assert.Equal(WorldActionRejection.None, world.CommitOwn(world.FirstWolf,
                new AcknowledgeEncounterOutcomeIntent(current.EncounterId, current.ActionHash)).Rejection);
            await world.Tick();
            WorldSnapshot completed = world.State;
            EncounterOutcomeRecord last = completed.EncounterOutcome!.Last!;
            Assert.Equal(EncounterLifecyclePhase.Exploration, completed.EncounterLifecycle!.Phase);
            Assert.Equal(last.EncounterId, completed.EncounterLifecycle.Actions!.Scope.EncounterId);
            AssertDecodes(completed, completed.EncounterOutcome);
            AssertRefused(Repinned(completed), completed);
            // A reading outside Outcome (exploration), even one naming another, unfinalized episode.
            AssertRefused(completed, Owner(1, [Reading(world.Player, world.Player, current.ActionHash, completed.Revision)], last));
            // The retained archive still belongs to the record's episode, so the record must name its exact final hash.
            AssertRefused(completed, Owner(1, [], new EncounterOutcomeRecord(last.EncounterId, OtherHash, last.Result,
                last.StandingSide, last.WorldSequence, last.AcknowledgedActorIds)));
            // More completions than episodes ever begun, a record from beyond committed time, a non-resident reader.
            AssertRefused(completed, Owner(2, [], last));
            AssertRefused(completed, Owner(1, [], new EncounterOutcomeRecord(last.EncounterId, last.ActionHash, last.Result,
                last.StandingSide, completed.Revision + 1, last.AcknowledgedActorIds)));
            AssertRefused(completed, Owner(1, [], new EncounterOutcomeRecord(last.EncounterId, last.ActionHash, last.Result,
                last.StandingSide, last.WorldSequence, [current.EncounterId])));

            // F2: the player's own completed view keeps the record's facts; the observed adjacent wolf's condition is present
            // because it is actually observed now (same floor, clear sightline, three cells), not because it is history.
            LocalEncounterOutcomeView own = LocalEncounterOutcomeView.Project(completed, world.Player)!;
            Assert.Equal(LocalOutcomeStage.Completed, own.Stage);
            foreach (LocalOutcomeParticipantView participant in own.Participants)
            {
                Assert.Equal(participant.Side == last.StandingSide, participant.OnStandingSide);
                Assert.Equal(participant.IsObserver || LocalRoadThreatView.Observes(completed.Space, world.Player, participant.ActorId),
                    participant.Condition is not null);
            }
            LocalEncounterOutcomeView bystander = LocalEncounterOutcomeView.Project(completed, world.SecondWolf)!;
            Assert.False(bystander.ObserverIsParticipant);
            Assert.False(bystander.IsTerminal);
            Assert.Equal("This encounter between others is complete.", OpenFantasy.Client.EncounterOutcomeText.ReadBlocker(bystander));
        }
        finally { Remove(path); }
    }

    [Fact]
    public async Task TwoHumanCareContrastResumesTheSameEpisodeAndClearsTheOtherReading()
    {
        string path = NewSave("care-humans");
        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded("outcome-care-humans", RoadThreatJourney.PlayerName);
        EntityId player = threatened.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId wolf = threatened.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).TargetActorId;
        // Declared calibration: the existing patient (the player, Wren's agreement) starts at 1 HP and the inherited first
        // wolf is bound to its existing human controller. Nothing else is granted.
        WorldSnapshot genesis = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [player] = 1 }, [wolf]));
        try
        {
            await using var world = new TwoHumanOutcomeWorld(path, genesis);
            Assert.Equal(player, world.Player);
            Assert.Equal(wolf, world.FirstWolf);
            WorldSnapshot defeated = await FightFirstWolf(world, wolfStrikes: true);
            Assert.Equal(0, defeated.Progression!.GetActor(player).CurrentHp);
            Assert.True(defeated.Progression.GetActor(wolf).CurrentHp > 0);
            EncounterOutcomeDetermination first = WorldEncounterOutcomes.Determine(defeated)!;
            Assert.Equal(defeated.EncounterActions!.GetCombatant(wolf).Side, first.StandingSide);
            Assert.Equal(new[] { player, wolf }.Order(), first.RequiredHumans);

            // The winning human reads first, through its own control binding.
            WorldActionCommand wolfRead = world.Command(wolf, new AcknowledgeEncounterOutcomeIntent(first.EncounterId, first.ActionHash));
            WorldActionReceipt wolfReceipt = world.CommitCommand(wolfRead);
            Assert.Equal(WorldActionRejection.None, wolfReceipt.Rejection);
            Assert.True(world.State.EncounterOutcome!.HasAcknowledged(wolf));
            await world.Tick();
            LocalWorldView view = world.Session.ReadView();
            Assert.Equal(LocalOutcomeStage.AwaitingYourReading, view.EncounterOutcome!.Stage);

            // The incapacitated patient has not read; it asks for Wren's real, admitted care instead.
            LocalCareAcknowledgmentView offer = Assert.Single(view.Care!.Offers, value => value.CanAcknowledge);
            Assert.Contains(offer.HelperName + " can help now", OpenFantasy.Client.EncounterOutcomeText.ReadDetail(view.EncounterOutcome, view.Care),
                StringComparison.Ordinal);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(player, offer.Intent))).Rejection);
            WorldSnapshot pending = world.State;
            Assert.NotNull(pending.Care!.Pending);
            Assert.Equal(EncounterLifecyclePhase.Outcome, pending.EncounterLifecycle!.Phase);
            Assert.Equal(new[] { wolf }, pending.EncounterOutcome!.Acknowledgments.Select(value => value.ActorId));
            Assert.False(WorldEncounterOutcomes.IsReadyToFinalize(pending));
            // The care hold is the specific refusal: the processor refuses every non-care boundary while the acknowledged
            // obligation is pending, and the authority refuses that completion as a dispatch failure instead of committing it.
            WorldStepBatch completion = world.Batch(null);
            InvalidOperationException held = Assert.Throws<InvalidOperationException>(() =>
                new WorldStepProcessor().Decide(pending, completion, _ => false));
            Assert.Equal("An acknowledged care obligation must resolve before another world activity.", held.Message);
            Assert.Equal("The selected batch cannot be committed in the current world.",
                Assert.Throws<AuthorityDispatchException>(() => world.Authority.Preview(completion)).Message);
            Assert.Equal(pending.ContentHash, world.State.ContentHash);
            Assert.Equal(LocalOutcomeStage.CarePending, world.Session.ReadView().EncounterOutcome!.Stage);

            // One committed treatment: the helper's real dose resumes the same episode and clears the other human's reading.
            await world.Tick();
            WorldSnapshot resumed = world.State;
            Assert.Equal(pending.Revision + 1, resumed.Revision);
            Assert.Null(resumed.Care!.Pending);
            Assert.Equal(EncounterLifecyclePhase.Active, resumed.EncounterLifecycle!.Phase);
            Assert.Equal(first.EncounterId, resumed.EncounterActions!.Scope.EncounterId);
            Assert.True(resumed.Progression!.GetActor(player).CurrentHp > 0);
            Assert.Empty(resumed.EncounterOutcome!.Acknowledgments);
            Assert.Equal(0UL, resumed.EncounterOutcome.FinalizedCount);

            // The exact retry of the wolf's earlier reading returns its original receipt and satisfies nothing new.
            WorldActionReceipt retried = world.Authority.FindReceipt(wolfRead, TwoHumanOutcomeWorld.DeliveryOf(world.State, wolf))!;
            Assert.Equal(wolfReceipt.ResultId, retried.ResultId);
            Assert.Equal(WorldActionRejection.None, retried.Rejection);
            Assert.Equal(resumed.ContentHash, world.State.ContentHash);
            Assert.Empty(world.State.EncounterOutcome!.Acknowledgments);

            // The resumed episode is unfinalized and Active: the old outcome's reading, even as a fresh command by the same
            // human, is unavailable, nothing completes it, and the world holds no reading for anyone.
            Assert.Equal(WorldActionRejection.Unavailable, world.CommitOwn(wolf,
                new AcknowledgeEncounterOutcomeIntent(first.EncounterId, first.ActionHash)).Rejection);
            Assert.Empty(world.State.EncounterOutcome!.Acknowledgments);
            Assert.Null(WorldEncounterOutcomes.Determine(world.State));
            Assert.False(WorldEncounterOutcomes.IsReadyToFinalize(world.State));
            // An actionless lifecycle boundary in the resumed Active episode is a staging boundary, which Active refuses.
            WorldStepBatch resumedBoundary = world.Batch(null);
            Assert.Equal("Only confrontation and staging admit an in-place staging boundary.", Assert.Throws<InvalidOperationException>(() =>
                new WorldStepProcessor().Decide(world.State, resumedBoundary, _ => false)).Message);
            Assert.Equal("The selected batch cannot be committed in the current world.",
                Assert.Throws<AuthorityDispatchException>(() => world.Authority.Preview(resumedBoundary)).Message);
            Assert.Equal((EncounterLifecyclePhase.Active, first.EncounterId, 0UL),
                (world.State.EncounterLifecycle!.Phase, world.State.EncounterActions!.Scope.EncounterId, world.State.EncounterOutcome.FinalizedCount));

            // The resumed fight reaches a new outcome of the same episode; the old reading does not count toward it and
            // its stale archive hash is refused. Both humans then read and the world completes this outcome.
            WorldSnapshot again = await FightFirstWolf(world, wolfStrikes: true, enter: false);
            EncounterOutcomeDetermination second = WorldEncounterOutcomes.Determine(again)!;
            Assert.Equal(first.EncounterId, second.EncounterId);
            Assert.NotEqual(first.ActionHash, second.ActionHash);
            Assert.Equal(new[] { player, wolf }.Order(), WorldEncounterOutcomes.AwaitingReaders(again));
            Assert.Equal(WorldActionRejection.RevisionConflict, world.CommitOwn(wolf,
                new AcknowledgeEncounterOutcomeIntent(first.EncounterId, first.ActionHash)).Rejection);
            Assert.Equal(WorldActionRejection.None, world.CommitOwn(wolf,
                new AcknowledgeEncounterOutcomeIntent(second.EncounterId, second.ActionHash)).Rejection);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(player,
                new AcknowledgeEncounterOutcomeIntent(second.EncounterId, second.ActionHash)))).Rejection);
            WorldSnapshot bothRead = world.State;
            await world.Tick();
            WorldSnapshot completed = world.State;
            AssertCompletedWithoutEffects(again, completed, bothRead.Revision + 1);
            Assert.Equal(new[] { player, wolf }.Order(), completed.EncounterOutcome!.Last!.AcknowledgedActorIds);
            Assert.Equal(second.ActionHash, completed.EncounterOutcome.Last.ActionHash);
            Assert.Equal(1UL, completed.EncounterOutcome.FinalizedCount);
        }
        finally { Remove(path); }
    }

    [Fact]
    public async Task AcceptingDeclinesAvailableCareAndTheNextOrdinaryCutoffCompletesTheEpisode()
    {
        string path = NewSave("decline");
        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded("outcome-decline", RoadThreatJourney.PlayerName);
        EntityId player = threatened.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        WorldSnapshot genesis = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [player] = 1 }, []));
        WorldSnapshot closed;
        try
        {
            await using (var world = new RetreatJourneyWorld(path, genesis, reopen: false))
            {
                await world.ApproachFirstWolf();
                Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
                await FightUntilOutcome(world, Wait);
                WorldSnapshot atOutcome = world.State;
                Assert.Equal(0, atOutcome.Progression!.GetActor(player).CurrentHp);

                // Control kind isolated from participation: the standing first wolf is a positive-side participant under
                // host control, so it is no required reader and a reading by it is refused at complete-root decode, while
                // the human player's reading of this same root decodes (the two-human root admits the human first wolf's).
                EntityId wolf = world.FirstScenario.TargetActorId;
                EncounterOutcomeDetermination determination = WorldEncounterOutcomes.Determine(atOutcome)!;
                Assert.True(atOutcome.EncounterActions!.GetCombatant(wolf).Side > 0);
                Assert.Equal(atOutcome.EncounterActions.GetCombatant(wolf).Side, determination.StandingSide);
                Assert.Equal(ResidentControllerKind.Host, atOutcome.GetResident(wolf).ControllerKind);
                Assert.Equal(new[] { player }, determination.RequiredHumans);
                AssertDecodes(atOutcome, Owner(0, [Reading(player, determination.EncounterId, determination.ActionHash,
                    atOutcome.Revision)], null));
                AssertRefused(atOutcome, Owner(0, [Reading(wolf, determination.EncounterId, determination.ActionHash,
                    atOutcome.Revision)], null));

                LocalWorldView view = world.Session.ReadView();
                LocalEncounterOutcomeView outcome = view.EncounterOutcome!;
                LocalCareAcknowledgmentView offer = Assert.Single(view.Care!.Offers, value => value.CanAcknowledge);

                // F3: available care is named, and accepting is stated as declining it and ending the fight.
                Assert.Equal("Accept defeat", OpenFantasy.Client.EncounterOutcomeText.ReadLabel(outcome));
                string detail = OpenFantasy.Client.EncounterOutcomeText.ReadDetail(outcome, view.Care);
                Assert.Contains(offer.HelperName + " can help now", detail, StringComparison.Ordinal);
                Assert.Contains("Accepting declines that care", detail, StringComparison.Ordinal);
                // Completion is stated with its whole condition: every required reading and no pending care.
                Assert.Contains("completes the encounter at its next ready boundary once every required player has read it and "
                    + "no care is pending", detail, StringComparison.Ordinal);
                Assert.DoesNotContain("no rescue", detail, StringComparison.Ordinal);
                Assert.Contains(offer.HelperName + " can help (care)", OpenFantasy.Client.EncounterOutcomeText.Status(outcome, view.Care),
                    StringComparison.Ordinal);
                // The hint names only what each key does: E / A asks for care and V opens the choices; the consequence of
                // accepting (it declines that care) is on the reading's own control and inspection, asserted above.
                Assert.EndsWith("\nE / A: care · V: choices", OpenFantasy.Client.EncounterOutcomeText.Hint(outcome, view.Care), StringComparison.Ordinal);

                // The deliberate decline: the player accepts without asking for care.
                Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(outcome.Acknowledgment!,
                    RetreatJourneyWorld.NewId()))).Rejection);
                WorldSnapshot afterRead = world.State;
                Assert.Equal(EncounterLifecyclePhase.Outcome, afterRead.EncounterLifecycle!.Phase);
                Assert.True(WorldEncounterOutcomes.IsReadyToFinalize(afterRead));

                // F4: the very next ordinary, unqueued cutoff completes the episode; no player-reachable care window remains.
                await world.Tick();
                WorldSnapshot completed = world.State;
                AssertCompletedWithoutEffects(atOutcome, completed, afterRead.Revision + 1);
                LocalWorldView after = world.Session.ReadView();
                Assert.True(after.Care is null || after.Care.Offers.All(value => !value.CanAcknowledge));
                WorldActionReceipt late = await world.Submit(world.PlayerCommand(offer.Intent, RetreatJourneyWorld.NewId()));
                Assert.NotEqual(WorldActionRejection.None, late.Rejection);
                Assert.Equal(EncounterLifecyclePhase.Exploration, world.State.EncounterLifecycle!.Phase);
                Assert.Null(world.State.Care!.Pending);
                Assert.Equal(0, world.State.Progression!.GetActor(player).CurrentHp);
                LocalEncounterOutcomeView terminal = world.Session.ReadView().EncounterOutcome!;
                Assert.True(terminal.IsTerminal);
                string text = OpenFantasy.Client.EncounterOutcomeText.Terminal(terminal);
                Assert.Contains(OpenFantasy.Client.EncounterOutcomeText.CompletionPolicy, text, StringComparison.Ordinal);
                Assert.DoesNotContain("reach", text, StringComparison.Ordinal);
                closed = world.State;
            }

            await using (var world = new RetreatJourneyWorld(path, genesis, reopen: true))
            {
                // Leaving and reopening keeps the identical terminal world; nothing is reset, revived or rewritten.
                Assert.Equal(closed.ContentHash, world.State.ContentHash);
                Assert.True(world.Session.ReadView().EncounterOutcome!.IsTerminal);
            }
        }
        finally { Remove(path); }
    }

    [Fact]
    public async Task TwoCompletedEpisodesInOneWorldReplaceTheLatestRecordAndRetainIdentities()
    {
        string path = NewSave("episodes");
        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded("outcome-episodes", RoadThreatJourney.PlayerName);
        EncounterLifecycleSnapshot source = threatened.EncounterLifecycle!;
        EntityId firstWolf = source.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).TargetActorId;
        EntityId secondWolf = source.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        // Declared calibration over the existing two-wolf content: both inherited wolves start at 1 HP.
        WorldSnapshot genesis = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [firstWolf] = 1, [secondWolf] = 1 }, []));
        try
        {
            await using var world = new RetreatJourneyWorld(path, genesis, reopen: false);
            await world.ApproachFirstWolf();
            WorldActionCommand enterFirst = await Enter(world, EncounterJourneyAdmission.FirstScenarioKey);
            await FightUntilOutcome(world, state => Strike(state, firstWolf));
            WorldSnapshot outcome1 = world.State;
            WorldActionCommand read1 = world.PlayerCommand(world.Session.ReadView().EncounterOutcome!.Acknowledgment!, RetreatJourneyWorld.NewId());
            WorldActionReceipt receipt1 = await world.Submit(read1);
            Assert.Equal(WorldActionRejection.None, receipt1.Rejection);
            WorldSnapshot read1State = world.State;
            await world.Tick();
            WorldSnapshot completed1 = world.State;
            AssertCompletedWithoutEffects(outcome1, completed1, read1State.Revision + 1);
            EncounterOutcomeRecord record1 = completed1.EncounterOutcome!.Last!;
            Assert.Equal(1UL, completed1.EncounterOutcome.FinalizedCount);

            // The second inherited wolf, reached by ordinary walking once retained readiness is paid.
            await ApproachSecondWolf(world);
            WorldActionCommand enterSecond = await Enter(world, EncounterJourneyAdmission.SecondScenarioKey);
            WorldSnapshot entered = world.State;
            Assert.Equal(2UL, entered.EncounterLifecycle!.EncounterCount);
            Assert.NotEqual(record1.EncounterId, entered.EncounterActions!.Scope.EncounterId);
            // The completed view of the first episode is cleared at the new entry; its record is retained in the owner.
            Assert.Null(world.Session.ReadView().EncounterOutcome);
            Assert.Equal(record1.EncounterId, entered.EncounterOutcome!.Last!.EncounterId);
            Assert.Equal(1UL, entered.EncounterOutcome.FinalizedCount);

            // Occupied command identities: earlier causes (the first reading, the first entry) cannot carry new content.
            WorldActionIntent strikeNow = Strike(entered, secondWolf);
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(world.PlayerCommand(
                new AcknowledgeEncounterOutcomeIntent(entered.EncounterActions.Scope.EncounterId, entered.EncounterActions.ContentHash), read1.CommandId)));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(world.PlayerCommand(strikeNow, enterFirst.CommandId)));
            Assert.Equal(entered.ContentHash, world.State.ContentHash);

            await FightUntilOutcome(world, state => Strike(state, secondWolf));
            WorldSnapshot outcome2 = world.State;
            LocalEncounterOutcomeView reading2 = world.Session.ReadView().EncounterOutcome!;
            Assert.Equal(LocalOutcomeStage.AwaitingYourReading, reading2.Stage);
            Assert.Equal(outcome2.EncounterActions!.Scope.EncounterId, reading2.EncounterId);
            WorldActionCommand read2 = world.PlayerCommand(reading2.Acknowledgment!, RetreatJourneyWorld.NewId());
            Assert.Equal(WorldActionRejection.None, (await world.Submit(read2)).Rejection);
            WorldSnapshot read2State = world.State;
            await world.Tick();
            WorldSnapshot completed2 = world.State;
            AssertCompletedWithoutEffects(outcome2, completed2, read2State.Revision + 1);
            EncounterOutcomeRecord record2 = completed2.EncounterOutcome!.Last!;
            Assert.Equal(2UL, completed2.EncounterOutcome.FinalizedCount);
            Assert.Equal(2UL, completed2.EncounterLifecycle!.EncounterCount);
            Assert.NotEqual(enterFirst.CommandId, enterSecond.CommandId);
            Assert.NotEqual(record1.EncounterId, record2.EncounterId);
            Assert.Equal(outcome2.EncounterActions.ContentHash, record2.ActionHash);
            Assert.Equal(LocalOutcomeStage.Completed, world.Session.ReadView().EncounterOutcome!.Stage);
            Assert.Equal(record2.EncounterId, world.Session.ReadView().EncounterOutcome!.EncounterId);
            // Both wolves remain incapacitated where they fell; nothing was removed, revived or rewarded.
            Assert.Equal(0, completed2.Progression!.GetActor(firstWolf).CurrentHp);
            Assert.Equal(0, completed2.Progression.GetActor(secondWolf).CurrentHp);
            Assert.Equal(completed1.Space.GetActor(firstWolf).Feet, completed2.Space.GetActor(firstWolf).Feet);

            // The first episode's reading keeps its original receipt after the latest record was replaced.
            WorldActionReceipt retained = await world.Session.SubmitAction(read1);
            Assert.Equal(receipt1.ResultId, retained.ResultId);
            Assert.Equal(completed2.ContentHash, world.State.ContentHash);
            // The latest-record owner stays one bounded record and the root stays canonical.
            Assert.Equal(WorldSnapshotCodec.Encode(completed2), WorldSnapshotCodec.Encode(Decode(completed2, WorldSnapshotCodec.Encode(completed2))));
        }
        finally { Remove(path); }
    }

    /// <summary>Enters the named inherited wolf scenario through the player's own entry row.</summary>
    private static async Task<WorldActionCommand> Enter(RetreatJourneyWorld world, string scenarioKey)
    {
        EncounterScenarioDefinition scenario = world.State.EncounterLifecycle!.Scenarios.Single(value => value.Key == scenarioKey);
        LocalEncounterEntryView entry = world.Session.ReadView().EncounterLifecycle!.Entries.Single(value => value.ScenarioId == scenario.DefinitionId);
        Assert.True(entry.CanEnter);
        WorldActionCommand command = world.PlayerCommand(new EnterEncounterIntent(scenario.DefinitionId, entry.EncounterId,
            world.State.EncounterLifecycle!.EncounterCount, entry.TargetRevision), RetreatJourneyWorld.NewId());
        Assert.Equal(WorldActionRejection.None, (await world.Submit(command)).Rejection);
        return command;
    }

    /// <summary>Ordinary walking beside the second inherited wolf, as the first-wolf approach does for its own target.</summary>
    private static async Task ApproachSecondWolf(RetreatJourneyWorld world)
    {
        EncounterScenarioDefinition scenario = world.State.EncounterLifecycle!.Scenarios
            .Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey);
        for (int attempt = 0; ; attempt++)
        {
            await world.TickUntil(() => WorldActorAvailability.CanAct(world.State, world.Player), 2_000, "settle");
            if (world.Session.ReadView().EncounterLifecycle!.Entries.Single(value => value.ScenarioId == scenario.DefinitionId).CanEnter) return;
            Assert.True(attempt < 4, "The second wolf could not be approached through ordinary walking.");
            var here = world.Feet(world.Player).Cell;
            var near = world.Feet(scenario.TargetActorId).Cell;
            var from = new CellCoordinate(here.X, here.Y, here.Z - 1);
            var target = new CellCoordinate(near.X - 1, near.Y, here.Z - 1);
            await world.Walk([.. CargoCorridor.Lane([from, new CellCoordinate(target.X, from.Y, from.Z), target])], 2_000);
        }
    }

    /// <summary>
    /// The first wolf scenario fought through ordinary cutoffs: the player is the local session; the wolf is another human
    /// committing its own decisions. With <paramref name="wolfStrikes"/> the wolf strikes the player and the player waits;
    /// otherwise the player strikes the wolf and the wolf waits. Returns the root at the resolved Outcome.
    /// </summary>
    private static async Task<WorldSnapshot> FightFirstWolf(TwoHumanOutcomeWorld world, bool wolfStrikes, bool enter = true,
        bool playerStrikes = false)
    {
        if (enter)
        {
            LocalEncounterEntryView entry = world.Session.ReadView().EncounterLifecycle!.Entries
                .Single(value => value.ScenarioId == world.Scenario.DefinitionId);
            Assert.True(entry.CanEnter);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(world.Player, new EnterEncounterIntent(
                world.Scenario.DefinitionId, entry.EncounterId, world.State.EncounterLifecycle!.EncounterCount, entry.TargetRevision)))).Rejection);
        }
        for (int cutoff = 0; world.State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; cutoff++)
        {
            Assert.True(cutoff < 3_000, "The two-human fight exceeded its cutoff bound.");
            WorldSnapshot state = world.State;
            if (state.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active && state.Pacing?.Encounter is { } timing
                && AutomaticChoicesSettled(state))
            {
                IReadOnlyList<EncounterActorTiming> due = WorldEncounterActions.DueActors(state);
                if (due.Any(actor => actor.ActorId == world.FirstWolf && actor.Control == EncounterActorControl.RequiredHuman)
                    && timing.Decisions.All(value => value.ActorId != world.FirstWolf))
                {
                    WorldActionIntent choice = wolfStrikes ? Attack(state, world.FirstWolf, world.Player) : Wait(state);
                    Assert.Equal(WorldActionRejection.None, world.CommitOwn(world.FirstWolf, choice, WorldBatchKind.EncounterDecision).Rejection);
                    continue;
                }
                if (due.Any(actor => actor.ActorId == world.Player) && timing.Decisions.All(value => value.ActorId != world.Player))
                {
                    WorldActionIntent choice = wolfStrikes && !playerStrikes ? Wait(state) : Attack(state, world.Player, world.FirstWolf);
                    Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(world.Player, choice))).Rejection);
                    continue;
                }
            }
            await world.Tick();
        }
        return world.State;
    }

    /// <summary>
    /// A human's lawful attack with its own abilities, chosen as the host controller chooses for that body (the first own
    /// ability admitted against the target at this boundary), otherwise a Wait.
    /// </summary>
    private static WorldActionIntent Attack(WorldSnapshot state, EntityId actor, EntityId target)
    {
        var timing = state.Pacing!.Encounter!;
        foreach (EntityId ability in state.EncounterActions!.GetCombatant(actor).AbilityIds)
        {
            var intent = new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision, ability, target);
            if (WorldEncounterActions.EvaluateAbility(state, actor, intent) == WorldActionRejection.None) return intent;
        }
        return Wait(state);
    }

    private static EncounterOutcomeAcknowledgment Reading(EntityId actor, EntityId encounter, ContentHash hash, ulong sequence) =>
        new(actor, RetreatJourneyWorld.NewId(), encounter, hash, sequence);

    private static EncounterOutcomeState Owner(ulong finalized, IEnumerable<EncounterOutcomeAcknowledgment> readings,
        EncounterOutcomeRecord? last) => new(EncounterOutcomeState.Pin, finalized, readings, last);

    private static void AssertDecodes(WorldSnapshot world, EncounterOutcomeState owner)
    {
        byte[] bytes = WithOwner(world, EncounterOutcomeCodecV1.Encode(owner));
        WorldSnapshot decoded = Decode(world, bytes);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(decoded));
        if (ReferenceEquals(owner, world.EncounterOutcome)) Assert.Equal(WorldSnapshotCodec.Encode(world), bytes);
    }

    private static void AssertRefused(WorldSnapshot world, EncounterOutcomeState owner) =>
        AssertRefused(WithOwner(world, EncounterOutcomeCodecV1.Encode(owner)), world);

    private static void AssertRefused(byte[] bytes, WorldSnapshot world) =>
        Assert.ThrowsAny<FormatException>(() => Decode(world, bytes));

    private static WorldSnapshot Decode(WorldSnapshot world, byte[] bytes) => world.Coordination?.Anchor is { } anchor
        ? WorldSnapshotCodec.DecodeCoordinated(bytes, anchor) : WorldSnapshotCodec.Decode(bytes);

    /// <summary>The actual root with its outcome owner's 32 pin bytes replaced by another law's pin.</summary>
    private static byte[] Repinned(WorldSnapshot world)
    {
        byte[] owner = EncounterOutcomeCodecV1.Encode(world.EncounterOutcome!);
        byte[] pin = EncounterOutcomeState.Pin.ToByteArray();
        int at = owner.AsSpan().IndexOf(pin);
        Assert.True(at > 0);
        ContentHash.Compute(new byte[] { 7 }).ToByteArray().CopyTo(owner, at);
        return WithOwner(world, owner);
    }

    /// <summary>
    /// The actual canonical root with only its final field (the outcome owner) replaced. The root frame is the magic,
    /// version and field count, then each field as a big-endian 32-bit length and its bytes; the tag is the first field.
    /// </summary>
    private static byte[] WithOwner(WorldSnapshot world, byte[] owner)
    {
        byte[] root = WorldSnapshotCodec.Encode(world);
        const int HeaderByteLength = 12;
        uint count = BinaryPrimitives.ReadUInt32BigEndian(root.AsSpan(8));
        List<byte[]> fields = [];
        int offset = HeaderByteLength;
        for (uint index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(root.AsSpan(offset)));
            offset += sizeof(uint);
            fields.Add(root[offset..(offset + length)]);
            offset += length;
        }
        Assert.Equal(root.Length, offset);
        Assert.Equal(EncounterOutcomeCodecV1.Encode(world.EncounterOutcome!), fields[^1]);
        fields[^1] = owner;
        List<byte> rebuilt = [.. root[..HeaderByteLength]];
        foreach (byte[] field in fields)
        {
            byte[] length = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)field.Length));
            rebuilt.AddRange(length);
            rebuilt.AddRange(field);
        }
        return [.. rebuilt];
    }
}
