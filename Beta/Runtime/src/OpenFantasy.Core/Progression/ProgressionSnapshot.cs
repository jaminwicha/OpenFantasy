using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Progression;

/// <summary>A bounded local actor progression component and its finite currency supply.</summary>
public sealed class ProgressionSnapshot
{
    public const int MaximumDefinitions = 32;
    public const int MaximumActors = 128;
    private readonly Dictionary<EntityId, ActorProgressionState> _actors;
    private readonly HashSet<EntityId> _identities;

    public ProgressionSnapshot(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<ActorStatProfileDefinition> profiles, IEnumerable<LevelProgressionDefinition> curves,
        IEnumerable<ActorProgressionState> actors, CurrencyLedger currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        if (currency.WorldId != worldId || currency.BranchId != branchId)
        {
            throw new ArgumentException("The currency ledger belongs to another world or branch.");
        }
        Profiles = ProgressionValidation.Sorted(profiles, value => value.DefinitionId, MaximumDefinitions, nameof(profiles));
        Curves = ProgressionValidation.Sorted(curves, value => value.DefinitionId, MaximumDefinitions, nameof(curves));
        Actors = ProgressionValidation.Sorted(actors, value => value.ActorId, MaximumActors, nameof(actors));
        if (Actors.Count == 0 || Profiles.Count == 0 || Curves.Count == 0)
        {
            throw new ArgumentException("Progression requires complete actor and pinned definition sets.");
        }
        if (Profiles.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count() != Profiles.Count
            || Curves.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count() != Curves.Count)
        {
            throw new ArgumentException("Definition keys must be unique within each progression family.");
        }
        _identities = [];
        Register(worldId);
        Register(branchId);
        Register(currency.Currency.DefinitionId);
        foreach (ActorStatProfileDefinition profile in Profiles)
        {
            Register(profile.DefinitionId);
        }
        foreach (LevelProgressionDefinition curve in Curves)
        {
            Register(curve.DefinitionId);
        }
        foreach (CurrencyAccount account in currency.Accounts)
        {
            Register(account.AccountId);
        }
        foreach (ActorProgressionState actor in Actors)
        {
            Register(actor.ActorId);
            if (!Profiles.Any(value => value.DefinitionId == actor.Profile.DefinitionId && value.Version == actor.Profile.Version)
                || !Curves.Any(value => value.DefinitionId == actor.Curve.DefinitionId && value.Version == actor.Curve.Version))
            {
                throw new ArgumentException("An actor references an unadmitted or conflicting definition pin.");
            }
        }
        if (!currency.Accounts.Where(value => value.Kind == CurrencyAccountKind.Actor)
            .Select(value => value.ActorId!.Value).Order().SequenceEqual(Actors.Select(value => value.ActorId)))
        {
            throw new ArgumentException("Every admitted actor must have exactly one currency account and no other actor accounts may exist.");
        }
        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Currency = currency;
        _actors = Actors.ToDictionary(value => value.ActorId);
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<ActorStatProfileDefinition> Profiles { get; }
    public ReadOnlyCollection<LevelProgressionDefinition> Curves { get; }
    public ReadOnlyCollection<ActorProgressionState> Actors { get; }
    public CurrencyLedger Currency { get; }
    public ContentHash ContentHash => ContentHash.Compute(ProgressionCodecV1.Encode(this));

    public ActorProgressionState GetActor(EntityId actorId) => _actors.TryGetValue(actorId, out var actor)
        ? actor : throw new ArgumentException("The actor progression is not admitted.", nameof(actorId));

    public CurrencyAccount GetAccount(EntityId accountId) => Currency.GetAccount(accountId);
    public bool ContainsIdentity(EntityId identity) => _identities.Contains(identity);

    public void ValidateActorSet(IEnumerable<EntityId> actorIds)
    {
        ArgumentNullException.ThrowIfNull(actorIds);
        SortedSet<EntityId> identities = [];
        foreach (EntityId id in actorIds)
        {
            if (identities.Count == MaximumActors || !identities.Add(id))
            {
                throw new ArgumentException("The world actor set must be bounded and unique.", nameof(actorIds));
            }
        }
        if (!identities.SequenceEqual(Actors.Select(value => value.ActorId)))
        {
            throw new ArgumentException("World and progression actor sets must be exactly equal.", nameof(actorIds));
        }
    }

    internal ProgressionSnapshot WithActorState(ActorProgressionState actor, CurrencyLedger currency) =>
        new(WorldId, BranchId, checked(Revision + 1), Profiles, Curves,
            Actors.Select(value => value.ActorId == actor.ActorId ? actor : value), currency);

    internal ProgressionSnapshot WithResourceTransition(ActorResourceTransition transition)
    {
        if (!ReferenceEquals(GetActor(transition.Before.ActorId), transition.Before))
        {
            throw new ArgumentException("The prepared resource transition must use this exact actor basis.", nameof(transition));
        }
        return ReferenceEquals(transition.Before, transition.Candidate) ? this
            : WithActorState(transition.Candidate, Currency);
    }

    private void Register(EntityId identity)
    {
        if (!_identities.Add(identity))
        {
            throw new ArgumentException("Progression identity kinds must not alias one another.");
        }
    }
}
