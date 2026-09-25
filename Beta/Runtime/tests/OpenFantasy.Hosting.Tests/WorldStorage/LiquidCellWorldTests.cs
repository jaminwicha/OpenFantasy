using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Actual detailed placement and ordinary committed actions in explicitly authored contained geography.</summary>
public sealed class LiquidCellWorldTests
{
    [Fact]
    public void Fresh_admission_and_canonical_archive_require_the_exact_owner_and_clock()
    {
        var fixture = new LiquidCellWorldFixture();
        WorldSnapshot genesis = fixture.Genesis;
        Assert.Equal((ushort)20, genesis.ProfileVersion);
        Assert.Equal(fixture.ContainedBasis.Inventory.ContentHash, genesis.Inventory.ContentHash);
        Assert.Equal(fixture.ContainedBasis.Reservoirs!.ContentHash, genesis.Reservoirs!.ContentHash);
        Assert.NotEqual(fixture.ContainedBasis.AdmissionVersion, genesis.AdmissionVersion);
        AssertBound(genesis);
        AssertConserved(genesis);
        byte[] bytes = WorldSnapshotCodec.Encode(genesis);
        Assert.Equal(19, ReadFrame(bytes).Length);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV19.Encode(genesis));
        Assert.Throws<ArgumentException>(() => WorldLiquidCellAdmission.CreateGenesis(genesis, genesis.LiquidCells!));

        WorldSnapshot played = new WorldStepProcessor().Decide(fixture.ContainedBasis,
            fixture.Materials.Actions.Batch(fixture.ContainedBasis, [], [])).Candidate;
        Assert.Throws<ArgumentException>(() => WorldLiquidCellAdmission.CreateGenesis(played, genesis.LiquidCells!));
        LiquidCellState wrongTime = LiquidCellState.Create(genesis.Reservoirs!, genesis.LiquidCells!.Profile,
            genesis.Calendar.AddMilliseconds(1800), genesis.LiquidCells!.Portions);
        Assert.Throws<ArgumentException>(() => WorldLiquidCellAdmission.CreateGenesis(fixture.ContainedBasis, wrongTime));

