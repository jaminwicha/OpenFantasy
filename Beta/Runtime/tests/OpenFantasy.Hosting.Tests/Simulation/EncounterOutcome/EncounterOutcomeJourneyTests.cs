using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Tests.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;
using OpenFantasy.Hosting.Tests.Simulation.RoadThreat;

// These multi-minute world journeys belong to the permitted Worlds/EncounterOutcome qualification family: they run in
// the outcome-unit selection and stay outside the unchanged regression selection's fixed wall-clock bound. (The file
// still sits beside its session helper; moving it to tests/.../Worlds/EncounterOutcome/ is a pure relocation.)
namespace OpenFantasy.Hosting.Tests.Worlds.EncounterOutcome;

/// <summary>
/// Real committed profile-35 sequences through the durable authority and the local session: entry by the actual
/// initiator, resolution by actual bodies, the required readings, world-owned completion and lawful continuation, with
/// reopening at the meaningful boundaries. Declared calibrations (a wounded chief, a wounded player, human-bound wolves)
/// are fresh fixture parameters pinned in their admission identities, never lived history or balance.
/// </summary>
public sealed class EncounterOutcomeJourneyTests
{
    private static readonly ContentHash OtherHash = ContentHash.Compute(new byte[] { 9, 9, 9 });

    [Fact]
    public async Task WoundedChiefVictoryIsReadThenCompletedByTheWorldAndPlayContinues()
    {
        string path = NewSave("victory");
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateWoundedChief("outcome-victory", RoadThreatJourney.PlayerName, 1);
        EntityId chief = Threat(genesis).ChiefActorId;
        WorldSnapshot atOutcome;
        CountedSourceLot lot;
        IReadOnlyList<CellCoordinate> outbound;
        try
        {
            await using (var world = new RetreatJourneyWorld(path, genesis, reopen: false))
            {
                (_, _, lot) = await RoadThreatJourney.EarnAndBuy(world, 1);
                outbound = RoadThreatJourney.Outbound(world);
                Assert.True(await RoadThreatJourney.WalkUntilEngaged(world, outbound, 8_000));
                Assert.Equal(chief, world.State.EncounterLifecycle!.GetScenario(world.State.EncounterLifecycle.ScenarioId!.Value).InitiatingActorId);
                await FightUntilOutcome(world, state => Strike(state, chief));
                atOutcome = world.State;
                Assert.Equal(0, atOutcome.Progression!.GetActor(chief).CurrentHp);
                Assert.True(atOutcome.Progression.GetActor(world.Player).CurrentHp > 0);
                LocalWorldView view = world.Session.ReadView();
                LocalEncounterOutcomeView outcome = view.EncounterOutcome!;
                Assert.Equal(LocalOutcomeStage.AwaitingYourReading, outcome.Stage);
                Assert.Equal(EncounterOutcomeResult.Victory, outcome.Result);
                Assert.Equal(LocalOutcomeStanding.YourSideStands, outcome.Standing);
                Assert.Equal(1, outcome.RequiredReaders);
                Assert.NotNull(outcome.Acknowledgment);
                Assert.Null(view.EncounterLifecycle!.ReleaseActionHash);
                Assert.Equal("Continue", OpenFantasy.Client.EncounterOutcomeText.ReadLabel(outcome));
                Assert.Null(OpenFantasy.Client.EncounterOutcomeText.ReadBlocker(outcome));
                Assert.StartsWith("Victory", OpenFantasy.Client.EncounterOutcomeText.Status(outcome, view.Care), StringComparison.Ordinal);
                Assert.Equal(LocalWorldRunState.Waiting, world.Session.ReadRunState());
                // Full Wait: reading holds the world; no cutoff advances time, bodies or choices.
                for (int cutoff = 0; cutoff < 12; cutoff++) await world.Tick();
                Assert.Equal(atOutcome.ContentHash, world.State.ContentHash);
                // The initiator-only release is not part of this interpretation, for the winner or anyone else.
                WorldActionCommand release = world.PlayerCommand(new ReleaseEncounterIntent(outcome.EncounterId,
                    atOutcome.EncounterActions!.ContentHash), RetreatJourneyWorld.NewId());
                await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(release));
            }

            await using (var world = new RetreatJourneyWorld(path, genesis, reopen: true))
            {
                Assert.Equal(atOutcome.ContentHash, world.State.ContentHash);
                LocalEncounterOutcomeView outcome = world.Session.ReadView().EncounterOutcome!;
                WorldActionCommand read = world.PlayerCommand(outcome.Acknowledgment!, RetreatJourneyWorld.NewId());
                WorldActionReceipt receipt = await world.Submit(read);
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                WorldSnapshot afterRead = world.State;
                Assert.Equal(EncounterLifecyclePhase.Outcome, afterRead.EncounterLifecycle!.Phase);
                Assert.True(afterRead.EncounterOutcome!.HasAcknowledged(world.Player));
                Assert.Equal(atOutcome.Calendar, afterRead.Calendar);

                await world.Tick();
                WorldSnapshot completed = world.State;
                AssertCompletedWithoutEffects(atOutcome, completed, afterRead.Revision + 1);
                EncounterOutcomeRecord record = completed.EncounterOutcome!.Last!;
                Assert.Equal(EncounterOutcomeResult.Victory, record.Result);
                Assert.Equal(new[] { world.Player }, record.AcknowledgedActorIds);
                Assert.Equal(LocalOutcomeStage.Completed, world.Session.ReadView().EncounterOutcome!.Stage);
                Assert.False(world.Session.ReadView().EncounterOutcome!.IsTerminal);

                // An exact retry returns the original receipt; changed content under the same identity is refused.
                WorldActionReceipt retry = await world.Session.SubmitAction(read);
                Assert.Equal(receipt.ResultId, retry.ResultId);
                Assert.Equal(completed.ContentHash, world.State.ContentHash);
                WorldActionCommand changed = world.PlayerCommand(new AcknowledgeEncounterOutcomeIntent(outcome.EncounterId, OtherHash), read.CommandId);
                await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(changed));
                // A completed episode is never completed again or read again.
                Assert.ThrowsAny<Exception>(() => world.Authority.Preview(LifecycleBoundary(world.State)));
                WorldActionReceipt reread = await world.Submit(world.PlayerCommand(outcome.Acknowledgment!, RetreatJourneyWorld.NewId()));
                Assert.Equal(WorldActionRejection.Unavailable, reread.Rejection);
                Assert.Equal(1UL, world.State.EncounterOutcome!.FinalizedCount);

                // Lawful continuation: retained readiness debt is paid by exploration, then ordinary movement along the
                // lane and the sale of the retained cargo at the far counter.
                CellCoordinate standing = RoadThreatJourney.Standing(world.State, world.Player);
                int nearest = Enumerable.Range(0, outbound.Count)
                    .MinBy(index => Math.Abs(outbound[index].X - standing.X) + Math.Abs(outbound[index].Y - standing.Y));
                int samples = await world.Walk([.. outbound.Skip(nearest)], 8_000);
                Assert.True(samples > 0);
                Assert.Equal(0UL, world.State.EncounterLifecycle!.GetDebt(world.Player).RemainingDelay);
                var (sold, _) = await RoadThreatJourney.Sell(world, lot.LotId, 1);
                Assert.Equal(WorldActionRejection.None, sold.Rejection);
                Assert.Equal(0, world.State.Progression!.GetActor(chief).CurrentHp);
                Assert.Equal(atOutcome.Space.GetActor(chief).Feet, world.State.Space.GetActor(chief).Feet);
                // F2: at Kegga the completed record keeps its facts, but the far chief's present condition is not projected;
                // only the observer's own condition is.
                LocalEncounterOutcomeView atKegga = world.Session.ReadView().EncounterOutcome!;
                Assert.Equal(LocalOutcomeStage.Completed, atKegga.Stage);
                Assert.Equal(record.EncounterId, atKegga.EncounterId);
                Assert.Equal(record.StandingSide, atKegga.StandingSide);
                Assert.False(OpenFantasy.Hosting.Simulation.RoadThreat.LocalRoadThreatView.Observes(world.State.Space, world.Player, chief));
                LocalOutcomeParticipantView farChief = Assert.Single(atKegga.Participants, value => value.ActorId == chief);
                Assert.Null(farChief.Condition);
                Assert.False(farChief.OnStandingSide);
                LocalOutcomeParticipantView self = Assert.Single(atKegga.Participants, value => value.IsObserver);
                Assert.True(self.OnStandingSide);
                Assert.Equal(world.State.Progression!.GetActor(world.Player).CurrentHp, self.Condition!.CurrentHp);
                Assert.DoesNotContain("HP 0/", OpenFantasy.Client.EncounterOutcomeText.Participant(farChief, atKegga.Result), StringComparison.Ordinal);
            }
        }
        finally { Remove(path); }
    }

    [Fact]
    public async Task ChiefDefeatIsReadableThenTerminalAcrossReopening()
    {
        string path = NewSave("defeat");
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateEmbedded("outcome-defeat", RoadThreatJourney.PlayerName);
        EntityId chief = Threat(genesis).ChiefActorId;
        WorldSnapshot atOutcome;
        WorldSnapshot closed;
        CountedSourceLot lot;
        try
        {
            await using (var world = new RetreatJourneyWorld(path, genesis, reopen: false))
            {
                (_, _, lot) = await RoadThreatJourney.EarnAndBuy(world, 1);
                Assert.True(await RoadThreatJourney.WalkUntilEngaged(world, RoadThreatJourney.Outbound(world), 8_000));
                await FightUntilOutcome(world, Wait);
                atOutcome = world.State;
                Assert.Equal(0, atOutcome.Progression!.GetActor(world.Player).CurrentHp);
                Assert.True(atOutcome.Progression.GetActor(chief).CurrentHp > 0);
                LocalWorldView view = world.Session.ReadView();
                LocalEncounterOutcomeView outcome = view.EncounterOutcome!;
                Assert.Equal(LocalOutcomeStage.AwaitingYourReading, outcome.Stage);
                Assert.Equal(LocalOutcomeStanding.OpponentStands, outcome.Standing);
                Assert.Equal(LocalOutcomeRestriction.Incapacitated, outcome.Restriction);
                Assert.True(view.Care is null || view.Care.Offers.All(offer => !offer.CanAcknowledge));
                // The defeat explains why ordinary action is unavailable and that no admitted care can reach the player;
                // its one deliberate choice is enabled, never an unexplained disabled Continue.
                Assert.Equal("Accept defeat", OpenFantasy.Client.EncounterOutcomeText.ReadLabel(outcome));
                Assert.Null(OpenFantasy.Client.EncounterOutcomeText.ReadBlocker(outcome));
                string status = OpenFantasy.Client.EncounterOutcomeText.Status(outcome, view.Care);
                Assert.Contains("cannot move or act", status, StringComparison.Ordinal);
                // F3: the stated care reason is the care view's own (nothing offered, or the helper's actual refusal). A
                // missing view establishes only that no care is offered, never that no agreement exists.
                string expected = view.Care is null ? "No care is offered to you"
                    : view.Care.Offers[0].Rejection == WorldActionRejection.OutOfReach ? $"{view.Care.Offers[0].HelperName} is out of reach"
                    : view.Care.Offers[0].Rejection == WorldActionRejection.RevisionConflict ? $"{view.Care.Offers[0].HelperName}'s care offer is out of date"
                    : view.Care.Offers[0].Rejection == WorldActionRejection.NotAuthorized ? $"{view.Care.Offers[0].HelperName}'s care does not cover you"
                    : $"{view.Care.Offers[0].HelperName} cannot treat you now";
                Assert.Contains(expected, status, StringComparison.Ordinal);
                Assert.DoesNotContain("agreed", status, StringComparison.Ordinal);
                // The card carries the same view-derived reason beside the full explanation of accepting.
                string card = OpenFantasy.Client.EncounterOutcomeText.Card(outcome, view.Care);
                Assert.Contains("Care: " + expected + ".", card, StringComparison.Ordinal);
                Assert.Contains("once every required player has read it", card, StringComparison.Ordinal);
                Assert.DoesNotContain("can help", OpenFantasy.Client.EncounterOutcomeText.ReadDetail(outcome, view.Care), StringComparison.Ordinal);
                Assert.DoesNotContain("no rescue", OpenFantasy.Client.EncounterOutcomeText.ReadDetail(outcome, view.Care), StringComparison.Ordinal);
                for (int cutoff = 0; cutoff < 12; cutoff++) await world.Tick();
                Assert.Equal(atOutcome.ContentHash, world.State.ContentHash);
            }

            await using (var world = new RetreatJourneyWorld(path, genesis, reopen: true))
            {
                Assert.Equal(atOutcome.ContentHash, world.State.ContentHash);
                // The incapacitated player's controller reads; reading is not a bodily act and grants none.
                WorldActionReceipt receipt = await world.Submit(world.PlayerCommand(
                    world.Session.ReadView().EncounterOutcome!.Acknowledgment!, RetreatJourneyWorld.NewId()));
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                WorldSnapshot afterRead = world.State;
                await world.Tick();
                WorldSnapshot completed = world.State;
                AssertCompletedWithoutEffects(atOutcome, completed, afterRead.Revision + 1);
                EncounterOutcomeRecord record = completed.EncounterOutcome!.Last!;
                Assert.Equal(EncounterOutcomeResult.Victory, record.Result);
                Assert.Equal(atOutcome.EncounterActions!.GetCombatant(chief).Side, record.StandingSide);
                LocalEncounterOutcomeView terminal = world.Session.ReadView().EncounterOutcome!;
                Assert.True(terminal.IsTerminal);
                Assert.Equal(LocalOutcomeStanding.OpponentStands, terminal.Standing);
                // F3: the terminal state names the completion policy, not a physical-reach claim.
                string terminalText = OpenFantasy.Client.EncounterOutcomeText.Terminal(terminal);
                Assert.Contains(OpenFantasy.Client.EncounterOutcomeText.CompletionPolicy, terminalText, StringComparison.Ordinal);
                Assert.DoesNotContain("reach", terminalText, StringComparison.Ordinal);
                Assert.Contains(completed.Inventory.Custody.Lots, value => value.LotId == lot.LotId);
                // No hostile re-entry, rescue or revival follows: the incapacitated player cannot take part.
                for (int cutoff = 0; cutoff < 12; cutoff++) await world.Tick();
                WorldSnapshot later = world.State;
                Assert.Equal(EncounterLifecyclePhase.Exploration, later.EncounterLifecycle!.Phase);
                Assert.Equal(completed.EncounterLifecycle!.EncounterCount, later.EncounterLifecycle.EncounterCount);
                Assert.Equal(0, later.Progression!.GetActor(world.Player).CurrentHp);
                closed = later;
            }

            await using (var world = new RetreatJourneyWorld(path, genesis, reopen: true))
            {
                // Leaving and reopening neither resets nor rewrites the save.
                Assert.Equal(closed.ContentHash, world.State.ContentHash);
                Assert.True(world.Session.ReadView().EncounterOutcome!.IsTerminal);
            }
        }
        finally { Remove(path); }
    }

    [Fact]
    public async Task TwoRequiredHumansHoldCompletionUntilBothReadAndOthersCannotReadForThem()
    {
        string path = NewSave("humans");
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateTwoHumanWolves("outcome-humans", RoadThreatJourney.PlayerName, 1);
        EntityId chief = Threat(genesis).ChiefActorId;
        try
        {
            await using var world = new TwoHumanOutcomeWorld(path, genesis);
            LocalEncounterEntryView entry = world.Session.ReadView().EncounterLifecycle!.Entries
                .Single(value => value.ScenarioId == world.Scenario.DefinitionId);
            Assert.True(entry.CanEnter);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(world.Player, new EnterEncounterIntent(
                world.Scenario.DefinitionId, entry.EncounterId, world.State.EncounterLifecycle!.EncounterCount, entry.TargetRevision)))).Rejection);
            bool checkedActive = false;
            for (int cutoff = 0; world.State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; cutoff++)
            {
                Assert.True(cutoff < 2_000, "The two-human fight exceeded its cutoff bound.");
                WorldSnapshot state = world.State;
                if (state.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active && state.Pacing?.Encounter is { } timing)
                {
                    if (!checkedActive)
                    {
                        // Premature reading or completion of an active episode is refused at its boundary.
                        checkedActive = true;
                        Assert.Equal(WorldActionRejection.Unavailable, world.CommitOwn(world.FirstWolf,
                            new AcknowledgeEncounterOutcomeIntent(timing.EncounterId, state.EncounterActions!.ContentHash)).Rejection);
                        Assert.ThrowsAny<Exception>(() => world.Authority.Preview(world.Batch(null)));
                        continue;
                    }
                    IReadOnlyList<EncounterActorTiming> due = WorldEncounterActions.DueActors(state);
                    EncounterActorTiming? other = due.FirstOrDefault(actor => actor.ActorId != world.Player
                        && actor.Control == EncounterActorControl.RequiredHuman && timing.Decisions.All(value => value.ActorId != actor.ActorId));
                    if (other is not null && AutomaticChoicesSettled(state))
                    {
                        Assert.Equal(WorldActionRejection.None, world.CommitOwn(other.ActorId,
                            new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision), WorldBatchKind.EncounterDecision).Rejection);
                        continue;
                    }
                    if (AutomaticChoicesSettled(state) && due.Any(actor => actor.ActorId == world.Player)
                        && timing.Decisions.All(value => value.ActorId != world.Player))
                    {
                        Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(world.Player,
                            Strike(state, world.FirstWolf)))).Rejection);
                        continue;
                    }
                }
                await world.Tick();
            }

            WorldSnapshot outcome = world.State;
            Assert.Equal(0, outcome.Progression!.GetActor(world.FirstWolf).CurrentHp);
            EncounterOutcomeDetermination determination = WorldEncounterOutcomes.Determine(outcome)!;
            Assert.Equal(new[] { world.Player, world.FirstWolf }.Order(), determination.RequiredHumans);
            Assert.DoesNotContain(world.SecondWolf, determination.RequiredHumans);
            Assert.Equal(outcome.EncounterActions!.GetCombatant(world.Player).Side, determination.StandingSide);
            var intent = new AcknowledgeEncounterOutcomeIntent(determination.EncounterId, determination.ActionHash);

            // An unrelated side-zero human cannot read; neither the player's nor a host controller can read for another
            // human, and the local adapter accepts only its own player's commands.
            Assert.Equal(WorldActionRejection.NotAuthorized, world.CommitOwn(world.SecondWolf, intent).Rejection);
            // F5: the side-zero human observes a fight between others: no reading of its own is offered or owed.
            LocalEncounterOutcomeView bystander = LocalEncounterOutcomeView.Project(world.State, world.SecondWolf)!;
            Assert.Equal(LocalOutcomeStanding.Observer, bystander.Standing);
            Assert.False(bystander.ObserverIsParticipant);
            Assert.Null(bystander.Acknowledgment);
            Assert.False(bystander.ObserverHasRead);
            Assert.Equal(LocalOutcomeStage.AwaitingOthers, bystander.Stage);
            string bystanderText = OpenFantasy.Client.EncounterOutcomeText.ReadBlocker(bystander)!;
            Assert.Contains("between others", bystanderText, StringComparison.Ordinal);
            Assert.DoesNotContain("You have read", bystanderText, StringComparison.Ordinal);
            Assert.Contains("between others", OpenFantasy.Client.EncounterOutcomeText.Status(bystander, null), StringComparison.Ordinal);
            Assert.DoesNotContain("while you read", OpenFantasy.Client.EncounterOutcomeText.Status(bystander, null), StringComparison.Ordinal);
            // F2: present condition is the observer's own or a currently observed participant's, never everyone's.
            foreach (LocalOutcomeParticipantView participant in bystander.Participants)
            {
                Assert.False(participant.IsObserver);
                Assert.Equal(OpenFantasy.Hosting.Simulation.RoadThreat.LocalRoadThreatView.Observes(world.State.Space, world.SecondWolf,
                    participant.ActorId), participant.Condition is not null);
            }
            Assert.ThrowsAny<AuthorityDispatchException>(() => world.Authority.Preview(world.Batch(
                world.Command(world.FirstWolf, intent, world.Delivery))));
            Assert.ThrowsAny<AuthorityDispatchException>(() => world.Authority.Preview(world.Batch(
                world.Command(world.FirstWolf, intent, TwoHumanOutcomeWorld.DeliveryOf(outcome, chief)))));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(world.Command(world.FirstWolf, intent)));
            // Stale or wrong readings by the right controller receive durable refusals and change nothing else.
            Assert.Equal(WorldActionRejection.RevisionConflict, world.CommitOwn(world.FirstWolf,
                new AcknowledgeEncounterOutcomeIntent(determination.EncounterId, OtherHash)).Rejection);
            Assert.Equal(WorldActionRejection.Unavailable, world.CommitOwn(world.FirstWolf,
                new AcknowledgeEncounterOutcomeIntent(world.Player, determination.ActionHash)).Rejection);
            Assert.Empty(world.State.EncounterOutcome!.Acknowledgments);

            // One reading of two required: the world holds whatever the cutoffs (the other human is absent).
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(world.Player, intent))).Rejection);
            LocalEncounterOutcomeView waiting = world.Session.ReadView().EncounterOutcome!;
            Assert.Equal(LocalOutcomeStage.AwaitingOthers, waiting.Stage);
            Assert.Equal(1, waiting.ReadersRemaining);
            Assert.Null(waiting.Acknowledgment);
            Assert.Equal("You have read this outcome. Waiting for one other player to read it.",
                OpenFantasy.Client.EncounterOutcomeText.ReadBlocker(waiting));
            WorldSnapshot oneRead = world.State;
            for (int cutoff = 0; cutoff < 12; cutoff++) await world.Tick();
            Assert.Equal(oneRead.ContentHash, world.State.ContentHash);
            Assert.Equal(LocalWorldRunState.Waiting, world.Session.ReadRunState());

            // The incapacitated other human reads through its own controller while the local session is not ready:
            // nothing completes without a ready cutoff, and a second reading by that actor is refused.
            world.Session.SetReady(false);
            Assert.Equal(WorldActionRejection.None, world.CommitOwn(world.FirstWolf, intent).Rejection);
            Assert.Equal(WorldActionRejection.Unavailable, world.CommitOwn(world.FirstWolf, intent).Rejection);
            WorldSnapshot bothRead = world.State;
            Assert.True(WorldEncounterOutcomes.IsReadyToFinalize(bothRead));
            for (int cutoff = 0; cutoff < 6; cutoff++) await world.Tick();
            Assert.Equal(bothRead.ContentHash, world.State.ContentHash);
            world.Session.SetReady(true);
            await world.Tick();

            WorldSnapshot completed = world.State;
            AssertCompletedWithoutEffects(outcome, completed, bothRead.Revision + 1);
            EncounterOutcomeRecord record = completed.EncounterOutcome!.Last!;
            Assert.Equal(new[] { world.Player, world.FirstWolf }.Order(), record.AcknowledgedActorIds);
            Assert.Equal(determination.StandingSide, record.StandingSide);
            Assert.Contains(completed.Residents, resident => resident.ActorId == world.FirstWolf);
        }
        finally { Remove(path); }
    }

    [Fact]
    public async Task CareResumesOnlyTheUnfinalizedEpisodeAndClearsItsReadings()
    {
        string path = NewSave("care");
        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded("outcome-care", RoadThreatJourney.PlayerName);
        EntityId player = threatened.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        WorldSnapshot genesis = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [player] = 1 }, []));
        try
        {
            await using var world = new RetreatJourneyWorld(path, genesis, reopen: false);
            await world.ApproachFirstWolf();
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            await FightUntilOutcome(world, Wait);
            WorldSnapshot defeated = world.State;
            Assert.Equal(0, defeated.Progression!.GetActor(player).CurrentHp);
            LocalWorldView view = world.Session.ReadView();
            LocalCareAcknowledgmentView offer = Assert.Single(view.Care!.Offers, value => value.CanAcknowledge);

            // The patient first reads the defeat, then asks for the admitted helper before the world completes it:
            // care cutoffs precede completion, so the obligation is never discarded.
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(
                view.EncounterOutcome!.Acknowledgment!, RetreatJourneyWorld.NewId()))).Rejection);
            Assert.True(WorldEncounterOutcomes.IsReadyToFinalize(world.State));
            // The offer captured before the reading names an earlier boundary: it is refused, never refreshed silently.
            Assert.Equal(WorldActionRejection.RevisionConflict,
                (await world.Submit(world.PlayerCommand(offer.Intent, RetreatJourneyWorld.NewId()))).Rejection);
            Assert.Equal(EncounterLifecyclePhase.Outcome, world.State.EncounterLifecycle!.Phase);
            LocalCareAcknowledgmentView fresh = Assert.Single(world.Session.ReadView().Care!.Offers, value => value.CanAcknowledge);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(fresh.Intent, RetreatJourneyWorld.NewId()))).Rejection);
            WorldSnapshot pending = world.State;
            Assert.NotNull(pending.Care!.Pending);
            Assert.Equal(EncounterLifecyclePhase.Outcome, pending.EncounterLifecycle!.Phase);
            Assert.False(WorldEncounterOutcomes.IsReadyToFinalize(pending));
            Assert.Equal(LocalOutcomeStage.CarePending, world.Session.ReadView().EncounterOutcome!.Stage);
            Assert.ThrowsAny<Exception>(() => world.Authority.Preview(LifecycleBoundary(pending)));

            // The helper's real dose resumes the same unfinalized episode and ends the readings of the old outcome.
            await world.Tick();
            WorldSnapshot resumed = world.State;
            Assert.Null(resumed.Care!.Pending);
            Assert.Equal(EncounterLifecyclePhase.Active, resumed.EncounterLifecycle!.Phase);
            Assert.Equal(defeated.EncounterActions!.Scope.EncounterId, resumed.EncounterActions!.Scope.EncounterId);
            Assert.True(resumed.Progression!.GetActor(player).CurrentHp > 0);
            Assert.Empty(resumed.EncounterOutcome!.Acknowledgments);
            Assert.Equal(0UL, resumed.EncounterOutcome.FinalizedCount);

            // The resumed fight reaches a new outcome; this time the patient reads it and the world completes it.
            await FightUntilOutcome(world, Wait);
            WorldSnapshot again = world.State;
            Assert.NotEqual(defeated.EncounterActions.ContentHash, again.EncounterActions!.ContentHash);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(
                world.Session.ReadView().EncounterOutcome!.Acknowledgment!, RetreatJourneyWorld.NewId()))).Rejection);
            await world.Tick();
            WorldSnapshot completed = world.State;
            Assert.Equal(EncounterLifecyclePhase.Exploration, completed.EncounterLifecycle!.Phase);
            Assert.Equal(1UL, completed.EncounterOutcome!.FinalizedCount);

            // After completion no care is offered and an earlier care acknowledgment can no longer resume the fight.
            LocalCareView? careAfter = world.Session.ReadView().Care;
            Assert.True(careAfter is null || careAfter.Offers.All(value => !value.CanAcknowledge));
            WorldActionReceipt late = await world.Submit(world.PlayerCommand(fresh.Intent, RetreatJourneyWorld.NewId()));
            Assert.NotEqual(WorldActionRejection.None, late.Rejection);
            Assert.Equal(EncounterLifecyclePhase.Exploration, world.State.EncounterLifecycle!.Phase);
            Assert.Null(world.State.Care!.Pending);
        }
        finally { Remove(path); }
    }

    internal static RoadThreatBinding Threat(WorldSnapshot state) =>
        CargoOutcomeWorldAdmission.Find(state, CargoCircuitSources.LoadEmbedded(), RoadThreatSources.LoadEmbedded())!;

    internal static WorldActionIntent Wait(WorldSnapshot state)
    {
        var timing = state.Pacing!.Encounter!;
        return new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision);
    }

    internal static WorldActionIntent Strike(WorldSnapshot state, EntityId target)
    {
        EncounterAbilityDefinition strike = state.EncounterLifecycle!.Abilities.Single(value => value.Key == "strike");
        var timing = state.Pacing!.Encounter!;
        return new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision, strike.DefinitionId, target);
    }

    internal static WorldStepBatch LifecycleBoundary(WorldSnapshot state) => new(RetreatJourneyWorld.NewId(), state.WorldId,
        state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.EncounterLifecycle, [], [], state.ProfileVersion);

    /// <summary>Ordinary cutoffs, choosing for the player whenever it is the awaited human, until the episode resolves.</summary>
    internal static async Task FightUntilOutcome(RetreatJourneyWorld world, Func<WorldSnapshot, WorldActionIntent> choose,
        int maximumCutoffs = 3_000)
    {
        for (int cutoff = 0; ; cutoff++)
        {
            WorldSnapshot state = world.State;
            if (state.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome) return;
            Assert.True(cutoff < maximumCutoffs, "The fight exceeded its cutoff bound.");
            if (state.EncounterLifecycle.Phase == EncounterLifecyclePhase.Active && state.Pacing?.Encounter is { } timing
                && WorldEncounterActions.HasRequiredDecisions(state) && AutomaticChoicesSettled(state)
                && WorldEncounterActions.DueActors(state).Any(actor => actor.ActorId == world.Player)
                && timing.Decisions.All(decision => decision.ActorId != world.Player))
            {
                WorldActionReceipt receipt = await world.Submit(world.PlayerCommand(choose(state), RetreatJourneyWorld.NewId()));
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                continue;
            }
            await world.Tick();
        }
    }

    /// <summary>
    /// Due automatic participants have published their choices at this instant, so a human choice captured now keeps its
    /// timing revision (the same stable interval the native wheel waits for).
    /// </summary>
    internal static bool AutomaticChoicesSettled(WorldSnapshot state) => state.Pacing?.Encounter is { } timing
        && WorldEncounterActions.DueActors(state).All(actor => actor.Control == EncounterActorControl.RequiredHuman
            || timing.Decisions.Any(decision => decision.ActorId == actor.ActorId));

    /// <summary>Completion is one zero-duration boundary that changes only the phase, timing binding and record.</summary>
    internal static void AssertCompletedWithoutEffects(WorldSnapshot atOutcome, WorldSnapshot completed, ulong revision)
    {
        Assert.Equal(revision, completed.Revision);
        Assert.Equal(EncounterLifecyclePhase.Exploration, completed.EncounterLifecycle!.Phase);
        Assert.Null(completed.Pacing!.Encounter);
        Assert.Empty(completed.EncounterOutcome!.Acknowledgments);
        Assert.Equal(atOutcome.EncounterActions!.Scope.EncounterId, completed.EncounterOutcome.Last!.EncounterId);
        Assert.Equal(atOutcome.EncounterActions.ContentHash, completed.EncounterOutcome.Last.ActionHash);
        Assert.Equal(atOutcome.EncounterActions.Scope.EncounterId, completed.EncounterLifecycle.Actions!.Scope.EncounterId);
        Assert.Equal(atOutcome.EncounterLifecycle!.ScenarioId, completed.EncounterLifecycle.ScenarioId);
        Assert.Equal(atOutcome.Calendar, completed.Calendar);
        Assert.Equal(atOutcome.AdvancingSteps, completed.AdvancingSteps);
        Assert.Equal(atOutcome.EncounterLifecycle.TacticalTicks, completed.EncounterLifecycle.TacticalTicks);
        Assert.Equal(atOutcome.EncounterLifecycle.ExplorationSteps, completed.EncounterLifecycle.ExplorationSteps);
        Assert.Equal(atOutcome.EncounterLifecycle.EncounterCount, completed.EncounterLifecycle.EncounterCount);
        Assert.Equal(atOutcome.Residents.Select(value => value.ActorId), completed.Residents.Select(value => value.ActorId));
        foreach (WorldResident resident in atOutcome.Residents)
        {
            Assert.Equal(atOutcome.Space.GetActor(resident.ActorId).Feet, completed.Space.GetActor(resident.ActorId).Feet);
            Assert.Equal(atOutcome.Progression!.GetActor(resident.ActorId).CurrentHp, completed.Progression!.GetActor(resident.ActorId).CurrentHp);
            Assert.Equal(atOutcome.Progression.GetActor(resident.ActorId).CurrentMp, completed.Progression.GetActor(resident.ActorId).CurrentMp);
            Assert.Equal(atOutcome.EncounterLifecycle.GetDebt(resident.ActorId).RemainingDelay,
                completed.EncounterLifecycle.GetDebt(resident.ActorId).RemainingDelay);
        }
        Assert.Equal(atOutcome.Inventory.Custody.Lots.Select(value => (value.LotId, value.CustodianId, value.Quantity)),
            completed.Inventory.Custody.Lots.Select(value => (value.LotId, value.CustodianId, value.Quantity)));
        Assert.Equal(atOutcome.Progression!.Currency.Accounts.Select(value => (value.AccountId, value.Balance)),
            completed.Progression!.Currency.Accounts.Select(value => (value.AccountId, value.Balance)));
    }

    internal static string NewSave(string label)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"openfantasy-outcome-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "world.sqlite");
    }

    internal static void Remove(string path)
    {
        try { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
