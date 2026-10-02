using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit law permitting separately authorized assisted restoration from incapacity in exploration.
/// This immutable interpretation owns no health, injury, permission or encounter history.
/// </summary>
public sealed class WorldRecoveryPolicy
{
    private WorldRecoveryPolicy() { }

    public static WorldRecoveryPolicy AssistedIncapacity { get; } = new();

    public static ContentHash RulesPin { get; } = ContentHash.Compute(CanonicalEncodingV1.Frame(
        "world.assisted-incapacity.v1",
        "current-zero-hp;exact-corporeal-form;separate-helper-permission;owned-dose;ordinary-work;no-encounter-witness"u8.ToArray()));
}
