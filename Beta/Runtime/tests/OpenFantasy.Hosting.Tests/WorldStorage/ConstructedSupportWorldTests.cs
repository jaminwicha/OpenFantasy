using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConstructedSupportWorldTests
{
    [Fact]
    public void FundedNpcConstructionSupportsAnEarnedClimbThroughRestartAndExactHistory()
    {
        var fixture = new ConstructedSupportWorldFixture();
        using var files = new StorageFiles();
        var states = new List<WorldSnapshot> { fixture.Genesis };
        WorldSnapshot world = fixture.Actions.CreateSource(files.Path, completeQuest: true, genesis: fixture.Genesis);
        states.Add(world);
        Assert.Equal(ActorQuestStatus.Completed, world.Quests!.Participations.Single().Status);
        ConstructionProject project = world.Construction!.Projects.Single();
        Assert.Equal(6UL, world.Inventory.Custody.Lots.Where(l => l.ReservedBy == project.ReservationId)
            .Aggregate(0UL, (total, lot) => total + lot.Quantity));
        Assert.Null(project.Installed);
        Assert.Empty(world.Space.Structures);
        WorldActionCommand climb;
        WorldActionReceipt climbReceipt;
        WorldSnapshot beforeClimb;
        byte[] climbStep;
        ulong builtRevision;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            uint heldEffort = world.Gathering!.GetActorWork(fixture.Player).Effort;
            var heldFeet = world.Space.GetActor(fixture.Player).Feet;
            var heldCalendar = world.Calendar;
            var heldAttempt = authority.Commit(fixture.Actions.Batch(world,
                [fixture.Command(world, new ClimbIntent(256, 0, 64))], []));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(heldAttempt.Receipts).Rejection);
            world = heldAttempt.Candidate;
            Assert.Equal(heldEffort, world.Gathering!.GetActorWork(fixture.Player).Effort);
            Assert.Equal(heldFeet, world.Space.GetActor(fixture.Player).Feet);
            Assert.Equal(heldCalendar, world.Calendar);
            world = fixture.Actions.Act(authority, world, fixture.Player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
            world = fixture.MoveTo(authority, world, ConstructedSupportWorldFixture.PlatformApproach);
            world = fixture.EarnEffort(authority, world);
            states.Add(world);

            uint beforeRefusal = world.Gathering!.GetActorWork(fixture.Player).Effort;
            var unbuilt = authority.Commit(fixture.Actions.Batch(world,
                [fixture.Command(world, new ClimbIntent(256, 0, 64))], []));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(unbuilt.Receipts).Rejection);
            world = unbuilt.Candidate;
            Assert.Equal(ConstructedSupportWorldFixture.PlatformApproach, world.Space.GetActor(fixture.Player).Feet);
            Assert.Equal(Math.Min(GatherRulesV1.MaximumEffort, beforeRefusal + GatherRulesV1.EffortPerAdvancingStep),
                world.Gathering!.GetActorWork(fixture.Player).Effort);
            Assert.Empty(world.Space.Structures);
            states.Add(world);

            world = fixture.BuildPlatform(authority, world);
            builtRevision = world.Revision;
            states.Add(world);
            project = world.Construction!.Projects.Single();
            InstalledStructure installed = Assert.IsType<InstalledStructure>(project.Installed);
            Assert.Equal(1UL, project.Revision);
            Assert.Equal(1UL, project.Progress.CompletedUnits);
            Assert.Single(world.Production!.Batches);
            Assert.Single(Assert.Single(world.Space.Structures).BearingSurfaces);
            Assert.DoesNotContain(world.Inventory.Custody.Lots, l => l.ReservedBy == project.ReservationId);
            var output = world.Production.Lots.Single(l => l.LotId == installed.GuardLotId);
            Assert.Equal(project.ReservationId, output.ReservedBy);
            Assert.Equal(installed.ProductionBatchId, output.Output.BatchId);

            SpatialStructure structure = world.Space.Structures.Single();
            var omittedSurface = new SpatialStructure(structure.StructureId, structure.Boxes);
            Assert.Throws<ArgumentException>(() => ConstructedSupportWorldFixture.WithSpace(world,
                world.Space.WithStructures([omittedSurface])));

            world = fixture.EarnEffort(authority, world);
            beforeClimb = world;
            states.Add(world);
            climb = fixture.Command(world, new ClimbIntent(256, 0, 64));
            var climbBatch = fixture.Actions.Batch(world, [climb],
                [new(ConstructionUpgradeFixture.Delivery(world, fixture.Player), fixture.Player, 0, 32)]);
            climbStep = WorldStepCodec.Encode(climbBatch);
            Assert.Equal(climbStep, WorldStepCodec.Encode(WorldStepCodec.Decode(climbStep)));
        }
        RetainBeforeClimb(files.Path, climbStep);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(beforeClimb), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            var climbed = authority.Commit(WorldStepCodec.Decode(climbStep));
            climbReceipt = Assert.Single(climbed.Receipts);
            Assert.Equal(WorldActionRejection.None, climbReceipt.Rejection);
            world = climbed.Candidate;
            states.Add(world);
            Assert.Equal(ConstructedSupportWorldFixture.PlatformLanding, world.Space.GetActor(fixture.Player).Feet);
            Assert.Equal(beforeClimb.Space.GetActor(fixture.Player).Revision + 1, world.Space.GetActor(fixture.Player).Revision);
            Assert.Equal(beforeClimb.Gathering!.GetActorWork(fixture.Player).Effort
                - GatherRulesV1.EffortPerSwing + GatherRulesV1.EffortPerAdvancingStep,
                world.Gathering!.GetActorWork(fixture.Player).Effort);
            Assert.Equal(SpatialOccupancy.Clear, world.Space.QueryOccupancy(fixture.Player,
                ConstructedSupportWorldFixture.PlatformLanding));
            Assert.Equal(GroundSupportMode.TerrainAndConstruction, world.Space.GroundSupport);
            Assert.Equal((ushort)11, world.ProfileVersion);
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world))));
        }

        byte[] databaseBytes = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            foreach (WorldSnapshot expected in states.AsEnumerable().Reverse().Concat([states[0], world]))
            {
                RecordedWorldState recorded = reopened.ReadRecordedStateForHost(expected.Revision);
                Assert.Equal(world.Revision, recorded.SourceHeadRevision);
                Assert.Equal(world.ContentHash, recorded.SourceHeadHash);
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded.State));
                Assert.Equal(GroundSupportMode.TerrainAndConstruction, recorded.State.Space.GroundSupport);
            }
            var recovered = Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(climb,
                ConstructionUpgradeFixture.Delivery(world, fixture.Player)));
            Assert.Equal(WorldReceiptCodecV1.Encode(climbReceipt), WorldReceiptCodecV1.Encode(recovered));
        }
        Assert.Equal(databaseBytes, File.ReadAllBytes(files.Path));
        Retain(fixture, files.Path, states, builtRevision, beforeClimb, climb, climbReceipt);
    }

    [Fact]
    public void FreshSupportAdmissionPreservesLegacyBytesAndRefusesUnfundedSurfaces()
    {
        var fixture = new ConstructedSupportWorldFixture();
        byte[] legacy = WorldSnapshotCodec.Encode(fixture.ExcavationGenesis);
        Assert.Equal((ushort)10, fixture.ExcavationGenesis.ProfileVersion);
        Assert.Equal(GroundSupportMode.TerrainOnly, fixture.ExcavationGenesis.Space.GroundSupport);
        WorldSnapshot admitted = WorldConstructedSupportAdmission.CreateGenesis(fixture.ExcavationGenesis);
        Assert.Equal((ushort)11, admitted.ProfileVersion);
        Assert.Equal(legacy, WorldSnapshotCodec.Encode(fixture.ExcavationGenesis));
        Assert.Equal(legacy, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(legacy)));
        Assert.Equal(WorldSnapshotCodec.Encode(admitted), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(admitted))));
        Assert.Throws<ArgumentException>(() => fixture.Actions.Batch(fixture.ExcavationGenesis,
            [fixture.Command(fixture.ExcavationGenesis, new ClimbIntent(256, 0, 64))], []));
        var progressed = new WorldStepProcessor().Decide(fixture.ExcavationGenesis,
            fixture.Actions.Batch(fixture.ExcavationGenesis, [], [])).Candidate;
        Assert.Throws<ArgumentException>(() => WorldConstructedSupportAdmission.CreateGenesis(progressed));

        ConstructionProject unbuilt = admitted.Construction!.Projects.Single();
        SpatialStructure invented = unbuilt.Definition.CreateStructure(ConstructedSupportWorldFixture.Id(100),
            unbuilt.SiteFeet, supportsActors: true);
        Assert.Throws<ArgumentException>(() => ConstructedSupportWorldFixture.WithSpace(admitted,
            admitted.Space.WithStructures([invented])));
        Assert.Empty(admitted.Space.Structures);
        Assert.Null(unbuilt.Installed);
    }

    [Fact]
    public void SafeEarnedExcavationPreservesConstructedSupportInterpretationThroughRecovery()
    {
        var fixture = new ConstructedSupportWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot world;
        ulong beforeExcavation;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            world = fixture.MoveTo(authority, fixture.Genesis, ConstructedSupportWorldFixture.ExcavationApproach);
            world = fixture.EarnEffort(authority, world);
            world = fixture.Actions.Act(authority, world, fixture.Player, new ClimbIntent(-256, 0, -256));
            Assert.Equal(fixture.Site.Stance, world.Space.GetActor(fixture.Player).Feet);
            world = fixture.EarnEffort(authority, world);
            beforeExcavation = world.Revision;
            world = fixture.Actions.Act(authority, world, fixture.Player, fixture.ExcavationIntent(world));
            Assert.True(world.Excavation!.Sites.Single().IsComplete);
            Assert.True(world.Space.Regions.Single(r => r.Bounds.Contains(fixture.Site.Cell)).GetCell(fixture.Site.Cell).IsExcavated);
            Assert.Equal(1UL, world.Inventory.Custody.Lots.Single(l => l.Source.SourceId == fixture.Site.CountedSourceId).Quantity);
            Assert.Equal(GroundSupportMode.TerrainAndConstruction, world.Space.GroundSupport);
            Assert.Equal((ushort)11, world.ProfileVersion);
        }
        byte[] databaseBytes = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            WorldSnapshot earlier = reopened.ReadRecordedStateForHost(beforeExcavation).State;
            Assert.False(earlier.Excavation!.Sites.Single().IsComplete);
            Assert.DoesNotContain(earlier.Inventory.Custody.Lots, l => l.Source.SourceId == fixture.Site.CountedSourceId);
            Assert.Equal(GroundSupportMode.TerrainAndConstruction, earlier.Space.GroundSupport);
        }
        Assert.Equal(databaseBytes, File.ReadAllBytes(files.Path));
    }

    private static void RetainBeforeClimb(string databasePath, byte[] climbStep)
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_CONSTRUCTED_SUPPORT_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        File.Copy(databasePath, Path.Combine(directory, "pre-climb.sqlite"), overwrite: true);
        File.WriteAllBytes(Path.Combine(directory, "climb-step.bin"), climbStep);
    }

    private static void Retain(ConstructedSupportWorldFixture fixture, string databasePath,
        IReadOnlyList<WorldSnapshot> states, ulong builtRevision, WorldSnapshot beforeClimb,
        WorldActionCommand climb, WorldActionReceipt receipt)
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_CONSTRUCTED_SUPPORT_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "genesis.bin"), WorldSnapshotCodec.Encode(fixture.Genesis));
        File.Copy(databasePath, Path.Combine(directory, "constructed-support.sqlite"), overwrite: true);
        WorldSnapshot afterClimb = states[^1];
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Actual funded quest and NPC platform construction, earned explicit climb, exact actor support and recorded history",
            ProfileVersion = 11,
            Player = fixture.Player.ToString(),
            BuiltRevision = builtRevision,
            PreRevision = beforeClimb.Revision,
            PreHash = beforeClimb.ContentHash.Value,
            PostRevision = afterClimb.Revision,
            PostHash = afterClimb.ContentHash.Value,
            PreFeet = beforeClimb.Space.GetActor(fixture.Player).Feet,
            PostFeet = afterClimb.Space.GetActor(fixture.Player).Feet,
            EffortBefore = beforeClimb.Gathering!.GetActorWork(fixture.Player).Effort,
            EffortAfter = afterClimb.Gathering!.GetActorWork(fixture.Player).Effort,
            CommandId = climb.CommandId.ToString(),
            ReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)).ToLowerInvariant(),
            Approach = ConstructedSupportWorldFixture.PlatformApproach,
            Landing = ConstructedSupportWorldFixture.PlatformLanding,
            History = states.DistinctBy(s => s.Revision).Select(s => new { s.Revision, Hash = s.ContentHash.Value }),
            States = states.Select(s => new
            {
                s.Revision,
                WorldHash = s.ContentHash.Value,
                Feet = s.Space.GetActor(fixture.Player).Feet,
                Effort = s.Gathering!.GetActorWork(fixture.Player).Effort,
                Structures = s.Space.Structures.Count,
                GroundSupport = s.Space.GroundSupport.ToString()
            }),
            ClimbCommandHex = Convert.ToHexString(WorldActionCodec.Encode(climb)).ToLowerInvariant(),
            ClimbReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)).ToLowerInvariant()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
