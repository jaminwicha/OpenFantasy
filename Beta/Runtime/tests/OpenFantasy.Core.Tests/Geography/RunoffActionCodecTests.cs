using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class RunoffActionCodecTests
{
    private static EntityId Id(ulong n) => new(0x72756e6f66666163, n);
    private static readonly ContentHash Pin = ContentHash.Compute("physical-owner"u8);
    private static WorldActionCommand Command(WorldActionIntent intent) => new(Id(1), Id(2), Id(3), Id(4), Id(5), Id(6), 7, intent);
    private static AdmitRunoffIntent Admit(IEnumerable<WatershedRunoffInput> inputs) => new(Pin, 5, Id(8), new(-2, 100), new(-1, 20), inputs);

    [Fact]
    public void BoundedCanonicalActionsRetainCompletePinsInstantsAndEveryDryOrFundedInput()
    {
        var rows = Enumerable.Range(0, 16).Select(i => i == 0 ? new WatershedRunoffInput(Id((ulong)(10 + i)), 1000,
            new(Id(30), 10, 20), Id(31)) : WatershedRunoffInput.Dry(Id((ulong)(10 + i)))).ToArray();
        var command = Command(Admit(rows.Reverse()));
        byte[] bytes = WorldActionCodec.Encode(command);
        Assert.Equal(bytes, WorldActionCodec.Encode(Command(Admit(rows))));
        Assert.True(bytes.Length < WorldActionCodecV6.MaximumCommandByteLength);
        var restored = WorldActionCodec.Decode(bytes);
        Assert.Equal(bytes, WorldActionCodec.Encode(restored));
        var intent = Assert.IsType<AdmitRunoffIntent>(restored.Intent);
        Assert.Equal(16, intent.Inputs.Count);
        Assert.Equal(new WorldInstant(-2, 100), intent.Start);
        Assert.Equal(Pin, intent.ExpectedReservoirHash);
        var cancel = Command(new CancelRunoffIntent(Pin, 5, Id(8)));
        Assert.Equal(WorldActionCodec.Encode(cancel), WorldActionCodec.Encode(WorldActionCodec.Decode(WorldActionCodec.Encode(cancel))));
        Assert.Throws<ArgumentException>(() => Admit([]));
        Assert.Throws<ArgumentException>(() => Admit(rows.Append(WatershedRunoffInput.Dry(Id(40)))));
        Assert.Throws<ArgumentException>(() => Admit([rows[0], rows[0]]));
        Assert.Throws<ArgumentException>(() => new AdmitRunoffIntent(Pin, 0, Id(8), new(0, 0), new(0, 0), rows));
    }

    [Fact]
    public void DecodeRejectsAmbiguousOrderHiddenDryFundingMalformedLengthsAndVersions()
    {
        byte[] bytes = WorldActionCodec.Encode(Command(Admit([WatershedRunoffInput.Dry(Id(10)), WatershedRunoffInput.Dry(Id(11))])));
        byte[] action = Fields(bytes)[9];
        byte[] inputs = Fields(action)[8];
        var reversed = inputs[72..].Concat(inputs[..72]).ToArray();
        var duplicate = inputs[..72].Concat(inputs[..72]).ToArray();
        var hiddenFunding = (byte[])inputs.Clone(); hiddenFunding[24] = 1;
        List<byte[]> malformed = [bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(),
            new byte[WorldActionCodecV6.MaximumCommandByteLength + 1], Replace(bytes, 1, Number(99))];
        foreach (var badAction in new[] { Replace(action, 1, Number(99)), Replace(action, 2, Number(0)),
            Replace(action, 3, new byte[31]), Replace(action, 8, reversed), Replace(action, 8, duplicate),
            Replace(action, 8, hiddenFunding), Replace(action, 8, inputs[..^1]), Replace(action, 8, []),
            Replace(action, 6, new byte[13]), Replace(action, 7, Fields(action)[6]) })
        {
            malformed.Add(Replace(bytes, 9, badAction));
        }
        foreach (byte[] bad in malformed) Assert.Throws<FormatException>(() => WorldActionCodec.Decode(bad));
    }

    [Fact]
    public void OlderProfilesRejectNewActionsEvenWhenWrappedByTheirOwnValidBatchVersion()
    {
        var command = Command(new CancelRunoffIntent(Pin, 5, Id(8)));
        var batch = new WorldStepBatch(Id(50), Id(5), Id(6), 0, 0, WorldBatchKind.AdvancingStep, [], [command], 8);
        byte[] bytes = WorldStepCodec.Encode(batch);
        Assert.Equal(bytes, WorldStepCodec.Encode(WorldStepCodec.Decode(bytes)));
        for (ushort profile = 1; profile <= 7; profile++)
        {
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(50), Id(5), Id(6), 0, 0,
                WorldBatchKind.AdvancingStep, [], [command], profile));
            Assert.Throws<FormatException>(() => WorldStepCodec.Decode(Replace(bytes, 1, Number(profile))));
        }
        Assert.Throws<ArgumentException>(() => WorldActionCodecV1.Encode(command));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV5.Encode(command));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV6.Encode(Command(new CloseDialogueIntent(Id(51)))));
    }
}
