using System.Numerics;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using static OpenFantasy.Core.Tests.Encounters.EncounterActionFixture;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterActionRulesTests
{
    [Theory]
    [InlineData(100, 20, 10U, 900U)]
    [InlineData(7, 3, 15U, 1100U)]
    [InlineData(0, int.MaxValue, 1000U, 900U)]
    [InlineData(int.MaxValue, int.MaxValue, 10U, 1000U)]
    [InlineData(3, 0, 1U, 1000U)]
    [InlineData(int.MaxValue, 0, 1000U, 1100U)]
    [InlineData(int.MaxValue, 0, 1000U, 900U)]
    public void Damage_matches_independent_wide_integer_order_and_minimum(int attack, int defense, uint power, uint variation)
    {
        BigInteger basis = BigInteger.Max(1, (BigInteger)attack * power / 10 - defense / 2);
        uint expected = (uint)BigInteger.Min(uint.MaxValue, BigInteger.Max(1, basis * variation / 1000));
        Assert.Equal(expected, EncounterActionRules.ComputePhysicalDamage(attack, defense, power, variation));
    }

    [Theory]
    [InlineData(1U, 1U, 150U, 67UL)]
    [InlineData(1U, 3U, 100U, 34UL)]
    [InlineData(1000000U, 1U, 1U, 10000000000UL)]
    [InlineData(999999U, 999983U, uint.MaxValue, 1UL)]
    public void Delay_uses_positive_exact_rational_ceil(uint numerator, uint denominator, uint speed, ulong expected)
    {
        EncounterAbilityDefinition ability = Ability(numerator: numerator, denominator: denominator);
        Assert.Equal(expected, ability.Delay(speed));
        BigInteger product = (BigInteger)10_000 * numerator;
        BigInteger divisor = (BigInteger)denominator * speed;
        Assert.Equal((ulong)((product + divisor - 1) / divisor), ability.Delay(speed));
    }

    [Fact]
    public void Definitions_pin_source_capability_and_all_cost_fields_without_inferred_species_rules()
    {
        EncounterAbilityDefinition original = Ability();
        Assert.Equal(original.Version, Ability().Version);
        Assert.NotEqual(original.Version, Ability(power: 11).Version);
        Assert.NotEqual(original.Version, Ability(numerator: 2).Version);
        Assert.NotEqual(original.Version, Ability(denominator: 2).Version);
        Assert.NotEqual(original.Version, Ability(mana: 3).Version);
        Assert.NotEqual(original.Version, Ability(function: BodyFunction.NaturalWeapon).Version);
        var changedSource = new EncounterAbilityDefinition(original.DefinitionId, original.Key, original.DisplayName,
            ContentHash.Compute("changed source"u8), 10, 512, 1, 1, 2, 1);
        Assert.NotEqual(original.Version, changedSource.Version);
        Assert.Equal(BodyFunction.Manipulation, original.RequiredFunction);
    }

    [Fact]
    public void Invalid_arithmetic_inputs_refuse_instead_of_wrapping()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EncounterActionRules.ComputePhysicalDamage(-1, 0, 10, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => EncounterActionRules.ComputePhysicalDamage(1, -1, 10, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => EncounterActionRules.ComputePhysicalDamage(1, 0, 0, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => EncounterActionRules.ComputePhysicalDamage(1, 0, 10, 899));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ability().Delay(0));
        Assert.Throws<ArgumentException>(() => Ability(power: 1001));
        Assert.Throws<ArgumentException>(() => Ability(numerator: 0));
        Assert.Throws<ArgumentException>(() => Ability(denominator: 0));
        Assert.Throws<ArgumentException>(() => Ability(mana: 1000001));
        Assert.Throws<ArgumentException>(() => Ability(function: (BodyFunction)999));
        Assert.Throws<ArgumentException>(() => new EncounterAbilityDefinition(Id(100), "UpperCase", "Strike", Source, 10, 512, 1, 1, 0, 1));
        Assert.Throws<ArgumentException>(() => new EncounterAbilityDefinition(Id(100), "strike", new string('é', 65), Source, 10, 512, 1, 1, 0, 1));
    }
}
