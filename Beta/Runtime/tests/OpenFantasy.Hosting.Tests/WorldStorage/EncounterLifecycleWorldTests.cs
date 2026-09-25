using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class EncounterLifecycleWorldTests
{
    [Fact]
    public void Source_duel_releases_into_real_recovery_and_reentry_preserves_positive_debt_and_fractional_time()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        var genesis = fixture.Genesis;
        var evidence = new EncounterLifecycleEvidence(genesis);
        var selected = new SortedDictionary<ulong, WorldSnapshot> { [0] = genesis };
        var commands = new List<(WorldActionCommand Command, WorldActionReceipt Receipt)>();
        string outcomeCopy = files.Path + ".outcome.sqlite";
        string workCopy = files.Path + ".work.sqlite";
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        WorldSnapshot released;
        WorldSnapshot final;
        WorldStepDecision Commit(WorldStepBatch batch)
        {
            var before = authority.ReadStateForHost();
            var result = authority.Commit(batch);
            evidence.Record(result.Candidate, batch, result.Receipts);
            AssertClock(result.Candidate);
            Assert.Equal(before.Revision + 1, result.Candidate.Revision);
            Assert.Equal(genesis.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
            Assert.Equal(genesis.Construction!.ContentHash, result.Candidate.Construction!.ContentHash);
            Assert.Equal(genesis.Production!.ContentHash, result.Candidate.Production!.ContentHash);
            Assert.Equal(genesis.Excavation!.ContentHash, result.Candidate.Excavation!.ContentHash);
            Assert.Equal(genesis.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision, actor.Condition.ComputeContentHash())),
                result.Candidate.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision, actor.Condition.ComputeContentHash())));
            foreach (var receipt in result.Receipts)
                commands.Add((batch.Actions.Single(command => command.CommandId == receipt.CommandId), receipt));
            return result;
        }
        WorldSnapshot Boundary(string label, WorldActionCommand command)
        {
            var before = authority.ReadStateForHost();
            var batch = fixture.Batch(before, WorldBatchKind.EncounterLifecycle, [command]);
            authority.Dispose();
            evidence.PrepareBoundary(label, files.Path, batch);
            if (label == "release") File.Copy(files.Path, outcomeCopy);
            authority = SqliteWorldAuthority.OpenExisting(files.Path);
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            var result = Commit(batch);
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(result.Receipts).Status);
            evidence.CompleteBoundary(label, before, result.Candidate);
            selected[before.Revision] = before;
            selected[result.Candidate.Revision] = result.Candidate;
            return result.Candidate;
        }
        try
        {
            var state = Commit(fixture.Batch(genesis, WorldBatchKind.AdvancingStep)).Candidate;
            Assert.Equal(1UL, state.EncounterLifecycle!.ExplorationSteps);
            Assert.Equal(EncounterEpisodeIdentity.Derive(genesis.EncounterLifecycle!, fixture.FirstScenario),
                EncounterEpisodeIdentity.Derive(state.EncounterLifecycle!, fixture.FirstScenario));
            state = Boundary("entry", fixture.Enter(state, fixture.FirstScenario));
            Assert.Equal(EncounterLifecyclePhase.Confrontation, state.EncounterLifecycle!.Phase);
            for (int staging = 0; staging < 2; staging++)
            {
                var before = state;
                state = Commit(fixture.Batch(state, WorldBatchKind.EncounterLifecycle)).Candidate;
                Assert.Equal(before.Calendar, state.Calendar);
                Assert.Equal(before.Progression!.ContentHash, state.Progression!.ContentHash);
            }
            Assert.Equal(EncounterLifecyclePhase.Active, state.EncounterLifecycle!.Phase);
            for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
            {
                Assert.True(boundary < 1_000, "The unchanged source-backed first duel exceeded its finite boundary bound.");
                var timing = state.Pacing!.Encounter!;
                var due = WorldEncounterActions.DueActors(state);
                var choices = due.Where(actor => !timing.Decisions.Any(choice => choice.ActorId == actor.ActorId))
                    .Select(actor => fixture.Attack(state, actor.ActorId,
                        actor.ActorId == fixture.Player ? fixture.WolfFirst : fixture.Player)).ToArray();
                var kind = choices.Length != 0 ? WorldBatchKind.EncounterDecision
                    : due.Count != 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance;
                var result = Commit(fixture.Batch(state, kind, choices));
                Assert.All(result.Receipts, receipt => Assert.Equal(CommandReceiptStatus.Applied, receipt.Status));
                state = result.Candidate;
                Assert.Equal(genesis.Progression!.GetActor(fixture.Wren).CurrentHp, state.Progression!.GetActor(fixture.Wren).CurrentHp);
                Assert.Equal(genesis.Progression!.GetActor(fixture.WolfSecond).CurrentHp, state.Progression!.GetActor(fixture.WolfSecond).CurrentHp);
            }
            Assert.True(state.Progression!.GetActor(fixture.Player).CurrentHp > 0,
                $"The actual source encounter ended with the initiator incapacitated; release cannot be fabricated. Wolf HP={state.Progression!.GetActor(fixture.WolfFirst).CurrentHp}.");
            Assert.Equal(0, state.Progression!.GetActor(fixture.WolfFirst).CurrentHp);
            ulong debt = state.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay;
            Assert.True(debt > 0);
            Assert.True(state.Pacing!.Progress.Remainder > 0);
            byte[] outcomeActions = EncounterActionCodecV1.Encode(state.EncounterActions!);
            var outcome = state;
            released = Boundary("release", fixture.Release(state));
            Assert.Equal(EncounterLifecyclePhase.Exploration, released.EncounterLifecycle!.Phase);
            Assert.Null(released.Pacing!.Encounter);
            Assert.Null(released.EncounterActions);
            Assert.Equal(outcome.Calendar, released.Calendar);
            Assert.Equal(outcome.Pacing!.Progress.Capture(), released.Pacing!.Progress.Capture());
            Assert.Equal(debt, released.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay);
            Assert.Equal(outcomeActions, EncounterActionCodecV1.Encode(released.EncounterLifecycle!.Actions!));
            authority.Dispose();
            File.Copy(files.Path, workCopy); // Independent test continuation of this exact closed save, not a world fork API.
            authority = SqliteWorldAuthority.OpenExisting(files.Path);

            state = Commit(fixture.Batch(released, WorldBatchKind.EncounterLifecycle,
                [fixture.Enter(released, fixture.SecondScenario)])).Candidate;
            Assert.Equal(2UL, state.EncounterLifecycle!.EncounterCount);
            Assert.Equal(debt, state.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay);
            Assert.Equal(outcome.EncounterLifecycle!.TacticalTicks, state.EncounterLifecycle!.EntryTacticalTicks);
            Assert.Equal(released.Pacing!.Progress.Capture(), state.Pacing!.Progress.Capture());
            Assert.Equal(0UL, state.Pacing!.Encounter!.ElapsedTacticalUnits);
            for (int staging = 0; staging < 2; staging++)
                state = Commit(fixture.Batch(state, WorldBatchKind.EncounterLifecycle)).Candidate;
            var premature = fixture.Attack(state, fixture.Player, fixture.WolfSecond);
            var denied = Commit(fixture.Batch(state, WorldBatchKind.EncounterDecision, [premature]));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(denied.Receipts).Rejection);
            Assert.Equal(debt, denied.Candidate.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay);
            state = denied.Candidate;
            // Explicit trusted-host Wait is a legal costed choice. Automatic opponent policy is
            // tested separately; no claim that it would volunteer this recovery interval is made.
            var wait = fixture.Wait(state, fixture.WolfSecond);
            var queued = Commit(fixture.Batch(state, WorldBatchKind.EncounterDecision, [wait]));
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(queued.Receipts).Status);
            state = Commit(fixture.Batch(queued.Candidate, WorldBatchKind.EncounterResolution)).Candidate;
            Assert.Equal(EncounterActionResolutionStatus.Wait, state.EncounterActions!.LastResolution!.Status);
            Assert.Equal(0UL, state.EncounterActions!.Random.NextCounter);
            Assert.Equal(debt, state.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay);
            final = Commit(fixture.Batch(state, WorldBatchKind.TacticalAdvance)).Candidate;
            Assert.Equal(debt > 5 ? debt - 5 : 0, final.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay);
            Assert.Equal(released.Calendar.AddMilliseconds(50), final.Calendar);
            Assert.Equal(released.Progression!.ContentHash, final.Progression!.ContentHash);
            Assert.Equal((ulong)commands.Count, authority.InspectForHost().Journal.CommandOutcomeCount);
            foreach (var (command, receipt) in commands)
                Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                    authority.FindReceipt(command, fixture.DeliveryFor(command.ActorId))!));
        }
        finally { authority.Dispose(); }
        evidence.Complete(files.Path, fixture, final);
        byte[] saved = File.ReadAllBytes(files.Path);
        using (var restored = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(restored.ReadStateForHost()));
            foreach (var state in selected.Values.Reverse())
            {
                var history = restored.ReadRecordedStateForHost(state.Revision);
                Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(history.State));
                Assert.Equal(final.ContentHash, history.SourceHeadHash);
                Assert.Equal(final.Revision, history.SourceHeadRevision);
            }
        }
        Assert.Equal(saved, File.ReadAllBytes(files.Path));
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(2, database.ReadIdentities().Count(identity => identity.Kind == WorldIdentityKind.SpawnedEntity));
            Assert.Equal(commands.Count, database.ReadCommandOutcomes().Count());
        }
        Assert.Equal(saved, File.ReadAllBytes(files.Path));
        AssertOutcomeRefusals(outcomeCopy, fixture);
        AssertRecoveryAndFundedWork(workCopy, fixture, released);
    }

    [Fact]
    public void Entry_authenticates_roles_and_captures_presence_episode_count_and_derived_identity_not_command_randomness()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var derived = EncounterEpisodeIdentity.Derive(fixture.Genesis.EncounterLifecycle!, fixture.FirstScenario);
        var bad = new WorldActionIntent[]
        {
            new EnterEncounterIntent(fixture.FirstScenario, fixture.NextId(), 0, 0),
            new EnterEncounterIntent(fixture.FirstScenario, derived, 1, 0),
            new EnterEncounterIntent(fixture.FirstScenario, derived, 0, 1),
            new ReleaseEncounterIntent(derived, ContentHash.Compute("no outcome"u8))
        };
        var reasons = new[] { WorldActionRejection.Unavailable, WorldActionRejection.RevisionConflict,
            WorldActionRejection.RevisionConflict, WorldActionRejection.Unavailable };
        for (int index = 0; index < bad.Length; index++)
        {
            var state = authority.ReadStateForHost();
            var result = authority.Commit(fixture.Batch(state, WorldBatchKind.EncounterLifecycle,
                [fixture.Command(state, fixture.Player, bad[index])]));
            Assert.Equal(reasons[index], Assert.Single(result.Receipts).Rejection);
            Assert.Equal(0UL, result.Candidate.EncounterLifecycle!.EncounterCount);
            Assert.Equal(fixture.Genesis.Calendar, result.Candidate.Calendar);
            Assert.Equal(fixture.Genesis.Progression!.ContentHash, result.Candidate.Progression!.ContentHash);
        }
        var current = authority.ReadStateForHost();
        var wronglyAssigned = fixture.Command(current, fixture.Wren,
            new EnterEncounterIntent(fixture.FirstScenario, derived, 0, 0));
        Assert.Equal(WorldActionRejection.NotAuthorized, Assert.Single(authority.Commit(fixture.Batch(current,
            WorldBatchKind.EncounterLifecycle, [wronglyAssigned])).Receipts).Rejection);
        current = authority.ReadStateForHost();
        var one = fixture.Enter(current, fixture.FirstScenario);
        var two = fixture.Enter(current, fixture.FirstScenario);
        var previewOne = authority.Preview(fixture.Batch(current, WorldBatchKind.EncounterLifecycle, [one])).Candidate;
        var previewTwo = authority.Preview(fixture.Batch(current, WorldBatchKind.EncounterLifecycle, [two])).Candidate;
        Assert.NotEqual(one.CommandId, two.CommandId);
        Assert.Equal(derived, previewOne.EncounterActions!.Scope.EncounterId);
        Assert.Equal(EncounterActionCodecV1.Encode(previewOne.EncounterActions), EncounterActionCodecV1.Encode(previewTwo.EncounterActions!));
        Assert.Equal(previewOne.EncounterActions!.CreateRandom().NextUInt64(201), previewTwo.EncounterActions!.CreateRandom().NextUInt64(201));
        var forged = new WorldActionCommand(one.CommandId, fixture.DeliveryFor(fixture.Wren).PrincipalId,
            fixture.DeliveryFor(fixture.Wren).SessionId, one.ActorId, one.WorldId, one.BranchId, one.ExpectedActorRevision, one.Intent);
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(current, WorldBatchKind.EncounterLifecycle, [forged])));
        Assert.Equal(current.ContentHash, authority.ReadStateForHost().ContentHash);
        var entered = authority.Commit(fixture.Batch(current, WorldBatchKind.EncounterLifecycle, [one]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(entered.Receipts).Status);
        Assert.Equal(WorldReceiptCodecV1.Encode(entered.Receipts[0]), WorldReceiptCodecV1.Encode(authority.FindReceipt(one, fixture.Delivery)!));
    }

    private static void AssertOutcomeRefusals(string path, EncounterJourneyWorldFixture fixture)
    {
        using var authority = SqliteWorldAuthority.OpenExisting(path);
        var original = authority.ReadStateForHost();
        var actions = original.EncounterLifecycle!.Actions!;
        var cases = new[]
        {
            fixture.Command(original, fixture.Player, new ReleaseEncounterIntent(actions.Scope.EncounterId, ContentHash.Compute("stale outcome"u8))),
            fixture.Command(original, fixture.Wren, new ReleaseEncounterIntent(actions.Scope.EncounterId, actions.ContentHash)),
            fixture.Command(original, fixture.WolfFirst, new ReleaseEncounterIntent(actions.Scope.EncounterId, actions.ContentHash))
        };
        var expected = new[] { WorldActionRejection.RevisionConflict, WorldActionRejection.NotAuthorized, WorldActionRejection.Unavailable };
        for (int index = 0; index < cases.Length; index++)
        {
            var state = authority.ReadStateForHost();
            var result = authority.Commit(fixture.Batch(state, WorldBatchKind.EncounterLifecycle, [cases[index]]));
            Assert.Equal(expected[index], Assert.Single(result.Receipts).Rejection);
            Assert.Equal(EncounterLifecyclePhase.Outcome, result.Candidate.EncounterLifecycle!.Phase);
            Assert.Equal(original.Calendar, result.Candidate.Calendar);
            Assert.Equal(original.EncounterLifecycle!.Debts, result.Candidate.EncounterLifecycle!.Debts);
            Assert.Equal(original.Progression!.ContentHash, result.Candidate.Progression!.ContentHash);
        }
    }

    private static void AssertRecoveryAndFundedWork(string path, EncounterJourneyWorldFixture fixture, WorldSnapshot released)
    {
        using var authority = SqliteWorldAuthority.OpenExisting(path);
        var actions = new ConstructionUpgradeFixture();
        var state = authority.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(released), WorldSnapshotCodec.Encode(state));
        var deadSpeak = fixture.Command(state, fixture.WolfFirst, new SpeakToActorIntent(fixture.Player, state.Space.GetActor(fixture.Player).Revision));
        var batch = new WorldStepBatch(fixture.NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            WorldBatchKind.AdvancingStep,
            [new(fixture.Delivery, fixture.Player, 0, -32), new(fixture.DeliveryFor(fixture.WolfFirst), fixture.WolfFirst, 0, -32)],
            [deadSpeak], state.ProfileVersion);
        var recovering = authority.Commit(batch);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(recovering.Receipts).Rejection);
        state = recovering.Candidate;
        Assert.Equal(released.Space.GetActor(fixture.Player).Feet, state.Space.GetActor(fixture.Player).Feet);
        Assert.Equal(released.Space.GetActor(fixture.WolfFirst).Feet, state.Space.GetActor(fixture.WolfFirst).Feet);
        Assert.Equal(0UL, state.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay);
        Assert.Equal(released.Calendar.AddMilliseconds(1_800), state.Calendar);
        Assert.Equal(released.Pacing!.Progress.Remainder, state.Pacing!.Progress.Remainder);
        Assert.Equal(released.Inventory.ContentHash, state.Inventory.ContentHash);
        Assert.Equal(released.Construction!.ContentHash, state.Construction!.ContentHash);
        var feet = state.Space.GetActor(fixture.Player).Feet;
        var counterpart = fixture.Command(state, fixture.Player, new SpeakToActorIntent(fixture.WolfFirst, state.Space.GetActor(fixture.WolfFirst).Revision));
        var moved = authority.Commit(new WorldStepBatch(fixture.NextId(), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.AdvancingStep, [new(fixture.Delivery, fixture.Player, 0, -32)], [counterpart], state.ProfileVersion));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(moved.Receipts).Rejection);
        state = moved.Candidate;
        Assert.Equal(feet.Offset(0, -32, 0), state.Space.GetActor(fixture.Player).Feet);
        for (int step = 0; state.Space.QueryInteraction(fixture.Player, fixture.Wren) != SpatialInteraction.Reachable; step++)
        {
            Assert.True(step < 128, "The actual living giver must remain reachable around retained wolf bodies.");
            var route = state.Space.QueryGroundRoute(fixture.Player, state.Space.GetActor(fixture.Wren).Feet.Offset(0, -256, 0));
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            feet = state.Space.GetActor(fixture.Player).Feet;
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            state = authority.Commit(actions.Batch(state, [], [new(fixture.Delivery, fixture.Player, dx, dy)])).Candidate;
        }
        state = actions.Act(authority, state, fixture.Player, new SpeakToActorIntent(fixture.Wren, state.Space.GetActor(fixture.Wren).Revision));
        var quest = Assert.Single(state.Quests!.Definitions);
        state = actions.Act(authority, state, fixture.Player, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version,
            fixture.Wren, state.Space.GetActor(fixture.Wren).Revision, 0, state.Progression!.GetActor(fixture.Player).Revision));
        var bag = state.GetResident(fixture.Player).InventoryCustodianId;
        var wood = state.Inventory.Custody.Lots.Single(lot => lot.CustodianId == bag && lot.Definition == quest.Item);
        state = actions.Act(authority, state, fixture.Player, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version,
            fixture.Wren, state.Space.GetActor(fixture.Wren).Revision, 1, state.Progression!.GetActor(fixture.Player).Revision,
            state.Inventory.Custody.GetStorage(bag).Revision, [new(wood.LotId, wood.Revision, quest.Quantity)]));
        Assert.Equal(ActorQuestStatus.Completed, Assert.Single(state.Quests!.Participations).Status);
        Assert.Equal(6UL, state.Inventory.Custody.GetStoredQuantity(state.Construction!.Projects.Single().ProjectCustodianId));
        state = actions.Act(authority, state, fixture.Player, new CloseDialogueIntent(state.Dialogues.Single().DialogueId));
        var npcs = new LocalNpcController();
        for (int step = 0; !state.Construction!.Projects.Single().IsCompleted; step++)
        {
            Assert.True(step < 256, "Real funded Wren work must resume within the inherited construction bound.");
            var work = npcs.SelectActions(state, actions.NextIdentity);
            var movement = npcs.SelectMovement(state);
            Assert.DoesNotContain(work, command => command.ActorId == fixture.WolfFirst);
            Assert.DoesNotContain(movement, input => input.ActorId == fixture.WolfFirst);
            var committed = authority.Commit(actions.Batch(state, work, movement));
            Assert.All(committed.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            state = committed.Candidate;
            AssertClock(state);
        }
        Assert.Equal(200UL, state.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Single(state.Production!.Batches);
        Assert.Equal(0, state.Progression!.GetActor(fixture.WolfFirst).CurrentHp);
        Assert.Equal(released.Space.GetActor(fixture.WolfFirst).Feet, state.Space.GetActor(fixture.WolfFirst).Feet);
        Assert.Equal(released.Pacing!.Progress.Remainder, state.Pacing!.Progress.Remainder);
        byte[] canonical = WorldSnapshotCodec.Encode(state);
        authority.Dispose();
        byte[] saved = File.ReadAllBytes(path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(path))
            Assert.Equal(canonical, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(saved, File.ReadAllBytes(path));
    }

    internal static void AssertClock(WorldSnapshot state)
    {
        var lifecycle = state.EncounterLifecycle!;
        Assert.Equal(state.Revision, lifecycle.Revision);
        Assert.Equal(lifecycle.ExplorationSteps + lifecycle.TacticalTicks, state.AdvancingSteps);
        UInt128 milliseconds = (UInt128)lifecycle.ExplorationSteps * 1_800 + (UInt128)lifecycle.TacticalTicks * 50;
        Assert.Equal(milliseconds, state.Pacing!.Progress.Elapsed.TotalMilliseconds);
        Assert.Equal((ulong)(milliseconds / 1_800), state.ProcessIntervals);
        Assert.Equal((ulong)(milliseconds % 1_800), state.Pacing!.Progress.Remainder);
        Assert.All(state.Residents, resident => Assert.Equal(state.Pacing!.Progress.Elapsed, resident.ExperiencedTime));
        if (state.Pacing!.Encounter is { } timing)
        {
            Assert.Equal((lifecycle.TacticalTicks - lifecycle.EntryTacticalTicks) * 5, timing.ElapsedTacticalUnits);
            Assert.All(timing.Actors, actor => Assert.Equal(lifecycle.GetDebt(actor.ActorId).RemainingDelay, actor.RemainingDelay));
        }
    }
}
