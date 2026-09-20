using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Worlds;

/// <summary>Immutable deterministic query budget; this is not a wall-clock latency guarantee.</summary>
public sealed class ConsequenceExecutionPolicy
{
    public ConsequenceExecutionPolicy(int workUnitsPerContinuation)
    {
        if (workUnitsPerContinuation < 1 || workUnitsPerContinuation > ConstructionSettlementResolution.MaximumWorkBudget)
        {
            throw new ArgumentOutOfRangeException(nameof(workUnitsPerContinuation));
        }
        WorkUnitsPerContinuation = workUnitsPerContinuation;
    }

    public int WorkUnitsPerContinuation { get; }
    public ContentHash ContentHash => ContentHash.Compute(ConsequenceExecutionPolicyCodecV1.Encode(this));
}

public static class ConsequenceExecutionPolicyCodecV1
{
    public const int MaximumEncodedByteLength = 128;

    public static byte[] Encode(ConsequenceExecutionPolicy policy) => CanonicalEncodingV1.Frame(
        "world.consequence-policy.v1", CanonicalEncodingV1.Unsigned64(checked((ulong)policy.WorkUnitsPerContinuation)));

    public static ConsequenceExecutionPolicy Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 2);
            reader.Tag("world.consequence-policy.v1"u8);
            var policy = new ConsequenceExecutionPolicy(checked((int)reader.Number()));
            reader.Complete();
            if (!Encode(policy).AsSpan().SequenceEqual(bytes)) { throw new FormatException("Noncanonical consequence policy."); }
            return policy;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid consequence execution budget.", error);
        }
    }
}
