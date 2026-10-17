using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Authority;

/// <summary>
/// Complete finite custody and current access/source admission for one world/branch profile.
/// Actor participation is explicit; this type does not represent an actor's complete simulation state.
/// </summary>
public sealed class CountedAuthorityState
{
    public const int MaximumActors = 1024;
    public const int MaximumControls = 1024;
    public const int MaximumSources = 1024;
    public const int MaximumReservations = 4096;

    private readonly Dictionary<EntityId, ActorControlBinding> _controls;
    private readonly Dictionary<EntityId, CustodianAccessPolicy> _policies;
    private readonly AuthorityIdentityRegistry _identities = new();

    public CountedAuthorityState(CustodySnapshot custody, ulong admittedUnitCapacity,
        IEnumerable<EntityId> actors, IEnumerable<ActorControlBinding> controls,
        IEnumerable<CustodianAccessPolicy> policies, IEnumerable<AdmittedCountedSource> sources,
        IEnumerable<EntityId>? reservationIds = null)
    {
        ArgumentNullException.ThrowIfNull(custody);
        if (admittedUnitCapacity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(admittedUnitCapacity));
        }

        Custody = custody;
        AdmittedUnitCapacity = admittedUnitCapacity;
        _identities.Register(WorldId, AuthorityIdentityKind.World);
        _identities.Register(BranchId, AuthorityIdentityKind.Branch);

