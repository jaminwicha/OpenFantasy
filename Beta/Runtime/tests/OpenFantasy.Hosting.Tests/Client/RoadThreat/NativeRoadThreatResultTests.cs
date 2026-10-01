using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Tests.Simulation.RoadThreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.RoadThreat;

/// <summary>
/// Authenticates the native road-threat routes named by OPENFANTASY_NATIVE_ROAD_THREAT_RESULTS against their complete
/// saved journals. Every commit after the exported prefix (genesis itself for the keyboard farm route) is replayed
/// through the authority's processor and must reproduce its stored root. The only encounter entry is the chief's own
/// command under its own control binding, at a boundary where the player stood within its reach and sightline. From
/// that entry to the last retreat sample the player's commands are exactly one Strike on the keyboard route and none on
/// the others, then the recorded retreat: the cancelled inspection sent nothing. Retreat samples are counted only for
/// <see cref="WorldBatchKind.EncounterResolution"/> commits of the active recorded retreat with the player due, with a
/// tactical advance between samples, and the last sample is the record's own sequence; later travel never counts.
/// Final feet equal the report and the authored destination. The keyboard route began at the fresh farm: Wren's reward,
/// Marla's sale of an exact source interval, the chief's retained hit and Kegga's purchase of that same interval are
/// authenticated by parties and funds. An absent manifest is a failure, never a pass. Saves are read through copies.
/// </summary>
public sealed class NativeRoadThreatResultTests
{
    [Fact]
    public void Every_native_route_met_the_chief_on_the_road_and_escaped_by_a_saved_retreat()
    {
        string manifestPath = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_ROAD_THREAT_RESULTS") is { Length: > 0 } supplied
            ? supplied : throw new InvalidOperationException("OPENFANTASY_NATIVE_ROAD_THREAT_RESULTS must name the native manifest.");
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
        string scratch = Path.Combine(Path.GetTempPath(), "openfantasy-native-road-threat-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string original = result.GetProperty("save").GetString()!;
            Assert.False(File.Exists(original + "-wal") || File.Exists(original + "-shm"), "The native save must be closed.");
            string save = Path.Combine(scratch, "save.sqlite");
            File.Copy(original, save);
            byte[] genesisBytes = File.ReadAllBytes(result.GetProperty("genesis").GetString()!);
            WorldSnapshot genesis = WorldSnapshotCodec.Decode(genesisBytes);
            WorldSnapshot before = WorldSnapshotCodec.Decode(File.ReadAllBytes(result.GetProperty("before").GetString()!));
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(result.GetProperty("metadata").GetString()!));
            using JsonDocument report = JsonDocument.Parse(File.ReadAllText(result.GetProperty("report").GetString()!));
            RoadThreatBinding threat = RoadThreatWorldAdmission.Find(genesis, CargoCircuitSources.LoadEmbedded(), RoadThreatSources.LoadEmbedded())!;
            Assert.NotNull(threat);
            CargoCircuitMarkets markets = CargoWorldAdmission.Find(genesis, CargoCircuitSources.LoadEmbedded())!;
            EntityId player = before.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            EntityId chief = threat.ChiefActorId;
            EntityId marla = markets.Origin.MerchantActorId;
            EntityId kegga = markets.Destination.MerchantActorId;
            Assert.Equal(metadata.RootElement.GetProperty("ChiefActorId").GetString(), chief.ToString());
            ActorControlBinding chiefControl = before.Inventory.Controls.Single(value => value.ActorId == chief);
            bool fromFarm = mode == "keyboard";
            // The keyboard route is the fresh farm fixture: nothing earned, bought or walked before the native journey.
            Assert.Equal(fromFarm ? "native-road-threat-farm-fixture-v1" : "native-road-threat-fixture-v1",
                metadata.RootElement.GetProperty("Kind").GetString());
            if (fromFarm)
            {
                Assert.Equal(WorldSnapshotCodec.Encode(genesis), WorldSnapshotCodec.Encode(before));
                Assert.Equal(0UL, before.Progression!.Currency.GetActorAccount(player).Balance);
            }

            WorldSnapshot final;
            using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(save, genesisBytes))
            {
                final = authority.ReadStateForHost();
            }
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
            EncounterRetreatOutcome record = final.Retreat!.Last!;
            Assert.Equal((1UL, EncounterRetreatStatus.Disengaged, player), (final.Retreat.AcceptedCount, record.Status, record.ActorId));
            Assert.Equal(report.RootElement.GetProperty("Record").GetProperty("CommandId").GetString(), record.CommandId.ToString());
            Assert.Equal(record.RouteSamples, record.SamplesTaken);
            Assert.Equal((1UL, EncounterLifecyclePhase.Exploration), (final.EncounterLifecycle!.EncounterCount, final.EncounterLifecycle.Phase));
            Assert.Equal(before.Space.GetActor(chief).Feet, final.Space.GetActor(chief).Feet);

