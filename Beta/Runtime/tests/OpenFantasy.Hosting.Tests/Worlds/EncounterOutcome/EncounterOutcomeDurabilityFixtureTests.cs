using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EncounterOutcome;

/// <summary>
/// Durability fixtures around the profile-35 reading and completion publications. An ordinary session journey over the
/// declared two-human fixture enters the first wolf, resolves it by actual strikes, records the player's reading through
/// the session, the other human's reading through its own controller and the world's completion at a ready cutoff. Its
/// own journal is replayed batch by batch through a fresh normal authority; just before each selected publication the
/// replay exports a closed save and the exact step, and after it the unbroken result. Exports only when requested.
/// </summary>
public sealed class EncounterOutcomeDurabilityFixtureTests
{
    internal const string SeedText = "outcome-durability";

    [Fact]
    public async Task Reading_and_completion_publications_replay_exactly_from_the_ordinary_journal()
    {
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateTwoHumanWolves(SeedText, "Traveler", 1);
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-outcome-durability-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string session = Path.Combine(directory, "session.sqlite");
            WorldActionCommand playerRead;
            EntityId player, other;
            await using (var world = new TwoHumanOutcomeWorld(session, genesis))
            {
                player = world.Player;
                other = world.FirstWolf;
                LocalEncounterEntryView entry = world.Session.ReadView().EncounterLifecycle!.Entries
                    .Single(value => value.ScenarioId == world.Scenario.DefinitionId);
                Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(world.Player, new EnterEncounterIntent(
                    world.Scenario.DefinitionId, entry.EncounterId, world.State.EncounterLifecycle!.EncounterCount, entry.TargetRevision)))).Rejection);
                for (int cutoff = 0; world.State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; cutoff++)
                {
                    Assert.True(cutoff < 2_000, "The fight exceeded its cutoff bound.");
                    WorldSnapshot state = world.State;
                    if (state.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active && state.Pacing?.Encounter is { } timing
                        && EncounterOutcomeJourneyTests.AutomaticChoicesSettled(state))
                    {
                        IReadOnlyList<EncounterActorTiming> due = WorldEncounterActions.DueActors(state);
                        EncounterActorTiming? human = due.FirstOrDefault(actor => actor.ActorId != world.Player
                            && actor.Control == EncounterActorControl.RequiredHuman && timing.Decisions.All(value => value.ActorId != actor.ActorId));
                        if (human is not null)
                        {
                            Assert.Equal(WorldActionRejection.None, world.CommitOwn(human.ActorId,
                                new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision), WorldBatchKind.EncounterDecision).Rejection);
                            continue;
                        }
                        if (due.Any(actor => actor.ActorId == world.Player) && timing.Decisions.All(value => value.ActorId != world.Player))
                        {
                            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Command(world.Player,
                                EncounterOutcomeJourneyTests.Strike(state, world.FirstWolf)))).Rejection);
                            continue;
                        }
                    }
                    await world.Tick();
                }
                EncounterOutcomeDetermination determination = WorldEncounterOutcomes.Determine(world.State)!;
                var intent = new AcknowledgeEncounterOutcomeIntent(determination.EncounterId, determination.ActionHash);
                playerRead = world.Command(world.Player, intent);
                Assert.Equal(WorldActionRejection.None, (await world.Submit(playerRead)).Rejection);
                Assert.Equal(WorldActionRejection.None, world.CommitOwn(world.FirstWolf, intent).Rejection);
                await world.Tick();
                Assert.Equal(EncounterLifecyclePhase.Exploration, world.State.EncounterLifecycle!.Phase);
                world.Session.SetReady(false);
            }

            string replay = Path.Combine(directory, "replay.sqlite");
            List<StoredWorldCommit> journal;
            using (WorldDatabase source = WorldDatabase.Open(session, create: false))
            {
                journal = source.ReadJournal().ToList();
            }
            string? root = Environment.GetEnvironmentVariable("OPENFANTASY_ENCOUNTER_OUTCOME_DURABILITY_FIXTURES") is { Length: > 0 } requested
                ? Path.GetFullPath(requested) : null;
            if (root is not null)
            {
                Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(),
                    "Durability fixture export requires a fresh or empty directory; existing evidence is never overwritten.");
                Directory.CreateDirectory(root);
            }
            List<object> cases = [];
            HashSet<string> selected = [];
            using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(replay, genesis))
            {
                foreach (StoredWorldCommit commit in journal)
                {
                    WorldStepBatch batch = WorldStepCodec.Decode(commit.AcceptedBatch);
                    WorldSnapshot before = authority.ReadStateForHost();
                    string? label = Select(before, batch, player, other);
                    if (label is not null && root is not null)
                    {
                        authority.BackupForHost(Path.Combine(root, label + ".before.sqlite"));
                        File.WriteAllBytes(Path.Combine(root, label + ".step.bin"), commit.AcceptedBatch);
                    }
                    WorldStepDecision decision = authority.Commit(batch);
                    Assert.Equal(commit.AfterHash, decision.Candidate.ContentHash);
                    if (label is null) continue;
                    Assert.True(selected.Add(label), "Each publication is selected once.");
                    WorldSnapshot after = authority.ReadStateForHost();
                    AssertPublication(label, before, after, player, other);
                    if (root is not null) authority.BackupForHost(Path.Combine(root, label + ".after.sqlite"));
                    cases.Add(new
                    {
                        Label = label, Sequence = after.Revision, Kind = batch.Kind.ToString(),
                        Before = label + ".before.sqlite", Step = label + ".step.bin", After = label + ".after.sqlite",
                        BeforeHash = before.ContentHash.Value, AfterHash = after.ContentHash.Value,
                        AfterPhase = after.EncounterLifecycle!.Phase.ToString(), AfterReaders = after.EncounterOutcome!.Acknowledgments.Count,
                        AfterFinalized = after.EncounterOutcome.FinalizedCount
                    });
                }
            }
            Assert.Equal(new[] { "read-player", "read-human", "complete" }, cases.Select(value => (string)value.GetType().GetProperty("Label")!.GetValue(value)!));
            using (WorldDatabase unbroken = WorldDatabase.Open(session, create: false))
            using (WorldDatabase replayed = WorldDatabase.Open(replay, create: false))
            {
                WorldBackupComparison.RequireExactCopy(unbroken, replayed);
            }
            if (root is null) return;
            File.WriteAllBytes(Path.Combine(root, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
            File.WriteAllBytes(Path.Combine(root, "command.bin"), WorldActionCodec.Encode(playerRead));
            File.Copy(session, Path.Combine(root, "unbroken.sqlite"));
            File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
            {
                Kind = "encounter-outcome-durability-fixtures-v1", SeedText, ProfileVersion = genesis.ProfileVersion,
                Genesis = "genesis.ofc", Command = "command.bin", CommandId = playerRead.CommandId.ToString(),
                PlayerActorId = player.ToString(), OtherHumanActorId = other.ToString(), Unbroken = "unbroken.sqlite",
                UnbrokenHead = journal[^1].Sequence, Cases = cases,
                Calibration = "Declared two-human managed fixture: both inherited wolves bound to human controllers, the first at 1 HP.",
                Setup = "Ordinary session journey (enter the first wolf, actual strikes to its incapacity, the player's reading through "
                    + "the session, the other human's reading through its own controller, world completion at a ready cutoff); its journal "
                    + "replayed through a fresh normal authority with closed saves exported before each selected publication."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The player's reading, the other human's reading and the completion boundary; each selected once.</summary>
    private static string? Select(WorldSnapshot before, WorldStepBatch batch, EntityId player, EntityId other)
    {
        if (batch.Kind != WorldBatchKind.EncounterLifecycle || before.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Outcome) return null;
        if (batch.Actions.Count == 0) return "complete";
        WorldActionCommand command = batch.Actions.Single();
        if (command.Intent is not AcknowledgeEncounterOutcomeIntent) return null;
        return command.ActorId == player ? "read-player" : command.ActorId == other ? "read-human" : null;
    }

    private static void AssertPublication(string label, WorldSnapshot before, WorldSnapshot after, EntityId player, EntityId other)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
        switch (label)
        {
            case "read-player":
                Assert.True(after.EncounterOutcome!.HasAcknowledged(player));
                Assert.Equal(EncounterLifecyclePhase.Outcome, after.EncounterLifecycle!.Phase);
                break;
            case "read-human":
                Assert.True(after.EncounterOutcome!.HasAcknowledged(other));
                Assert.True(WorldEncounterOutcomes.IsReadyToFinalize(after));
                break;
            default:
                Assert.Equal(EncounterLifecyclePhase.Exploration, after.EncounterLifecycle!.Phase);
                Assert.Null(after.Pacing!.Encounter);
                Assert.Equal(new[] { player, other }.Order(), after.EncounterOutcome!.Last!.AcknowledgedActorIds);
                break;
        }
    }
}
