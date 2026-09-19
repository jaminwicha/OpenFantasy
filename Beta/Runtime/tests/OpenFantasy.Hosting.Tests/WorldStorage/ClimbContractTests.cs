using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ClimbContractTests
{
    [Fact]
    public void Earned_climb_changes_feet_and_effort_together_and_suppresses_same_step_movement()
    {
        var f = new ExcavationWorldFixture();
        var world = f.Earn(WorldConstructedSupportAdmission.CreateGenesis(f.Genesis));
        var command = Command(f, world);
        byte[] encoded = WorldActionCodec.Encode(command);
        Assert.Equal(encoded, WorldActionCodec.Encode(WorldActionCodec.Decode(encoded)));
        var movement = new WorldMovementInput(ConstructionUpgradeFixture.Delivery(world, f.Player), f.Player, -32, 0);
        var batch = f.Actions.Batch(world, [command], [movement]);
        byte[] step = WorldStepCodec.Encode(batch);
        var result = new WorldStepProcessor().Decide(world, WorldStepCodec.Decode(step));
        Assert.Equal(WorldActionRejection.None, Assert.Single(result.Receipts).Rejection);
        Assert.Equal(new SubunitPosition(128, 384, 256), result.Candidate.Space.GetActor(f.Player).Feet);
        Assert.Equal(world.Space.GetActor(f.Player).Revision + 1, result.Candidate.Space.GetActor(f.Player).Revision);
        Assert.Equal(GatherRulesV1.EffortPerAdvancingStep, result.Candidate.Gathering!.GetActorWork(f.Player).Effort);
        Assert.Equal(world.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(world.Excavation!.ContentHash, result.Candidate.Excavation!.ContentHash);
        byte[] saved = WorldSnapshotCodec.Encode(result.Candidate);
        Assert.Equal(saved, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(saved)));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(f.Actions.NextIdentity(), world.WorldId,
            world.BranchId, world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [command], 10));
    }

    [Theory]
    [InlineData("effort", WorldActionRejection.Unavailable)]
    [InlineData("revision", WorldActionRejection.RevisionConflict)]
    [InlineData("authority", WorldActionRejection.NotAuthorized)]
    public void Refused_climb_cannot_spend_effort_or_move_the_actor(string fault, WorldActionRejection expected)
    {
        var f = new ExcavationWorldFixture();
        var world = WorldConstructedSupportAdmission.CreateGenesis(f.Genesis);
        if (fault != "effort") world = f.Earn(world);
        var original = Command(f, world);
        var command = new WorldActionCommand(original.CommandId, original.PrincipalId,
            fault == "authority" ? f.Actions.NextIdentity() : original.OriginSessionId,
            original.ActorId, original.WorldId, original.BranchId,
            fault == "revision" ? 100UL : original.ExpectedActorRevision, original.Intent);
        var result = f.Step(world, command);
        Assert.Equal(expected, Assert.Single(result.Receipts).Rejection);
        Assert.Equal(world.Space.GetActor(f.Player).Feet, result.Candidate.Space.GetActor(f.Player).Feet);
        Assert.Equal(world.Gathering!.GetActorWork(f.Player).Effort + GatherRulesV1.EffortPerAdvancingStep,
            result.Candidate.Gathering!.GetActorWork(f.Player).Effort);
    }

    [Fact]
    public void Reading_hold_cannot_climb_or_recover_effort_and_live_profile_cannot_be_readmitted()
    {
        var f = new ExcavationWorldFixture();
        var world = f.Earn(WorldConstructedSupportAdmission.CreateGenesis(f.Genesis));
        var other = world.Residents.Single(r => r.ActorId != f.Player).ActorId;
        var c = Command(f, world);
        c = new(c.CommandId, c.PrincipalId, c.OriginSessionId, c.ActorId, c.WorldId, c.BranchId,
            c.ExpectedActorRevision, new SpeakToActorIntent(other, world.Space.GetActor(other).Revision));
        world = f.Step(world, c).Candidate;
        Assert.True(world.HasReadingHold);
        var result = f.Step(world, Command(f, world));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(result.Receipts).Rejection);
        Assert.Equal(world.Calendar, result.Candidate.Calendar);
        Assert.Equal(world.Gathering!.ContentHash, result.Candidate.Gathering!.ContentHash);
        Assert.Equal(world.Space.GetActor(f.Player).Feet, result.Candidate.Space.GetActor(f.Player).Feet);
        Assert.Throws<ArgumentException>(() => WorldConstructedSupportAdmission.CreateGenesis(f.Earn(f.Genesis)));
    }

    [Fact]
    public void Successful_excavation_preserves_the_explicit_support_rules()
    {
        var f = new ExcavationWorldFixture();
        var world = WorldConstructedSupportAdmission.CreateGenesis(f.Genesis);
        for (int i = 0; i < 3; i++)
        {
            world = f.Earn(world);
            var decision = f.Step(world, f.Command(world));
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            world = decision.Candidate;
        }
        Assert.True(world.Excavation!.Sites.Single().IsComplete);
        Assert.Equal(GroundSupportMode.TerrainAndConstruction, world.Space.GroundSupport);
        Assert.Equal((ushort)11, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world)).ProfileVersion);
    }

    private static WorldActionCommand Command(ExcavationWorldFixture fixture, WorldSnapshot world)
    {
        var delivery = ConstructionUpgradeFixture.Delivery(world, fixture.Player);
        return new(fixture.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId, fixture.Player,
            world.WorldId, world.BranchId, world.Space.GetActor(fixture.Player).Revision, new ClimbIntent(0, 256, 256));
    }
}
