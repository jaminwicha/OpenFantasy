using System.Text.Json;

namespace OpenFantasy.Core.Tests;

public sealed class CounterRandomTests
{
    private static CounterRandomV1 Create(string? domain = null, ulong counter = 0)
    {
        var vectors = ReferenceVectors.Read();
        var random = vectors.GetProperty("random");
        return new CounterRandomV1(Convert.FromHexString(vectors.GetProperty("seedHex").GetString()!),
            random.GetProperty("rules").GetString()!, domain ?? random.GetProperty("domain").GetString()!,
            EntityId.Parse(vectors.GetProperty("responsibleId").GetString()!), counter);
    }

    [Fact]
    public void WordsAndStreamFingerprintMatchIndependentGoldenVectors()
    {
        var random = ReferenceVectors.Read().GetProperty("random");
        var stream = Create();
        Assert.Equal(random.GetProperty("keyFingerprint").GetString(), stream.CaptureState().StreamFingerprint);
        foreach (var word in random.GetProperty("words").EnumerateArray())
            Assert.Equal(ReferenceVectors.Unsigned(word), stream.NextUInt64());
    }

    [Fact]
    public void RejectionSamplingSkipsBiasedPrefixAndPersistsEveryDraw()
    {
        var value = ReferenceVectors.Read().GetProperty("random").GetProperty("rejectedDraw");
        var stream = Create(counter: ReferenceVectors.Unsigned(value.GetProperty("startCounter")));
        Assert.Equal(ReferenceVectors.Unsigned(value.GetProperty("result")),
            stream.NextUInt64(ReferenceVectors.Unsigned(value.GetProperty("exclusiveMax"))));
        Assert.Equal(ReferenceVectors.Unsigned(value.GetProperty("nextCounter")), stream.CaptureState().NextCounter);
    }

    [Fact]
    public void SerializedCounterStateReproducesFutureContinuation()
    {
        var stream = Create();
        for (int i = 0; i < 17; i++) stream.NextInt32(100);
        string persisted = JsonSerializer.Serialize(stream.CaptureState());
        var restored = Create();
        restored.Restore(JsonSerializer.Deserialize<RandomCounterState>(persisted));
        for (int i = 0; i < 100; i++) Assert.Equal(stream.NextInt64(long.MaxValue), restored.NextInt64(long.MaxValue));
        Assert.Equal(stream.CaptureState(), restored.CaptureState());
    }

    [Fact]
    public void UnrelatedDomainsAndActorsCannotPerturbCombatSequence()
    {
        var combat = Create();
        var expected = Create();
        var idle = Create("npc.idle");
        var actor = new CounterRandomV1([0, 1, 2], "rules/1", "combat.hit", new EntityId(0, 1));
        for (int i = 0; i < 20; i++)
        {
            for (int j = 0; j < i; j++) { idle.NextUInt64(); actor.NextUInt64(); }
            Assert.Equal(expected.NextUInt64(), combat.NextUInt64());
        }
    }

    [Fact]
    public void BoundsRejectBeforeStateChangesAndValidBoundsAreExclusive()
    {
        var stream = Create();
        var state = stream.CaptureState();
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextUInt64(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextInt32(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextInt64(0));
        Assert.Equal(state, stream.CaptureState());
        for (int i = 0; i < 1000; i++)
        {
            Assert.InRange(stream.NextInt32(7), 0, 6);
            Assert.InRange(stream.NextInt64(long.MaxValue), 0, long.MaxValue - 1);
            Assert.InRange(stream.NextUInt64(ulong.MaxValue), 0UL, ulong.MaxValue - 1);
            Assert.Equal(0UL, stream.NextUInt64(1));
        }
    }

    [Fact]
    public void CounterExhaustionCannotWrapOrRerollLastWord()
    {
        var stream = Create(counter: ulong.MaxValue);
        Assert.Equal(ReferenceVectors.Unsigned(ReferenceVectors.Read().GetProperty("random").GetProperty("maxCounterWord")), stream.NextUInt64());
        var state = stream.CaptureState();
        Assert.True(state.Exhausted);
        Assert.Throws<OverflowException>(() => stream.NextUInt64());
        Assert.Equal(state, stream.CaptureState());
        var restored = Create();
        restored.Restore(state);
        Assert.Throws<OverflowException>(() => restored.NextUInt64());
    }

    [Fact]
    public void BoundedExhaustionRollsBackAllCandidateDraws()
    {
        var value = ReferenceVectors.Read().GetProperty("random").GetProperty("exhaustionRejectedDraw");
        var stream = Create(value.GetProperty("domain").GetString(), ulong.MaxValue);
        var state = stream.CaptureState();
        Assert.Throws<OverflowException>(() => stream.NextUInt64(ReferenceVectors.Unsigned(value.GetProperty("exclusiveMax"))));
        Assert.Equal(state, stream.CaptureState());
        Assert.Equal(ReferenceVectors.Unsigned(value.GetProperty("word")), stream.NextUInt64());
    }

    [Fact]
    public void EveryKeyFieldSeparatesStreams()
    {
        var streams = new[]
        {
            new CounterRandomV1([1, 2], "rules/1", "loot", new EntityId(0, 1)),
            new CounterRandomV1([1, 3], "rules/1", "loot", new EntityId(0, 1)),
            new CounterRandomV1([1, 2], "rules/2", "loot", new EntityId(0, 1)),
            new CounterRandomV1([1, 2], "rules/1", "combat", new EntityId(0, 1)),
            new CounterRandomV1([1, 2], "rules/1", "loot", new EntityId(0, 2))
        };
        Assert.Equal(streams.Length, streams.Select(stream => stream.CaptureState().StreamFingerprint).Distinct().Count());
        Assert.Equal(streams.Length, streams.Select(stream => stream.NextUInt64()).Distinct().Count());
    }

    [Fact]
    public void StateFromAnotherKeyVersionOrInvalidExhaustionIsRejected()
    {
        var stream = Create();
        var state = stream.CaptureState();
        Assert.Throws<ArgumentException>(() => stream.Restore(Create("loot").CaptureState()));
        Assert.Throws<ArgumentException>(() => stream.Restore(state with { AlgorithmVersion = 2 }));
        Assert.Throws<ArgumentException>(() => stream.Restore(state with { Exhausted = true }));
        Assert.Equal(state, stream.CaptureState());
    }

    [Fact]
    public void InputSeedIsCopiedAndLengthPrefixesSeparateFields()
    {
        byte[] seed = [1, 2, 3];
        var original = new CounterRandomV1(seed, "ab", "c", default);
        var expected = new CounterRandomV1([1, 2, 3], "ab", "c", default);
        var distinct = new CounterRandomV1([1, 2, 3], "a", "bc", default);
        seed[0] = 255;
        Assert.Equal(expected.NextUInt64(), original.NextUInt64());
        Assert.NotEqual(expected.CaptureState().StreamFingerprint, distinct.CaptureState().StreamFingerprint);
    }
}
