using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.Retreat;

/// <summary>
/// Authenticates the native retreat routes named by OPENFANTASY_NATIVE_RETREAT_RESULTS against their saved journals.
/// Every commit after the exported prefix is replayed through the authority's processor and must reproduce its stored
/// root. Between entering the encounter and the single retreat request the player sent nothing (the cancelled
/// inspection sent no command); every retreat sample is one 32-subunit step resolved at the player's own boundary with
/// tactical ticks between samples; the episode disengaged on the walked route with the wolf's HP unchanged. The keyboard
/// route then bought one ration from Marla at the quoted total and carries both lots from her shelf; the other routes
/// walked back to the encounter's origin. An absent manifest is a failure, never a pass. Saves are read through copies.
/// </summary>
public sealed class NativeRetreatResultTests
{
    [Fact]
    public void Every_native_route_saved_one_disengaged_retreat_walked_as_ordinary_samples()
    {
        string manifestPath = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_RETREAT_RESULTS") is { Length: > 0 } supplied
            ? supplied : throw new InvalidOperationException("OPENFANTASY_NATIVE_RETREAT_RESULTS must name the native manifest.");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement[] results = manifest.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(new[] { "keyboard", "controller", "pointer" }, results.Select(value => value.GetProperty("mode").GetString()));
        foreach (JsonElement result in results)
        {
            Authenticate(result);
        }
        Assert.Equal(results[0].GetProperty("save").GetString(), manifest.RootElement.GetProperty("reopen").GetProperty("save").GetString());
    }

    private static void Authenticate(JsonElement result)
    {
        string mode = result.GetProperty("mode").GetString()!;
        string scratch = Path.Combine(Path.GetTempPath(), "openfantasy-native-retreat-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string original = result.GetProperty("save").GetString()!;
            Assert.False(File.Exists(original + "-wal") || File.Exists(original + "-shm"), "The native save must be closed.");
            string save = Path.Combine(scratch, "save.sqlite");
            File.Copy(original, save);
            byte[] genesisBytes = File.ReadAllBytes(result.GetProperty("genesis").GetString()!);
            WorldSnapshot before = WorldSnapshotCodec.Decode(File.ReadAllBytes(result.GetProperty("before").GetString()!));
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(result.GetProperty("metadata").GetString()!));
            using JsonDocument report = JsonDocument.Parse(File.ReadAllText(result.GetProperty("report").GetString()!));
            EntityId player = before.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            EntityId wolf = before.Residents.Single(value => value.ActorId.ToString() == metadata.RootElement.GetProperty("WolfActorId").GetString()).ActorId;
            string lotText = metadata.RootElement.GetProperty("CarriedLotId").GetString()!;

            WorldSnapshot final;
            using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(save, genesisBytes))
            {
                final = authority.ReadStateForHost();
            }
            // The exact exported prefix is retained before the native journey.
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
            EncounterRetreatOutcome record = final.Retreat!.Last!;
            Assert.Equal((1UL, EncounterRetreatStatus.Disengaged, player), (final.Retreat.AcceptedCount, record.Status, record.ActorId));
            Assert.Equal(report.RootElement.GetProperty("Record").GetProperty("CommandId").GetString(), record.CommandId.ToString());
            Assert.Equal(record.RouteSamples, record.SamplesTaken);
            Assert.Equal(1UL, final.EncounterLifecycle!.EncounterCount);
            Assert.Equal(EncounterLifecyclePhase.Exploration, final.EncounterLifecycle.Phase);
            Assert.Equal(before.Progression!.GetActor(wolf).CurrentHp, final.Progression!.GetActor(wolf).CurrentHp);
            Assert.True(final.Progression.GetActor(player).CurrentHp <= before.Progression.GetActor(player).CurrentHp);

