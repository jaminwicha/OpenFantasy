namespace OpenFantasy.Core.Encounters;

/// <summary>Exact physical damage arithmetic; inputs require separate live stat, ability and target admission.</summary>
public static class EncounterActionRules
{
    public static uint ComputePhysicalDamage(int attack, int defense, uint powerTenths, uint variationPermille)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attack);
        ArgumentOutOfRangeException.ThrowIfNegative(defense);
        if (powerTenths is 0 or > EncounterAbilityDefinition.MaximumPowerTenths || variationPermille is < 900 or > 1_100)
        {
            throw new ArgumentOutOfRangeException(nameof(powerTenths), "Damage requires admitted positive power and bounded variation.");
        }
        UInt128 powered = checked((UInt128)(uint)attack * powerTenths) / 10;
        UInt128 defended = (uint)defense / 2;
        UInt128 baseDamage = powered > defended ? powered - defended : 1;
        UInt128 varied = checked(baseDamage * variationPermille) / 1_000;
        // Resources are bounded by signed-int HP. Saturation preserves the eventual HP clamp
        // without making an otherwise admitted high-power action unresolvable.
        return varied > uint.MaxValue ? uint.MaxValue : (uint)(varied == 0 ? 1 : varied);
    }
}
