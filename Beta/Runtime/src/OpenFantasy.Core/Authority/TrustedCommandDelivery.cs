namespace OpenFantasy.Core.Authority;

/// <summary>
/// Identity supplied by the host's authenticated delivery adapter, separately from submitted claims.
/// Constructing this value does not authenticate a connection or establish actor control.
/// </summary>
public sealed record TrustedCommandDelivery(EntityId PrincipalId, EntityId SessionId);
