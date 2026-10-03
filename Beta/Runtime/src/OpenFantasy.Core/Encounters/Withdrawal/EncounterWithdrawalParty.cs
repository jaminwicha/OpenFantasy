using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// Immutable commitments for one declared encounter side. Required membership survives cancellation
/// and interruption: removing a choice never removes the person who must be accounted for at exit.
/// The complete world separately admits membership, commands, physical movement, time and departure.
/// </summary>
public sealed class EncounterWithdrawalParty
{
    public const int MaximumMembers = EncounterTimingSnapshot.MaximumActors;
    public const string RulesText = "encounter.withdrawal-party.v1;members=1..128;route=1..32;command-bytes<=1024;"
        + "component-bytes<=204800;complete-declared-side;independent-actor-commands;"
        + "fixed-episode-speed;one-32-subunit-cardinal-sample;per-sample-retreat-v2-readiness;"
        + "arrival-not-departure;ended-choice-retains-required-member;world-admission-required";
    public static ContentHash RulesPin { get; } = ContentHash.Compute(CanonicalEncodingV1.Text(RulesText, nameof(RulesText)));

    private readonly Dictionary<EntityId, EncounterWithdrawalMember> _byActor;

    public EncounterWithdrawalParty(EncounterTimingScope scope, ushort side, ulong revision,
        IEnumerable<EntityId> requiredActors, IEnumerable<EncounterWithdrawalMember> members)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(requiredActors);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfZero(side);
        SortedSet<EntityId> required = [];
        foreach (EntityId actor in requiredActors)
        {
            if (required.Count == MaximumMembers || scope.Contains(actor) || !required.Add(actor))
            {
                throw new ArgumentException("Withdrawal membership must be bounded, unique and distinct from its scope.");
            }
        }
        if (required.Count == 0) throw new ArgumentException("A withdrawal side must retain its members.");
        _byActor = [];
        HashSet<EntityId> commands = [];
        UInt128 minimumRevision = 0;
        foreach (EncounterWithdrawalMember member in members)
        {
            ArgumentNullException.ThrowIfNull(member);
            var command = member.Command;
            if (!required.Contains(member.ActorId) || !_byActor.TryAdd(member.ActorId, member)
                || command.WorldId != scope.WorldId || command.BranchId != scope.BranchId
                || member.Intent.EncounterId != scope.EncounterId || scope.Contains(command.CommandId)
                || required.Contains(command.CommandId) || !commands.Add(command.CommandId))
            {
                throw new ArgumentException("Every withdrawal choice names its own declared member in the same episode.");
            }
            minimumRevision += checked((ulong)member.SamplesWalked + 1);
        }
        if (minimumRevision > revision)
        {
            throw new ArgumentException("Retained acceptances and samples require more party revisions than recorded.");
        }
        Scope = scope;
        Side = side;
        Revision = revision;
        RequiredActors = Array.AsReadOnly(required.ToArray());
        Members = Array.AsReadOnly(_byActor.Values.OrderBy(member => member.ActorId).ToArray());
    }

    public EncounterTimingScope Scope { get; }
    public ushort Side { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<EntityId> RequiredActors { get; }
    public ReadOnlyCollection<EncounterWithdrawalMember> Members { get; }
    public ContentHash ContentHash => ContentHash.Compute(EncounterWithdrawalCodecV1.Encode(this));

    /// <summary>Every declared person chose and walked their route; this alone never authorizes departure.</summary>
    public bool AllArrived => Members.Count == RequiredActors.Count && Members.All(member => member.HasArrived);

    public static EncounterWithdrawalParty Fresh(EncounterTimingScope scope, ushort side, IEnumerable<EntityId> requiredActors) =>
        new(scope, side, 0, requiredActors, []);

    public EncounterWithdrawalMember GetMember(EntityId actorId) => _byActor.TryGetValue(actorId, out var member)
        ? member : throw new KeyNotFoundException("This actor has no retained withdrawal choice.");

    /// <summary>Records a separately authenticated and admitted actor choice without choosing for any ally.</summary>
    public EncounterWithdrawalParty RecordAccepted(ulong expectedRevision, EncounterWithdrawalMember member)
    {
        RequireRevision(expectedRevision);
        ArgumentNullException.ThrowIfNull(member);
        if (member.SamplesWalked != 0 || _byActor.ContainsKey(member.ActorId))
        {
            throw new ArgumentException("A new withdrawal starts unwalked and cannot replace an existing choice.");
        }
        return new(Scope, Side, checked(Revision + 1), RequiredActors, Members.Append(member));
    }

    /// <summary>Records one movement already admitted for publication with the world's matching readiness charge.</summary>
    public EncounterWithdrawalParty RecordMoved(ulong expectedRevision, EntityId actorId, EntityId commandId,
        SubunitPosition from, SubunitPosition to)
    {
        RequireRevision(expectedRevision);
        var previous = RequireCommand(actorId, commandId);
        var next = previous.RecordMoved(from, to);
        return new(Scope, Side, checked(Revision + 1), RequiredActors,
            Members.Select(member => member.ActorId == actorId ? next : member));
    }

    /// <summary>
    /// Ends a choice after an independently admitted cancellation or interruption. The world owns its terminal
    /// record and any charged failed sample; this component neither refunds debt nor erases required membership.
    /// </summary>
    public EncounterWithdrawalParty RecordEnded(ulong expectedRevision, EntityId actorId, EntityId commandId)
    {
        RequireRevision(expectedRevision);
        _ = RequireCommand(actorId, commandId);
        return new(Scope, Side, checked(Revision + 1), RequiredActors, Members.Where(member => member.ActorId != actorId));
    }

    private EncounterWithdrawalMember RequireCommand(EntityId actorId, EntityId commandId)
    {
        var member = GetMember(actorId);
        if (member.Command.CommandId != commandId) throw new ArgumentException("The withdrawal command has changed.");
        return member;
    }

    private void RequireRevision(ulong expectedRevision)
    {
        if (expectedRevision != Revision) throw new InvalidOperationException("The withdrawal party has changed.");
    }
}
