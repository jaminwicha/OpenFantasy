using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

public enum EncounterRetreatStatus
{
    /// <summary>The actor walked its whole committed route and broke contact; the episode ended.</summary>
    Disengaged = 1,
    /// <summary>A sample could not be walked or contact remained; combat continues and the actor decides again.</summary>
    Interrupted = 2,
    /// <summary>Incapacity or the encounter's outcome ended the committed route before completion.</summary>
    Cancelled = 3
}

public enum EncounterRetreatReason
{
    None = 0,
    Blocked = 1,
    Unadmitted = 2,
    ControlLost = 3,
    NoGroundLocomotion = 4,
    ContactRemains = 5,
    AllyRemains = 6,
    Incapacitated = 7,
    EncounterOutcome = 8
}

/// <summary>
/// A committed retreat in progress: the exact accepted command, the origin pinned by its actor revision, the
/// samples already walked and the readiness already charged. The next sample is the route entry at
/// <see cref="SamplesTaken"/>; the actor's current feet must equal the walked prefix's endpoint.
/// </summary>
public sealed class EncounterRetreatContinuation
{
    public EncounterRetreatContinuation(WorldActionCommand command, SubunitPosition origin, int samplesTaken, ulong readinessCharged)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Intent is not QueueEncounterRetreatIntent intent || samplesTaken < 0 || samplesTaken >= intent.Route.Count
            || (samplesTaken == 0) != (readinessCharged == 0)
            || QueueEncounterRetreatIntent.Endpoint(origin, intent.Route) != intent.Destination
            || WorldActionCodec.Encode(command).Length > EncounterRetreatState.MaximumCommandByteLength)
        {
            throw new ArgumentException("A retreat continuation retains its exact command, consistent origin and an unwalked remainder.");
        }
        Command = command;
        Intent = intent;
        Origin = origin;
        SamplesTaken = samplesTaken;
        ReadinessCharged = readinessCharged;
    }

    public WorldActionCommand Command { get; }
    public QueueEncounterRetreatIntent Intent { get; }
    public SubunitPosition Origin { get; }
    public int SamplesTaken { get; }
    public ulong ReadinessCharged { get; }
    public EntityId ActorId => Command.ActorId;
    public EntityId EncounterId => Intent.EncounterId;
    public bool HasStarted => SamplesTaken != 0;
    public EncounterRetreatDirection NextDirection => Intent.Route[SamplesTaken];
    public int RemainingSamples => Intent.Route.Count - SamplesTaken;
    public SubunitPosition ExpectedFeet => QueueEncounterRetreatIntent.Endpoint(Origin, Intent.Route.Take(SamplesTaken));

    internal EncounterRetreatContinuation Walked(ulong delay) =>
        new(Command, Origin, checked(SamplesTaken + 1), checked(ReadinessCharged + delay));
}

/// <summary>The latest terminal retreat record; health, position and debt keep their own owners.</summary>
public sealed record EncounterRetreatOutcome
{
    public const int MaximumCancelledDecisions = 64;

    public EncounterRetreatOutcome(EntityId actorId, EntityId commandId, EntityId encounterId, SubunitPosition origin,
        SubunitPosition final, int samplesTaken, int routeSamples, ulong readinessCharged, EncounterRetreatStatus status,
        EncounterRetreatReason reason, ulong worldSequence, IEnumerable<EntityId>? cancelledDecisionIds = null)
    {
        SortedSet<EntityId> cancelled = [];
        foreach (EntityId identity in cancelledDecisionIds ?? [])
        {
            if (cancelled.Count == MaximumCancelledDecisions || identity == commandId || identity == actorId || !cancelled.Add(identity))
            {
                throw new ArgumentException("Cancelled choices are bounded, unique and distinct from the retreat.", nameof(cancelledDecisionIds));
            }
        }
        if (cancelled.Count != 0 && status != EncounterRetreatStatus.Disengaged)
        {
            throw new ArgumentException("Only a disengagement ends other actors' queued choices.", nameof(cancelledDecisionIds));
        }
        CancelledDecisionIds = Array.AsReadOnly(cancelled.ToArray());
        if (!Enum.IsDefined(status) || !Enum.IsDefined(reason) || worldSequence == 0 || actorId == commandId
            || routeSamples is < 1 or > EncounterRetreatRules.MaximumRouteSamples || samplesTaken < 0 || samplesTaken > routeSamples
            || (status == EncounterRetreatStatus.Disengaged) != (reason == EncounterRetreatReason.None)
            || status == EncounterRetreatStatus.Disengaged && samplesTaken != routeSamples
            || status == EncounterRetreatStatus.Cancelled && reason is not (EncounterRetreatReason.Incapacitated or EncounterRetreatReason.EncounterOutcome))
        {
            throw new ArgumentException("A retreat record requires consistent status, reason, samples and a committed boundary.");
        }
        ActorId = actorId;
        CommandId = commandId;
        EncounterId = encounterId;
        Origin = origin;
        Final = final;
        SamplesTaken = samplesTaken;
        RouteSamples = routeSamples;
        ReadinessCharged = readinessCharged;
        Status = status;
        Reason = reason;
        WorldSequence = worldSequence;
    }