        // Each nested archive is well framed; the root must still join its actual owner and time.
        foreach (string fault in new[] { "owner", "tick", "boundary", "origin" })
        {
            byte[][] root = ReadFrame(bytes);
            byte[][] cells = ReadFrame(root[18]);
            switch (fault)
            {
                case "owner": cells[4][0] ^= 1; break;
                case "tick": cells[1] = Number(1); break;
                case "boundary": cells[3] = Number((ulong)genesis.Calendar.MillisecondsWithinDay + 1800); break;
                case "origin": BinaryPrimitives.WriteUInt64BigEndian(cells[6].AsSpan(40), 3); break;
            }
            root[18] = Frame(cells);
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(Frame(root)));
        }
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(bytes[..^1]));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode([.. bytes, 0]));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(genesis));
    }

    [Fact]
    public void Committed_front_falls_then_reaches_pool_without_relay_and_replays_every_exact_state()
    {
        var fixture = new LiquidCellWorldFixture();
        using var files = new StorageFiles();
        var states = new List<WorldSnapshot> { fixture.Genesis };
        var batches = new List<WorldStepBatch>();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, states[0]))
        {
            for (int step = 1; step <= 6; step++)
            {
                WorldSnapshot before = states[^1];
                WorldStepBatch batch = fixture.Materials.Actions.Batch(before, [], []);
                byte[] encoded = WorldStepCodec.Encode(batch);
                Assert.Equal(encoded, WorldStepCodec.Encode(WorldStepCodec.Decode(encoded)));
                WorldStepDecision decision = authority.Commit(WorldStepCodec.Decode(encoded));
                WorldSnapshot after = decision.Candidate;
                Assert.Empty(decision.Receipts);
                AssertAdvanced(before, after);
                AssertBound(after);
                AssertConserved(after);
                Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
                Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
                Assert.Equal(before.Reservoirs!.ContentHash, after.Reservoirs!.ContentHash);
                Assert.Equal(50UL, Quantity(after, new(LiquidCellWorldFixture.ReceiverReservoir, new(6, 0, -2))));
                batches.Add(batch);
                states.Add(after);
            }
            // These amounts follow the starting quantities, rather than a second application of the planner.
            Assert.Equal(650UL, Quantity(states[1], LiquidCellWorldFixture.SourceCell(0, -1)));
            Assert.Equal(450UL, Quantity(states[1], LiquidCellWorldFixture.SourceCell(1, -1)));
            Assert.Equal(200UL, Quantity(states[1], LiquidCellWorldFixture.SourceCell(2, -1)));
            Assert.Equal(0UL, Quantity(states[1], LiquidCellWorldFixture.SourceCell(2, -2)));
            Assert.Equal(200UL, Quantity(states[2], LiquidCellWorldFixture.SourceCell(2, -2)));
            Assert.Equal(0UL, Quantity(states[2], LiquidCellWorldFixture.SourceCell(3, -2)));
            Assert.Equal(100UL, Quantity(states[3], LiquidCellWorldFixture.SourceCell(3, -2)));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(batches[0]));
            Assert.Equal(states[^1].ContentHash, authority.InspectForHost().State.ContentHash);
        }
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(3, database.SchemaVersion);
            Assert.Equal(6, database.ReadJournal().Count());
            Assert.Empty(database.ReadCommandOutcomes());
            Assert.Equal(WorldSnapshotCodec.Encode(states[0]), database.ReadGenesis());
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(states[^1]), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            foreach (WorldSnapshot expected in states.AsEnumerable().Reverse())
            {
                RecordedWorldState recorded = reopened.ReadRecordedStateForHost(expected.Revision);
                Assert.Equal(states[^1].Revision, recorded.SourceHeadRevision);
                Assert.Equal(states[^1].ContentHash, recorded.SourceHeadHash);
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded.State));
                AssertConserved(recorded.State);
            }
            Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(batches[^1]));
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Opening_and_closing_a_real_reading_hold_preserve_placement_without_catch_up()
    {
        var fixture = new LiquidCellWorldFixture();
        // A separately authored fresh one-unit rate keeps a visible front throughout the real farm approach.
        // No live policy or actor pose is replaced to arrange the hold.
        LiquidCellState initial = LiquidCellState.Create(fixture.ContainedBasis.Reservoirs!, new(1, 1),
            fixture.ContainedBasis.Calendar, fixture.Genesis.LiquidCells!.Portions);
        WorldSnapshot world = WorldLiquidCellAdmission.CreateGenesis(fixture.ContainedBasis, initial);
        using var files = new StorageFiles();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, world))
        {
            world = ApproachSpeaker(fixture, authority, world);
            WorldSnapshot before = world;
            EntityId player = Player(world);
            EntityId speaker = world.Quests!.Definitions.Single().GiverActorId;
            LiquidCellState wouldFlow = new LiquidCellPlanner().PlanAdvance(world.LiquidCells!,
                world.Calendar.AddMilliseconds(1800)).ApplyTo(world.LiquidCells!);
            Assert.False(before.LiquidCells!.Portions.SequenceEqual(wouldFlow.Portions));
            world = fixture.Materials.Actions.Act(authority, world, player,
                new SpeakToActorIntent(speaker, world.Space.GetActor(speaker).Revision));
            Assert.True(world.HasReadingHold);
            AssertAdvanced(before, world);
            Assert.Equal(before.LiquidCells!.Portions, world.LiquidCells!.Portions);

            WorldSnapshot held = world;
            var illegalAdvance = new WorldStepBatch(fixture.Materials.Actions.NextIdentity(), world.WorldId,
                world.BranchId, world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], 20);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(illegalAdvance));
            Assert.Equal(held.ContentHash, authority.InspectForHost().State.ContentHash);
            world = fixture.Materials.Actions.Act(authority, world, player,
                new CloseDialogueIntent(Assert.Single(world.Dialogues).DialogueId));
            Assert.False(world.HasReadingHold);
            Assert.Equal(held.Revision + 1, world.Revision);
            Assert.Equal(held.Calendar, world.Calendar);
            Assert.Equal(held.AdvancingSteps, world.AdvancingSteps);
            Assert.Equal(LiquidCellStateCodecV1.Encode(held.LiquidCells!), LiquidCellStateCodecV1.Encode(world.LiquidCells!));
            Assert.Equal(held.Residents.Select(resident => resident.ExperiencedTime),
                world.Residents.Select(resident => resident.ExperiencedTime));

            WorldSnapshot closed = world;
            LiquidCellState expected = new LiquidCellPlanner().PlanAdvance(world.LiquidCells!,
                world.Calendar.AddMilliseconds(1800)).ApplyTo(world.LiquidCells!);
            world = authority.Commit(fixture.Materials.Actions.Batch(world, [], [])).Candidate;
            AssertAdvanced(closed, world);
            Assert.Equal(LiquidCellStateCodecV1.Encode(expected), LiquidCellStateCodecV1.Encode(world.LiquidCells!));
            AssertConserved(world);
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
    }

    [Fact]
    public void Water_repacking_refuses_unowned_placement_changes_but_keeps_movement_time_and_one_receipt()
    {
        var fixture = new LiquidCellWorldFixture(primaryReceiver: true);
        WorldSnapshot before = fixture.Genesis;
        EntityId player = Player(before);
        WorldActionCommand filter = fixture.Materials.Craft(before);
        CraftPlan raw = new InstantCraftPlanner().Plan(before.Inventory, before.Production!,
            ConstructionUpgradeFixture.Delivery(before, player), player, filter.CommandId,
            ((CraftIntent)filter.Intent).Request);
        Assert.Equal(WorldActionRejection.None, raw.Rejection);
        WorldMovementInput move = AvailableMovement(before, player);
        WorldStepDecision legacy = new WorldStepProcessor().Decide(fixture.ContainedBasis,
            fixture.Materials.Actions.Batch(fixture.ContainedBasis, [filter], [move]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(legacy.Receipts).Rejection);
        Assert.Single(legacy.Candidate.Production!.Batches);

        using var files = new StorageFiles();
        WorldSnapshot after;
        WorldActionReceipt receipt;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, before))
        {
            WorldStepDecision decision = authority.Commit(fixture.Materials.Actions.Batch(before, [filter], [move]));
            after = decision.Candidate;
            receipt = Assert.Single(decision.Receipts);
            Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection);
            Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
            Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
            Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
            Assert.Equal(before.Space.GetActor(player).Feet.Offset(move.DeltaX, move.DeltaY, 0), after.Space.GetActor(player).Feet);
            Assert.Equal(before.Space.GetActor(player).Revision + 1, after.Space.GetActor(player).Revision);
            AssertAdvanced(before, after);
            AssertBound(after);
            AssertConserved(after);
            Assert.Equal(200UL, Quantity(after, LiquidCellWorldFixture.SourceCell(2, -1)));
            Assert.Equal(0UL, Quantity(after, LiquidCellWorldFixture.SourceCell(2, -2)));
            Assert.DoesNotContain(decision.Identities, identity => raw.CreatedIds.Contains(identity.Identity));
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                authority.FindReceipt(filter, ConstructionUpgradeFixture.Delivery(after, player))!));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Materials.Actions.Batch(after, [filter], [])));
            Assert.Equal(after.ContentHash, authority.InspectForHost().State.ContentHash);
        }
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                reopened.FindReceipt(filter, ConstructionUpgradeFixture.Delivery(after, player))!));
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(0).State));
        }
        using WorldDatabase database = WorldDatabase.Open(files.Path, create: false);
        Assert.Single(database.ReadJournal());
        Assert.Single(database.ReadCommandOutcomes());
        Assert.DoesNotContain(database.ReadIdentities(), identity => raw.CreatedIds.Contains(identity.Identity));
    }

    [Fact]
    public void Real_quest_funding_and_completed_npc_fabrication_rebind_without_relabeling_liquid_sources()
    {
        var fixture = new LiquidCellWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot world = fixture.Materials.Actions.CreateSource(files.Path, completeQuest: true, genesis: fixture.Genesis);
        WorldSnapshot funded = world;
        AssertConserved(world);
        AssertBound(world);
        Assert.NotEqual(fixture.Genesis.Reservoirs!.ContentHash, world.Reservoirs!.ContentHash);
        Assert.Empty(world.Production!.Batches);
        Assert.Null(world.Construction!.Projects.Single().Installed);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            world = fixture.Materials.Actions.Act(authority, world, Player(world),
                new CloseDialogueIntent(Assert.Single(world.Dialogues).DialogueId));
            var controller = new LocalNpcController();
            for (int step = 0; !world.Construction!.Projects.Single().IsCompleted; step++)
            {
                Assert.True(step < 256, "Funded work must finish within the retained finite farm project bound.");
                world = authority.Commit(fixture.Materials.Actions.Batch(world,
                    controller.SelectActions(world, fixture.Materials.Actions.NextIdentity), controller.SelectMovement(world))).Candidate;
                AssertBound(world);
                AssertConserved(world);
            }
            var project = world.Construction!.Projects.Single();
            Assert.Equal(project.Definition.RequiredWork.Value, project.Progress.CompletedUnits);
            Assert.Single(world.Space.Structures);
            ProductionBatch produced = Assert.Single(world.Production!.Batches);
            Assert.Equal(project.Installed!.ProductionBatchId, produced.BatchId);
            Assert.Equal(project.ReservationId, world.Production!.Lots.Single(lot => lot.LotId == project.Installed!.GuardLotId).ReservedBy);
            Assert.NotEqual(funded.Reservoirs!.ContentHash, world.Reservoirs!.ContentHash);
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            foreach (WorldSnapshot expected in new[] { funded, fixture.Genesis, world })
            {
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(expected.Revision).State));
            }
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    private static void AssertBound(WorldSnapshot world)
    {
        Assert.Equal((ushort)20, world.ProfileVersion);
        Assert.Equal(world.Reservoirs!.ContentHash, world.LiquidCells!.ReservoirHash);
        Assert.Equal(world.Inventory.ContentHash, world.Reservoirs!.Capacity.Inventory.ContentHash);
        Assert.Equal(world.Production!.ContentHash, world.Reservoirs!.Capacity.Production!.ContentHash);
        Assert.Equal(world.Calendar, world.LiquidCells!.Boundary);
        Assert.Equal(world.AdvancingSteps, world.LiquidCells!.Revision);
        Assert.Empty(world.Reservoirs!.Capacity.Pending);
    }

    private static void AssertConserved(WorldSnapshot world)
    {
        LiquidCellState cells = world.LiquidCells!;
        Assert.Equal(1350UL, cells.Portions.Aggregate(0UL, (sum, portion) => checked(sum + portion.Quantity)));
        Assert.All(cells.Portions, portion =>
        {
            Assert.Equal(LiquidStockOrigin.RawSource, portion.Origin);
            Assert.Equal(ReservoirWorldFixture.Source, portion.OriginId);
            Assert.Equal(portion.Start < 1300 ? LiquidCellWorldFixture.SourceReservoir : LiquidCellWorldFixture.ReceiverReservoir,
                portion.Cell.ReservoirId);
        });
        ulong cursor = 0;
        foreach (LiquidCellPortion portion in cells.Portions.OrderBy(portion => portion.Start))
        {
            if (cursor == 1300) { cursor = 2000; }
            Assert.Equal(cursor, portion.Start);
            cursor = portion.End;
        }
        Assert.Equal(2050UL, cursor);
        Assert.All(cells.Quantities.Values, units => Assert.InRange(units, 1UL, cells.UnitsPerCell));
    }

    private static void AssertAdvanced(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.Equal(before.LiquidCells!.Revision + 1, after.LiquidCells!.Revision);
        foreach (var resident in before.Residents)
        {
            Assert.Equal(resident.ExperiencedTime.Add(LocalExposureDuration.FromMilliseconds(1800)),
                after.GetResident(resident.ActorId).ExperiencedTime);
        }
    }

    private static ulong Quantity(WorldSnapshot world, LiquidCellKey cell) => world.LiquidCells!.Quantities.GetValueOrDefault(cell);

    private static EntityId Player(WorldSnapshot world) => world.Residents.Single(resident =>
        resident.ControllerKind == ResidentControllerKind.Player).ActorId;

    private static WorldMovementInput AvailableMovement(WorldSnapshot world, EntityId actorId)
    {
        ActorPresence actor = world.Space.GetActor(actorId);
        foreach ((long x, long y) in new (long, long)[] { (32, 0), (-32, 0), (0, 32), (0, -32) })
        {
            if (world.Space.PrepareMove(actorId, actor.Revision, x, y).Status == SpatialMoveStatus.Prepared)
            {
                return new(ConstructionUpgradeFixture.Delivery(world, actorId), actorId, x, y);
            }
        }
        throw new InvalidOperationException("The retained farm actor needs an ordinary clear movement sample.");
    }

    private static WorldSnapshot ApproachSpeaker(LiquidCellWorldFixture fixture, SqliteWorldAuthority authority, WorldSnapshot world)
    {
        EntityId player = Player(world);
        EntityId speaker = world.Quests!.Definitions.Single().GiverActorId;
        for (int step = 0; world.Space.QueryInteraction(player, speaker) != SpatialInteraction.Reachable; step++)
        {
            Assert.True(step < 128, "The actual farm speaker must remain reachable without replacing an actor pose.");
            ActorPresence actor = world.Space.GetActor(player);
            SubunitPosition target = world.Space.GetActor(speaker).Feet;
            long dx = Math.Sign(target.X - actor.Feet.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(target.Y - actor.Feet.Y) * WorldMovementInput.DisplacementPerStep : 0;
            world = authority.Commit(fixture.Materials.Actions.Batch(world, [],
                [new WorldMovementInput(ConstructionUpgradeFixture.Delivery(world, player), player, dx, dy)])).Candidate;
        }
        return world;
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
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
}
