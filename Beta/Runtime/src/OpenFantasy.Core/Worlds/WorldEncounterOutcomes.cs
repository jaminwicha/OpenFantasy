using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The resolved outcome as the actual bodies show it at the current Outcome boundary: the episode, its final action
/// archive, the one standing positive side (or none) and the required humans who must read it before completion.
/// </summary>
public sealed record EncounterOutcomeDetermination(EntityId EncounterId, ContentHash ActionHash, EncounterOutcomeResult Result,
    int StandingSide, IReadOnlyList<EntityId> RequiredHumans);

/// <summary>
/// Profile-35 outcome rules. The result is always recomputed from canonical owners (lifecycle archive, progression HP,
/// resident control), never stored twice while pending. Completion depends on readers and pending care, never on which
/// actor initiated or survived.
/// </summary>
public static class WorldEncounterOutcomes
{
    /// <summary>The current unfinalized outcome, or null outside an Outcome phase or in another interpretation.</summary>
    public static EncounterOutcomeDetermination? Determine(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.EncounterOutcome is null || world.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Outcome } lifecycle
            || lifecycle.Actions is not { } actions)
        {
            return null;
        }
        int[] standing = actions.Combatants
            .Where(actor => actor.Side != 0 && world.Progression!.GetActor(actor.ActorId).CurrentHp > 0)
            .Select(actor => (int)actor.Side).Distinct().ToArray();
        if (standing.Length > 1) throw new InvalidOperationException("An outcome leaves at most one positive side standing.");
        return new EncounterOutcomeDetermination(actions.Scope.EncounterId, actions.ContentHash,
            standing.Length == 0 ? EncounterOutcomeResult.NoSurvivors : EncounterOutcomeResult.Victory,
            standing.Length == 0 ? 0 : standing[0], RequiredHumans(world, actions));
    }

    /// <summary>Positive-side participants controlled by a player, living or incapacitated, in identity order.</summary>
    public static IReadOnlyList<EntityId> RequiredHumans(WorldSnapshot world, EncounterActionSnapshot actions)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(actions);
        return actions.Combatants.Where(actor => actor.Side != 0
                && world.GetResident(actor.ActorId).ControllerKind == ResidentControllerKind.Player)
            .Select(actor => actor.ActorId).Order().ToArray();
    }

    /// <summary>Every required human read this exact outcome, no care obligation is pending and nothing else holds.</summary>
    public static bool IsReadyToFinalize(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.EncounterOutcome is { } outcome && Determine(world) is { } determination
            && world.Care?.Pending is null && !world.HasReadingHold && world.PendingExcavation is null
            && determination.RequiredHumans.All(outcome.HasAcknowledged)
            && outcome.Acknowledgments.All(row => row.EncounterId == determination.EncounterId
                && row.ActionHash == determination.ActionHash);
    }

    /// <summary>Required humans who have not yet read the current outcome.</summary>
    public static IReadOnlyList<EntityId> AwaitingReaders(WorldSnapshot world) =>
        world.EncounterOutcome is { } outcome && Determine(world) is { } determination
            ? determination.RequiredHumans.Where(actor => !outcome.HasAcknowledged(actor)).ToArray() : [];

    internal static WorldActionRejection EvaluateAcknowledgment(WorldSnapshot world, EntityId actorId,
        AcknowledgeEncounterOutcomeIntent intent)
    {
        if (world.EncounterOutcome is not { } outcome || Determine(world) is not { } determination
            || world.Care?.Pending is not null || determination.EncounterId != intent.EncounterId)
        {
            return WorldActionRejection.Unavailable;
        }
        if (!determination.RequiredHumans.Contains(actorId)) return WorldActionRejection.NotAuthorized;
        if (determination.ActionHash != intent.ExpectedActionHash) return WorldActionRejection.RevisionConflict;
        return outcome.HasAcknowledged(actorId) ? WorldActionRejection.Unavailable : WorldActionRejection.None;
    }

    /// <summary>The historical record of the completion this boundary performs.</summary>
    internal static EncounterOutcomeRecord Record(WorldSnapshot world, ulong worldSequence)
    {
        if (!IsReadyToFinalize(world)) throw new InvalidOperationException("Completion requires every required reader and no pending care.");
        EncounterOutcomeDetermination determination = Determine(world)!;
        return new EncounterOutcomeRecord(determination.EncounterId, determination.ActionHash, determination.Result,
            determination.StandingSide, worldSequence, determination.RequiredHumans);
    }
}
