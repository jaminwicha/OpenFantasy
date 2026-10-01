using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Tests.Encounters.Retreat;
using static OpenFantasy.Core.Tests.Encounters.EncounterLifecycleFixture;

namespace OpenFantasy.Core.Tests.Worlds.Retreat;

public sealed class RetreatProfileBoundaryTests
{
    private static WorldStepBatch Batch(ushort profile, WorldBatchKind kind, params WorldActionCommand[] actions) =>
        new(Id(77_000), World, Branch, 12, 4, kind, [], actions, profile);

    [Fact]
    public void Only_the_explicit_retreat_profile_interprets_a_retreat_and_only_at_a_decision_boundary()
    {
        WorldActionCommand retreat = EncounterRetreatContractTests.Command();
        foreach (ushort previous in new ushort[] { 22, 23, 24, 25, 26, 27, 28, 30, 32, 33 })
        {
            Assert.Throws<ArgumentException>(() => Batch(previous, WorldBatchKind.EncounterDecision, retreat));
        }
        foreach (WorldBatchKind kind in new[] { WorldBatchKind.AdvancingStep, WorldBatchKind.EncounterLifecycle,
            WorldBatchKind.EncounterResolution, WorldBatchKind.TacticalAdvance, WorldBatchKind.Care })
        {
            Assert.Throws<ArgumentException>(() => Batch(WorldEncounterRetreatAdmission.ProfileVersion, kind, retreat));
        }
        WorldStepBatch accepted = Batch(WorldEncounterRetreatAdmission.ProfileVersion, WorldBatchKind.EncounterDecision, retreat);
        byte[] bytes = WorldStepCodec.Encode(accepted);
        WorldStepBatch restored = WorldStepCodec.Decode(bytes);
        Assert.Equal(WorldEncounterRetreatAdmission.ProfileVersion, restored.ProfileVersion);
        Assert.Equal(bytes, WorldStepCodecV34.Encode(restored));
        Assert.IsType<QueueEncounterRetreatIntent>(Assert.Single(restored.Actions).Intent);
        // A profile-34 batch never decodes under the profile-33 interpretation, and the reverse.
        Assert.Throws<FormatException>(() => WorldStepCodecV33.Decode(bytes));
        byte[] older = WorldStepCodec.Encode(Batch(33, WorldBatchKind.EncounterDecision, new WorldActionCommand(Id(902), Id(800),
            Id(801), Id(100), World, Branch, 3, new QueueEncounterWaitIntent(Id(10_000), 7))));
        Assert.Throws<FormatException>(() => WorldStepCodecV34.Decode(older));
    }

    [Fact]
    public void The_retreat_profile_keeps_every_existing_encounter_and_exploration_boundary()
    {
        var wait = new WorldActionCommand(Id(903), Id(800), Id(801), Id(100), World, Branch, 3, new QueueEncounterWaitIntent(Id(10_000), 7));
        ushort profile = WorldEncounterRetreatAdmission.ProfileVersion;
        Assert.Single(Batch(profile, WorldBatchKind.EncounterDecision, wait).Actions);
        Assert.Empty(Batch(profile, WorldBatchKind.TacticalAdvance).Actions);
        Assert.Empty(Batch(profile, WorldBatchKind.EncounterResolution).Actions);
        Assert.Empty(Batch(profile, WorldBatchKind.EncounterLifecycle).Actions);
        Assert.Empty(Batch(profile, WorldBatchKind.AdvancingStep).Actions);
        // A retreat and a Wait by different actors may be selected together; one actor never chooses twice.
        var other = new WorldActionCommand(Id(904), Id(810), Id(811), Id(101), World, Branch, 3, new QueueEncounterWaitIntent(Id(10_000), 7));
        Assert.Equal(2, Batch(profile, WorldBatchKind.EncounterDecision, EncounterRetreatContractTests.Command(), other).Actions.Count);
        Assert.Throws<ArgumentException>(() => Batch(profile, WorldBatchKind.EncounterDecision, EncounterRetreatContractTests.Command(), wait));
    }
}
