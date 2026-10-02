using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Encounters;

/// <summary>What the actual bodies left standing when an episode was finalized.</summary>
public enum EncounterOutcomeResult { Victory = 1, NoSurvivors = 2 }

/// <summary>
/// One required human's reading of an exact, not yet finalized outcome. It is a controller fact: it grants no bodily
/// action, time, health, debt change or reward, and it names the exact episode and final action archive it read.
/// </summary>
public sealed record EncounterOutcomeAcknowledgment(EntityId ActorId, EntityId CommandId, EntityId EncounterId,
    ContentHash ActionHash, ulong WorldSequence);

/// <summary>
/// Historical receipt of one world-owned completion: the finalized episode and archive, what the bodies showed at that
/// boundary (the standing side, or none) and which required humans had read it. Later lawful changes never rewrite it.
/// </summary>
public sealed class EncounterOutcomeRecord
{
    public EncounterOutcomeRecord(EntityId encounterId, ContentHash actionHash, EncounterOutcomeResult result,
        int standingSide, ulong worldSequence, IEnumerable<EntityId> acknowledgedActorIds)
    {
        ArgumentNullException.ThrowIfNull(actionHash);
        ArgumentNullException.ThrowIfNull(acknowledgedActorIds);
        EntityId[] acknowledged = [.. acknowledgedActorIds];
        if (!Enum.IsDefined(result) || (result == EncounterOutcomeResult.Victory) != (standingSide > 0) || standingSide < 0
            || standingSide > byte.MaxValue || worldSequence == 0 || acknowledged.Length > EncounterOutcomeState.MaximumAcknowledgments
            || acknowledged.Where((identity, index) => index > 0 && identity.CompareTo(acknowledged[index - 1]) <= 0).Any())
        {
            throw new ArgumentException("A finalization record needs a defined result, its standing side and ordered unique readers.");
        }
        EncounterId = encounterId;
        ActionHash = actionHash;
        Result = result;
        StandingSide = standingSide;
        WorldSequence = worldSequence;
        AcknowledgedActorIds = Array.AsReadOnly(acknowledged);
    }

    public EntityId EncounterId { get; }
    public ContentHash ActionHash { get; }
    public EncounterOutcomeResult Result { get; }
    /// <summary>The one positive side with a living member, or zero when no side is standing.</summary>
    public int StandingSide { get; }
    public ulong WorldSequence { get; }
    public ReadOnlyCollection<EntityId> AcknowledgedActorIds { get; }
}

/// <summary>
/// Root owner of the explicit encounter-outcome interpretation (profile 35): its pinned law, the number of finalized
/// episodes, the required humans' readings of the current unfinalized outcome and the latest finalization record.
/// It owns no HP, body, position, debt, possession or time; those stay in their canonical owners.
/// </summary>
public sealed class EncounterOutcomeState
{
    public const int MaximumAcknowledgments = EncounterLifecycleSnapshot.MaximumActors;
    public const string RulesText =
        "encounter.outcome.v1: an episode resolved by actual bodies is completed by the world at a zero-duration "
        + "boundary once every positive-side participant controlled by a player has acknowledged that exact outcome and no "
        + "care obligation is pending; acknowledgment is a controller fact without bodily action; completion retains every "
        + "body, position, health, debt, possession, history and identity and issues no reward";

    public static ContentHash Pin { get; } = ContentHash.Compute(CanonicalEncodingV1.Text(RulesText, nameof(RulesText)));

    public EncounterOutcomeState(ContentHash rulesPin, ulong finalizedCount,
        IEnumerable<EncounterOutcomeAcknowledgment> acknowledgments, EncounterOutcomeRecord? last)
    {
        ArgumentNullException.ThrowIfNull(rulesPin);
        ArgumentNullException.ThrowIfNull(acknowledgments);
        // The pin names the only law this owner can interpret; another law's owner must never be read as this one.
        if (rulesPin != Pin)
        {
            throw new ArgumentException("The outcome owner supports only the encounter.outcome.v1 rules pin.", nameof(rulesPin));
        }
        EncounterOutcomeAcknowledgment[] rows = [.. acknowledgments];
        if (rows.Length > MaximumAcknowledgments || rows.Any(row => row is null || row.ActionHash is null || row.WorldSequence == 0)
            || rows.Where((row, index) => index > 0 && row.ActorId.CompareTo(rows[index - 1].ActorId) <= 0).Any()
            || rows.Select(row => (row.EncounterId, row.ActionHash.Value)).Distinct().Count() > 1
            || rows.Select(row => row.CommandId).Distinct().Count() != rows.Length
            || (last is null) != (finalizedCount == 0)
            || last is not null && rows.Any(row => row.EncounterId == last.EncounterId))
        {
            throw new ArgumentException("Outcome readings name one exact unfinalized outcome, once per actor and command, in actor order.");
        }
        RulesPin = rulesPin;
        FinalizedCount = finalizedCount;
        Acknowledgments = Array.AsReadOnly(rows);
        Last = last;
    }

    public ContentHash RulesPin { get; }
    public ulong FinalizedCount { get; }
    public ReadOnlyCollection<EncounterOutcomeAcknowledgment> Acknowledgments { get; }
    public EncounterOutcomeRecord? Last { get; }
    public ContentHash ContentHash => ContentHash.Compute(EncounterOutcomeCodecV1.Encode(this));

    public static EncounterOutcomeState Fresh() => new(Pin, 0, [], null);

    public bool HasAcknowledged(EntityId actorId) => Acknowledgments.Any(row => row.ActorId == actorId);

    internal EncounterOutcomeState Acknowledge(EncounterOutcomeAcknowledgment acknowledgment) =>
        new(RulesPin, FinalizedCount, Acknowledgments.Append(acknowledgment).OrderBy(row => row.ActorId), Last);

    internal EncounterOutcomeState Finalize(EncounterOutcomeRecord record) =>
        new(RulesPin, checked(FinalizedCount + 1), [], record);

    /// <summary>Care resumed the unfinalized episode: earlier readings describe an outcome that no longer exists.</summary>
    internal EncounterOutcomeState ClearAcknowledgments() => Acknowledgments.Count == 0 ? this : new(RulesPin, FinalizedCount, [], Last);
}