        Actors = CopyIdentities(actors, MaximumActors, AuthorityIdentityKind.Actor);
        ReservationIds = CopyIdentities(reservationIds ?? [], MaximumReservations, AuthorityIdentityKind.Reservation);
        _controls = ReadControls(controls);
        Controls = _controls.Values.OrderBy(value => value.ActorId).ToList().AsReadOnly();
        _policies = ReadPolicies(policies);
        Policies = _policies.Values.OrderBy(value => value.CustodianId).ToList().AsReadOnly();
        Dictionary<EntityId, AdmittedCountedSource> admittedSources = ReadSources(sources);
        Sources = admittedSources.Values.OrderBy(value => value.SourceId).ToList().AsReadOnly();
        ValidateCustody(admittedSources);
    }

    public CustodySnapshot Custody { get; }
    public EntityId WorldId => Custody.WorldId;
    public EntityId BranchId => Custody.BranchId;
    public ulong AdmittedUnitCapacity { get; }
    public ReadOnlyCollection<EntityId> Actors { get; }
    public ReadOnlyCollection<ActorControlBinding> Controls { get; }
    public ReadOnlyCollection<CustodianAccessPolicy> Policies { get; }
    public ReadOnlyCollection<AdmittedCountedSource> Sources { get; }
    public ReadOnlyCollection<EntityId> ReservationIds { get; }
    public ContentHash ContentHash => ContentHash.Compute(AuthorityEncodingV1.Encode(this));

    /// <summary>Checks current state against host-verified identity data; performs no authentication itself.</summary>
    public bool CanControl(TrustedCommandDelivery delivery, EntityId actorId)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        return _controls.TryGetValue(actorId, out ActorControlBinding? control)
            && control.PrincipalId == delivery.PrincipalId && control.SessionId == delivery.SessionId;
    }

    internal ActorControlBinding? FindControl(EntityId actorId) => _controls.GetValueOrDefault(actorId);

    public bool ContainsIdentity(EntityId identity) => _identities.Contains(identity);
    internal bool ContainsIdentityMatching(Func<EntityId, bool> predicate) => _identities.ContainsMatching(predicate);

    internal bool AllowsTransfer(EntityId actorId, EntityId sourceCustodianId, EntityId destinationCustodianId) =>
        _policies.TryGetValue(sourceCustodianId, out CustodianAccessPolicy? source)
        && source.OwnerActorId == actorId
        && _policies.TryGetValue(destinationCustodianId, out CustodianAccessPolicy? destination)
        && destination.AllowsDeposit(actorId);

    internal CountedAuthorityState WithCustody(CustodySnapshot custody) => new(custody, AdmittedUnitCapacity,
        Actors, Controls, Policies, Sources, ReservationIds);

    private ReadOnlyCollection<EntityId> CopyIdentities(IEnumerable<EntityId> values, int maximum,
        AuthorityIdentityKind kind)
    {
        ArgumentNullException.ThrowIfNull(values);
        HashSet<EntityId> unique = [];
        foreach (EntityId value in values)
        {
            if (unique.Count == maximum || !unique.Add(value))
            {
                throw new ArgumentException("Identity collection is excessive or contains duplicates.", nameof(values));
            }

            _identities.Register(value, kind);
        }

        return unique.Order().ToList().AsReadOnly();
    }

    private Dictionary<EntityId, ActorControlBinding> ReadControls(IEnumerable<ActorControlBinding> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        Dictionary<EntityId, ActorControlBinding> result = [];
        Dictionary<EntityId, EntityId> sessionPrincipals = [];
        foreach (ActorControlBinding control in controls)
        {
            ArgumentNullException.ThrowIfNull(control);
            if (result.Count == MaximumControls || !Actors.Contains(control.ActorId)
                || !result.TryAdd(control.ActorId, control))
            {
                throw new ArgumentException("Control requires one binding per admitted actor within the profile limit.", nameof(controls));
            }

            if (sessionPrincipals.TryGetValue(control.SessionId, out EntityId principal)
                && principal != control.PrincipalId)
            {
                throw new ArgumentException("A current session cannot name multiple principals.", nameof(controls));
            }

            sessionPrincipals[control.SessionId] = control.PrincipalId;
            _identities.Register(control.PrincipalId, AuthorityIdentityKind.Principal);
            _identities.Register(control.SessionId, AuthorityIdentityKind.Session);
        }

        return result;
    }

    private Dictionary<EntityId, CustodianAccessPolicy> ReadPolicies(IEnumerable<CustodianAccessPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        Dictionary<EntityId, CustodianAccessPolicy> result = [];
        foreach (CustodianAccessPolicy policy in policies)
        {
            ArgumentNullException.ThrowIfNull(policy);
            if (result.Count == CustodySnapshot.MaximumCustodians || !Actors.Contains(policy.OwnerActorId)
                || !result.TryAdd(policy.CustodianId, policy))
            {
                throw new ArgumentException("Custodian policy requires one admitted owner and a unique storage.", nameof(policies));
            }

            _identities.Register(policy.CustodianId, AuthorityIdentityKind.Custodian);
        }

        return result;
    }

    private Dictionary<EntityId, AdmittedCountedSource> ReadSources(IEnumerable<AdmittedCountedSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        Dictionary<EntityId, AdmittedCountedSource> result = [];
        Dictionary<EntityId, CountedItemDefinition> definitions = [];
        UInt128 capacity = 0;
        foreach (AdmittedCountedSource source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (result.Count == MaximumSources || !result.TryAdd(source.SourceId, source))
            {
                throw new ArgumentException("Admitted sources must be unique and within the profile limit.", nameof(sources));
            }

            if (definitions.TryGetValue(source.Definition.DefinitionId, out CountedItemDefinition? definition)
                && definition != source.Definition)
            {
                throw new ArgumentException("A counted definition identity must use one exact admitted version.", nameof(sources));
            }

            definitions[source.Definition.DefinitionId] = source.Definition;
            capacity += source.UnitCapacity;
            if (capacity > AdmittedUnitCapacity)
            {
                throw new ArgumentException("Source capacities exceed the finite admission budget.", nameof(sources));
            }

            _identities.Register(source.SourceId, AuthorityIdentityKind.Source);
            _identities.Register(source.Definition.DefinitionId, AuthorityIdentityKind.Definition);
        }

        return result;
    }

    private void ValidateCustody(Dictionary<EntityId, AdmittedCountedSource> sources)
    {
        if (_policies.Count != Custody.Storages.Count)
        {
            throw new ArgumentException("Every included storage requires exactly one access policy.");
        }

        foreach (CustodianStorage storage in Custody.Storages)
        {
            if (storage.UnitCapacity == 0 || !_policies.ContainsKey(storage.CustodianId))
            {
                throw new ArgumentException("An admitted storage needs positive capacity and an explicit owner policy.");
            }
        }

        foreach (CountedSourceLot lot in Custody.Lots)
        {
            if (!sources.TryGetValue(lot.Source.SourceId, out AdmittedCountedSource? source)
                || lot.Definition != source.Definition || lot.Source.End > source.UnitCapacity)
            {
                throw new ArgumentException("A lot must fit one admitted source interval and exact definition pin.");
            }

            if (lot.ReservedBy is { } reservation && !ReservationIds.Contains(reservation))
            {
                throw new ArgumentException("A lot reservation must name an explicitly admitted reservation identity.");
            }

            _identities.Register(lot.LotId, AuthorityIdentityKind.Lot);
        }
    }
}
