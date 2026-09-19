using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ReservoirWorldRecoveryTests
{
    [Fact]
    public void FreshAdmissionRoundTripsAndRetainsPhysicalOwnershipThroughTimeAndRecovery()
    {
        var fixture = new ReservoirWorldFixture();
        WorldSnapshot state = fixture.Genesis;
        byte[] reservoirs = LiquidReservoirCodecV1.Encode(fixture.Reservoirs);
        Assert.Equal(8, state.ProfileVersion);
        Assert.NotEqual(fixture.Basis.AdmissionVersion, state.AdmissionVersion);
        Assert.Equal(state.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(state)).ContentHash);
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            state = authority.Commit(fixture.Actions.Batch(state, [], [])).Candidate;
            Assert.True(state.Calendar > fixture.Reservoirs.Capacity.Pending[0].Request.End);
            Assert.Equal(reservoirs, LiquidReservoirCodecV1.Encode(state.Reservoirs!));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(state.ContentHash, reopened.InspectForHost().State.ContentHash);
        Assert.Equal(fixture.Genesis.ContentHash, reopened.ReadRecordedStateForHost(0).StateHash);
    }
    [Fact]
    public void AdmissionRefusesLivedStateIndependentNetworkAndMismatchedMaterialRoots()
    {
        var fixture = new ReservoirWorldFixture();
        WorldSnapshot basis = fixture.Basis;
        var lived = new WorldStepProcessor().Decide(basis, fixture.Actions.Batch(basis, [], [])).Candidate;
        Assert.Throws<ArgumentException>(() => WorldReservoirAdmission.CreateGenesis(lived, fixture.Reservoirs));
        Assert.Throws<ArgumentException>(() => WorldReservoirAdmission.CreateGenesis(fixture.Genesis, fixture.Reservoirs));
        Assert.Throws<ArgumentException>(() => Copy(basis, fixture.Reservoirs,
            network: fixture.Reservoirs.Capacity.Domain.Network));
        var custody = basis.Inventory.Custody;
        var changed = new CountedAuthorityState(new(custody.WorldId, custody.BranchId, 1, custody.Storages, custody.Lots),
            basis.Inventory.AdmittedUnitCapacity, basis.Inventory.Actors, basis.Inventory.Controls,
            basis.Inventory.Policies, basis.Inventory.Sources, basis.Inventory.ReservationIds);
        Assert.Throws<ArgumentException>(() => Copy(basis, fixture.Reservoirs, inventory: changed));
        var production = new ProductionSnapshot(basis.WorldId, basis.BranchId, 0, basis.Production!.Recipes.Take(1), [], []);
        Assert.Throws<ArgumentException>(() => Copy(basis, fixture.Reservoirs, production: production));
    }

    [Fact]
    public void GeometryCannotAliasWorldOnlyBodyTerrainOrQuestIdentities()
    {
        var fixture = new ReservoirWorldFixture();
        var basis = fixture.Basis;
        var geometry = fixture.Reservoirs.Geometries[0];
        foreach (var id in new[] { basis.Space.Actors[0].Condition.Definition.DefinitionId,
            basis.Space.Regions[0].Cells.First(c => c.SourceId.HasValue).SourceId!.Value,
            basis.Quests!.Definitions.Single().DefinitionId })
        {
            var changed = LiquidReservoirState.Create(fixture.Reservoirs.Capacity,
                fixture.Reservoirs.Geometries.Select(g => g == geometry
                    ? new ReservoirGeometry(g.Scope, id, g.CustodianId, g.SpillZ, g.Columns) : g));
            Assert.Throws<ArgumentException>(() => Copy(basis, changed));
        }
        WorldSnapshot state = fixture.Genesis;
        foreach (var id in fixture.Reservoirs.Geometries.Select(g => g.ReservoirId)
            .Append(ReservoirWorldFixture.Material.Physical.DefinitionId))
        {
            Assert.True(state.IdentityIndex.Contains(id));
            Assert.Throws<InvalidOperationException>(() => new WorldStepProcessor().Decide(state,
                new(id, state.WorldId, state.BranchId, 0, 0, WorldBatchKind.AdvancingStep, [], [], 8)));
        }
    }

    [Fact]
    public void CanceledObligationsDoNotRemovePhysicalOwnershipOnStepsOrReload()
    {
        var fixture = new ReservoirWorldFixture(canceled: true);
        var state = fixture.Genesis;
        byte[] before = LiquidReservoirCodecV1.Encode(state.Reservoirs!);
        state = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state, [], [])).Candidate;
        state = WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(state));
        Assert.Empty(state.Reservoirs!.Capacity.Pending);
        Assert.Single(state.Reservoirs.Capacity.Closed);
        Assert.Equal(2, state.Reservoirs.Geometries.Count);
        Assert.Equal(before, LiquidReservoirCodecV1.Encode(state.Reservoirs));
        Assert.Equal(600UL, state.Inventory.Custody.Lots.Single(l => l.ReservedBy == ReservoirWorldFixture.Reservation).Quantity);
    }

    [Fact]
    public void RejectedGatherDoesNotIssueSourceSpendEffortOrReserveSpawnedIdentity()
    {
        var fixture = new ReservoirWorldFixture(primaryReceiver: true);
        WorldSnapshot state = fixture.Genesis;
        EntityId player = Player(state);
        var node = state.Gathering!.Nodes.Where(n => n.RemainingSwings > 0).OrderBy(n => Math.Abs(n.Feet.X - state.Space.GetActor(player).Feet.X)
            + Math.Abs(n.Feet.Y - state.Space.GetActor(player).Feet.Y)).First();
        state = Approach(fixture, state, player, node.Feet,
            w => ResourceInteractionQueries.Query(w.Space, player, node.Feet) == SpatialInteraction.Reachable);
        while (state.Gathering!.GetActorWork(player).Effort < GatherRulesV1.MaximumEffort)
        {
            state = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state, [], [])).Candidate;
        }
        var tool = state.Gathering!.Tools.First(t => t.Kind == node.Definition.RequiredToolKind);
        var lot = state.Inventory.Custody.Lots.First(l => l.Definition == tool.Definition
            && state.Inventory.Policies.Any(p => p.CustodianId == l.CustodianId && p.OwnerActorId == player));
        var action = fixture.Command(state, player, new GatherIntent(node.NodeId, node.Revision, lot.LotId, lot.Revision));
        var rawPlan = new GatherPlanner().Plan(state, state.Inventory, state.Gathering, action);
        Assert.True(rawPlan.Rejection == WorldActionRejection.None, JsonSerializer.Serialize(new {
            rawPlan.Rejection, node.RemainingSwings, node.NextUnissuedUnit, node.UnitCapacity, tool.Power,
            Effort = state.Gathering.GetActorWork(player).Effort, lot.Protection, lot.ReservedBy }));
        var result = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state, [action], []));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(result.Receipts).Rejection);
        Assert.Equal(state.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(state.Reservoirs!.ContentHash, result.Candidate.Reservoirs!.ContentHash);
        Assert.Equal(state.Gathering.Nodes, result.Candidate.Gathering!.Nodes);
        Assert.Equal(state.Gathering.GetActorWork(player).Effort, result.Candidate.Gathering.GetActorWork(player).Effort);
        Assert.DoesNotContain(result.Identities, i => i.Identity == rawPlan.SpawnedId);
    }

    [Fact]
    public void RejectedPhaseChangeRestoresRawRetirementProductionAndGeneratedIds()
    {
        var fixture = new ReservoirWorldFixture(primaryReceiver: true);
        WorldSnapshot state = fixture.Genesis;
        var action = fixture.Craft(state, freeze: true);
        var rawPlan = new InstantCraftPlanner().Plan(state.Inventory, state.Production!,
            ConstructionUpgradeFixture.Delivery(state, Player(state)), Player(state), action.CommandId,
            ((CraftIntent)action.Intent).Request);
        Assert.Equal(WorldActionRejection.None, rawPlan.Rejection);
        var result = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state, [action], []));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(result.Receipts).Rejection);
        Assert.Equal(state.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(state.Production!.ContentHash, result.Candidate.Production!.ContentHash);
        Assert.Equal(state.Reservoirs!.ContentHash, result.Candidate.Reservoirs!.ContentHash);
        Assert.DoesNotContain(result.Identities, i => rawPlan.CreatedIds.Contains(i.Identity));
    }

    [Fact]
    public void CraftingMovementHeldDialogueAndHistoryShareOneDurablePhysicalOwner()
    {
        var fixture = new ReservoirWorldFixture(primaryReceiver: true);
        WorldSnapshot state = fixture.Genesis;
        var snapshots = new List<WorldSnapshot> { state };
        var batches = new List<WorldStepBatch>();
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            void Commit(WorldStepBatch batch)
            {
                state = authority.Commit(WorldStepCodec.Decode(WorldStepCodec.Encode(batch))).Candidate;
                Assert.Equal(8, state.ProfileVersion);
                Assert.Equal(state.Inventory.ContentHash, state.Reservoirs!.Capacity.Inventory.ContentHash);
                Assert.Equal(state.Production!.ContentHash, state.Reservoirs.Capacity.Production!.ContentHash);
                batches.Add(batch);
                snapshots.Add(state);
            }
            Commit(fixture.Actions.Batch(state, [fixture.Craft(state)], []));
            Assert.Single(state.Production!.Batches);
            Assert.Equal(1UL, state.Reservoirs!.Capacity.Revision);
            Assert.NotEqual(snapshots[0].Reservoirs!.ContentHash, state.Reservoirs.ContentHash);
            Assert.Equal(50UL, state.Reservoirs.Projections.Single(p => p.Geometry.CustodianId == ReservoirWorldFixture.Receiver).ExistingUnits);
            EntityId player = Player(state), wren = state.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Host).ActorId;
            Approach(fixture, state, player, state.Space.GetActor(wren).Feet,
                w => w.Space.QueryInteraction(player, wren) == SpatialInteraction.Reachable,
                batch => { Commit(batch); return state; });
            Commit(fixture.Actions.Batch(state, [fixture.Command(state, player,
                new SpeakToActorIntent(wren, state.Space.GetActor(wren).Revision))], []));
            Assert.True(state.HasReadingHold);
            var held = state.Calendar;
            Commit(fixture.Actions.Batch(state, [fixture.Command(state, player,
                new CloseDialogueIntent(state.Dialogues.Single().DialogueId))], []));
            Assert.Equal(held, state.Calendar);
            Assert.Equal(snapshots[1].Reservoirs!.ContentHash, state.Reservoirs.ContentHash);
        }
        byte[] before = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            foreach (int i in new[] { snapshots.Count - 1, 0, snapshots.Count - 2, 1 })
            {
                Assert.Equal(WorldSnapshotCodec.Encode(snapshots[i]), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost((ulong)i).State));
            }
            Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(batches[^1]));
        }
        Assert.Equal(before, File.ReadAllBytes(files.Path));
        Retain(fixture, snapshots, batches);
    }

    [Theory]
    [InlineData("world_genesis")]
    [InlineData("world_checkpoints")]
    [InlineData("world_head")]
    public void ValidIndependentGeometrySubstitutionStillFailsReplay(string table)
    {
        var fixture = new ReservoirWorldFixture();
        var changed = LiquidReservoirState.Create(fixture.Reservoirs.Capacity,
            fixture.Reservoirs.Geometries.Select(g => new ReservoirGeometry(g.Scope, g.ReservoirId, g.CustodianId,
                g.SpillZ, g.Columns.Select(c => c with { X = c.X + 10 }))));
        using var files = new StorageFiles();
        WorldSnapshot recorded = fixture.Genesis;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, recorded))
        {
            WorldSnapshot head = authority.Commit(fixture.Actions.Batch(recorded, [], [])).Candidate;
            if (table == "world_head")
            {
                recorded = head;
            }
        }
        WorldSnapshot replacement = Copy(recorded, changed);
        Assert.Equal(recorded.AdmissionVersion, replacement.AdmissionVersion);
        var bytes = WorldSnapshotCodec.Encode(replacement);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = files.Path, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE " + table + " SET state=$state,hash=$hash";
            command.Parameters.AddWithValue("$state", bytes);
            command.Parameters.AddWithValue("$hash", ContentHash.Compute(bytes).ToByteArray());
            Assert.Equal(1, command.ExecuteNonQuery());
        }
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }


    [Fact]
    public void OrdinaryQuestFundingAndCompletedConstructionRetainTheReservoirOwner()
    {
        var fixture = new ReservoirWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot state = fixture.Actions.CreateSource(files.Path, completeQuest: true, genesis: fixture.Genesis);
        Assert.Equal(1UL, state.Reservoirs!.Capacity.Revision);
        var water = state.Reservoirs.Projections.Select(p => (p.ExistingUnits, p.ReservedIncomingUnits)).ToArray();
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            state = fixture.Actions.Act(authority, state, Player(state), new CloseDialogueIntent(state.Dialogues.Single().DialogueId));
            var controller = new LocalNpcController();
            for (int i = 0; !state.Construction!.Projects.Single().IsCompleted; i++)
            {
                Assert.True(i < 512, "The ordinary builder must complete within its existing bounded approach/work allowance.");
                var decision = authority.Commit(fixture.Actions.Batch(state,
                    controller.SelectActions(state, fixture.Actions.NextIdentity), controller.SelectMovement(state)));
                Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
                state = decision.Candidate;
            }
            Assert.Single(state.Space.Structures);
            Assert.Single(state.Production!.Batches);
            Assert.Equal(2UL, state.Reservoirs!.Capacity.Revision);
            Assert.Equal(water, state.Reservoirs.Projections.Select(p => (p.ExistingUnits, p.ReservedIncomingUnits)));
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(state.ContentHash, recovered.InspectForHost().State.ContentHash);
        Assert.Equal(fixture.Genesis.ContentHash, recovered.ReadRecordedStateForHost(0).StateHash);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectedHandoverAndAcceptedCraftRemainIndependentInEitherActorOrder(bool craftFirst)
    {
        // Select fresh deterministic test seeds with both canonical actor orderings, before any lived state exists.
        var fixture = Enumerable.Range(0, 16).Select(i => new ReservoirWorldFixture(primaryReceiver: true,
            worldSeed: "reservoir-action-order-" + i)).First(f =>
            (Player(f.Basis).CompareTo(f.Basis.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Host).ActorId) < 0) == craftFirst);
        var state = fixture.Genesis;
        EntityId player = Player(state), wren = state.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Host).ActorId;
        state = Approach(fixture, state, player, state.Space.GetActor(wren).Feet,
            w => w.Space.QueryInteraction(player, wren) == SpatialInteraction.Reachable);
        var gift = state.Inventory.Custody.Lots.First(l => state.Inventory.Policies.Any(p => p.CustodianId == l.CustodianId
            && p.OwnerActorId == wren) && l.ReservedBy is null);
        var give = fixture.Command(state, wren, new GiveItemIntent(player, state.Space.GetActor(player).Revision,
            gift.LotId, gift.Revision, gift.CustodianId, state.Inventory.Custody.GetStorage(gift.CustodianId).Revision, 1));
        // Without the physical owner this same semantic handover succeeds; rejection is not a reach/rights false positive.
        var unbound = new WorldSnapshot(state.Inventory, state.Space, state.Residents, state.AdmissionVersion, state.Calendar,
            state.Revision, state.AdvancingSteps, state.Dialogues, state.Gathering, state.Production, state.Progression,
            state.Quests, state.Construction);
        Assert.Equal(WorldActionRejection.None, Assert.Single(new WorldStepProcessor().Decide(unbound,
            fixture.Actions.Batch(unbound, [give], [])).Receipts).Rejection);
        var craft = fixture.Craft(state);
        var expected = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state, [craft], [])).Candidate;
        var batch = fixture.Actions.Batch(state, [give, craft], []);
        Assert.Equal(craftFirst ? craft.CommandId : give.CommandId, batch.Actions[0].CommandId);
        var decision = new WorldStepProcessor().Decide(state, batch);
        Assert.Equal(WorldActionRejection.None, decision.Receipts.Single(r => r.CommandId == craft.CommandId).Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, decision.Receipts.Single(r => r.CommandId == give.CommandId).Rejection);
        Assert.Equal(expected.ContentHash, decision.Candidate.ContentHash);
        Assert.Equal(1UL, decision.Candidate.Reservoirs!.Capacity.Revision);
        Assert.DoesNotContain(decision.Identities, i => i.Identity == EntityId.DeriveSpawned(give.CommandId, 0));
    }

    [Fact]
    public void CodecRejectsChangedWitnessesTrailingBytesAndUnsupportedEncoders()
    {
        var fixture = new ReservoirWorldFixture();
        byte[] bytes = WorldSnapshotCodec.Encode(fixture.Genesis);
        byte[] badWitness = (byte[])bytes.Clone(); badWitness[^1] ^= 1;
        foreach (byte[] bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), badWitness,
            new byte[WorldSnapshotCodec.MaximumStateByteLength + 1] })
        {
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(bad));
        }
        Assert.Equal(16 * 1024 * 1024, WorldSnapshotCodec.MaximumStateByteLength);
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV7.Encode(fixture.Genesis));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV8.Encode(fixture.Basis));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Basis), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(fixture.Basis))));
    }


    [Fact]
    public void MaximumNetworkAndReservoirSupportFitTheUnchangedWorldEnvelope()
    {
        var fixture = new ReservoirWorldFixture(maximum: true);
        var state = fixture.Genesis;
        byte[] bytes = WorldSnapshotCodec.Encode(state);
        Assert.Equal(65536, state.WatershedNetwork!.ColumnCount);
        Assert.Equal(4096, state.Reservoirs!.Geometries.Sum(g => g.Columns.Count));
        Assert.True(bytes.Length < WorldSnapshotCodec.MaximumStateByteLength);
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            state = authority.Commit(fixture.Actions.Batch(state, [], [])).Candidate;
        }
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_RESERVOIR_WORLD_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(path))
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "maximum.json"), JsonSerializer.Serialize(new
            {
                WorldBytes = bytes.Length, NetworkColumns = state.WatershedNetwork!.ColumnCount,
                ReservoirColumns = state.Reservoirs!.Geometries.Sum(g => g.Columns.Count),
                Bound = WorldSnapshotCodec.MaximumStateByteLength, PerformanceQualified = false
            }));
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(state.ContentHash, recovered.InspectForHost().State.ContentHash);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(0).State));
    }


    [Theory]
    [InlineData(1300UL, true)]
    [InlineData(1350UL, true)]
    [InlineData(1351UL, false)]
    public void HandoverUsesOutstandingInflowAsWellAsActualVolume(ulong quantity, bool accepted)
    {
        var fixture = new ReservoirWorldFixture(primaryReceiver: true, incomingSupply: quantity);
        var state = fixture.Genesis;
        EntityId player = Player(state), wren = state.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Host).ActorId;
        state = Approach(fixture, state, player, state.Space.GetActor(wren).Feet,
            w => w.Space.QueryInteraction(player, wren) == SpatialInteraction.Reachable);
        var lot = state.Inventory.Custody.GetLot(ReservoirWorldFixture.Id(13));
        var action = fixture.Command(state, wren, new GiveItemIntent(player, state.Space.GetActor(player).Revision,
            lot.LotId, lot.Revision, lot.CustodianId, state.Inventory.Custody.GetStorage(lot.CustodianId).Revision, quantity));
        var unbound = new WorldSnapshot(state.Inventory, state.Space, state.Residents, state.AdmissionVersion, state.Calendar,
            state.Revision, state.AdvancingSteps, state.Dialogues, state.Gathering, state.Production, state.Progression,
            state.Quests, state.Construction);
        Assert.Equal(WorldActionRejection.None, Assert.Single(new WorldStepProcessor().Decide(unbound,
            fixture.Actions.Batch(unbound, [action], [])).Receipts).Rejection);
        var decision = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state, [action], []));
        Assert.Equal(accepted ? WorldActionRejection.None : WorldActionRejection.Unavailable, Assert.Single(decision.Receipts).Rejection);
        var projection = decision.Candidate.Reservoirs!.Projections.Single(p => p.Geometry.CustodianId == ReservoirWorldFixture.Receiver);
        Assert.Equal(accepted ? 50 + quantity : 50UL, projection.ExistingUnits);
        Assert.Equal(600UL, projection.ReservedIncomingUnits);
        Assert.Equal(accepted ? 1UL : 0UL, decision.Candidate.Reservoirs.Capacity.Revision);
        Assert.Equal(decision.Candidate.Inventory.ContentHash, decision.Candidate.Reservoirs.Capacity.Inventory.ContentHash);
        if (!accepted)
        {
            Assert.Equal(state.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
        }
    }

    private static EntityId Player(WorldSnapshot state) => state.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Player).ActorId;

    private static WorldSnapshot Copy(WorldSnapshot basis, LiquidReservoirState reservoirs,
        CountedAuthorityState? inventory = null, ProductionSnapshot? production = null, PreparedWatershedNetwork? network = null) =>
        new(inventory ?? basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion, basis.Calendar,
            basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering, production ?? basis.Production,
            basis.Progression, basis.Quests, basis.Construction, watershedNetwork: network, reservoirs: reservoirs);

    private static WorldSnapshot Approach(ReservoirWorldFixture fixture, WorldSnapshot state, EntityId actor,
        SubunitPosition target, Func<WorldSnapshot, bool> reached, Func<WorldStepBatch, WorldSnapshot>? commit = null)
    {
        for (int i = 0; !reached(state); i++)
        {
            Assert.True(i < 128, "Authored target must be reachable within the existing farm approach bound.");
            var feet = state.Space.GetActor(actor).Feet;
            long x = Math.Sign(target.X - feet.X) * WorldMovementInput.DisplacementPerStep;
            long y = x == 0 ? Math.Sign(target.Y - feet.Y) * WorldMovementInput.DisplacementPerStep : 0;
            var batch = fixture.Actions.Batch(state, [], [new(ConstructionUpgradeFixture.Delivery(state, actor), actor, x, y)]);
            state = commit is null ? new WorldStepProcessor().Decide(state, batch).Candidate : commit(batch);
        }
        return state;
    }

    private static void Retain(ReservoirWorldFixture fixture, IReadOnlyList<WorldSnapshot> states, IReadOnlyList<WorldStepBatch> batches)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_RESERVOIR_WORLD_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "basis.bin"), WorldSnapshotCodec.Encode(fixture.Basis));
        File.WriteAllBytes(Path.Combine(path, "reservoirs.bin"), LiquidReservoirCodecV1.Encode(fixture.Reservoirs));
        File.WriteAllBytes(Path.Combine(path, "genesis.bin"), WorldSnapshotCodec.Encode(states[0]));
        for (int i = 0; i < batches.Count; i++) File.WriteAllBytes(Path.Combine(path, $"step-{i + 1}.bin"), WorldStepCodec.Encode(batches[i]));
        File.WriteAllText(Path.Combine(path, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Authored farm with explicit water custody/recipes, primary receiving vessel and physical owner; no liquid flow or terrain support",
            WorldHashes = states.Select(s => s.ContentHash.Value),
            ReservoirHashes = states.Select(s => s.Reservoirs!.ContentHash.Value),
            NetworkHash = fixture.Reservoirs.Capacity.Domain.Network.ComputeContentHash().Value,
            HeldRevision = states.Count - 2, FinalRevision = states.Count - 1
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

}