            List<StoredWorldCommit> journal;
            using (WorldDatabase database = WorldDatabase.Open(save, create: false)) journal = database.ReadJournal(before.Revision).ToList();
            var processor = new WorldStepProcessor();
            WorldSnapshot state = before;
            ulong? entered = null, queued = null;
            List<ulong> samples = [];
            List<WorldBatchKind> between = [];
            List<WorldActionCommand> encounterCommands = [];
            List<TradeLeg> trades = [];
            ulong? reward = null;
            foreach (StoredWorldCommit commit in journal)
            {
                WorldStepBatch batch = WorldStepCodec.Decode(commit.AcceptedBatch);
                WorldSnapshot next = processor.Decide(state, batch, _ => false).Candidate;
                Assert.Equal(commit.AfterHash, next.ContentHash);
                Assert.DoesNotContain(batch.Actions, command => command.ActorId == player && command.Intent is EnterEncounterIntent);
                WorldActionCommand? entry = batch.Actions.SingleOrDefault(command => command.Intent is EnterEncounterIntent);
                if (entry is not null && next.EncounterLifecycle!.EncounterCount > state.EncounterLifecycle!.EncounterCount)
                {
                    // The chief's own authenticated command, at a boundary where the player stood within its reach.
                    Assert.Null(entered);
                    Assert.Equal(WorldBatchKind.EncounterLifecycle, batch.Kind);
                    Assert.Equal((chief, chiefControl.PrincipalId, chiefControl.SessionId, threat.ScenarioId),
                        (entry.ActorId, entry.PrincipalId, entry.OriginSessionId, ((EnterEncounterIntent)entry.Intent).ScenarioId));
                    Assert.Equal(SpatialInteraction.Reachable,
                        state.Space.QueryInteraction(chief, player, threat.Scenario.EntryRangeSubunits));
                    // Nothing of the player's moved or acted in the entry batch itself.
                    Assert.Equal(state.Space.GetActor(player).Feet, next.Space.GetActor(player).Feet);
                    Assert.DoesNotContain(batch.Actions, command => command.ActorId == player);
                    entered = commit.Sequence;
                }
                WorldActionCommand[] own = batch.Actions.Where(command => command.ActorId == player).ToArray();
                if (own.Any(command => command.CommandId == record.CommandId)) queued ??= commit.Sequence;
                if (entered is not null && samples.Count < record.RouteSamples) encounterCommands.AddRange(own);
                SubunitPosition from = state.Space.GetActor(player).Feet;
                SubunitPosition to = next.Space.GetActor(player).Feet;
                if (from != to)
                {
                    // Every player movement is one ordinary cardinal 32-subunit sample.
                    Assert.Equal(WorldMovementInput.DisplacementPerStep, Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y));
                }
                if (state.Retreat?.Active?.Command.CommandId == record.CommandId)
                {
                    if (from != to)
                    {
                        // One ordinary sample at the player's own due boundary, by the active recorded retreat only.
                        Assert.Equal(WorldBatchKind.EncounterResolution, batch.Kind);
                        Assert.Equal(EncounterRetreatRules.SampleSubunits, Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y));
                        Assert.Contains(WorldEncounterActions.DueActors(state), actor => actor.ActorId == player);
                        if (samples.Count != 0) Assert.Contains(WorldBatchKind.TacticalAdvance, between);
                        samples.Add(commit.Sequence);
                        between.Clear();
                        if (samples.Count == record.RouteSamples) Assert.Equal(record.Final, to);
                    }
                    else between.Add(batch.Kind);
                }
                ulong had = state.Progression!.Currency.GetActorAccount(player).Balance;
                ulong has = next.Progression!.Currency.GetActorAccount(player).Balance;
                if (has > had && trades.Count == 0) reward ??= has - had;
                else if (has != had) trades.Add(Trade(commit.Sequence, state, next, player, has > had ? kegga : marla, markets.Origin.Commodity));
                Assert.Equal(before.Space.GetActor(chief).Feet, next.Space.GetActor(chief).Feet);
                state = next;
            }
            Assert.Equal(final.ContentHash, state.ContentHash);
            Assert.NotNull(entered);
            Assert.True(queued > entered, $"{mode}: the retreat request must follow the chief's entry.");
            Assert.Equal(record.RouteSamples, samples.Count);
            Assert.Equal(record.WorldSequence, samples[^1]);
            // Inspection and cancellation sent nothing: exactly the keyboard Strike, then the recorded retreat.
            string[] expectedCommands = fromFarm ? ["strike", "retreat"] : ["retreat"];
            Assert.Equal(expectedCommands,
                encounterCommands.Select(command => command.CommandId == record.CommandId ? "retreat"
                    : command.Intent is QueueEncounterAbilityIntent ? "strike" : command.Intent.GetType().Name).ToArray());

            JsonElement feet = report.RootElement.GetProperty("FinalFeet");
            SubunitPosition reported = new(feet.GetProperty("X").GetInt64(), feet.GetProperty("Y").GetInt64(), feet.GetProperty("Z").GetInt64());
            CargoCorridor corridor = RoadThreatJourney.Corridor(genesis, player);
            SubunitPosition authored = fromFarm ? CargoCorridor.FeetAt(corridor.DestinationApproach)
                : CargoCorridor.FeetAt(new CellCoordinate(346, 318, corridor.Corner.Z));
            Assert.Equal((authored, authored), (final.Space.GetActor(player).Feet, reported));

            int playerHp = final.Progression!.GetActor(player).CurrentHp;
            ulong balanceBefore = before.Progression!.Currency.GetActorAccount(player).Balance;
            ulong balanceAfter = final.Progression.Currency.GetActorAccount(player).Balance;
            if (fromFarm)
            {
                // Wren's earned reward, Marla's exact interval and price, and Kegga's purchase of that same interval.
                JsonElement purchase = report.RootElement.GetProperty("Purchase");
                JsonElement sale = report.RootElement.GetProperty("Trade");
                Assert.Equal((ulong?)report.RootElement.GetProperty("Reward").GetUInt64(), reward);
                TradeLeg bought = Assert.Single(trades, leg => leg.Merchant == marla);
                TradeLeg sold = Assert.Single(trades, leg => leg.Merchant == kegga);
                Assert.Equal(2, trades.Count);
                Assert.True(bought.Sequence < entered && sold.Sequence > record.WorldSequence, "Buy before the road, sell after the escape.");
                Assert.Equal((purchase.GetProperty("Total").GetUInt64(), purchase.GetProperty("LotId").GetString()), (bought.Total, bought.LotId.ToString()));
                Assert.Equal((sale.GetProperty("Total").GetUInt64(), sale.GetProperty("LotId").GetString()), (sold.Total, sold.LotId.ToString()));
                Assert.Equal((1UL, bought.Interval), (bought.Interval.End - bought.Interval.Start, sold.Interval));
                Assert.Equal(reward!.Value - bought.Total + sold.Total, balanceAfter);
                Assert.True(playerHp < final.Progression.GetActor(player).Stats.MaxHp, "The chief's hit must be retained.");
                Assert.Equal(report.RootElement.GetProperty("FinalHp").GetInt32(), playerHp);
                Assert.DoesNotContain(final.Inventory.Custody.Lots, lot => lot.CustodianId == final.GetResident(player).InventoryCustodianId
                    && lot.Definition == markets.Origin.Commodity);
            }
            else
            {
                EntityId lotId = before.Inventory.Custody.Lots.Single(lot => lot.LotId.ToString() == metadata.RootElement.GetProperty("CarriedLotId").GetString()).LotId;
                Assert.Empty(trades);
                Assert.Null(reward);
                Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("Trade").ValueKind);
                Assert.Equal(balanceBefore, balanceAfter);
                Assert.Equal(before.Inventory.Custody.Lots.Single(lot => lot.LotId == lotId), final.Inventory.Custody.Lots.Single(lot => lot.LotId == lotId));
            }
        }
        finally { Directory.Delete(scratch, recursive: true); }
    }

    /// <summary>
    /// One paid transfer between the player and a merchant: exact opposite fund movements between those two parties
    /// and one counted lot of the cargo commodity changing custody between them with its source interval. A purchase
    /// takes an exact sub-interval of one of the merchant's prior lots; a sale moves the player's whole lot unchanged.
    /// </summary>
    private static TradeLeg Trade(ulong sequence, WorldSnapshot state, WorldSnapshot next, EntityId player, EntityId merchant,
        CountedItemDefinition commodity)
    {
        ulong had = state.Progression!.Currency.GetActorAccount(player).Balance;
        ulong has = next.Progression!.Currency.GetActorAccount(player).Balance;
        ulong merchantHad = state.Progression.Currency.GetActorAccount(merchant).Balance;
        ulong merchantHas = next.Progression.Currency.GetActorAccount(merchant).Balance;
        bool buying = has < had;
        ulong total = buying ? had - has : has - had;
        Assert.Equal(buying ? merchantHad + total : merchantHad - total, merchantHas);
        EntityId playerStore = state.GetResident(player).InventoryCustodianId;
        EntityId merchantStore = state.GetResident(merchant).InventoryCustodianId;
        CountedSourceLot[] PlayerLots(WorldSnapshot world) => world.Inventory.Custody.Lots
            .Where(lot => lot.CustodianId == playerStore && lot.Definition == commodity).ToArray();
        if (buying)
        {
            CountedSourceLot gained = Assert.Single(PlayerLots(next));
            Assert.Empty(PlayerLots(state));
            CountedSourceLot shelf = Assert.Single(state.Inventory.Custody.Lots, lot => lot.CustodianId == merchantStore
                && lot.Definition == commodity && lot.Source.SourceId == gained.Source.SourceId
                && lot.Source.Start <= gained.Source.Start && gained.Source.End <= lot.Source.End);
            // The merchant keeps exactly the rest of that shelf interval.
            ulong kept = next.Inventory.Custody.Lots.Where(lot => lot.CustodianId == merchantStore && lot.Source.SourceId == shelf.Source.SourceId
                && lot.Source.Start >= shelf.Source.Start && lot.Source.End <= shelf.Source.End).Aggregate(0UL, (sum, lot) => sum + lot.Quantity);
            Assert.Equal(shelf.Quantity - gained.Quantity, kept);
            return new TradeLeg(sequence, merchant, gained.LotId, gained.Source, total);
        }
        CountedSourceLot carried = Assert.Single(PlayerLots(state));
        Assert.Empty(PlayerLots(next));
        CountedSourceLot delivered = Assert.Single(next.Inventory.Custody.Lots, lot => lot.CustodianId == merchantStore
            && lot.Source.SourceId == carried.Source.SourceId && lot.Source.Start <= carried.Source.Start
            && carried.Source.End <= lot.Source.End);
        Assert.Equal(carried.Definition, delivered.Definition);
        return new TradeLeg(sequence, merchant, carried.LotId, carried.Source, total);
    }

    private sealed record TradeLeg(ulong Sequence, EntityId Merchant, EntityId LotId, SourceInterval Interval, ulong Total);
}
