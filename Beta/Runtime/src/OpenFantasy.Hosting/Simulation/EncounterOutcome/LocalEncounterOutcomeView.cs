using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation.RoadThreat;

namespace OpenFantasy.Hosting.Simulation.EncounterOutcome;

/// <summary>
/// Where the observer's current or last outcome stands. Reading names remain for compatibility; controller worlds
/// interpret them as recorded character acknowledgment through <see cref="LocalOutcomeAcknowledgmentMode"/>.
/// </summary>
public enum LocalOutcomeStage
{
    /// <summary>The observer is a required reader who has not yet acknowledged this exact outcome.</summary>
    AwaitingYourReading = 1,
    /// <summary>
    /// Other required humans have not read, so the world holds. A required observer has already read; an observer outside
    /// the fight (<see cref="LocalOutcomeStanding.Observer"/>) has no reading of its own to give.
    /// </summary>
    AwaitingOthers = 2,
    /// <summary>Every required human has read; the world completes the episode at its next ready boundary.</summary>
    Completing = 3,
    /// <summary>A care obligation holds the unfinalized episode; completion waits for the helper's real treatment.</summary>
    CarePending = 4,
    /// <summary>The episode was completed by the world; this is its historical record.</summary>
    Completed = 5
}

/// <summary>The outcome from the observer's side.</summary>
public enum LocalOutcomeStanding { YourSideStands = 1, OpponentStands = 2, NoSideStands = 3, Observer = 4 }

/// <summary>Why the observer cannot take an ordinary bodily action now, if so.</summary>
public enum LocalOutcomeRestriction { None = 0, AwaitingCompletion = 1, Incapacitated = 2, Recovering = 3 }

/// <summary>How recorded character acknowledgments are described, independently of the current controller assignment.</summary>
public enum LocalOutcomeAcknowledgmentMode { HumanReading = 0, ControllerAcceptance = 1 }

/// <summary>A participant's present bodily condition, disclosed only to the actor itself or to a current observer.</summary>
public sealed record LocalOutcomeCondition(int CurrentHp, int MaxHp)
{
    public bool IsIncapacitated => CurrentHp == 0;
}

/// <summary>
/// One positive-side participant of the outcome; bystanders are not listed. <see cref="OnStandingSide"/> is an outcome
/// fact (recomputed while unfinalized, the completion record's standing side afterwards). <see cref="Condition"/> is
/// present condition, never history: it is the observer's own, or another participant's only while the observer
/// currently observes it under the road view's bound (same floor, clear sightline, three cells); otherwise null.
/// </summary>
public sealed record LocalOutcomeParticipantView(EntityId ActorId, string Name, int Side, bool OnStandingSide, bool IsObserver,
    LocalOutcomeCondition? Condition);

/// <summary>
/// The observer's current unfinalized outcome or last completed record under the outcome-completion interpretation.
/// It exposes the exact acknowledgment intent only to a required reader who has not yet read; it grants nothing, carries
/// no other actor's command, credentials or private inventory, and is null in every other world (omitted publicly).
/// Outcome facts (result, standing side, readers, sequence) and present condition are kept apart: a completed record never
/// reports historical health, and nobody's current health is disclosed beyond the observer's own or a bounded observation.
/// </summary>
public sealed class LocalEncounterOutcomeView
{
    internal LocalEncounterOutcomeView(LocalOutcomeStage stage, EntityId encounterId, EncounterOutcomeResult result,
        int standingSide, LocalOutcomeStanding standing, IEnumerable<LocalOutcomeParticipantView> participants,
        AcknowledgeEncounterOutcomeIntent? acknowledgment, int requiredReaders, int readersRemaining, bool observerHasRead,
        LocalOutcomeRestriction restriction, ulong observerRecoveryDelay, ulong? completedAtSequence)
        : this(stage, encounterId, result, standingSide, standing, participants, acknowledgment, requiredReaders,
            readersRemaining, observerHasRead, restriction, observerRecoveryDelay, completedAtSequence,
            LocalOutcomeAcknowledgmentMode.HumanReading)
    {
    }

    internal LocalEncounterOutcomeView(LocalOutcomeStage stage, EntityId encounterId, EncounterOutcomeResult result,
        int standingSide, LocalOutcomeStanding standing, IEnumerable<LocalOutcomeParticipantView> participants,
        AcknowledgeEncounterOutcomeIntent? acknowledgment, int requiredReaders, int readersRemaining, bool observerHasRead,
        LocalOutcomeRestriction restriction, ulong observerRecoveryDelay, ulong? completedAtSequence,
        LocalOutcomeAcknowledgmentMode acknowledgmentMode = LocalOutcomeAcknowledgmentMode.HumanReading)
    {
        if (!Enum.IsDefined(acknowledgmentMode)) throw new ArgumentOutOfRangeException(nameof(acknowledgmentMode));
        Stage = stage;
        EncounterId = encounterId;
        Result = result;
        StandingSide = standingSide;
        Standing = standing;
        Participants = Array.AsReadOnly(participants.ToArray());
        Acknowledgment = acknowledgment;
        RequiredReaders = requiredReaders;
        ReadersRemaining = readersRemaining;
        ObserverHasRead = observerHasRead;
        Restriction = restriction;
        ObserverRecoveryDelay = observerRecoveryDelay;
        CompletedAtSequence = completedAtSequence;
        AcknowledgmentMode = acknowledgmentMode;
    }

