using System.Buffers.Binary;
using System.Security.Cryptography;

namespace OpenFantasy.Core;

/// <summary>
/// A single-owner deterministic SHA-256 counter stream. Each draw consumes one
/// digest and reads its first big-endian UInt64. This type is not thread-safe;
/// immutable worker inputs need independent streams and canonical ownership.
/// </summary>
public sealed class CounterRandomV1
{
    public const uint AlgorithmVersion = 1;
    private readonly byte[] drawPayload;
    private readonly string streamFingerprint;
    private ulong nextCounter;
    private bool exhausted;

    public CounterRandomV1(ReadOnlySpan<byte> worldSeed, string rulesVersion, string domain,
        EntityId responsibleId, ulong nextCounter = 0)
    {
        if (worldSeed.IsEmpty) throw new ArgumentException("A world seed cannot be empty.", nameof(worldSeed));
        byte[] seed = worldSeed.ToArray();
        byte[] rules = CanonicalEncodingV1.Text(rulesVersion, nameof(rulesVersion));
        byte[] scope = CanonicalEncodingV1.Text(domain, nameof(domain));
        byte[] owner = responsibleId.ToByteArray();
        byte[] key = CanonicalEncodingV1.Frame("rng.sha256-counter.key", seed, rules, scope, owner);
        streamFingerprint = Convert.ToHexString(SHA256.HashData(key)).ToLowerInvariant();
        drawPayload = CanonicalEncodingV1.Frame("rng.sha256-counter.draw", seed, rules, scope, owner,
            CanonicalEncodingV1.Unsigned64(0));
        this.nextCounter = nextCounter;
    }

    public RandomCounterState CaptureState() => new(AlgorithmVersion, streamFingerprint, nextCounter, exhausted);

    public void Restore(RandomCounterState state)
    {
        if (state.AlgorithmVersion != AlgorithmVersion
            || !StringComparer.Ordinal.Equals(state.StreamFingerprint, streamFingerprint)
            || (state.Exhausted && state.NextCounter != ulong.MaxValue))
            throw new ArgumentException("Counter state has an incompatible version, stream key or exhaustion marker.", nameof(state));
        nextCounter = state.NextCounter;
        exhausted = state.Exhausted;
    }

    public ulong NextUInt64()
    {
        ulong candidateCounter = nextCounter;
        bool candidateExhausted = exhausted;
        ulong value = Draw(ref candidateCounter, ref candidateExhausted);
        nextCounter = candidateCounter;
        exhausted = candidateExhausted;
        return value;
    }

    /// <summary>
    /// Uniform over [0, exclusiveMax). Rejected candidates consume counter values.
    /// If the stream exhausts before acceptance, the entire request leaves state unchanged.
    /// </summary>
    public ulong NextUInt64(ulong exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfZero(exclusiveMax);
        // Unsigned subtraction intentionally computes 2^64 modulo the bound.
        ulong threshold = unchecked(0UL - exclusiveMax) % exclusiveMax;
        ulong candidateCounter = nextCounter;
        bool candidateExhausted = exhausted;
        while (true)
        {
            ulong value = Draw(ref candidateCounter, ref candidateExhausted);
            if (value < threshold) continue;
            nextCounter = candidateCounter;
            exhausted = candidateExhausted;
            return value % exclusiveMax;
        }
    }

    public int NextInt32(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);
        return checked((int)NextUInt64((ulong)exclusiveMax));
    }

    public long NextInt64(long exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);
        return checked((long)NextUInt64((ulong)exclusiveMax));
    }

    private ulong Draw(ref ulong counter, ref bool isExhausted)
    {
        if (isExhausted) throw new OverflowException("The random stream has exhausted its UInt64 draw counter.");
        BinaryPrimitives.WriteUInt64BigEndian(drawPayload.AsSpan(drawPayload.Length - sizeof(ulong)), counter);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(drawPayload, digest);
        ulong value = BinaryPrimitives.ReadUInt64BigEndian(digest);
        if (counter == ulong.MaxValue) isExhausted = true;
        else counter = checked(counter + 1);
        return value;
    }
}
