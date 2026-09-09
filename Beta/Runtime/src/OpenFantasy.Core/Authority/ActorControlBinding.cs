namespace OpenFantasy.Core.Authority;

/// <summary>One current principal/session controller of a counted-domain actor.</summary>
public sealed record ActorControlBinding(EntityId PrincipalId, EntityId SessionId, EntityId ActorId);
