using System.Text.Json;
using OpenFantasy.Core.Cognition;
using OpenFantasy.Core.Content;
using static OpenFantasy.Core.Tests.Cognition.CognitionFixture;

namespace OpenFantasy.Core.Tests.Cognition;

public sealed class CognitionWireTests
{
    [Fact]
    public void CognitionFramesMatchTheIndependentPythonOracle()
    {
        Dictionary<string, byte[]> encoded = new()
        {
            ["empty"] = CognitionCodecV1.Encode(new CognitionSnapshot(World, Branch, 0, [])),
            ["wren_working"] = CognitionCodecV1.Encode(WrenWorking()),
        };
        using JsonDocument vectors = Load();
        foreach (JsonElement item in vectors.RootElement.GetProperty("valid").EnumerateArray())
        {
            byte[] expected = Convert.FromHexString(item.GetProperty("hex").GetString()!);
            Assert.Equal(item.GetProperty("sha256").GetString(), ContentHash.Compute(expected).Value);
            Assert.Equal(expected, encoded[item.GetProperty("name").GetString()!]);
            Assert.Equal(expected, CognitionCodecV1.Encode(CognitionCodecV1.Decode(expected)));
        }
    }

    [Fact]
    public void MalformedOrDishonestFramesNeverAcquireCognitiveMeaning()
    {
        using JsonDocument vectors = Load();
        int count = 0;
        foreach (JsonElement item in vectors.RootElement.GetProperty("invalid").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(item.GetProperty("hex").GetString()!);
            Assert.Equal(item.GetProperty("sha256").GetString(), ContentHash.Compute(bytes).Value);
            FormatException error = Assert.Throws<FormatException>(() => CognitionCodecV1.Decode(bytes));
            Assert.NotNull(error.Message);
            count++;
        }
        Assert.Equal(27, count);
    }

    [Fact]
    public void DecodedCognitionPreservesProvenanceAndChronology()
    {
        CognitionSnapshot decoded = CognitionCodecV1.Decode(CognitionCodecV1.Encode(WrenWorking()));
        ActorCognition wren = decoded.For(Wren);
        CognitionEvidence delivery = Assert.Single(wren.Evidence, entry => entry.Kind == EvidenceKind.QuestDeliveryReceived);
        Assert.Equal((Wren, Player, DeliveryCommand, Quest, 4UL), (delivery.KnowerId, delivery.CounterpartActorId!.Value,
            delivery.SourceCommandId, delivery.SubjectId, delivery.Quantity));
        Assert.Equal([Delivered], wren.Intention!.Basis);
        Assert.Equal([MemoryOutcome.Adopted, MemoryOutcome.Superseded, MemoryOutcome.Adopted],
            wren.Memory.Select(memory => memory.Outcome));
        Assert.Equal(CognitionDeferral.MissingTool, wren.Memory[0].Reason);
        Assert.Equal([Delivered], wren.Memory[2].Basis);
        Assert.All(wren.Memory, memory => Assert.Equal(CognitionPolicy.Version, memory.PolicyVersion));
        Assert.Equal(new CognitionWake(200, WakeCause.Fallback), wren.Wake);
        Assert.Empty(decoded.For(Player).Evidence);
    }

    [Fact]
    public void TheFullyBoundedActorFitsItsDeclaredByteBounds()
    {
        ActorCognition actor = ActorCognition.Empty(Wren);
        for (ulong index = 0; index < ActorCognition.MaximumEvidence; index++)
            actor = actor.Acquire(new CognitionEvidence(Id(10_000 + index), Wren, EvidenceKind.QuestDeliveryReceived,
                Id(20_000 + index), Player, Id(30_000 + index), ulong.MaxValue, new WorldInstant(long.MaxValue, 86_399_999)), 0);
        var intention = new CognitionIntention(IntentionKind.WorkFundedProject, Project, At(1),
            actor.Evidence.Take(CognitionIntention.MaximumBasis).Select(entry => entry.EvidenceId));
        string policy = new('p', CognitionMemory.MaximumPolicyVersionBytes);
        var memory = Enumerable.Range(0, ActorCognition.MaximumMemory).Select(index => new CognitionMemory(
            IntentionKind.WorkFundedProject, Project, MemoryOutcome.Adopted, new WorldInstant(long.MaxValue, 86_399_999),
            CognitionDeferral.AwaitingSupply, policy, actor.Evidence.Take(CognitionIntention.MaximumBasis).Select(entry => entry.EvidenceId)));
        var full = new ActorCognition(Wren, ulong.MaxValue, actor.Evidence, intention, memory,
            new CognitionWake(ulong.MaxValue, WakeCause.OwnResourceChanged, WakeSuppression.PaidContinuation));
        var actors = Enumerable.Range(0, CognitionSnapshot.MaximumActors).Select(index => index == 0 ? full
            : new ActorCognition(Id(50_000 + (ulong)index), 0, [], null, []));
        byte[] bytes = CognitionCodecV1.Encode(new CognitionSnapshot(World, Branch, ulong.MaxValue, actors));
        Assert.True(bytes.Length <= CognitionCodecV1.MaximumSnapshotByteLength);
        Assert.Equal(bytes, CognitionCodecV1.Encode(CognitionCodecV1.Decode(bytes)));
    }

    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
        AppContext.BaseDirectory, "Cognition", "cognition-v1.json")));
}
