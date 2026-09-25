using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Encounters;

/// <summary>Explicit sides and ability grants for one encounter; a side is not a species or alignment.</summary>
public sealed class EncounterCombatant
{
    public const int MaximumAbilities = 32;
    public const ushort MaximumSide = 64;

    public EncounterCombatant(EntityId actorId, ushort side, IEnumerable<EntityId> abilityIds)
    {
        ArgumentNullException.ThrowIfNull(abilityIds);
        if (side > MaximumSide)
        {
            throw new ArgumentOutOfRangeException(nameof(side));
        }
        SortedSet<EntityId> abilities = [];
        foreach (EntityId ability in abilityIds)
        {
            if (abilities.Count == MaximumAbilities || !abilities.Add(ability))
            {
                throw new ArgumentException("Ability grants must be bounded and unique.", nameof(abilityIds));
            }
        }
        if (side == 0 && abilities.Count != 0)
        {
            throw new ArgumentException("A coupled bystander cannot carry combat ability grants.", nameof(abilityIds));
        }
        ActorId = actorId;
        Side = side;
        AbilityIds = Array.AsReadOnly(abilities.ToArray());
    }

    public EntityId ActorId { get; }
    public ushort Side { get; }
    public ReadOnlyCollection<EntityId> AbilityIds { get; }
}