            // Replay every commit after the prefix; each must reproduce its stored root.
            List<StoredWorldCommit> journal;
            using (WorldDatabase database = WorldDatabase.Open(save, create: false)) journal = database.ReadJournal(before.Revision).ToList();
            var processor = new WorldStepProcessor();
            WorldSnapshot state = before;
            ulong? entered = null, queued = null;
            List<ulong> samples = [];
            List<WorldBatchKind> between = [];
            foreach (StoredWorldCommit commit in journal)
            {
                WorldStepBatch batch = WorldStepCodec.Decode(commit.AcceptedBatch);
                WorldSnapshot next = processor.Decide(state, batch, _ => false).Candidate;
                Assert.Equal(commit.AfterHash, next.ContentHash);
                WorldActionCommand[] own = batch.Actions.Where(command => command.ActorId == player).ToArray();
                if (own.Any(command => command.Intent is EnterEncounterIntent)) entered = commit.Sequence;
                else if (own.Any(command => command.CommandId == record.CommandId)) queued = commit.Sequence;
                if (entered is not null && (queued is null || samples.Count < record.RouteSamples))
                {
                    // Nothing else from the player between entering and the retreat, nor while it is walked.
                    Assert.All(own, command => Assert.True(command.Intent is EnterEncounterIntent || command.CommandId == record.CommandId,
                        $"{mode}: the player sent another command at {commit.Sequence}."));
                }
                SubunitPosition from = state.Space.GetActor(player).Feet;
                SubunitPosition to = next.Space.GetActor(player).Feet;
                if (queued is not null && samples.Count < record.RouteSamples)
                {
                    if (from != to)
                    {
                        // One ordinary sample at the player's own boundary, by its standing or queued retreat.
                        Assert.Equal(WorldBatchKind.EncounterResolution, batch.Kind);
                        Assert.Equal(EncounterRetreatRules.SampleSubunits, Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y));
                        Assert.Equal(player, WorldEncounterActions.DueActors(state)[0].ActorId);
                        Assert.Equal(record.CommandId, state.Retreat!.Active!.Command.CommandId);
                        if (samples.Count != 0) Assert.Contains(WorldBatchKind.TacticalAdvance, between);
                        samples.Add(commit.Sequence);
                        between.Clear();
                    }
                    else between.Add(batch.Kind);
                }
                state = next;
            }
            Assert.Equal(final.ContentHash, state.ContentHash);
            Assert.NotNull(entered);
            Assert.True(queued > entered, $"{mode}: the retreat request must follow the entry.");
            Assert.Equal(record.RouteSamples, samples.Count);
            Assert.Equal(record.WorldSequence, samples[^1]);

            CountedSourceLotView[] carried = final.Inventory.Custody.Lots.Where(lot => lot.CustodianId == final.GetResident(player).InventoryCustodianId)
                .Select(lot => new CountedSourceLotView(lot.LotId.ToString(), lot.Definition, lot.Source.SourceId, lot.Quantity)).ToArray();
            var origin = before.Inventory.Custody.Lots.Single(lot => lot.LotId.ToString() == lotText);
            JsonElement trade = report.RootElement.GetProperty("Trade");
            if (mode == "keyboard")
            {
                // The second purchase after retreating: exact quoted total paid, both lots from Marla's shelf carried.
                ulong total = trade.GetProperty("Total").GetUInt64();
                Assert.Equal(before.Progression.Currency.GetActorAccount(player).Balance - total, final.Progression.Currency.GetActorAccount(player).Balance);
                var rations = carried.Where(lot => lot.Definition == origin.Definition).ToArray();
                Assert.Equal(2UL, rations.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
                Assert.All(rations, lot => Assert.Equal(origin.Source.SourceId, lot.Source));
                Assert.Contains(rations, lot => lot.LotId == lotText);
            }
            else
            {
                Assert.Equal(JsonValueKind.Null, trade.ValueKind);
                Assert.Equal(before.Progression.Currency.GetActorAccount(player).Balance, final.Progression.Currency.GetActorAccount(player).Balance);
                Assert.Single(carried, lot => lot.LotId == lotText && lot.Quantity == 1);
                // The held-key return ended at the encounter's origin, which is the retreat's recorded origin.
                Assert.Equal(record.Origin, final.Space.GetActor(player).Feet);
            }
        }
        finally { Directory.Delete(scratch, recursive: true); }
    }

    private sealed record CountedSourceLotView(string LotId, OpenFantasy.Core.Custody.CountedItemDefinition Definition, EntityId Source, ulong Quantity);
}
