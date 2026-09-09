using System.Buffers.Binary;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Terrain;

public sealed class TerrainCodecTests
{
    [Fact]
    public void InitialEditedAndMaximumRevisionSnapshotsMatchIndependentOracleAndReloadAllCells()
    {
        foreach (var example in TerrainFixture.Read().GetProperty("examples").EnumerateArray())
        {
            var initial = TerrainFixture.Generate(example);
            foreach (var state in example.GetProperty("states").EnumerateArray())
            {
                byte[] bytes = Convert.FromHexString(state.GetProperty("canonicalHex").GetString()!);
                var restored = TerrainRegionCodecV1.Decode(bytes);
                Assert.Equal(state.GetProperty("revision").GetUInt64(), restored.Revision);
                Assert.Equal(bytes, TerrainRegionCodecV1.Encode(restored));
                Assert.Equal(state.GetProperty("sha256").GetString(), restored.ComputeContentHash().Value);
                Assert.Equal(initial.Scope, restored.Scope);
                Assert.Equal(initial.Bounds, restored.Bounds);
                Assert.Equal(initial.Rules.Version, restored.Rules.Version);
                Assert.Equal(initial.Seed, restored.Seed);
                HashSet<int> excavated = state.GetProperty("excavatedIndices").EnumerateArray()
                    .Select(value => value.GetInt32()).ToHashSet();
                for (int index = 0; index < restored.Cells.Count; index++)
                {
                    TerrainCell actual = restored.Cells[index];
                    TerrainCell before = initial.Cells[index];
                    Assert.Equal(excavated.Contains(index), actual.IsExcavated);
                    Assert.Equal(before.SourceId, actual.SourceId);
                    Assert.Equal(before.FormationId, actual.FormationId);
                    Assert.Equal(before.Material, actual.Material);
                    Assert.Equal(before.Stratum, actual.Stratum);
                }
                if (restored.Revision == 0)
                {
                    Assert.Equal(bytes, TerrainRegionCodecV1.Encode(initial));
                }
                if (restored.Revision == 1)
                {
                    var edited = initial.Excavate(0, excavated.Select(index => initial.Bounds.CoordinateAt(index)));
                    Assert.Equal(bytes, TerrainRegionCodecV1.Encode(edited));
                }
            }
        }
    }

    [Fact]
    public void IndependentMalformedCorpusRejectsShapeBoundsOrderingPinsAndImpossibleExcavation()
    {
        foreach (var example in TerrainFixture.Read().GetProperty("malformed").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(example.GetProperty("canonicalHex").GetString()!);
            Assert.Throws<FormatException>(() => TerrainRegionCodecV1.Decode(bytes));
        }
    }

    [Fact]
    public void EveryTruncatedPrefixAndOversizedInputRejectWithoutPublishingPartialState()
    {
        var initial = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        byte[] bytes = TerrainRegionCodecV1.Encode(initial.Excavate(0, [initial.Cells[0].Coordinate]));
        for (int length = 0; length < bytes.Length; length++)
        {
            int prefixLength = length;
            Assert.Throws<FormatException>(() => TerrainRegionCodecV1.Decode(bytes.AsSpan(0, prefixLength)));
        }
        Assert.Throws<FormatException>(() => TerrainRegionCodecV1.Decode(new byte[TerrainRegionCodecV1.MaximumEncodedByteLength + 1]));
        Assert.Throws<ArgumentNullException>(() => TerrainRegionCodecV1.Encode(null!));
        Assert.Throws<ArgumentNullException>(() => TerrainRegionCodecV1.EncodeRules(null!));
    }

    [Fact]
    public void DecoderCopiesInputAndRejectsNoncanonicalNestedHeadersAndTypedFieldLengths()
    {
        var initial = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        byte[] bytes = TerrainRegionCodecV1.Encode(initial);
        var restored = TerrainRegionCodecV1.Decode(bytes);
        var hash = restored.ComputeContentHash();
        Array.Fill(bytes, (byte)0);
        Assert.Equal(hash, restored.ComputeContentHash());
        foreach (int nestedField in new[] { 1, 3, 4 })
        {
            byte[] altered = TerrainRegionCodecV1.Encode(initial);
            int payload = FieldPayloadOffset(altered, nestedField);
            BinaryPrimitives.WriteUInt32BigEndian(altered.AsSpan(payload + 8), uint.MaxValue);
            Assert.Throws<FormatException>(() => TerrainRegionCodecV1.Decode(altered));
        }
        byte[] shortIdentity = TerrainRegionCodecV1.Encode(initial);
        int scopeOffset = FieldPayloadOffset(shortIdentity, 1);
        int identityOffset = scopeOffset + FieldPayloadOffset(shortIdentity.AsSpan(scopeOffset), 1);
        BinaryPrimitives.WriteUInt32BigEndian(shortIdentity.AsSpan(identityOffset - 4), 15);
        Assert.Throws<FormatException>(() => TerrainRegionCodecV1.Decode(shortIdentity));
    }

    private static int FieldPayloadOffset(ReadOnlySpan<byte> frame, int fieldIndex)
    {
        int offset = 12;
        for (int index = 0; index < fieldIndex; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame[offset..]));
            offset += sizeof(uint) + length;
        }
        return offset + sizeof(uint);
    }
}
