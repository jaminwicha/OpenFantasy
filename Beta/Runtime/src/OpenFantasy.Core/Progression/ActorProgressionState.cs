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

    /// <summary>Prepares capped damage. Target legality and the damage rule belong to the invoking action.</summary>
    public ActorResourceTransition TakeDamage(uint amount)
    {
        int actual = (int)Math.Min(amount, (uint)CurrentHp);
        return PrepareResources(ActorResourceOperation.Damage, amount, CurrentHp - actual, CurrentMp);
    }

    /// <summary>Prepares additive vitality restoration; it does not treat injuries or decide life/death eligibility.</summary>
    public ActorResourceTransition RestoreHealth(uint amount)
    {
        int actual = (int)Math.Min(amount, (uint)(Stats.MaxHp - CurrentHp));
        return PrepareResources(ActorResourceOperation.HealthRestoration, amount, CurrentHp + actual, CurrentMp);
    }

    /// <summary>Prepares the exact magic cost. Insufficient resources cannot pay a partial cost.</summary>
    public ActorResourceTransition SpendMagic(uint amount)
    {
        if (amount > (uint)CurrentMp)
        {
            throw new InvalidOperationException("The actor cannot pay the complete magic cost.");
        }
        return PrepareResources(ActorResourceOperation.MagicExpenditure, amount, CurrentHp, CurrentMp - (int)amount);
    }

    private ActorResourceTransition PrepareResources(ActorResourceOperation operation, uint amount, int hp, int mp)
    {
        var candidate = hp == CurrentHp && mp == CurrentMp ? this
            : new ActorProgressionState(ActorId, checked(Revision + 1), Profile, Curve, Level, Experience, hp, mp);
        return new(this, candidate, operation, amount);
    }

    internal ActorResourceTransition GainExperience(ulong amount)
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
        var candidate = new ActorProgressionState(ActorId, checked(Revision + 1), Profile, Curve, level, experience,
            advanced ? stats.MaxHp : CurrentHp, advanced ? stats.MaxMp : CurrentMp);
        return new(this, candidate, ActorResourceOperation.ExperienceReward, amount);
    }
}
