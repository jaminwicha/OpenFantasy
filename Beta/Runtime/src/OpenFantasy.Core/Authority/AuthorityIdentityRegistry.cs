namespace OpenFantasy.Core.Authority;

internal enum AuthorityIdentityKind
{
    World,
    Branch,
    Actor,
    Principal,
    Session,
    Custodian,
    Lot,
    Source,
    Definition,
    Reservation
}

/// <summary>Rejects known cross-kind aliases without asserting a global historical registry.</summary>
internal sealed class AuthorityIdentityRegistry
{
    private readonly Dictionary<EntityId, AuthorityIdentityKind> _kinds = [];

    internal void Register(EntityId identity, AuthorityIdentityKind kind)
    {
        if (_kinds.TryGetValue(identity, out AuthorityIdentityKind existing) && existing != kind)
        {
            throw new ArgumentException("An admitted identity cannot name different authority entity kinds.");
        }

        _kinds[identity] = kind;
    }

    internal bool Contains(EntityId identity) => _kinds.ContainsKey(identity);
}
