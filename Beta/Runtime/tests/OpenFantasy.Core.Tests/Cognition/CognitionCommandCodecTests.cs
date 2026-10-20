using OpenFantasy.Core.Cognition;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Cognition.CognitionFixture;

namespace OpenFantasy.Core.Tests.Cognition;

/// <summary>The recorded decision frame and the profile-42 action version that carries it.</summary>
public sealed class CognitionCommandCodecTests
{
    private static WorldActionCommand Command(WorldActionIntent intent, WorldControlWitness? witness = null) =>
        new(Id(900), Id(901), Id(902), Wren, World, Branch, 7, intent, witness);

    [Fact]
    public void A_recorded_decision_round_trips_canonically_with_and_without_an_intention()
    {
        var gather = new CognitionIntention(IntentionKind.GatherPersonalStock, Source, At(1000), [Observed]);
        foreach (CognitionDecision decision in new[]
        {
            Decision(gather, CognitionDeferral.AwaitingSupply, step: 40),
            Decision(null, CognitionDeferral.None, step: 9, completed: true),
            Decision(new CognitionIntention(IntentionKind.AwaitSupply, Project, At(0), []), CognitionDeferral.MissingTool)
                with { PolicyVersion = "cognition.delayed-supply.v1-variant" },
        })
        {
            byte[] bytes = CognitionCodecV2.EncodeDecision(decision);
            CognitionDecision restored = CognitionCodecV2.DecodeDecision(bytes);
            Assert.Equal(bytes, CognitionCodecV2.EncodeDecision(restored));
            Assert.Equal(decision.PolicyVersion, restored.PolicyVersion);
            Assert.Equal(decision.Deferral, restored.Deferral);
            Assert.Equal(decision.NextWake, restored.NextWake);
            Assert.Equal(decision.CompletedCurrent, restored.CompletedCurrent);
            Assert.Equal(decision.Intention?.Basis, restored.Intention?.Basis);
        }
        byte[] valid = CognitionCodecV2.EncodeDecision(Decision(gather, CognitionDeferral.AwaitingSupply));
        Assert.Throws<FormatException>(() => CognitionCodecV2.DecodeDecision([.. valid, 0]));
        Assert.Throws<FormatException>(() => CognitionCodecV2.DecodeDecision(valid[..^1]));
    }

    [Fact]
    public void Cognition_actions_encode_canonically_as_version_twenty_five()
    {
        var decide = new RecordCognitionDecisionIntent(3, Decision(null, CognitionDeferral.None, step: 12));
        var inspect = new InspectGatherSourceIntent(Source, 4);
        foreach (WorldActionIntent intent in new WorldActionIntent[] { decide, inspect })
        {
            byte[] bytes = WorldActionCodec.Encode(Command(intent));
            Assert.Equal(25UL, WorldCodecVersion.Read(bytes, "world.actor-action"u8, WorldActionCodec.MaximumCommandByteLength, 10));
            WorldActionCommand restored = WorldActionCodec.Decode(bytes);
            Assert.Equal(bytes, WorldActionCodec.Encode(restored));
            Assert.Equal(intent.GetType(), restored.Intent.GetType());
            Assert.Throws<FormatException>(() => WorldActionCodec.Decode([.. bytes, 0]));
        }
        var restoredDecision = (RecordCognitionDecisionIntent)WorldActionCodec.Decode(WorldActionCodec.Encode(Command(decide))).Intent;
        Assert.Equal(3UL, restoredDecision.ExpectedCognitionRevision);
        Assert.Equal(decide.Decision.NextWake, restoredDecision.Decision.NextWake);
        var restoredInspection = (InspectGatherSourceIntent)WorldActionCodec.Decode(WorldActionCodec.Encode(Command(inspect))).Intent;
        Assert.Equal(inspect, restoredInspection);
    }

    [Fact]
    public void A_decision_command_cannot_carry_an_invalid_decision()
    {
        Assert.Throws<ArgumentException>(() => new RecordCognitionDecisionIntent(0,
            Decision(null, CognitionDeferral.None) with { PolicyVersion = "bad\nversion" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecordCognitionDecisionIntent(0,
            Decision(null, CognitionDeferral.None) with { NextWake = new CognitionWake(5, (WakeCause)99) }));
    }
}
