using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Journaled controller modes for a fixed admitted roster. This owner validates transitions, not transport
/// authentication, historical cause uniqueness, world residency or the world's safe-reconnection boundary.
/// </summary>
public sealed class WorldControllerState
{
    public const int MaximumActors = 4;
    public const ulong ReconnectAllowanceMilliseconds = 30_000;
    public const string RulesText = "world.controllers.v1; fixed admitted roster of one to four human actors; "
        + "authenticated human input requires Human; accepted Disconnected records its cause as the disconnect episode "
        + "and enters ReconnectAllowance; the allowance withholds required human choices and grants no autonomous control; "
        + "host-observed elapsed duration of at least 30000 milliseconds permits AllowanceExpired for that exact episode "
        + "and grants Autonomous host-policy control; authenticated Reconnected at a world-approved safe boundary restores "
        + "Human from either disconnected mode for the exact episode; each event increments the actor control revision once; "
        + "events never advance simulation clocks, change credentials, alter ownership or manufacture a gameplay decision; "
        + "no stale event or no-op retry is admitted; restoring a saved root alone changes no controller state";
    public static ContentHash RulesPin { get; } = ContentHash.Compute(CanonicalEncodingV1.Text(RulesText, nameof(RulesText)));

    private readonly Dictionary<EntityId, WorldControllerAssignment> _byActor;

    public WorldControllerState(IEnumerable<WorldControllerAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        var rows = new SortedDictionary<EntityId, WorldControllerAssignment>();
        foreach (WorldControllerAssignment row in assignments)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (rows.Count == MaximumActors || !rows.TryAdd(row.ActorId, row))
                throw new ArgumentException("Controller ownership requires one distinct assignment per actor within the roster bound.", nameof(assignments));
        }
        if (rows.Count == 0) throw new ArgumentException("Controller ownership requires a nonempty admitted roster.", nameof(assignments));
        var episodes = rows.Values.Where(row => row.DisconnectId is not null).Select(row => row.DisconnectId!.Value).ToArray();
        if (episodes.Distinct().Count() != episodes.Length || episodes.Any(rows.ContainsKey))
            throw new ArgumentException("Current disconnect episodes must be distinct from each other and from actor identities.", nameof(assignments));
        Assignments = Array.AsReadOnly(rows.Values.ToArray());
        _byActor = rows.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public ReadOnlyCollection<WorldControllerAssignment> Assignments { get; }
    public ContentHash ContentHash => ContentHash.Compute(WorldControllerCodecV1.Encode(this));

    public static WorldControllerState Fresh(IEnumerable<EntityId> actors)
    {
        ArgumentNullException.ThrowIfNull(actors);
        return new(actors.Select(actor => new WorldControllerAssignment(actor, 0, WorldControllerMode.Human, null)));
    }

    public WorldControllerAssignment Get(EntityId actorId) => _byActor.TryGetValue(actorId, out var assignment)
        ? assignment : throw new ArgumentException("The actor is outside the admitted controller roster.", nameof(actorId));

    public WorldControllerState Apply(WorldControllerEvent input, EntityId causeId)
    {
        ArgumentNullException.ThrowIfNull(input);
        WorldControllerAssignment current = Get(input.ActorId);
        if (current.Revision != input.ExpectedControlRevision)
            throw new InvalidOperationException("The controller event names a stale control revision.");
        if (_byActor.ContainsKey(causeId) || Assignments.Any(row => row.DisconnectId == causeId))
            throw new ArgumentException("A new controller cause cannot alias a current actor or disconnect episode.", nameof(causeId));
        WorldControllerMode next = input.Kind switch
        {
            WorldControllerEventKind.Disconnected when current.Mode == WorldControllerMode.Human => WorldControllerMode.ReconnectAllowance,
            WorldControllerEventKind.AllowanceExpired when current.Mode == WorldControllerMode.ReconnectAllowance
                && current.DisconnectId == input.DisconnectId => WorldControllerMode.Autonomous,
            WorldControllerEventKind.Reconnected when (current.Mode is WorldControllerMode.ReconnectAllowance or WorldControllerMode.Autonomous)
                && current.DisconnectId == input.DisconnectId => WorldControllerMode.Human,
            _ => throw new InvalidOperationException("The controller event is impossible in this mode or names another disconnect episode.")
        };
        var updated = new WorldControllerAssignment(current.ActorId, checked(current.Revision + 1), next,
            next == WorldControllerMode.Human ? null : input.Kind == WorldControllerEventKind.Disconnected ? causeId : current.DisconnectId);
        return new(Assignments.Select(row => row.ActorId == current.ActorId ? updated : row));
    }
}