    public LocalOutcomeStage Stage { get; }
    public EntityId EncounterId { get; }
    public EncounterOutcomeResult Result { get; }
    public int StandingSide { get; }
    public LocalOutcomeStanding Standing { get; }
    public ReadOnlyCollection<LocalOutcomeParticipantView> Participants { get; }
    /// <summary>The exact reading the observer may submit now; null when nothing is awaited from the observer.</summary>
    public AcknowledgeEncounterOutcomeIntent? Acknowledgment { get; }
    public int RequiredReaders { get; }
    public int ReadersRemaining { get; }
    public bool ObserverHasRead { get; }
    public LocalOutcomeRestriction Restriction { get; }
    public ulong ObserverRecoveryDelay { get; }
    public ulong? CompletedAtSequence { get; }
    /// <summary>
    /// In controller worlds the compatibility reader fields describe recorded character acknowledgments, not evidence
    /// that the currently connected person read the outcome. The mode persists through completion and human rejoin.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public LocalOutcomeAcknowledgmentMode AcknowledgmentMode { get; }
    /// <summary>Whether the observer fought on a positive side; an observer outside the fight owes no reading.</summary>
    public bool ObserverIsParticipant => Standing != LocalOutcomeStanding.Observer;
    /// <summary>
    /// The observer took part, is incapacitated and the episode is complete: no ordinary action, and the aftermath-care
    /// opportunity closed when the outcome was accepted.
    /// </summary>
    public bool IsTerminal => Stage == LocalOutcomeStage.Completed && ObserverIsParticipant
        && Restriction == LocalOutcomeRestriction.Incapacitated;

    public static LocalEncounterOutcomeView? Project(WorldSnapshot world, EntityId observerId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.EncounterOutcome is not { } outcome || world.EncounterLifecycle is not { } lifecycle) return null;
        LocalOutcomeAcknowledgmentMode acknowledgmentMode = world.Controllers is null
            ? LocalOutcomeAcknowledgmentMode.HumanReading : LocalOutcomeAcknowledgmentMode.ControllerAcceptance;
        bool incapacitated = world.Progression!.GetActor(observerId).CurrentHp == 0;
        ulong debt = lifecycle.GetDebt(observerId).RemainingDelay;
        if (WorldEncounterOutcomes.Determine(world) is { } current)
        {
            EncounterActionSnapshot actions = lifecycle.Actions!;
            bool required = current.RequiredHumans.Contains(observerId);
            bool read = outcome.HasAcknowledged(observerId);
            int remaining = current.RequiredHumans.Count(actor => !outcome.HasAcknowledged(actor));
            LocalOutcomeStage stage = world.Care?.Pending is not null ? LocalOutcomeStage.CarePending
                : required && !read ? LocalOutcomeStage.AwaitingYourReading
                : remaining != 0 ? LocalOutcomeStage.AwaitingOthers : LocalOutcomeStage.Completing;
            return new LocalEncounterOutcomeView(stage, current.EncounterId, current.Result, current.StandingSide,
                StandingOf(actions, observerId, current.StandingSide),
                ProjectParticipants(world, actions, observerId, current.StandingSide),
                stage == LocalOutcomeStage.AwaitingYourReading ? new AcknowledgeEncounterOutcomeIntent(current.EncounterId, current.ActionHash) : null,
                current.RequiredHumans.Count, remaining, read,
                incapacitated ? LocalOutcomeRestriction.Incapacitated : LocalOutcomeRestriction.AwaitingCompletion, debt, null,
                acknowledgmentMode);
        }
        if (outcome.Last is not { } last || lifecycle.Phase != EncounterLifecyclePhase.Exploration
            || lifecycle.Actions is not { } archive || archive.Scope.EncounterId != last.EncounterId)
        {
            return null;
        }
        LocalOutcomeRestriction restriction = incapacitated ? LocalOutcomeRestriction.Incapacitated
            : debt != 0 ? LocalOutcomeRestriction.Recovering : LocalOutcomeRestriction.None;
        // The completed record: result, standing side, readers and sequence are its facts; condition stays present-only.
        return new LocalEncounterOutcomeView(LocalOutcomeStage.Completed, last.EncounterId, last.Result, last.StandingSide,
            StandingOf(archive, observerId, last.StandingSide), ProjectParticipants(world, archive, observerId, last.StandingSide),
            null, last.AcknowledgedActorIds.Count, 0, last.AcknowledgedActorIds.Contains(observerId), restriction, debt,
            last.WorldSequence, acknowledgmentMode);
    }

    private static LocalOutcomeStanding StandingOf(EncounterActionSnapshot actions, EntityId observerId, int standingSide)
    {
        int side = actions.Combatants.SingleOrDefault(actor => actor.ActorId == observerId)?.Side ?? 0;
        return side == 0 ? LocalOutcomeStanding.Observer
            : standingSide == 0 ? LocalOutcomeStanding.NoSideStands
            : standingSide == side ? LocalOutcomeStanding.YourSideStands : LocalOutcomeStanding.OpponentStands;
    }

    private static IEnumerable<LocalOutcomeParticipantView> ProjectParticipants(WorldSnapshot world, EncounterActionSnapshot actions,
        EntityId observerId, int standingSide) => actions.Combatants.Where(actor => actor.Side != 0).Select(actor =>
            new LocalOutcomeParticipantView(actor.ActorId, world.GetResident(actor.ActorId).DisplayName, actor.Side,
                actor.Side == standingSide, actor.ActorId == observerId, ObservedCondition(world, observerId, actor.ActorId)));

    /// <summary>The observer's own condition, or another actor's only while it is currently observed; otherwise none.</summary>
    private static LocalOutcomeCondition? ObservedCondition(WorldSnapshot world, EntityId observerId, EntityId actorId)
    {
        if (actorId != observerId && !LocalRoadThreatView.Observes(world.Space, observerId, actorId)) return null;
        var resources = world.Progression!.GetActor(actorId);
        return new LocalOutcomeCondition(resources.CurrentHp, resources.Stats.MaxHp);
    }
}
