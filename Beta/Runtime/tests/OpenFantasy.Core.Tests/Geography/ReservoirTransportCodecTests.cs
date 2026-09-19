using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Geography.ReservoirTransportFixture;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class ReservoirTransportCodecTests
{
    [Fact]
    public void CompleteTransportArchiveRetainsScheduleBlockedAndTerminalStatesCanonically()
    {
        var f = new ReservoirTransportFixture(receiverUnits: 1001);
        var scheduled = f.Scheduled();
        var blocked = scheduled.PlanAdvance(f.Due).ApplyTo(scheduled);
        var canceled = blocked.PlanCancel(Budget).ApplyTo(blocked);
        foreach (var state in new[] { f.State, scheduled, blocked, canceled })
        {
            byte[] bytes = ReservoirTransportCodecV1.Encode(state);
            Assert.Equal(bytes, ReservoirTransportCodecV1.Encode(ReservoirTransportCodecV1.Decode(bytes, TestContext.Current.CancellationToken)));
            foreach (var bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(),
                Replace(bytes, 0, Text("unknown-transport-version")), Replace(bytes, 4, Fields(bytes)[4][..^1]),
                Replace(bytes, 2, new byte[11]) })
            {
                Assert.Throws<FormatException>(() => ReservoirTransportCodecV1.Decode(bad, TestContext.Current.CancellationToken));
            }
        }
        byte[] archived = ReservoirTransportCodecV1.Encode(scheduled);
        var stage = Fields(Fields(archived)[5])[1];
        foreach (var corrupt in new[] { Replace(stage, 7, Number(99)), Replace(stage, 8, new byte[1]),
            Replace(stage, 4, new byte[32]), Replace(stage, 5, Number(999)), Replace(stage, 6, new byte[31]) })
        {
            Assert.Throws<FormatException>(() => ReservoirTransportCodecV1.Decode(Replace(archived, 5,
                Frame(new[] { Text("geography.transport-stages.v1"), corrupt })), TestContext.Current.CancellationToken));
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => ReservoirTransportCodecV1.Decode(archived, cancellation.Token));
    }

    [Fact]
    public void ScheduleWireAndOldProfileExclusionAreIndependentOfWorldProfileNumber()
    {
        var f = new ReservoirTransportFixture(freeSource: 10000, splitDestinations: true);
        var intent = new ScheduleRunoffTransportIntent(f.State.ContentHash, f.State.Revision, Budget, f.Due, f.Routes.Reverse());
        var command = new WorldActionCommand(Id(200), Id(7), Id(8), Owner, Id(1), Id(2), 0, intent);
        byte[] bytes = WorldActionCodec.Encode(command);
        Assert.True(bytes.Length < WorldActionCodecV7.MaximumCommandByteLength);
        Assert.Equal(bytes, WorldActionCodec.Encode(WorldActionCodec.Decode(bytes)));
        var nested = Fields(bytes)[9];
        var reversed = Fields(nested)[6].Chunk(32).Reverse().SelectMany(x => x).ToArray();
        foreach (var bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), Replace(bytes, 1, Number(99)),
            Replace(bytes, 9, Replace(nested, 1, Number(2))), Replace(bytes, 9, Replace(nested, 2, new byte[31])),
            Replace(bytes, 9, Replace(nested, 5, new byte[11])), Replace(bytes, 9, Replace(nested, 6, reversed)),
            Replace(bytes, 9, Replace(nested, 6, Fields(nested)[6].Concat(Fields(nested)[6]).ToArray())),
            new byte[WorldActionCodecV7.MaximumCommandByteLength + 1] })
        {
            Assert.Throws<FormatException>(() => WorldActionCodec.Decode(bad));
        }
        var batch = new WorldStepBatch(Id(201), Id(1), Id(2), 0, 0, WorldBatchKind.AdvancingStep, [], [command], 9);
        byte[] encoded = WorldStepCodec.Encode(batch);
        Assert.Equal(encoded, WorldStepCodec.Encode(WorldStepCodec.Decode(encoded)));
        for (ushort profile = 1; profile < 9; profile++)
        {
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(201), Id(1), Id(2), 0, 0,
                WorldBatchKind.AdvancingStep, [], [command], profile));
            Assert.Throws<FormatException>(() => WorldStepCodec.Decode(Replace(encoded, 1, Number(profile))));
        }
        Assert.Throws<ArgumentException>(() => WorldActionCodecV6.Encode(command));
        Assert.Throws<ArgumentException>(() => new ScheduleRunoffTransportIntent(f.State.ContentHash, 0, Budget, f.Due,
            Enumerable.Repeat(f.Routes[0], 2)));
    }

    [Fact]
    public void PublicPhysicalPlansCannotReplaceAStaleOrForeignTransportOwner()
    {
        var f = new ReservoirTransportFixture();
        var state = f.State;
        var cancel = state.Reservoirs.PlanApply(state.Reservoirs.Capacity.PlanCancel(Budget));
        var plan = state.PlanApply(cancel);
        var final = plan.ApplyTo(state);
        Assert.Throws<InvalidOperationException>(() => final.PlanApply(cancel));
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(f.Scheduled()));
        Assert.Throws<ArgumentException>(() => f.Scheduled().PlanApply(cancel));
        var changed = LiquidReservoirState.Create(state.Reservoirs.Capacity, state.Reservoirs.Geometries.Select(g =>
            new ReservoirGeometry(g.Scope, g.ReservoirId, g.CustodianId, g.SpillZ, g.Columns.Select(c => c with { Y = c.Y + 10 }))));
        var foreign = changed.PlanApply(changed.Capacity.PlanCancel(Budget));
        Assert.Throws<InvalidOperationException>(() => state.PlanApply(foreign));
    }
}
