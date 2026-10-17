using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Encounters;

public enum EncounterWithdrawalEndReason
{
    Disengaged = 1,
    Blocked = 2,
    Unadmitted = 3,
    ControlLost = 4,
    NoGroundLocomotion = 5,
    ContactRemains = 6,
    ChoiceChanged = 7,
    Incapacitated = 8,
    EncounterEnded = 9
}

/// <summary>Exact ended choice and its paid unsuccessful sample, if any. Earlier records remain in world history.</summary>
public sealed record EncounterWithdrawalEnd
{
    public EncounterWithdrawalEnd(EncounterWithdrawalMember member, EncounterWithdrawalEndReason reason, ulong failedSampleCost = 0)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!Enum.IsDefined(reason) || reason == EncounterWithdrawalEndReason.Disengaged && !member.HasArrived
            || reason == EncounterWithdrawalEndReason.ContactRemains && !member.HasArrived
            || (reason is EncounterWithdrawalEndReason.Blocked or EncounterWithdrawalEndReason.Unadmitted
                or EncounterWithdrawalEndReason.ControlLost or EncounterWithdrawalEndReason.NoGroundLocomotion)
                != (failedSampleCost == member.SampleReadiness)
            || failedSampleCost != 0 && (failedSampleCost != member.SampleReadiness || member.HasArrived))
        {
            throw new ArgumentException("A terminal withdrawal retains consistent progress and paid attempted movement.");
        }
        Member = member;
        Reason = reason;
        FailedSampleCost = failedSampleCost;
    }

    public EncounterWithdrawalMember Member { get; }
    public EncounterWithdrawalEndReason Reason { get; }
    public ulong FailedSampleCost { get; }
    public ulong ReadinessCharged => checked(Member.WalkedReadiness + FailedSampleCost);
}

/// <summary>The latest boundary that ends one or more independent choices; time and resources keep their owners.</summary>
public sealed class EncounterWithdrawalResolution
{
    public EncounterWithdrawalResolution(ulong worldSequence, IEnumerable<EncounterWithdrawalEnd> ended,
        IEnumerable<EntityId>? cancelledDecisions = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(worldSequence);
        ArgumentNullException.ThrowIfNull(ended);
        var ordered = ended.OrderBy(value => value.Member.ActorId).ToArray();
        if (ordered.Length is 0 or > EncounterTimingSnapshot.MaximumActors
            || ordered.Select(value => value.Member.ActorId).Distinct().Count() != ordered.Length
            || ordered.Select(value => value.Member.Command.CommandId).Distinct().Count() != ordered.Length
            || ordered.Any(value => value.Member.Command.WorldId != ordered[0].Member.Command.WorldId
                || value.Member.Command.BranchId != ordered[0].Member.Command.BranchId
                || value.Member.Intent.EncounterId != ordered[0].Member.Intent.EncounterId))
        {
            throw new ArgumentException("A withdrawal resolution retains unique choices from one episode.");
        }
        var cancelled = (cancelledDecisions ?? []).Order().ToArray();
        if (cancelled.Length > EncounterTimingSnapshot.MaximumActors || cancelled.Distinct().Count() != cancelled.Length
            || cancelled.Intersect(ordered.Select(value => value.Member.Command.CommandId)).Any()
            || cancelled.Length != 0 && !ordered.Any(value => value.Reason == EncounterWithdrawalEndReason.Disengaged))
        {
            throw new ArgumentException("Departure records distinct cancelled decisions separately from ended routes.");
        }
        WorldSequence = worldSequence;
        Ended = Array.AsReadOnly(ordered);
        CancelledDecisions = Array.AsReadOnly(cancelled);
    }

    public ulong WorldSequence { get; }
    public ReadOnlyCollection<EncounterWithdrawalEnd> Ended { get; }
    public ReadOnlyCollection<EntityId> CancelledDecisions { get; }
}

