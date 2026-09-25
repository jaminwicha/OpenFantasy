using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Encounters;

/// <summary>Source-pinned physical single-target ability. Owning its definition does not grant an actor its use.</summary>
public sealed class EncounterAbilityDefinition
{
    public const int MaximumKeyLength = 64;
    public const int MaximumDisplayNameBytes = 128;
    public const uint MaximumPowerTenths = 1_000;
    public const long MaximumRangeSubunits = 768;
    public const uint MaximumReadinessCostPart = 1_000_000;
    public const uint MaximumManaCost = 1_000_000;
    public const uint MaximumMinimumLevel = 256;

    public EncounterAbilityDefinition(EntityId definitionId, string key, string displayName, ContentHash sourceVersion,
        uint powerTenths, long rangeSubunits, uint readinessCostNumerator, uint readinessCostDenominator,
        uint manaCost, uint minimumLevel, BodyFunction requiredFunction = BodyFunction.Manipulation)
    {
        ArgumentNullException.ThrowIfNull(sourceVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (key.Length > MaximumKeyLength || key[0] is < 'a' or > 'z'
            || key.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')))
        {
            throw new ArgumentException("An ability key requires bounded lowercase ASCII letters, digits, underscores or hyphens.", nameof(key));
        }
        if (CanonicalEncodingV1.Text(displayName, nameof(displayName)).Length > MaximumDisplayNameBytes)
        {
            throw new ArgumentException("The ability display name exceeds its encoded bound.", nameof(displayName));
        }
        if (powerTenths is 0 or > MaximumPowerTenths || rangeSubunits is < 1 or > MaximumRangeSubunits
            || readinessCostNumerator is 0 or > MaximumReadinessCostPart
            || readinessCostDenominator is 0 or > MaximumReadinessCostPart || manaCost > MaximumManaCost
            || minimumLevel is 0 or > MaximumMinimumLevel || !Enum.IsDefined(requiredFunction))
        {
            throw new ArgumentException("Ability power, reach, readiness cost, mana, level and body function must fit the physical action contract.");
        }
        DefinitionId = definitionId;
        Key = key;
        DisplayName = displayName;
        SourceVersion = sourceVersion;
        PowerTenths = powerTenths;
        RangeSubunits = rangeSubunits;
        ReadinessCostNumerator = readinessCostNumerator;
        ReadinessCostDenominator = readinessCostDenominator;
        ManaCost = manaCost;
        MinimumLevel = minimumLevel;
        RequiredFunction = requiredFunction;
        Version = ContentHash.Compute(EncounterActionCodecV1.EncodeAbility(this));
    }

    public EntityId DefinitionId { get; }
    public string Key { get; }
    public string DisplayName { get; }
    public ContentHash SourceVersion { get; }
    public uint PowerTenths { get; }
    public long RangeSubunits { get; }
    public uint ReadinessCostNumerator { get; }
    public uint ReadinessCostDenominator { get; }
    public uint ManaCost { get; }
    public uint MinimumLevel { get; }
    public BodyFunction RequiredFunction { get; }
    public ContentHash Version { get; }

    public ulong Delay(uint normalizedSpeed)
    {
        ArgumentOutOfRangeException.ThrowIfZero(normalizedSpeed);
        UInt128 numerator = checked((UInt128)10_000 * ReadinessCostNumerator);
        UInt128 denominator = checked((UInt128)ReadinessCostDenominator * normalizedSpeed);
        return checked((ulong)((numerator + denominator - 1) / denominator));
    }
}
