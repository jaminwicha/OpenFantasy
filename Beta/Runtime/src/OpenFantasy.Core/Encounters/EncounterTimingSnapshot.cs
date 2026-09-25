using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// One bounded tactical clock and its full required-decision barrier. This component neither
/// admits participants nor changes world time, health, custody, effects or controller ownership.
/// Exceeding its bound refuses the request; it does not implement the shared-pause fallback.
/// </summary>
public sealed class EncounterTimingSnapshot
{
    public const int MaximumActors = 128;
    public const ulong UnitsPerTick = 5;
    private readonly ReadOnlyCollection<EncounterActorTiming> _actors;
    private readonly ReadOnlyCollection<EncounterDecision> _decisions;
    private readonly ReadOnlyCollection<EncounterActorTiming> _dueActors;
    private readonly ReadOnlyCollection<EncounterActorTiming> _requiredDecisions;
    private readonly Dictionary<EntityId, EncounterActorTiming> _actorsById;
    private readonly Dictionary<EntityId, EncounterDecision> _decisionsByActor;

    public EncounterTimingSnapshot(EncounterTimingScope scope, ulong revision, ulong elapsedTacticalUnits,
        IEnumerable<EncounterActorTiming> actors, IEnumerable<EncounterDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(decisions);
        if (elapsedTacticalUnits % UnitsPerTick != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedTacticalUnits),
                "Tactical time must contain only whole five-unit ticks.");
        }
        if ((UInt128)elapsedTacticalUnits > (UInt128)revision * UnitsPerTick)
        {
            throw new ArgumentException("Tactical time exceeds the number of possible committed ticks.", nameof(elapsedTacticalUnits));
        }

        List<EncounterActorTiming> actorList = [];
        _actorsById = [];
        foreach (EncounterActorTiming actor in actors)
        {
            ArgumentNullException.ThrowIfNull(actor);
            if (actorList.Count == MaximumActors)
            {
                throw new ArgumentException("The complete tactical actor set exceeds its qualified bound.", nameof(actors));
            }
            if (scope.Contains(actor.ActorId) || !_actorsById.TryAdd(actor.ActorId, actor))
            {
                throw new ArgumentException("Actors must have unique identities distinct from the clock scope.", nameof(actors));
            }
            actorList.Add(actor);
        }
        if (actorList.Count == 0)
        {
            throw new ArgumentException("A tactical clock requires a nonempty actor set.", nameof(actors));
        }

        List<EncounterDecision> decisionList = [];
        HashSet<EntityId> decisionIds = [];
        _decisionsByActor = [];
        foreach (EncounterDecision decision in decisions)
        {
            ArgumentNullException.ThrowIfNull(decision);
            if (decisionList.Count == MaximumActors)
            {
                throw new ArgumentException("The pending decision set exceeds its retained bound.", nameof(decisions));
            }
            if (!_actorsById.TryGetValue(decision.ActorId, out EncounterActorTiming? actor) || !actor.IsDue ||
                scope.Contains(decision.DecisionId) || _actorsById.ContainsKey(decision.DecisionId) ||
                !decisionIds.Add(decision.DecisionId) || !_decisionsByActor.TryAdd(decision.ActorId, decision))
            {
                throw new ArgumentException("Decisions must uniquely name due actors and distinct pending identities.", nameof(decisions));
            }
            decisionList.Add(decision);
        }

        actorList.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));
        decisionList.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));
        Scope = scope;
        Revision = revision;
        ElapsedTacticalUnits = elapsedTacticalUnits;
        _actors = actorList.AsReadOnly();
        _decisions = decisionList.AsReadOnly();
        _dueActors = actorList.Where(actor => actor.IsDue).ToList().AsReadOnly();
        _requiredDecisions = _dueActors.Where(actor => actor.Control == EncounterActorControl.RequiredHuman &&
            !_decisionsByActor.ContainsKey(actor.ActorId)).ToList().AsReadOnly();
    }

    public EncounterTimingScope Scope { get; }
    public EntityId WorldId => Scope.WorldId;
    public EntityId BranchId => Scope.BranchId;
    public EntityId EncounterId => Scope.EncounterId;
    public ulong Revision { get; }
    public ulong ElapsedTacticalUnits { get; }
    public ReadOnlyCollection<EncounterActorTiming> Actors => _actors;
    public ReadOnlyCollection<EncounterDecision> Decisions => _decisions;
    public ReadOnlyCollection<EncounterActorTiming> DueActors => _dueActors;
    public ReadOnlyCollection<EncounterActorTiming> RequiredDecisions => _requiredDecisions;
    public ContentHash ContentHash => ContentHash.Compute(EncounterTimingCodecV1.Encode(this));

    public EncounterActorTiming GetActor(EntityId actorId) => _actorsById.TryGetValue(actorId, out var actor)
        ? actor : throw new KeyNotFoundException("The actor is not in this tactical clock.");

    /// <summary>
    /// Retains a choice without advancing time or granting effects. The outer authority authenticates
    /// the controller, validates the action pin, and forbids reuse of previously consumed decision IDs.
    /// </summary>
    public EncounterTimingSnapshot QueueDecision(ulong expectedRevision, EncounterDecision decision)
    {
        RequireRevision(expectedRevision);
        ArgumentNullException.ThrowIfNull(decision);
        return new EncounterTimingSnapshot(Scope, checked(Revision + 1), ElapsedTacticalUnits,
            Actors, Decisions.Append(decision));
    }

    /// <summary>Accepts exactly one tick, never wall-clock catch-up and never progress across a due actor.</summary>
    public EncounterTimingTransition TryAdvance(ulong expectedRevision)
    {
        RequireRevision(expectedRevision);
        if (DueActors.Count != 0)
        {
            EncounterTimingStatus status = RequiredDecisions.Count != 0
                ? EncounterTimingStatus.RequiredDecisionMissing
                : DueActors.Any(actor => !_decisionsByActor.ContainsKey(actor.ActorId))
                    ? EncounterTimingStatus.AutomaticDecisionMissing
                    : EncounterTimingStatus.ActionBoundaryPending;
            return new EncounterTimingTransition(this, this, status);
        }

        var next = new EncounterTimingSnapshot(Scope, checked(Revision + 1),
            checked(ElapsedTacticalUnits + UnitsPerTick),
            Actors.Select(actor => actor.WithDelay(actor.RemainingDelay > UnitsPerTick
                ? actor.RemainingDelay - UnitsPerTick : 0)), []);
        return new EncounterTimingTransition(this, next, EncounterTimingStatus.Advanced);
    }

    /// <summary>
    /// Charges the first due actor in identity order only after every required human has chosen.
    /// The outer authority must revalidate and atomically publish the request with this timing change;
    /// confirmation alone does not execute an action. Both Wait and Action retain positive debt.
    /// </summary>
    public EncounterTimingTransition TryConfirmNext(ulong expectedRevision)
    {
        RequireRevision(expectedRevision);
        if (RequiredDecisions.Count != 0)
        {
            return new EncounterTimingTransition(this, this, EncounterTimingStatus.RequiredDecisionMissing);
        }
        if (DueActors.Count == 0)
        {
            return new EncounterTimingTransition(this, this, EncounterTimingStatus.NoDueActors);
        }
        EncounterActorTiming actor = DueActors[0];
        if (!_decisionsByActor.TryGetValue(actor.ActorId, out EncounterDecision? decision))
        {
            return new EncounterTimingTransition(this, this, EncounterTimingStatus.AutomaticDecisionMissing);
        }

        var next = new EncounterTimingSnapshot(Scope, checked(Revision + 1), ElapsedTacticalUnits,
            Actors.Select(current => current.ActorId == actor.ActorId ? current.WithDelay(current.StandardDelay) : current),
            Decisions.Where(current => current.ActorId != actor.ActorId));
        return new EncounterTimingTransition(this, next, EncounterTimingStatus.Confirmed, decision);
    }

    private void RequireRevision(ulong expectedRevision)
    {
        if (expectedRevision != Revision)
        {
            throw new InvalidOperationException("The timing request was prepared against a different revision.");
        }
    }
}