/// <summary>One encounter withdrawal owner with independent side books and bounded latest terminal evidence.</summary>
public sealed class EncounterWithdrawalState
{
    public const string RulesText = "encounter.withdrawal-world.v1;two-positive-sides;actors<=128;parties<=2;"
        + "independent-authenticated-choices;complete-side-required;full-wait;captured-speed;paid-32-subunit-samples;"
        + "arrivals-remain-in-encounter;arrivals-require-ordinary-due-choices;cancel-preserves-debt;"
        + "incapacity-never-removes-roster;collective-clear-contact;retain-latest-terminal-boundary;root-bytes<=16777216";
    public static ContentHash RulesPin { get; } = ContentHash.Compute(CanonicalEncodingV1.Text(RulesText, nameof(RulesText)));

    public EncounterWithdrawalState(ulong revision, IEnumerable<EncounterWithdrawalParty> parties,
        EncounterWithdrawalResolution? last = null)
    {
        ArgumentNullException.ThrowIfNull(parties);
        var ordered = parties.OrderBy(party => party.Side).ToArray();
        if (ordered.Length > 2 || ordered.Select(party => party.Side).Distinct().Count() != ordered.Length
            || ordered.Sum(party => party.RequiredActors.Count) > EncounterTimingSnapshot.MaximumActors
            || ordered.SelectMany(party => party.RequiredActors).Distinct().Count() != ordered.Sum(party => party.RequiredActors.Count)
            || ordered.Any(party => party.Scope != ordered[0].Scope)
            || revision == 0 && (ordered.Length != 0 || last is not null))
        {
            throw new ArgumentException("Withdrawal state requires bounded distinct sides in one episode.");
        }
        var commands = ordered.SelectMany(party => party.Members).Select(member => member.Command.CommandId).ToArray();
        var actors = ordered.SelectMany(party => party.RequiredActors).ToHashSet();
        if (commands.Distinct().Count() != commands.Length || commands.Any(actors.Contains)
            || last is not null && commands.Intersect(last.Ended.Select(value => value.Member.Command.CommandId)).Any())
        {
            throw new ArgumentException("Active and ended withdrawal command identities cannot alias.");
        }
        Revision = revision;
        Parties = Array.AsReadOnly(ordered);
        Last = last;
    }

    public ulong Revision { get; }
    public ReadOnlyCollection<EncounterWithdrawalParty> Parties { get; }
    public EncounterWithdrawalResolution? Last { get; }
    public static EncounterWithdrawalState Empty { get; } = new(0, []);

    public EncounterWithdrawalMember? FindMember(EntityId actorId) => Parties.SelectMany(party => party.Members)
        .SingleOrDefault(member => member.ActorId == actorId);

    public bool Holds(EntityId actorId) => FindMember(actorId) is { HasArrived: false };

    internal EncounterWithdrawalState WithParty(EncounterWithdrawalParty party) => new(checked(Revision + 1),
        Parties.Where(value => value.Side != party.Side).Append(party), Last);

    internal EncounterWithdrawalState End(ulong sequence, IEnumerable<EncounterWithdrawalEnd> ended,
        bool closeEncounter = false, IEnumerable<EntityId>? cancelledDecisions = null)
    {
        var resolution = new EncounterWithdrawalResolution(sequence, ended, cancelledDecisions);
        var parties = Parties.ToArray();
        foreach (var end in resolution.Ended)
        {
            int index = Array.FindIndex(parties, party => party.Members.Any(member => member.ActorId == end.Member.ActorId));
            var retained = index < 0 ? null : parties[index].GetMember(end.Member.ActorId);
            if (retained is null || retained.Command.ContentHash != end.Member.Command.ContentHash
                || retained.Origin != end.Member.Origin || retained.NormalizedSpeed != end.Member.NormalizedSpeed
                || retained.SamplesWalked != end.Member.SamplesWalked)
                throw new ArgumentException("A terminal record must end its exact retained command.");
            parties[index] = parties[index].RecordEnded(parties[index].Revision, end.Member.ActorId, end.Member.Command.CommandId);
        }
        if (closeEncounter && parties.Any(party => party.Members.Count != 0))
            throw new ArgumentException("An ended episode accounts for every retained withdrawal choice.");
        if (Last?.WorldSequence == sequence)
            resolution = new(sequence, Last.Ended.Concat(resolution.Ended), Last.CancelledDecisions.Concat(resolution.CancelledDecisions));
        return new(checked(Revision + 1), closeEncounter ? [] : parties, resolution);
    }
}
