namespace OpenFantasy.Core.Progression;

/// <summary>Controller-neutral progression and current resources, independent of anatomy and custody.</summary>
public sealed class ActorProgressionState
{
    public ActorProgressionState(EntityId actorId, ulong revision, ActorStatProfileDefinition profile,
        LevelProgressionDefinition curve, int level, ulong experience, int currentHp, int currentMp)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(curve);
        if (level < 1 || level > curve.MaximumLevel
            || (level < curve.MaximumLevel && experience >= curve.RequiredExperience(level)))
        {
            throw new ArgumentException("Progression requires a valid level and normalized residual experience below the cap.");
        }
        ActorStatValues stats = profile.Calculate(level);
        if (currentHp < 0 || currentHp > stats.MaxHp || currentMp < 0 || currentMp > stats.MaxMp)
        {
            throw new ArgumentException("Current health or magic exceeds the admitted effective statistics.");
        }
        ActorId = actorId;
        Revision = revision;
        Profile = profile;
        Curve = curve;
        Level = level;
        Experience = experience;
        CurrentHp = currentHp;
        CurrentMp = currentMp;
        Stats = stats;
    }

    public EntityId ActorId { get; }
    public ulong Revision { get; }
    public ActorStatProfileDefinition Profile { get; }
    public LevelProgressionDefinition Curve { get; }
    public int Level { get; }
    public ulong Experience { get; }
    public int CurrentHp { get; }
    public int CurrentMp { get; }
    public ActorStatValues Stats { get; }

    internal ActorProgressionState GainExperience(ulong amount)
    {
        ulong experience = checked(Experience + amount);
        int level = Level;
        while (level < Curve.MaximumLevel && experience >= Curve.RequiredExperience(level))
        {
            experience -= Curve.RequiredExperience(level);
            level++;
        }
        ActorStatValues stats = Profile.Calculate(level);
        bool advanced = level != Level;
        return new ActorProgressionState(ActorId, checked(Revision + 1), Profile, Curve, level, experience,
            advanced ? stats.MaxHp : CurrentHp, advanced ? stats.MaxMp : CurrentMp);
    }
}
