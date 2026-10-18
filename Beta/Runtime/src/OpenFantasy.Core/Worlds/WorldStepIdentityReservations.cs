namespace OpenFantasy.Core.Worlds;

/// <summary>Private identity claims for one selected step; the durable owner publishes all or none.</summary>
internal sealed class WorldStepIdentityReservations(Func<EntityId, bool> alreadyOccupied)
{
    private readonly HashSet<EntityId> _reserved = [];
    private readonly List<WorldCommittedIdentity> _identities = [];

    internal IReadOnlyList<WorldCommittedIdentity> Identities => _identities;

    internal bool Contains(EntityId identity) => _reserved.Contains(identity) || alreadyOccupied(identity);

    internal void Reserve(EntityId identity, WorldCommittedIdentityKind kind)
    {
        if (Contains(identity)) throw new InvalidOperationException("A committed identity is already occupied.");
        _reserved.Add(identity);
        _identities.Add(new(identity, kind));
    }
}
