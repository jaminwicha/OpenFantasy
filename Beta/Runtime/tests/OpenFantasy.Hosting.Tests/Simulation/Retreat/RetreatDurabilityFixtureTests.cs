using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Retreat;

/// <summary>
/// Durability fixtures around selected retreat publications. An ordinary session journey enters the real wolf,
/// commits the advised retreat and walks it to disengagement. Its own journal is then replayed batch by batch through
/// a fresh normal authority; just before three selected publications (the queued retreat, a standing mid-route sample
/// and the disengaging sample) the replay exports a closed save and the exact host-selected step, and after each it
/// exports the unbroken result. The replay must reproduce the session's journal exactly. Exports only when requested.
/// </summary>
public sealed class RetreatDurabilityFixtureTests
{
    internal const string SeedText = "retreat-durability";

    [Fact]
    public async Task Selected_retreat_publications_replay_exactly_from_the_ordinary_journal()
    {
        WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded(SeedText, "Traveler");
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-durability-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string session = Path.Combine(directory, "session.sqlite");
            WorldActionCommand retreat;
            await using (var world = new RetreatJourneyWorld(session, genesis, reopen: false))
            {
                Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
                LocalRetreatOffer offer = await world.PlayerRetreatOffer();
                Assert.True(offer.CanQueue && offer.Samples >= 3, $"The proving route needs several samples: {offer.BlockedReason}.");
                retreat = world.RetreatCommand(offer, RetreatJourneyWorld.NewId());
                Assert.Equal(WorldActionRejection.None, (await world.Submit(retreat)).Rejection);
                await world.TickUntil(() => world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration, 2_000, "retreat");
                Assert.Equal(EncounterRetreatStatus.Disengaged, world.State.Retreat!.Last!.Status);
                world.Session.SetReady(false);
                await world.StopAsync();
            }

            // Replay the session's own journal through a fresh normal authority and select the publications.
            string replay = Path.Combine(directory, "replay.sqlite");
            List<StoredWorldCommit> journal;
            using (WorldDatabase source = WorldDatabase.Open(session, create: false))
            {
                journal = source.ReadJournal().ToList();
            }
            string? root = Environment.GetEnvironmentVariable("OPENFANTASY_RETREAT_DURABILITY_FIXTURES") is { Length: > 0 } requested
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
                    string? label = Select(before, batch, retreat, selected);
                    string? beforePath = null;
                    if (label is not null && root is not null)
                    {
                        beforePath = Path.Combine(root, label + ".before.sqlite");
                        authority.BackupForHost(beforePath);
                        File.WriteAllBytes(Path.Combine(root, label + ".step.bin"), commit.AcceptedBatch);
                    }
                    WorldStepDecision decision = authority.Commit(batch);
                    Assert.Equal(commit.AfterHash, decision.Candidate.ContentHash);
                    if (label is null) continue;
                    selected.Add(label);
                    WorldSnapshot after = authority.ReadStateForHost();
                    AssertPublication(label, before, after, retreat);
                    if (root is not null)
                    {
                        authority.BackupForHost(Path.Combine(root, label + ".after.sqlite"));
                    }
                    cases.Add(new
                    {
                        Label = label, Sequence = after.Revision, Kind = batch.Kind.ToString(),
                        Before = label + ".before.sqlite", Step = label + ".step.bin", After = label + ".after.sqlite",
                        BeforeHash = before.ContentHash.Value, AfterHash = after.ContentHash.Value,
                        BeforeSamples = before.Retreat!.Active?.SamplesTaken, AfterSamples = after.Retreat!.Active?.SamplesTaken,
                        AfterStatus = after.Retreat.Last?.Status.ToString()
                    });
                }
            }
            Assert.Equal(new[] { "queue", "sample", "disengage" }, selected.OrderBy(Order));
            // The replay reproduced the unbroken session journal row for row.
            using (WorldDatabase unbroken = WorldDatabase.Open(session, create: false))
            using (WorldDatabase replayed = WorldDatabase.Open(replay, create: false))
            {
                WorldBackupComparison.RequireExactCopy(unbroken, replayed);
            }
            if (root is null) return;
            File.WriteAllBytes(Path.Combine(root, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
            File.WriteAllBytes(Path.Combine(root, "command.bin"), WorldActionCodec.Encode(retreat));
            File.Copy(session, Path.Combine(root, "unbroken.sqlite"));
            File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
            {
                Kind = "retreat-durability-fixtures-v1", SeedText, ProfileVersion = genesis.ProfileVersion,
                Genesis = "genesis.ofc", Command = "command.bin", CommandId = retreat.CommandId.ToString(),
                PlayerActorId = retreat.ActorId.ToString(), Unbroken = "unbroken.sqlite", UnbrokenHead = journal[^1].Sequence,
                Cases = cases,
                Setup = "Ordinary session journey (enter the real wolf, commit the advised retreat, walk to disengagement); "
                    + "its journal replayed through a fresh normal authority with closed saves exported before each selected publication."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static int Order(string label) => label switch { "queue" => 0, "sample" => 1, _ => 2 };

    /// <summary>The queued retreat, the second (standing) sample and the disengaging sample; each selected once.</summary>
    private static string? Select(WorldSnapshot before, WorldStepBatch batch, WorldActionCommand retreat, HashSet<string> selected)
    {
        if (batch.Actions.Any(command => command.CommandId == retreat.CommandId)) return selected.Contains("queue") ? null : "queue";
        if (batch.Kind != WorldBatchKind.EncounterResolution || before.Retreat?.Active is not { } active
            || WorldEncounterActions.DueActors(before) is not { Count: > 0 } due || due[0].ActorId != active.ActorId) return null;
        if (active.SamplesTaken == active.Intent.Route.Count - 1) return "disengage";
        return active.SamplesTaken == 1 && !selected.Contains("sample") ? "sample" : null;
    }

    private static void AssertPublication(string label, WorldSnapshot before, WorldSnapshot after, WorldActionCommand retreat)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        switch (label)
        {
            case "queue":
                Assert.Null(before.Retreat!.Active);
                Assert.Equal(retreat.CommandId, after.Retreat!.Active!.Command.CommandId);
                Assert.False(after.Retreat.Active.HasStarted);
                break;
            case "sample":
                Assert.Equal(before.Retreat!.Active!.SamplesTaken + 1, after.Retreat!.Active!.SamplesTaken);
                Assert.Equal(EncounterRetreatRules.SampleSubunits, Math.Abs(after.Space.GetActor(retreat.ActorId).Feet.X - before.Space.GetActor(retreat.ActorId).Feet.X)
                    + Math.Abs(after.Space.GetActor(retreat.ActorId).Feet.Y - before.Space.GetActor(retreat.ActorId).Feet.Y));
                break;
            default:
                Assert.Equal(EncounterRetreatStatus.Disengaged, after.Retreat!.Last!.Status);
                Assert.Equal(EncounterLifecyclePhase.Exploration, after.EncounterLifecycle!.Phase);
                Assert.Null(after.Pacing!.Encounter);
                break;
        }
    }
}
