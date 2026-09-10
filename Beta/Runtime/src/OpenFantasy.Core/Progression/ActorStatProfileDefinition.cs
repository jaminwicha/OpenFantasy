using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Progression;

public enum ActorStatProfileKind
{
    Class = 1,
    Unassigned = 2
}

/// <summary>
/// Pinned unequipped statistics. A class profile composes class growth and explicitly admitted
/// heritage modifiers; an unassigned profile preserves fixed source statistics without inventing a class.
/// This definition does not grant abilities, movement, equipment or species identity.
/// </summary>
public sealed class ActorStatProfileDefinition
{
    public ActorStatProfileDefinition(EntityId definitionId, string key, string displayName,
        ActorStatProfileKind kind, ActorStatValues baseStats, ActorStatModifiers growth,
        ActorStatModifiers heritageModifiers)
    {
        ProgressionValidation.Text(key, 128, nameof(key));
        ProgressionValidation.Text(displayName, 128, nameof(displayName));
        ArgumentNullException.ThrowIfNull(baseStats);
        ArgumentNullException.ThrowIfNull(growth);
        ArgumentNullException.ThrowIfNull(heritageModifiers);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentException("Unknown statistic profile kind.", nameof(kind));
        }
        if (kind == ActorStatProfileKind.Unassigned
            && (growth != ActorStatModifiers.Zero || heritageModifiers != ActorStatModifiers.Zero))
        {
            throw new ArgumentException("An unassigned profile preserves fixed statistics without growth or heritage recalculation.");
        }
        DefinitionId = definitionId;
        Key = key;
        DisplayName = displayName;
        Kind = kind;
        BaseStats = baseStats;
        Growth = growth;
        HeritageModifiers = heritageModifiers;
        Version = ContentHash.Compute(ProgressionCodecV1.EncodeProfile(this));
    }

    public EntityId DefinitionId { get; }
    public string Key { get; }
    public string DisplayName { get; }
    public ActorStatProfileKind Kind { get; }
    public ActorStatValues BaseStats { get; }
    public ActorStatModifiers Growth { get; }
    public ActorStatModifiers HeritageModifiers { get; }
    public ContentHash Version { get; }

    public ActorStatValues Calculate(int level)
    {
        if (level is < 1 or > LevelProgressionDefinition.MaximumSupportedLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }
        if (Kind == ActorStatProfileKind.Unassigned)
        {
            return BaseStats;
        }
        int increments = level - 1;
        return new ActorStatValues(
            CalculateValue(BaseStats.MaxHp, Growth.Hp, HeritageModifiers.Hp, increments, 1),
            CalculateValue(BaseStats.MaxMp, Growth.Mp, HeritageModifiers.Mp, increments, 0),
            CalculateValue(BaseStats.Attack, Growth.Attack, HeritageModifiers.Attack, increments, 1),
            CalculateValue(BaseStats.Magic, Growth.Magic, HeritageModifiers.Magic, increments, 0),
            CalculateValue(BaseStats.Defense, Growth.Defense, HeritageModifiers.Defense, increments, 0),
            CalculateValue(BaseStats.Speed, Growth.Speed, HeritageModifiers.Speed, increments, 1));
    }

    private static int CalculateValue(int basis, int growth, int modifier, int increments, int minimum)
    {
        long value = basis + (long)growth * increments + modifier;
        return checked((int)Math.Max(minimum, value));
    }
}
