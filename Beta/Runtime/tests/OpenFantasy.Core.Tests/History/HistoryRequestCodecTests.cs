using System.Buffers.Binary;
using System.Text.Json;
using OpenFantasy.Core.History;

namespace OpenFantasy.Core.Tests.History;

public sealed class HistoryRequestCodecTests
{
    [Fact]
    public void CanonicalEncodingAndHashesMatchIndependentPythonOracle()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "history-v1.json")));
        foreach (JsonElement expected in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            HistoryRequest request = FromVector(expected);
            byte[] encoded = HistoryRequestCodecV1.Encode(request);
            Assert.Equal(expected.GetProperty("canonicalHex").GetString(), Convert.ToHexString(encoded).ToLowerInvariant());
            Assert.Equal(expected.GetProperty("sha256").GetString(), request.ComputeContentHash().Value);
            Assert.Equal(expected.GetProperty("byteLength").GetInt32(), encoded.Length);
            Assert.Equal(request, HistoryRequestCodecV1.Decode(encoded));
        }
    }

    [Fact]
    public void ReturnedBytesAndDecodeInputHaveNoMutableAliasToRequestState()
    {
        HistoryRequest request = Sample();
        byte[] encoded = HistoryRequestCodecV1.Encode(request);
        HistoryRequest restored = HistoryRequestCodecV1.Decode(encoded);
        encoded.AsSpan().Clear();
        Assert.Equal(request, restored);
        Assert.NotEqual(encoded, HistoryRequestCodecV1.Encode(request));
        Assert.NotEqual(request.ComputeContentHash(), Sample(0).ComputeContentHash());
        Assert.NotEqual(Sample(0).ComputeContentHash(), Sample(4).ComputeContentHash());
    }

    [Fact]
    public void EveryTruncatedPrefixOversizedInputAndTrailingDataReject()
    {
        byte[] complete = HistoryRequestCodecV1.Encode(Sample());
        for (int length = 0; length < complete.Length; length++)
        {
            byte[] truncated = complete[..length];
            Assert.Throws<FormatException>(() => HistoryRequestCodecV1.Decode(truncated));
        }

        Assert.Throws<FormatException>(() => HistoryRequestCodecV1.Decode(new byte[HistoryRequestCodecV1.MaximumRequestByteLength + 1]));
        Assert.Throws<FormatException>(() => HistoryRequestCodecV1.Decode([.. complete, 0]));
    }

    [Fact]
    public void UnknownHeadersTagsCountsFieldLengthsAndNoncanonicalInstantsReject()
    {
        AssertCorruptionRejected(bytes => bytes[0] = 0);
        AssertCorruptionRejected(bytes => bytes[7] = 2);
        AssertCorruptionRejected(bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), uint.MaxValue));
        AssertCorruptionRejected(bytes => bytes[16] = (byte)'x');
        AssertCorruptionRejected(bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12), uint.MaxValue));
        AssertCorruptionRejected(bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(FieldOffset(bytes, 1) - 4), 15));
        AssertCorruptionRejected(bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(FieldOffset(bytes, 7) + 8), 86_400_000));
        AssertCorruptionRejected(bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(FieldOffset(bytes, 8) + 8), uint.MaxValue));
    }

    [Fact]
    public void DecoderRejectsInvalidTemporalWindowOrCursorRatherThanNormalizingIt()
    {
        AssertCorruptionRejected(bytes => BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(FieldOffset(bytes, 7)), 12));
        AssertCorruptionRejected(bytes => BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(FieldOffset(bytes, 9)), 11));
        byte[] cursorRequest = HistoryRequestCodecV1.Encode(Sample(4));
        BinaryPrimitives.WriteUInt64BigEndian(cursorRequest.AsSpan(FieldOffset(cursorRequest, 11)), 6);
        Assert.Throws<FormatException>(() => HistoryRequestCodecV1.Decode(cursorRequest));

        byte[] malformedCursor = HistoryRequestCodecV1.Encode(Sample(4));
        BinaryPrimitives.WriteUInt32BigEndian(malformedCursor.AsSpan(FieldOffset(malformedCursor, 11) - 4), 7);
        Assert.Throws<FormatException>(() => HistoryRequestCodecV1.Decode(malformedCursor[..^1]));
    }

    private static void AssertCorruptionRejected(Action<byte[]> corrupt)
    {
        byte[] bytes = HistoryRequestCodecV1.Encode(Sample());
        corrupt(bytes);
        Assert.Throws<FormatException>(() => HistoryRequestCodecV1.Decode(bytes));
    }

    private static int FieldOffset(byte[] bytes, int fieldIndex)
    {
        int offset = 12;
        for (int index = 0; index < fieldIndex; index++)
        {
            offset += checked(4 + (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
        }

        return offset + 4;
    }

    private static HistoryRequest Sample(ulong? cursor = null) =>
        new(new HistoryAddress(new EntityId(0, 1), new EntityId(0, 2), new EntityId(0, 3), -257, 1, long.MaxValue),
            new HistoryBoundary(new WorldInstant(-10, 0), new WorldInstant(10, 0), new WorldInstant(0, 1), 5, cursor));

    private static HistoryRequest FromVector(JsonElement vector)
    {
        var address = new HistoryAddress(EntityId.Parse(vector.GetProperty("world").GetString()),
            EntityId.Parse(vector.GetProperty("branch").GetString()), EntityId.Parse(vector.GetProperty("realm").GetString()),
            long.Parse(vector.GetProperty("x").GetString()!), long.Parse(vector.GetProperty("y").GetString()!),
            long.Parse(vector.GetProperty("z").GetString()!));
        JsonElement cursor = vector.GetProperty("cursor");
        var boundary = new HistoryBoundary(Instant(vector, "origin"), Instant(vector, "head"), Instant(vector, "requested"),
            ulong.Parse(vector.GetProperty("pinnedRevision").GetString()!),
            cursor.ValueKind == JsonValueKind.Null ? null : ulong.Parse(cursor.GetString()!));
        return new HistoryRequest(address, boundary);
    }

    private static WorldInstant Instant(JsonElement vector, string name)
    {
        JsonElement instant = vector.GetProperty(name);
        return new WorldInstant(long.Parse(instant[0].GetString()!), instant[1].GetInt32());
    }
}
