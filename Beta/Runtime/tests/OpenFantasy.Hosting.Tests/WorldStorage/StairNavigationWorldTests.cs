using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class StairNavigationWorldTests
{
    [Fact]
    public void FundedStairsCarryOrdinaryPlayerAndNpcTravelAcrossRestartAndStackedSpace()
    {
        var fixture = new StairNavigationWorldFixture();
        using var files = new StorageFiles();
        var history = new List<WorldSnapshot> { fixture.Genesis };
        WorldSnapshot world = fixture.FundAndBuild(files.Path);
        history.Add(world);
        Assert.Equal(ActorQuestStatus.Completed, world.Quests!.Participations.Single().Status);
        ConstructionProject project = world.Construction!.Projects.Single();
        Assert.NotNull(project.Installed);
        Assert.Equal(1UL, project.Progress.CompletedUnits);
        Assert.Single(world.Production!.Batches);
        Assert.Equal(12, Assert.Single(world.Space.Structures).BearingSurfaces.Count);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, lot => lot.ReservedBy == project.ReservationId);
        Assert.Equal(project.Installed!.GuardLotId,
            Assert.Single(world.Production.Lots, lot => lot.ReservedBy == project.ReservationId).LotId);

        WorldSnapshot beforeStep;
        WorldSnapshot afterStep;
        byte[] retainedStep;
        SubunitPosition retainedWaypoint;
        HashSet<long> playerHeights = [256];
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            world = fixture.WalkTo(authority, world, fixture.Player, StairNavigationWorldFixture.StairApproach);
            for (int step = 0; ; step++)
            {
                Assert.True(step < 128, "The player must reach a real middle-riser transition through ordinary samples.");
                WorldStepBatch next = fixture.NextStep(world, fixture.Player, StairNavigationWorldFixture.UpperDeck,
                    out SubunitPosition waypoint);
                SubunitPosition current = world.Space.GetActor(fixture.Player).Feet;
                if (current.Z >= 512 && waypoint.Z > current.Z)
                {
                    beforeStep = world;
                    retainedStep = WorldStepCodec.Encode(next);
                    retainedWaypoint = waypoint;
                    history.Add(world);
                    break;
                }
                world = fixture.CommitStep(authority, world, next, fixture.Player, waypoint);
                playerHeights.Add(world.Space.GetActor(fixture.Player).Feet.Z);
            }
        }
        RetainBefore(files.Path, retainedStep);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(beforeStep), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            Assert.Equal(retainedStep, WorldStepCodec.Encode(WorldStepCodec.Decode(retainedStep)));
            afterStep = fixture.CommitStep(authority, beforeStep, WorldStepCodec.Decode(retainedStep), fixture.Player, retainedWaypoint);
            history.Add(afterStep);
            playerHeights.Add(afterStep.Space.GetActor(fixture.Player).Feet.Z);
        }
        RetainTransition(fixture, files.Path, beforeStep, afterStep, history);

        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            world = fixture.WalkTo(authority, afterStep, fixture.Player, StairNavigationWorldFixture.UpperDeck,
                state => playerHeights.Add(state.Space.GetActor(fixture.Player).Feet.Z));
            history.Add(world);
            Assert.Equal(Enumerable.Range(0, 10).Select(index => 256L + index * 64), playerHeights.Order());
            var descentHeights = new HashSet<long> { world.Space.GetActor(fixture.Player).Feet.Z };
            world = fixture.WalkTo(authority, world, fixture.Player, StairNavigationWorldFixture.StairApproach,
                state => descentHeights.Add(state.Space.GetActor(fixture.Player).Feet.Z));
            Assert.Equal(playerHeights.Order(), descentHeights.Order());
            history.Add(world);

            world = fixture.WalkTo(authority, world, fixture.Player, StairNavigationWorldFixture.UnderDeck);
            var npcHeights = new HashSet<long> { world.Space.GetActor(fixture.Builder).Feet.Z };
            world = fixture.WalkTo(authority, world, fixture.Builder, StairNavigationWorldFixture.UpperDeck,
                state => npcHeights.Add(state.Space.GetActor(fixture.Builder).Feet.Z));
            Assert.Equal(playerHeights.Order(), npcHeights.Order());
            Assert.Equal(StairNavigationWorldFixture.UnderDeck, world.Space.GetActor(fixture.Player).Feet);
            Assert.Equal(StairNavigationWorldFixture.UpperDeck, world.Space.GetActor(fixture.Builder).Feet);
            Assert.Equal(SpatialOccupancy.Clear, world.Space.QueryOccupancy(fixture.Player, StairNavigationWorldFixture.UnderDeck));
            Assert.Equal(SpatialOccupancy.Clear, world.Space.QueryOccupancy(fixture.Builder, StairNavigationWorldFixture.UpperDeck));
            Assert.True(new LocalConstructionController().NeedsReturnHome(world, world.GetResident(fixture.Builder)));
            history.Add(world);

            world = fixture.WalkTo(authority, world, fixture.Player, StairNavigationWorldFixture.PlayerParking);
            var controller = new LocalNpcController();
            var returnHeights = new HashSet<long> { world.Space.GetActor(fixture.Builder).Feet.Z };
            for (int step = 0; world.Space.GetActor(fixture.Builder).Feet != StairNavigationWorldFixture.UnderDeck; step++)
            {
                Assert.True(step < 256, "The completed builder must route downstairs to its home at the same XY.");
                Assert.Empty(controller.SelectActions(world, fixture.Actions.NextIdentity));
                WorldMovementInput movement = Assert.Single(controller.SelectMovement(world));
                Assert.Equal(fixture.Builder, movement.ActorId);
                ActorPresence actor = world.Space.GetActor(fixture.Builder);
                SpatialMoveResult prepared = world.Space.PrepareMove(fixture.Builder, actor.Revision, movement.DeltaX, movement.DeltaY);
                Assert.Equal(SpatialMoveStatus.Prepared, prepared.Status);
                world = fixture.CommitStep(authority, world, fixture.Actions.Batch(world, [], [movement]),
                    fixture.Builder, prepared.Candidate!.Feet);
                returnHeights.Add(world.Space.GetActor(fixture.Builder).Feet.Z);
            }
            Assert.Equal(playerHeights.Order(), returnHeights.Order());
            Assert.False(new LocalConstructionController().NeedsReturnHome(world, world.GetResident(fixture.Builder)));
            Assert.Empty(controller.SelectMovement(world));
            Assert.Equal(1UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
            history.Add(world);
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            foreach (WorldSnapshot expected in history.AsEnumerable().Reverse())
            {
                RecordedWorldState recorded = authority.ReadRecordedStateForHost(expected.Revision);
                Assert.Equal(world.Revision, recorded.SourceHeadRevision);
                Assert.Equal(world.ContentHash, recorded.SourceHeadHash);
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded.State));
                Assert.Equal(GroundSupportMode.SteppedTerrainAndConstruction, recorded.State.Space.GroundSupport);
            }
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
        RetainJourney(fixture, files.Path, history, playerHeights);
    }

    [Fact]
    public void SteppedAdmissionPreservesProfileElevenBytesAndRejectsPlayedReadmission()
    {
        var fixture = new StairNavigationWorldFixture();
        byte[] legacy = WorldSnapshotCodec.Encode(fixture.LegacyGenesis);
        Assert.Equal((ushort)11, fixture.LegacyGenesis.ProfileVersion);
        Assert.Equal(legacy, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(legacy)));
        Assert.Equal((ushort)12, fixture.Genesis.ProfileVersion);
        Assert.Empty(fixture.Genesis.Space.Structures);
        Assert.NotEqual(GroundRouteStatus.Reachable,
            fixture.Genesis.Space.QueryWalkingRoute(fixture.Player, StairNavigationWorldFixture.UpperDeck).Status);
        byte[] stepped = WorldSnapshotCodec.Encode(fixture.Genesis);
        Assert.Equal(stepped, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(stepped)));
        Assert.Equal(legacy, WorldSnapshotCodec.Encode(fixture.LegacyGenesis));
        WorldSnapshot played = new WorldStepProcessor().Decide(fixture.LegacyGenesis,
            fixture.Actions.Batch(fixture.LegacyGenesis, [], [])).Candidate;
        Assert.Throws<ArgumentException>(() => WorldSteppedSupportAdmission.CreateGenesis(played));
        Assert.Throws<ArgumentException>(() => WorldSteppedSupportAdmission.CreateGenesis(fixture.Genesis));
    }

    private static void RetainBefore(string path, byte[] step)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.Copy(path, Path.Combine(directory, "midstairs.sqlite"), overwrite: true);
        File.WriteAllBytes(Path.Combine(directory, "ordinary-step.bin"), step);
    }

    private static void RetainTransition(StairNavigationWorldFixture fixture, string path,
        WorldSnapshot before, WorldSnapshot after, IEnumerable<WorldSnapshot> history)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.Copy(path, Path.Combine(directory, "post-step.sqlite"), overwrite: true);
        File.WriteAllBytes(Path.Combine(directory, "genesis.bin"), WorldSnapshotCodec.Encode(fixture.Genesis));
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Actual funded nine-tread assembly, ordinary mid-stair movement across a riser, with no climb action or effort cost",
            ProfileVersion = 12,
            Player = fixture.Player.ToString(),
            Actor = fixture.Player.ToString(),
            PreRevision = before.Revision,
            PreHash = before.ContentHash.Value,
            PostRevision = after.Revision,
            PostHash = after.ContentHash.Value,
            PreFeet = before.Space.GetActor(fixture.Player).Feet,
            PostFeet = after.Space.GetActor(fixture.Player).Feet,
            EffortBefore = before.Gathering!.GetActorWork(fixture.Player).Effort,
            EffortAfter = after.Gathering!.GetActorWork(fixture.Player).Effort,
            History = history.DistinctBy(state => state.Revision).Select(state => new { state.Revision, Hash = state.ContentHash.Value })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string? EvidenceDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_STAIR_NAVIGATION_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) { return null; }
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void RetainJourney(StairNavigationWorldFixture fixture, string path,
        IEnumerable<WorldSnapshot> history, IEnumerable<long> visitedHeights)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.Copy(path, Path.Combine(directory, "completed-journey.sqlite"), overwrite: true);
        File.WriteAllText(Path.Combine(directory, "journey.json"), JsonSerializer.Serialize(new
        {
            Player = fixture.Player.ToString(),
            Builder = fixture.Builder.ToString(),
            VisitedStandingHeights = visitedHeights.Order(),
            UnderpassClearanceSubunits = 512,
            BodyHeightSubunits = fixture.Genesis.Space.GetActor(fixture.Player).Condition.Definition.ClearanceDimensions.HeightSubunits,
            States = history.DistinctBy(state => state.Revision).Select(state => new
            {
                state.Revision,
                Hash = state.ContentHash.Value,
                PlayerFeet = state.Space.GetActor(fixture.Player).Feet,
                BuilderFeet = state.Space.GetActor(fixture.Builder).Feet,
                CompletedWork = state.Construction!.Projects.Single().Progress.CompletedUnits
            })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
