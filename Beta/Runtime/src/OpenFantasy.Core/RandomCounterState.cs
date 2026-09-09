namespace OpenFantasy.Core;

/// <summary>
/// Persist alongside the stream's seed, rules version, domain and responsible identity.
/// Exhausted is valid only when NextCounter equals ulong.MaxValue; it records that
/// the final counter was already consumed, so restoring cannot replay that draw.
/// </summary>
public readonly record struct RandomCounterState(
    uint AlgorithmVersion, string StreamFingerprint, ulong NextCounter, bool Exhausted);