    public EntityId ActorId { get; }
    public EntityId CommandId { get; }
    public EntityId EncounterId { get; }
    public SubunitPosition Origin { get; }
    public SubunitPosition Final { get; }
    public int SamplesTaken { get; }
    public int RouteSamples { get; }
    public ulong ReadinessCharged { get; }
    public EncounterRetreatStatus Status { get; }
    public EncounterRetreatReason Reason { get; }
    public ulong WorldSequence { get; }
    /// <summary>Other actors' queued choices ended by a disengagement, retained by identity.</summary>
    public IReadOnlyList<EntityId> CancelledDecisionIds { get; }
}

/// <summary>
/// One retreat owner: legacy law, acceptance count and terminal evidence, plus either its lone continuation
/// or an explicitly selected collective interpretation. The world owns legality and publication.
/// </summary>
public sealed class EncounterRetreatState
{
    public const int MaximumCommandByteLength = 1_024;

    public EncounterRetreatState(ContentHash rulesPin, ulong acceptedCount, EncounterRetreatContinuation? active,
        EncounterRetreatOutcome? last, EncounterWithdrawalState? withdrawal = null)
    {
        ArgumentNullException.ThrowIfNull(rulesPin);
        if (withdrawal is not null && active is not null)
            throw new ArgumentException("Legacy and collective retreat cannot execute together.");
        if (rulesPin != EncounterRetreatRules.Pin || active is not null && acceptedCount == 0
            || last is not null && acceptedCount == 0 || active is not null && last?.CommandId == active.Command.CommandId)
        {
            throw new ArgumentException("Retreat state retains the pinned law, a counted acceptance and distinct records.");
        }
        RulesPin = rulesPin;
        AcceptedCount = acceptedCount;
        Active = active;
        Last = last;
        Withdrawal = withdrawal;
    }

    public ContentHash RulesPin { get; }
    public ulong AcceptedCount { get; }
    public EncounterRetreatContinuation? Active { get; }
    public EncounterRetreatOutcome? Last { get; }
    public EncounterWithdrawalState? Withdrawal { get; }
    public ContentHash ContentHash => ContentHash.Compute(Withdrawal is null
        ? EncounterRetreatCodecV1.Encode(this) : EncounterRetreatCodecV2.Encode(this));

    public static EncounterRetreatState Fresh() => new(EncounterRetreatRules.Pin, 0, null, null);

    public bool Holds(EntityId actorId) => Withdrawal?.Holds(actorId) ?? (Active?.ActorId == actorId);

    internal EncounterRetreatState WithWithdrawal(EncounterWithdrawalState value) => new(RulesPin, AcceptedCount, Active, Last, value);

    internal EncounterRetreatState Commit(WorldActionCommand command, SubunitPosition origin)
    {
        if (Active is not null || Withdrawal is not null)
        {
            throw new InvalidOperationException("Only one committed retreat can be in flight.");
        }
        return new(RulesPin, checked(AcceptedCount + 1), new EncounterRetreatContinuation(command, origin, 0, 0), Last);
    }

    internal EncounterRetreatState Continue(EncounterRetreatContinuation next) => new(RulesPin, AcceptedCount, next, Last);

    internal EncounterRetreatState Finish(EncounterRetreatOutcome outcome) => new(RulesPin, AcceptedCount, null, outcome);
}
