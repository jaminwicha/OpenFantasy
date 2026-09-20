using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

public static class StructuralSettlementPolicyCodecV1
{
    public const int MaximumEncodedByteLength = 256;

    public static byte[] Encode(StructuralSettlementPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return CanonicalEncodingV1.Frame("construction.settlement-policy.v1",
            CanonicalEncodingV1.Unsigned64(checked((ulong)policy.MaximumDropSubunits)),
            CanonicalEncodingV1.Unsigned64(checked((ulong)policy.SafeActorDropSubunits)),
            CanonicalEncodingV1.Unsigned64(checked((ulong)policy.SafeMemberDropSubunits)),
            CanonicalEncodingV1.Unsigned64(policy.GravityMillimetresPerSecondSquared));
    }

    public static StructuralSettlementPolicy Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 5);
            reader.Tag("construction.settlement-policy.v1"u8);
            var policy = new StructuralSettlementPolicy(checked((long)reader.Number()),
                checked((long)reader.Number()), checked((long)reader.Number()), reader.Number());
            reader.Complete();
            if (!Encode(policy).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The settlement policy is not canonical.");
            }
            return policy;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The settlement policy violates its value bounds.", error);
        }
    }
}
