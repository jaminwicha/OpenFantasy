using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Encounters.EncounterOutcome;

/// <summary>
/// Canonical, malformed and admission obligations of the profile-35 outcome owner, its acknowledgment command and the
/// profile-35 step frame. World-level admission and committed sequences are qualified in the hosting tests.
/// </summary>
public sealed class EncounterOutcomeOwnerTests
{
    private static readonly ContentHash Archive = ContentHash.Compute(new byte[] { 1, 2, 3 });
    private static readonly ContentHash OtherArchive = ContentHash.Compute(new byte[] { 4, 5, 6 });

    private static EntityId Id(byte value)
    {
        byte[] bytes = new byte[EntityId.ByteLength];
        bytes[0] = 0x5A;
        bytes[EntityId.ByteLength - 1] = value;
        return EntityId.FromBytes(bytes);
    }

    private static EncounterOutcomeAcknowledgment Reading(byte actor, byte command, byte encounter = 90, ContentHash? hash = null) =>
        new(Id(actor), Id(command), Id(encounter), hash ?? Archive, 7);

    [Fact]
    public void FreshOwnerPinsTheLawAndIsCanonical()
    {
        EncounterOutcomeState fresh = EncounterOutcomeState.Fresh();
        Assert.Equal(EncounterOutcomeState.Pin, fresh.RulesPin);
        Assert.Equal(0UL, fresh.FinalizedCount);
        Assert.Empty(fresh.Acknowledgments);
        Assert.Null(fresh.Last);
        byte[] bytes = EncounterOutcomeCodecV1.Encode(fresh);
        Assert.Equal(bytes, EncounterOutcomeCodecV1.Encode(EncounterOutcomeCodecV1.Decode(bytes)));
        Assert.Contains("encounter.outcome.v1", EncounterOutcomeState.RulesText, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadingsAndCompletionRecordRoundTripExactly()
    {
        var pending = new EncounterOutcomeState(EncounterOutcomeState.Pin, 1, [Reading(1, 11), Reading(2, 12)],
            new EncounterOutcomeRecord(Id(80), OtherArchive, EncounterOutcomeResult.Victory, 2, 5, [Id(1)]));
        byte[] bytes = EncounterOutcomeCodecV1.Encode(pending);
        EncounterOutcomeState decoded = EncounterOutcomeCodecV1.Decode(bytes);
        Assert.Equal(bytes, EncounterOutcomeCodecV1.Encode(decoded));
        Assert.Equal(new[] { Id(1), Id(2) }, decoded.Acknowledgments.Select(row => row.ActorId));
        Assert.True(decoded.HasAcknowledged(Id(2)));
        Assert.False(decoded.HasAcknowledged(Id(3)));
        Assert.Equal(EncounterOutcomeResult.Victory, decoded.Last!.Result);
        Assert.Equal(2, decoded.Last.StandingSide);

        var none = new EncounterOutcomeState(EncounterOutcomeState.Pin, 2, [],
            new EncounterOutcomeRecord(Id(81), Archive, EncounterOutcomeResult.NoSurvivors, 0, 9, [Id(1), Id(2)]));
        byte[] noneBytes = EncounterOutcomeCodecV1.Encode(none);
        Assert.Equal(noneBytes, EncounterOutcomeCodecV1.Encode(EncounterOutcomeCodecV1.Decode(noneBytes)));
        Assert.NotEqual(EncounterOutcomeCodecV1.Encode(EncounterOutcomeState.Fresh()), noneBytes);
    }

    [Fact]
    public void OwnerRefusesUnorderedDuplicateMixedOrFinalizedReadings()
    {
        ContentHash pin = EncounterOutcomeState.Pin;
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(pin, 0, [Reading(2, 12), Reading(1, 11)], null));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(pin, 0, [Reading(1, 11), Reading(1, 12)], null));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(pin, 0, [Reading(1, 11), Reading(2, 11)], null));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(pin, 0, [Reading(1, 11), Reading(2, 12, 91)], null));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(pin, 0,
            [Reading(1, 11), Reading(2, 12, hash: OtherArchive)], null));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(pin, 1, [], null));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(pin, 0, [],
            new EncounterOutcomeRecord(Id(90), Archive, EncounterOutcomeResult.Victory, 1, 3, [])));
        // A finalized episode can never collect readings again.
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(pin, 1, [Reading(1, 11)],
            new EncounterOutcomeRecord(Id(90), Archive, EncounterOutcomeResult.Victory, 1, 3, [])));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(pin, 0, [Reading(1, 11) with { WorldSequence = 0 }], null));
    }

    [Fact]
    public void RecordRefusesInconsistentResultStandingSideOrReaders()
    {
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeRecord(Id(9), Archive, EncounterOutcomeResult.Victory, 0, 1, []));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeRecord(Id(9), Archive, EncounterOutcomeResult.NoSurvivors, 1, 1, []));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeRecord(Id(9), Archive, (EncounterOutcomeResult)7, 1, 1, []));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeRecord(Id(9), Archive, EncounterOutcomeResult.Victory, 1, 0, []));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeRecord(Id(9), Archive, EncounterOutcomeResult.Victory, 1, 1,
            [Id(2), Id(1)]));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeRecord(Id(9), Archive, EncounterOutcomeResult.Victory, 1, 1,
            [Id(1), Id(1)]));
    }

    [Fact]
    public void ForeignRulesPinIsRefusedByTheOwnerAndItsCodec()
    {
        // F1: the pin names the only law this owner interprets; another law's owner is never read as encounter.outcome.v1.
        ContentHash foreign = ContentHash.Compute(new byte[] { 7 });
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(foreign, 0, [], null));
        Assert.Throws<ArgumentException>(() => new EncounterOutcomeState(foreign, 0, [Reading(1, 11)], null));
        foreach (EncounterOutcomeState owner in new[]
        {
            EncounterOutcomeState.Fresh(),
            new EncounterOutcomeState(EncounterOutcomeState.Pin, 1, [Reading(1, 11)],
                new EncounterOutcomeRecord(Id(80), OtherArchive, EncounterOutcomeResult.Victory, 2, 5, [Id(1)]))
        })
        {
            byte[] bytes = EncounterOutcomeCodecV1.Encode(owner);
            int at = IndexOf(bytes, EncounterOutcomeState.Pin.ToByteArray());
            Assert.True(at > 0);
            byte[] repinned = [.. bytes];
            foreign.ToByteArray().CopyTo(repinned, at);
            Assert.ThrowsAny<FormatException>(() => EncounterOutcomeCodecV1.Decode(repinned));
            Assert.Equal(bytes, EncounterOutcomeCodecV1.Encode(EncounterOutcomeCodecV1.Decode(bytes)));
        }
    }

    [Fact]
    public void MalformedOwnerBytesAreRefused()
    {
        var owner = new EncounterOutcomeState(EncounterOutcomeState.Pin, 0, [Reading(1, 11)], null);
        byte[] bytes = EncounterOutcomeCodecV1.Encode(owner);
        Assert.ThrowsAny<FormatException>(() => EncounterOutcomeCodecV1.Decode(bytes[..^1]));
        Assert.ThrowsAny<FormatException>(() => EncounterOutcomeCodecV1.Decode([.. bytes, 0]));
        Assert.ThrowsAny<FormatException>(() => EncounterOutcomeCodecV1.Decode([]));
        byte[] retagged = [.. bytes];
        int tag = IndexOf(retagged, "encounter.outcome.v1"u8.ToArray());
        retagged[tag + 10] = (byte)'X';
        Assert.ThrowsAny<FormatException>(() => EncounterOutcomeCodecV1.Decode(retagged));
        // A reading row whose world sequence is zero is not an admitted reading even if its frame is well formed.
        byte[] zeroed = [.. bytes];
        byte[] sequence = CanonicalSequence(7);
        int at = LastIndexOf(zeroed, sequence);
        Assert.True(at > 0);
        zeroed[at + sequence.Length - 1] = 0;
        Assert.ThrowsAny<FormatException>(() => EncounterOutcomeCodecV1.Decode(zeroed));
    }

    [Fact]
    public void AcknowledgmentCommandRoundTripsInItsOwnVersionOnly()
    {
        var command = new WorldActionCommand(Id(30), Id(31), Id(32), Id(1), Id(40), Id(41), 3,
            new AcknowledgeEncounterOutcomeIntent(Id(90), Archive));
        byte[] bytes = WorldActionCodec.Encode(command);
        Assert.Equal(bytes, WorldActionCodecV20.Encode(command));
        WorldActionCommand decoded = WorldActionCodec.Decode(bytes);
        var intent = Assert.IsType<AcknowledgeEncounterOutcomeIntent>(decoded.Intent);
        Assert.Equal(Id(90), intent.EncounterId);
        Assert.Equal(Archive, intent.ExpectedActionHash);
        Assert.Equal(bytes, WorldActionCodec.Encode(decoded));
        Assert.ThrowsAny<FormatException>(() => WorldActionCodecV20.Decode(bytes[..^1]));
        Assert.ThrowsAny<FormatException>(() => WorldActionCodecV19.Decode(bytes));
        var release = new WorldActionCommand(Id(30), Id(31), Id(32), Id(1), Id(40), Id(41), 3,
            new ReleaseEncounterIntent(Id(90), Archive));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV20.Encode(release));
        Assert.NotEqual(bytes, WorldActionCodec.Encode(release));
    }

    [Fact]
    public void AcknowledgmentIsOnlyAProfile35LifecycleCommandAndReleaseIsNotProfile35()
    {
        EntityId world = Id(40), branch = Id(41);
        var read = new WorldActionCommand(Id(30), Id(31), Id(32), Id(1), world, branch, 3,
            new AcknowledgeEncounterOutcomeIntent(Id(90), Archive));
        var release = new WorldActionCommand(Id(33), Id(31), Id(32), Id(1), world, branch, 3,
            new ReleaseEncounterIntent(Id(90), Archive));
        _ = new WorldStepBatch(Id(50), world, branch, 4, 0, WorldBatchKind.EncounterLifecycle, [], [read], 35);
        _ = new WorldStepBatch(Id(50), world, branch, 4, 0, WorldBatchKind.EncounterLifecycle, [], [], 35);
        _ = new WorldStepBatch(Id(50), world, branch, 4, 0, WorldBatchKind.EncounterLifecycle, [], [release], 34);
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(50), world, branch, 4, 0, WorldBatchKind.EncounterLifecycle, [], [read], 34));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(50), world, branch, 4, 0, WorldBatchKind.AdvancingStep, [], [read], 35));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(50), world, branch, 4, 0, WorldBatchKind.Care, [], [read], 35));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(50), world, branch, 4, 0, WorldBatchKind.EncounterLifecycle, [], [release], 35));
    }

    [Fact]
    public void ProfileThirtyFiveStepFrameCarriesReadingsAndCompletionBoundaries()
    {
        EntityId world = Id(40), branch = Id(41);
        var read = new WorldActionCommand(Id(30), Id(31), Id(32), Id(1), world, branch, 3,
            new AcknowledgeEncounterOutcomeIntent(Id(90), Archive));
        foreach (WorldStepBatch batch in new[]
        {
            new WorldStepBatch(Id(50), world, branch, 4, 0, WorldBatchKind.EncounterLifecycle, [], [read], 35),
            new WorldStepBatch(Id(51), world, branch, 5, 0, WorldBatchKind.EncounterLifecycle, [], [], 35)
        })
        {
            byte[] bytes = WorldStepCodec.Encode(batch);
            Assert.Equal(bytes, WorldStepCodecV35.Encode(batch));
            WorldStepBatch decoded = WorldStepCodec.Decode(bytes);
            Assert.Equal((ushort)35, decoded.ProfileVersion);
            Assert.Equal(bytes, WorldStepCodec.Encode(decoded));
            Assert.ThrowsAny<FormatException>(() => WorldStepCodecV34.Decode(bytes));
        }
    }

    private static byte[] CanonicalSequence(ulong value)
    {
        byte[] bytes = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static int IndexOf(byte[] source, byte[] value)
    {
        for (int index = 0; index + value.Length <= source.Length; index++)
            if (source.AsSpan(index, value.Length).SequenceEqual(value)) return index;
        return -1;
    }

    private static int LastIndexOf(byte[] source, byte[] value)
    {
        for (int index = source.Length - value.Length; index >= 0; index--)
            if (source.AsSpan(index, value.Length).SequenceEqual(value)) return index;
        return -1;
    }
}
