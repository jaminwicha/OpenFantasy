using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

public static class CoupledSettlementPolicyCodecV1
{
    public const int MaximumEncodedByteLength = 128;

    public static byte[] Encode(CoupledSettlementPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return CanonicalEncodingV1.Frame("construction.coupled-settlement-policy.v1",
            CanonicalEncodingV1.Unsigned64(checked((ulong)policy.MaximumAssemblies)));
    }

    public static CoupledSettlementPolicy Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 2);
            reader.Tag("construction.coupled-settlement-policy.v1"u8);
            var policy = new CoupledSettlementPolicy(checked((int)reader.Number()));
            reader.Complete();
            if (!Encode(policy).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The coupled settlement policy is not canonical.");
            }
            return policy;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("The coupled settlement policy exceeds its finite bounds.", error);
        }
    }
}
