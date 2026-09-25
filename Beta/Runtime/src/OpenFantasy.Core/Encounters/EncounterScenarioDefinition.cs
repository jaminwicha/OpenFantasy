using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Encounters;

/// <summary>An authored local encounter opportunity; sides and grants apply only to this scenario.</summary>
public sealed class EncounterScenarioDefinition
{
    public const int MaximumCombatants = 64;
    public const int MaximumKeyLength = 64;
    public const int MaximumDisplayNameBytes = 128;
    public const long MaximumEntryRangeSubunits = 768;

    public EncounterScenarioDefinition(EntityId definitionId, string key, string displayName,
        EntityId initiatingActorId, EntityId targetActorId, long entryRangeSubunits,
        IEnumerable<EncounterCombatant> combatants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(combatants);
        if (key.Length > MaximumKeyLength || key[0] is < 'a' or > 'z'
            || key.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')))
        {
            throw new ArgumentException("A scenario key requires bounded lowercase ASCII letters, digits, underscores or hyphens.", nameof(key));
        }
        if (displayName.Length > MaximumDisplayNameBytes
            || CanonicalEncodingV1.Text(displayName, nameof(displayName)).Length > MaximumDisplayNameBytes
            || entryRangeSubunits is < 1 or > MaximumEntryRangeSubunits)
        {
            throw new ArgumentException("Scenario name and entry reach must fit the retained local bounds.");
        }
        SortedDictionary<EntityId, EncounterCombatant> actors = [];
        foreach (var actor in combatants)
        {
            ArgumentNullException.ThrowIfNull(actor, nameof(combatants));
            if (actors.Count == MaximumCombatants || !actors.TryAdd(actor.ActorId, actor)
                || actor.ActorId == definitionId || actor.AbilityIds.Contains(definitionId))
            {
                throw new ArgumentException("A scenario needs bounded unique actors and distinct definition roles.", nameof(combatants));
            }
        }
        if (initiatingActorId == targetActorId || !actors.TryGetValue(initiatingActorId, out var initiator)
            || !actors.TryGetValue(targetActorId, out var target) || initiator.Side == 0 || target.Side == 0
            || initiator.Side == target.Side)
        {
            throw new ArgumentException("Scenario entry requires distinct admitted actors on positive opposing sides.");
        }
        DefinitionId = definitionId;
        Key = key;
        DisplayName = displayName;
        InitiatingActorId = initiatingActorId;
        TargetActorId = targetActorId;
        EntryRangeSubunits = entryRangeSubunits;
        Combatants = Array.AsReadOnly(actors.Values.ToArray());
        Version = ContentHash.Compute(EncounterLifecycleCodecV1.EncodeScenario(this));
    }

    public EntityId DefinitionId { get; }
    public string Key { get; }
    public string DisplayName { get; }
    public EntityId InitiatingActorId { get; }
    public EntityId TargetActorId { get; }
    public long EntryRangeSubunits { get; }
    public ReadOnlyCollection<EncounterCombatant> Combatants { get; }
    public ContentHash Version { get; }
}
