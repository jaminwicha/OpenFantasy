using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Ephemeral authorization for one exact operation; construction belongs to the world's authority boundary.</summary>
public sealed class WorldActorAuthorization
{
    private readonly ContentHash? _commandHash;
    private readonly WorldControlCapabilities _scope;

    internal WorldActorAuthorization(EntityId actorId, TrustedCommandDelivery delivery, EntityId commandId,
        ContentHash? commandHash, WorldControlWitness? witness, WorldControlCapabilities scope)
    {
        ActorId = actorId;
        PrincipalId = delivery.PrincipalId;
        SessionId = delivery.SessionId;
        CommandId = commandId;
        _commandHash = commandHash;
        ControlWitness = witness;
        _scope = scope;
    }

    public EntityId ActorId { get; }
    public EntityId PrincipalId { get; }
    public EntityId SessionId { get; }
    public EntityId CommandId { get; }
    public WorldControlWitness? ControlWitness { get; }

    public bool Matches(TrustedCommandDelivery delivery, EntityId actorId, EntityId commandId) =>
        ActorId == actorId && PrincipalId == delivery.PrincipalId && SessionId == delivery.SessionId && CommandId == commandId;
    public bool Matches(WorldActionCommand command) => Matches(new(command.PrincipalId, command.OriginSessionId), command.ActorId, command.CommandId)
        && _commandHash is not null && _commandHash == command.ContentHash;
    public bool Permits(WorldControlCapabilities required) => required != 0 && (_scope & required) == required;
    public void Require(EntityId actorId, TrustedCommandDelivery delivery, EntityId commandId, WorldControlCapabilities required)
    {
        if (!Matches(delivery, actorId, commandId) || !Permits(required))
        {
            throw new ArgumentException("The authorization belongs to another actor operation or scope.");
        }
    }
}
