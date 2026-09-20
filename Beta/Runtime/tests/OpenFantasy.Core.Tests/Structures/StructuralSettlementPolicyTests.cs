using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Tests.Structures;

public sealed class StructuralSettlementPolicyTests
{
    [Fact]
    public void Drop_time_is_the_first_integer_millisecond_reaching_the_requested_distance()
    {
        var policy = new StructuralSettlementPolicy(256, 256, 256, 9800);
        Assert.Equal(0UL, policy.FlightMilliseconds(0));
        Assert.Equal(29UL, policy.FlightMilliseconds(1));
        Assert.Equal(452UL, policy.FlightMilliseconds(256));
        for (long distance = 1; distance <= 256; distance++)
        {
            ulong time = policy.FlightMilliseconds(distance);
            ulong target = checked((ulong)distance * 2 * 1000 * 1000000);
            Assert.True(9800UL * time * time * 256 >= target);
            Assert.True(9800UL * (time - 1) * (time - 1) * 256 < target);
        }
    }

    [Fact]
    public void No_policy_grants_unbounded_distance_or_extra_exploration_time()
    {
        Assert.Throws<ArgumentException>(() => new StructuralSettlementPolicy(257, 256, 256, 9800));
        Assert.Throws<ArgumentException>(() => new StructuralSettlementPolicy(256, 257, 256, 9800));
        Assert.Throws<ArgumentException>(() => new StructuralSettlementPolicy(256, 256, 257, 9800));
        Assert.Throws<ArgumentException>(() => new StructuralSettlementPolicy(256, 256, 256, 1));
        Assert.Throws<ArgumentException>(() => new StructuralSettlementPolicy(256, 256, 256, 0));
        Assert.Throws<ArgumentException>(() => new StructuralSettlementPolicy(256, 256, 256, ulong.MaxValue));
        var policy = new StructuralSettlementPolicy(256, 0, 0, 9800);
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.FlightMilliseconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.FlightMilliseconds(257));
    }

    [Fact]
    public void Canonical_policy_retains_distinct_body_material_and_gravity_calibration()
    {
        var policy = new StructuralSettlementPolicy(240, 128, 64, 10000);
        byte[] encoded = StructuralSettlementPolicyCodecV1.Encode(policy);
        var decoded = StructuralSettlementPolicyCodecV1.Decode(encoded);
        Assert.Equal(240, decoded.MaximumDropSubunits);
        Assert.Equal(128, decoded.SafeActorDropSubunits);
        Assert.Equal(64, decoded.SafeMemberDropSubunits);
        Assert.Equal(10000UL, decoded.GravityMillimetresPerSecondSquared);
        Assert.Equal(policy.ContentHash, ContentHash.Compute(encoded));
        Assert.Equal(encoded, StructuralSettlementPolicyCodecV1.Encode(decoded));
        Assert.NotEqual(policy.ContentHash, new StructuralSettlementPolicy(240, 129, 64, 10000).ContentHash);
        Assert.NotEqual(policy.ContentHash, new StructuralSettlementPolicy(240, 128, 65, 10000).ContentHash);
        Assert.NotEqual(policy.ContentHash, new StructuralSettlementPolicy(240, 128, 64, 10001).ContentHash);
        Assert.Throws<FormatException>(() => StructuralSettlementPolicyCodecV1.Decode(encoded[..^1]));
        Assert.Throws<FormatException>(() => StructuralSettlementPolicyCodecV1.Decode([.. encoded, 0]));
        byte[] invalid = encoded.ToArray();
        int firstNumber = 12 + 4 + checked((int)BinaryPrimitives.ReadUInt32BigEndian(encoded.AsSpan(12))) + 4;
        BinaryPrimitives.WriteUInt64BigEndian(invalid.AsSpan(firstNumber, 8), 257);
        Assert.Throws<FormatException>(() => StructuralSettlementPolicyCodecV1.Decode(invalid));
    }
}
