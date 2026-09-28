using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class CareContinuationAdmissionTests
{
    [Theory]
    [InlineData(false, "1844f97105a8144c3076c8edf7dbe1d12917dfc6e90410511385ad1d33001634")]
    [InlineData(true, "9c897e368ad8f0bf3d2e524104401ae997ac3b93c65e013442f5e5d43b20548c")]
    public void Authored_world_frames_preserve_the_precontinuation_bytes(bool ordinary, string hash)
    {
        var world = CreateWorld(ordinary);
        Assert.Equal(hash, world.ContentHash.Value);
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        if (Environment.GetEnvironmentVariable("OPENFANTASY_CARE_COMPATIBILITY_EVIDENCE") is { } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "world-" + world.ProfileVersion + ".bin"), bytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Component_care_closure_cannot_silently_upgrade_an_existing_world(bool ordinary)
    {
        var world = CreateWorld(ordinary);
        byte[] original = WorldSnapshotCodec.Encode(world);
        var continued = CareContinuationContext.Capture(world.Care!).Continue();

        // Empty care can need a cut because a different material owner is full. Even this
        // otherwise unchanged component must not acquire authority in a legacy world.
        Assert.Equal(world.Care!.Revision, continued.Revision);
        Assert.NotNull(continued.Continuation);
        var error = Assert.Throws<ArgumentException>(() => RebindCare(world, continued));
        Assert.Equal("care", error.ParamName);
        Assert.Equal(original, WorldSnapshotCodec.Encode(world));
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(original)));
    }

    private static WorldSnapshot CreateWorld(bool ordinary) => ordinary
        ? RestorativeJourneyAdmission.CreateEmbedded(EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName)
        : CareJourneyAdmission.CreateEmbedded(EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName);

    private static WorldSnapshot RebindCare(WorldSnapshot world, CareSnapshot care) => new(
        world.Inventory, world.Space, world.Residents, world.AdmissionVersion, world.Calendar,
        world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production,
        world.Progression, world.Quests, world.Construction, reservoirs: world.Reservoirs,
        excavation: world.Excavation, reservoirSeals: world.ReservoirSeals, liquidCells: world.LiquidCells,
        pacing: world.Pacing, encounterLifecycle: world.EncounterLifecycle, care: care,
        resourceHistory: world.ResourceHistory);
}
