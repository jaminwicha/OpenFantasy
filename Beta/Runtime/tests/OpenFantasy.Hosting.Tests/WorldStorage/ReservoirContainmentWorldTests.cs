using System.Buffers.Binary;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>
/// The holes in ReservoirBoundaryFixture are explicitly authored initial geography. These tests
/// claim actual funded water, actor actions and construction, not earned excavation or flowing water.
/// </summary>
public sealed class ReservoirContainmentWorldTests
{
    [Theory]
    [InlineData("floor")]
    [InlineData("wall")]
    [InlineData("missing")]
    [InlineData("solid-interior")]
    [InlineData("solid-version")]
    [InlineData("liquid-version")]
    [InlineData("work-limit")]
    public void Unsealed_or_unproved_envelopes_remain_legacy_readable_but_cannot_gain_containment(string fault)
    {
        var fixture = fault switch
        {
            "floor" => new ReservoirBoundaryFixture(extraHoles: [new(0, 0, -3)]),
            "wall" => new ReservoirBoundaryFixture(extraHoles: [new(-1, 0, -2)]),
            "missing" => new ReservoirBoundaryFixture(missingWest: true),
            "solid-interior" => new ReservoirBoundaryFixture(leaveSubstrate: true),
            _ => new ReservoirBoundaryFixture()
        };
        ReservoirSealPolicy policy = fault switch
        {
            "solid-version" => new(fixture.Policy.Liquid, fixture.Policy.Rules.Select(rule =>
                new ReservoirSealRule(new(rule.Material.DefinitionId, ContentHash.Compute("wrong-seal"u8)), 1000))),
            "liquid-version" => new(new(fixture.Policy.Liquid.DefinitionId, ContentHash.Compute("wrong-liquid"u8)),
                fixture.Policy.Rules),
            _ => fixture.Policy
        };
        WorldSnapshot basis = fixture.World;
        if (fault == "work-limit")
        {
            LiquidReservoirState physical = LiquidReservoirState.Create(basis.Reservoirs!.Capacity,
                basis.Reservoirs.Geometries.Select(geometry => geometry.ReservoirId == ReservoirBoundaryFixture.SourceReservoir
                    ? new ReservoirGeometry(geometry.Scope, geometry.ReservoirId, geometry.CustodianId, 1024,
                        Enumerable.Range(0, 65).Select(x => new ReservoirColumn(x, 10, 0))) : geometry));
            basis = CopyLegacy(basis, reservoirs: physical);
            Assert.False(new ReservoirBoundaryEvaluator().Assess(basis, policy, TestContext.Current.CancellationToken).Completed);
        }
        byte[] legacy = WorldSnapshotCodec.Encode(basis);
        Assert.Equal((ushort)8, WorldSnapshotCodec.Decode(legacy).ProfileVersion);
        Assert.Equal(legacy, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(legacy)));
        Assert.Null(basis.ReservoirSeals);
        Assert.Throws<ArgumentException>(() => WorldReservoirContainmentAdmission.CreateGenesis(basis, policy));
        Assert.Equal(legacy, WorldSnapshotCodec.Encode(basis));
    }

    [Fact]
    public void Fresh_admission_retains_exact_funded_stock_and_policy_without_resetting_played_worlds()
    {
        var fixture = new ReservoirBoundaryFixture(splitRegions: true);
        WorldSnapshot genesis = Admit(fixture);
        Assert.Equal((ushort)19, genesis.ProfileVersion);
        Assert.Equal(fixture.Policy.Version, genesis.ReservoirSeals!.Version);
        Assert.NotEqual(fixture.World.AdmissionVersion, genesis.AdmissionVersion);
        Assert.Equal(fixture.World.Inventory.ContentHash, genesis.Inventory.ContentHash);
        Assert.Equal(fixture.World.Reservoirs!.ContentHash, genesis.Reservoirs!.ContentHash);
        Assert.Equal(1300UL, genesis.Reservoirs.Projections.Single(value =>
            value.Geometry.CustodianId == ReservoirWorldFixture.SourceStore).ExistingUnits);
        Assert.Equal(50UL, genesis.Reservoirs.Projections.Single(value =>
            value.Geometry.CustodianId == ReservoirWorldFixture.Receiver).ExistingUnits);
        Assert.Equal(600UL, genesis.Reservoirs.Projections.Single(value =>
            value.Geometry.CustodianId == ReservoirWorldFixture.Receiver).ReservedIncomingUnits);
        AssertContained(genesis);
        Assert.Throws<ArgumentException>(() => WorldReservoirContainmentAdmission.CreateGenesis(genesis, fixture.Policy));
        WorldSnapshot played = new WorldStepProcessor().Decide(fixture.World,
            fixture.Materials.Actions.Batch(fixture.World, [], [])).Candidate;
        Assert.Throws<ArgumentException>(() => WorldReservoirContainmentAdmission.CreateGenesis(played, fixture.Policy));
        Assert.Throws<ArgumentException>(() => new ReservoirSealPolicy(fixture.Policy.Liquid, []));
        Assert.Throws<ArgumentException>(() => new ReservoirSealPolicy(fixture.Policy.Liquid,
            [fixture.Policy.Rules[0], fixture.Policy.Rules[0]]));
        var unusedUnknown = new ReservoirSealPolicy(fixture.Policy.Liquid, fixture.Policy.Rules.Append(new(
            new(ReservoirWorldFixture.Id(900), ContentHash.Compute("unused-unadmitted-seal"u8)), 1)));
        Assert.True(new ReservoirBoundaryEvaluator().Assess(fixture.World, unusedUnknown,
            TestContext.Current.CancellationToken).IsContained);
        Assert.Throws<ArgumentException>(() => WorldReservoirContainmentAdmission.CreateGenesis(fixture.World, unusedUnknown));
    }

    [Fact]
    public void Occupied_or_unsealed_spatial_roots_and_changed_policy_cannot_be_smuggled_through_the_codec()
    {
        var fixture = new ReservoirBoundaryFixture();
        WorldSnapshot genesis = Admit(fixture);
        ActorPresence player = fixture.World.Space.GetActor(Player(fixture.World));
        // A supplied fresh placement proves the legacy spatial contract alone accepts this body.
        // It is deliberately not represented as earned movement through the surrounding walls.
        var inside = new ActorPresence(player.Binding, player.Condition, player.Scope,
            new(128, 128, -512), player.Revision, player.HomePlaceId);
        LocalSpatialSnapshot occupiedSpace = fixture.World.Space.WithActors(fixture.World.Space.Actors.Select(actor =>
            actor.ActorId == player.ActorId ? inside : actor));
        WorldSnapshot occupied = CopyLegacy(fixture.World, occupiedSpace);
        Assert.Equal(SpatialOccupancy.Clear, occupied.Space.QueryOccupancy(player.ActorId, inside.Feet));
        Assert.Equal((ushort)8, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(occupied)).ProfileVersion);
        Assert.Throws<ArgumentException>(() => WorldReservoirContainmentAdmission.CreateGenesis(occupied, fixture.Policy));

        byte[] canonical = WorldSnapshotCodec.Encode(genesis);
        var leaking = new ReservoirBoundaryFixture(extraHoles: [new(0, 0, -3)]);
        foreach (WorldSnapshot badSpace in new[] { occupied, leaking.World })
        {
            byte[][] fields = ReadFrame(canonical);
            fields[3] = LegacySpatialBytes(badSpace);
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(Frame(fields)));
        }
        byte[][] changedPolicy = ReadFrame(canonical);
        changedPolicy[17] = ReservoirSealPolicyCodecV1.Encode(new(fixture.Policy.Liquid,
            fixture.Policy.Rules.Select(rule => new ReservoirSealRule(
                new(rule.Material.DefinitionId, ContentHash.Compute("unadmitted-material-version"u8)), 1000))));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(Frame(changedPolicy)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(canonical[..^1]));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode([.. canonical, 0]));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV8.Encode(genesis));
        Assert.Equal(canonical, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(canonical)));
    }

    [Fact]
    public void Actual_filtering_rejection_movement_and_time_retain_one_owner_across_exact_history_and_reopen()
    {
        var fixture = new ReservoirBoundaryFixture(splitRegions: true);
        WorldSnapshot legacy = CopyLegacy(fixture.World, primaryReceiver: true);
        WorldSnapshot state = WorldReservoirContainmentAdmission.CreateGenesis(legacy, fixture.Policy);
        var states = new List<WorldSnapshot> { state };
        var batches = new List<WorldStepBatch>();
        var receipts = new List<WorldActionReceipt>();
        using var files = new StorageFiles();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            CountedSourceLot source = state.Inventory.Custody.GetLot(ReservoirWorldFixture.Id(12));
            WorldActionCommand filter = fixture.Materials.Craft(state);
            WorldStepDecision filtered = Commit(fixture.Materials.Actions.Batch(state, [filter], []));
            Assert.Equal(WorldActionRejection.None, Assert.Single(filtered.Receipts).Rejection);
            ProductionBatch actual = Assert.Single(state.Production!.Batches);
            RetiredIngredient retired = Assert.Single(actual.Ingredients);
            Assert.Equal(source.LotId, retired.LotId);
            Assert.Equal(source.Source, retired.Source);
            Assert.DoesNotContain(state.Inventory.Custody.Lots, lot => lot.LotId == source.LotId);
            TransformedLot output = Assert.Single(state.Production!.Lots);
            Assert.Equal(ReservoirWorldFixture.Water, output.Definition);
            Assert.Equal(50UL, output.Quantity);
            Assert.Equal(ReservoirWorldFixture.Receiver, output.CustodianId);
            Assert.Equal(1350UL, state.Reservoirs!.Projections.Aggregate(0UL, (sum, value) => sum + value.ExistingUnits));
            Assert.Equal(states[0].Inventory.Custody.Lots.Where(lot => lot.LotId != source.LotId),
                state.Inventory.Custody.Lots);

            EntityId builder = state.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Host).ActorId;
            WorldMovementInput movement = AvailableMovement(state, builder);
            ActorPresence beforeMove = state.Space.GetActor(builder);
            CraftRecipeDefinition ice = ReservoirWorldFixture.Recipe(freeze: true);
            WorldActionCommand freeze = fixture.Materials.Command(state, Player(state), new CraftIntent(new(
                ice.DefinitionId, ice.Version, state.Production!.Revision, output.CustodianId,
                state.Inventory.Custody.GetStorage(output.CustodianId).Revision,
                [new(IngredientLotKind.Transformed, output.LotId, output.Revision, output.Quantity)])));
            CraftPlan raw = new InstantCraftPlanner().Plan(state.Inventory, state.Production!,
                ConstructionUpgradeFixture.Delivery(state, Player(state)), Player(state), freeze.CommandId,
                ((CraftIntent)freeze.Intent).Request);
            Assert.Equal(WorldActionRejection.None, raw.Rejection);
            WorldSnapshot beforeRefusal = state;
            WorldStepDecision refused = Commit(fixture.Materials.Actions.Batch(state, [freeze], [movement]));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
            Assert.Equal(beforeRefusal.Inventory.ContentHash, state.Inventory.ContentHash);
            Assert.Equal(beforeRefusal.Production!.ContentHash, state.Production!.ContentHash);
            Assert.Equal(beforeMove.Feet.Offset(movement.DeltaX, movement.DeltaY, 0), state.Space.GetActor(builder).Feet);
            Assert.Equal(beforeMove.Revision + 1, state.Space.GetActor(builder).Revision);
            Assert.Equal(beforeRefusal.Calendar.AddMilliseconds(1800), state.Calendar);
            Assert.DoesNotContain(refused.Identities, identity => raw.CreatedIds.Contains(identity.Identity));
            Commit(fixture.Materials.Actions.Batch(state, [], []));

            WorldStepDecision Commit(WorldStepBatch batch)
            {
                byte[] encoded = WorldStepCodec.Encode(batch);
                Assert.Equal(encoded, WorldStepCodec.Encode(WorldStepCodec.Decode(encoded)));
                WorldStepDecision decision = authority.Commit(WorldStepCodec.Decode(encoded));
                state = decision.Candidate;
                Assert.Equal((ushort)19, state.ProfileVersion);
                Assert.Equal(fixture.Policy.Version, state.ReservoirSeals!.Version);
                Assert.Equal(state.Inventory.ContentHash, state.Reservoirs!.Capacity.Inventory.ContentHash);
                Assert.Equal(state.Production!.ContentHash, state.Reservoirs!.Capacity.Production!.ContentHash);
                AssertContained(state);
                batches.Add(batch);
                receipts.AddRange(decision.Receipts);
                states.Add(state);
                return decision;
            }
        }
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(3, database.SchemaVersion);
            Assert.Equal(WorldSnapshotCodec.Encode(states[0]), database.ReadGenesis());
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            foreach (WorldSnapshot historical in states.AsEnumerable().Reverse().Concat(states))
            {
                var recorded = reopened.ReadRecordedStateForHost(historical.Revision);
                Assert.Equal(historical.ContentHash, recorded.StateHash);
                Assert.Equal(WorldSnapshotCodec.Encode(historical), WorldSnapshotCodec.Encode(recorded.State));
                AssertContained(recorded.State);
            }
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
        Retain(fixture, legacy, files.Path, states, batches, receipts);
    }

    [Fact]
    public void Actual_quest_funding_and_npc_construction_preserve_the_sealed_reservoir_owner()
    {
        var fixture = new ReservoirBoundaryFixture();
        WorldSnapshot genesis = Admit(fixture);
        using var files = new StorageFiles();
        WorldSnapshot world = fixture.Materials.Actions.CreateSource(files.Path, completeQuest: true, genesis: genesis);
        Assert.Empty(world.Production!.Batches);
        Assert.Null(world.Construction!.Projects.Single().Installed);
        byte[] reservoirs = LiquidReservoirCodecV1.Encode(world.Reservoirs!);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            world = fixture.Materials.Actions.Act(authority, world, Player(world),
                new CloseDialogueIntent(Assert.Single(world.Dialogues).DialogueId));
            var controller = new LocalNpcController();
            for (int step = 0; world.Construction!.Projects.Single().Installed is null; step++)
            {
                Assert.True(step < 256, "Actual funded farm work must finish within the existing finite project bound.");
                world = authority.Commit(fixture.Materials.Actions.Batch(world,
                    controller.SelectActions(world, fixture.Materials.Actions.NextIdentity), controller.SelectMovement(world))).Candidate;
                AssertContained(world);
            }
            var project = world.Construction!.Projects.Single();
            Assert.Equal(project.Definition.RequiredWork.Value, project.Progress.CompletedUnits);
            Assert.Single(world.Space.Structures);
            ProductionBatch batch = Assert.Single(world.Production!.Batches);
            Assert.Equal(project.Installed!.ProductionBatchId, batch.BatchId);
            Assert.Equal(project.ReservationId, world.Production!.Lots.Single(lot =>
                lot.LotId == project.Installed.GuardLotId).ReservedBy);
            Assert.Equal(1350UL, world.Reservoirs!.Projections.Aggregate(0UL, (sum, value) => sum + value.ExistingUnits));
            Assert.NotEqual(reservoirs, LiquidReservoirCodecV1.Encode(world.Reservoirs));
        }
        using SqliteWorldAuthority recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(recovered.InspectForHost().State));
        Assert.Equal(WorldSnapshotCodec.Encode(genesis), WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(0).State));
    }

    [Fact]
    public void Earned_overhang_completion_is_legal_in_profile8_but_containment_refuses_only_its_final_publication()
    {
        var legacyFixture = new ReservoirContainmentOverhangFixture(contained: false);
        var containedFixture = new ReservoirContainmentOverhangFixture(contained: true);
        using var legacyFiles = new StorageFiles();
        using var containedFiles = new StorageFiles();
        WorldSnapshot legacyBefore = legacyFixture.PrepareLastWork(legacyFiles.Path);
        WorldSnapshot before = containedFixture.PrepareLastWork(containedFiles.Path);
        WorldActionCommand legacyWork = legacyFixture.LastWork(legacyBefore);
        WorldActionCommand work = containedFixture.LastWork(before);
        Assert.Equal(WorldActionCodec.Encode(legacyWork), WorldActionCodec.Encode(work));
        Assert.Equal(legacyBefore.Inventory.ContentHash, before.Inventory.ContentHash);
        Assert.Equal(legacyBefore.Production!.ContentHash, before.Production!.ContentHash);
        Assert.Equal(legacyBefore.Construction!.ContentHash, before.Construction!.ContentHash);
        var project = before.Construction!.Projects.Single();
        CountedSourceLot[] reserved = before.Inventory.Custody.Lots.Where(lot => lot.ReservedBy == project.ReservationId).ToArray();
        Assert.Equal(6UL, reserved.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Equal(1UL, project.Progress.CompletedUnits);
        AssertContained(before);

        ConstructionPlan geometricPlan = new ConstructionPlanner().Plan(before, before.Inventory, before.Production!,
            before.Construction!, before.Space, work, isAdvancingStep: true, held: false);
        Assert.True(geometricPlan.Succeeded);
        Assert.True(geometricPlan.CandidateConstruction.Projects.Single().IsCompleted);
        Assert.NotEmpty(geometricPlan.CreatedIds);
        WorldMovementInput legacyMove = AvailableMovement(legacyBefore, legacyFixture.Player);
        WorldMovementInput movement = AvailableMovement(before, containedFixture.Player);
        Assert.Equal(legacyMove.DeltaX, movement.DeltaX);
        Assert.Equal(legacyMove.DeltaY, movement.DeltaY);
        WorldStepBatch legacyBatch = legacyFixture.Materials.Actions.Batch(legacyBefore, [legacyWork], [legacyMove]);
        WorldStepBatch batch = containedFixture.Materials.Actions.Batch(before, [work], [movement]);
        WorldStepDecision legacyDecision;
        WorldStepDecision decision;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(legacyFiles.Path))
        {
            legacyDecision = authority.Commit(WorldStepCodec.Decode(WorldStepCodec.Encode(legacyBatch)));
        }
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(containedFiles.Path))
        {
            decision = authority.Commit(WorldStepCodec.Decode(WorldStepCodec.Encode(batch)));
        }
        Assert.Equal(WorldActionRejection.None, Assert.Single(legacyDecision.Receipts).Rejection);
        WorldSnapshot legacyAfter = legacyDecision.Candidate;
        Assert.Equal(2UL, legacyAfter.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Single(legacyAfter.Space.Structures);
        ProductionBatch installed = Assert.Single(legacyAfter.Production!.Batches);
        Assert.Equal(reserved.Select(lot => lot.Source), installed.Ingredients.Select(ingredient => ingredient.Source));
        Assert.DoesNotContain(legacyAfter.Inventory.Custody.Lots, lot => lot.ReservedBy == project.ReservationId);
        ReservoirBoundaryResult invaded = new ReservoirBoundaryEvaluator().Assess(legacyAfter, legacyFixture.Policy,
            TestContext.Current.CancellationToken).Results.Single(result => result.ReservoirId == ReservoirBoundaryFixture.SourceReservoir);
        Assert.False(invaded.IsContained);
        Assert.Equal(1L, invaded.Failures[ReservoirBoundaryFailure.InteriorConstruction]);

        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(decision.Receipts).Rejection);
        WorldSnapshot after = decision.Candidate;
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.Quests!.ContentHash, after.Quests!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Reservoirs!.ContentHash, after.Reservoirs!.ContentHash);
        Assert.Null(after.Construction.Projects.Single().Installed);
        Assert.Empty(after.Space.Structures);
        Assert.DoesNotContain(decision.Identities, identity => geometricPlan.CreatedIds.Contains(identity.Identity));
        Assert.All(geometricPlan.CreatedIds, identity => Assert.False(after.IdentityIndex.Contains(identity)));
        Assert.Equal(before.Space.GetActor(containedFixture.Builder).Revision, after.Space.GetActor(containedFixture.Builder).Revision);
        ActorPresence player = before.Space.GetActor(containedFixture.Player);
        Assert.Equal(player.Feet.Offset(movement.DeltaX, movement.DeltaY, 0), after.Space.GetActor(player.ActorId).Feet);
        Assert.Equal(player.Revision + 1, after.Space.GetActor(player.ActorId).Revision);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Revision + 1, after.Revision);
        AssertContained(after);
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(containedFiles.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(before.Revision).State));
    }

    private static WorldSnapshot Admit(ReservoirBoundaryFixture fixture) =>
        WorldReservoirContainmentAdmission.CreateGenesis(fixture.World, fixture.Policy);

    private static EntityId Player(WorldSnapshot state) => state.Residents.Single(resident =>
        resident.ControllerKind == ResidentControllerKind.Player).ActorId;

    private static WorldSnapshot CopyLegacy(WorldSnapshot basis, LocalSpatialSnapshot? space = null,
        bool primaryReceiver = false, LiquidReservoirState? reservoirs = null) =>
        new(basis.Inventory, space ?? basis.Space, basis.Residents.Select(resident => primaryReceiver && resident.ActorId == Player(basis)
            ? new WorldResident(resident.ActorId, ReservoirWorldFixture.Receiver, resident.DisplayName, resident.ControllerKind,
                resident.HomeFeet, resident.WanderRadiusCells, resident.Comprehension, resident.ExperiencedTime, resident.OpeningUtterance)
            : resident), basis.AdmissionVersion, basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues,
            basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction, reservoirs: reservoirs ?? basis.Reservoirs);

    private static WorldMovementInput AvailableMovement(WorldSnapshot state, EntityId actorId)
    {
        ActorPresence actor = state.Space.GetActor(actorId);
        foreach ((long x, long y) in new (long, long)[] { (32, 0), (-32, 0), (0, 32), (0, -32) })
        {
            if (state.Space.PrepareMove(actorId, actor.Revision, x, y).Status == SpatialMoveStatus.Prepared)
            {
                return new(ConstructionUpgradeFixture.Delivery(state, actorId), actorId, x, y);
            }
        }
        throw new InvalidOperationException("The authored farm actor requires an ordinary clear movement sample.");
    }

    private static void AssertContained(WorldSnapshot state) => Assert.True(new ReservoirBoundaryEvaluator()
        .Assess(state, state.ReservoirSeals!, TestContext.Current.CancellationToken).IsContained);

    private static byte[] LegacySpatialBytes(WorldSnapshot state)
    {
        byte[][] fields = ReadFrame(WorldSnapshotCodecV8.Encode(state));
        while (BinaryPrimitives.ReadUInt64BigEndian(fields[1]) != 1)
        {
            fields = ReadFrame(fields[2]);
        }
        return fields[3];
    }

    private static byte[][] ReadFrame(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        int offset = 12;
        var fields = new byte[count][];
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            fields[index] = bytes.AsSpan(offset + 4, length).ToArray();
            offset += 4 + length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] Frame(byte[][] fields)
    {
        byte[] bytes = new byte[12 + fields.Sum(field => 4 + field.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), (uint)fields.Length);
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), (uint)field.Length);
            field.CopyTo(bytes, offset + 4);
            offset += field.Length + 4;
        }
        return bytes;
    }

    private static void Retain(ReservoirBoundaryFixture fixture, WorldSnapshot legacy, string database,
        IReadOnlyList<WorldSnapshot> states, IReadOnlyList<WorldStepBatch> batches, IReadOnlyList<WorldActionReceipt> receipts)
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_CONTAINMENT_FIXTURE_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "basis.bin"), WorldSnapshotCodec.Encode(legacy));
        File.WriteAllBytes(Path.Combine(directory, "policy.bin"), ReservoirSealPolicyCodecV1.Encode(fixture.Policy));
        File.WriteAllBytes(Path.Combine(directory, "genesis.bin"), WorldSnapshotCodec.Encode(states[0]));
        File.WriteAllBytes(Path.Combine(directory, "step.bin"), WorldStepCodec.Encode(batches[0]));
        File.WriteAllBytes(Path.Combine(directory, "after.bin"), WorldSnapshotCodec.Encode(states[1]));
        for (int index = 0; index < batches.Count; index++)
        {
            File.WriteAllBytes(Path.Combine(directory, $"step-{index + 1}.bin"), WorldStepCodec.Encode(batches[index]));
        }
        File.Copy(database, Path.Combine(directory, "world.sqlite"), overwrite: true);
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Fixed retained basins with explicitly authored initial holes and real water custody; no flow or earned excavation",
            ProfileVersion = 19, SchemaVersion = 3, PolicyHash = fixture.Policy.Version.ToString(),
            WorldId = legacy.WorldId.ToString(), BranchId = legacy.BranchId.ToString(), Player = Player(legacy).ToString(),
            SourceId = ReservoirWorldFixture.Source.ToString(),
            PreRevision = states[0].Revision, PreHash = states[0].ContentHash.ToString(),
            PostRevision = states[1].Revision, PostHash = states[1].ContentHash.ToString(),
            CommandId = receipts[0].CommandId.ToString(), ReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipts[0])),
            History = states.Select(state => new { state.Revision, Hash = state.ContentHash.ToString() }),
            ReservoirHashes = states.Select(state => state.Reservoirs!.ContentHash.ToString()),
            Receipts = receipts.Select(receipt => new { CommandId = receipt.CommandId.ToString(),
                Rejection = receipt.Rejection.ToString() })
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
