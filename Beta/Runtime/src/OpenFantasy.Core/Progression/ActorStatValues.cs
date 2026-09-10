namespace OpenFantasy.Core.Progression;

/// <summary>Effective integer statistics. Zero attack is valid for an unassigned actor.</summary>
public sealed record ActorStatValues
{
    public ActorStatValues(int maxHp, int maxMp, int attack, int magic, int defense, int speed)
    {
        if (maxHp < 1 || maxMp < 0 || attack < 0 || magic < 0 || defense < 0 || speed < 1)
        {
            throw new ArgumentException("Actor statistics require positive health and speed and nonnegative other values.");
        }
        MaxHp = maxHp;
        MaxMp = maxMp;
        Attack = attack;
        Magic = magic;
        Defense = defense;
        Speed = speed;
    }

    public int MaxHp { get; }
    public int MaxMp { get; }
    public int Attack { get; }
    public int Magic { get; }
    public int Defense { get; }
    public int Speed { get; }
}

/// <summary>Signed linear growth or admitted flat modifiers; these are not equipment ownership.</summary>
public sealed record ActorStatModifiers(int Hp, int Mp, int Attack, int Magic, int Defense, int Speed)
{
    public static ActorStatModifiers Zero { get; } = new(0, 0, 0, 0, 0, 0);
}
