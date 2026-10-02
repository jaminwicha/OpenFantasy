using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Fully parsed client input. No authority, observation or host callback runs inside this boundary.</summary>
internal sealed record PlayerCommandInput(PlayerMessage Message, bool Ready = false, int X = 0, int Y = 0,
    WorldActionCommand? Command = null, PlayerQuery? Query = null, PlayerControlStatus? Control = null)
{
    internal static PlayerCommandInput Decode(byte[] bytes, ulong lastRequest)
    {
        PlayerMessage message = PlayerMessageCodec.Decode(bytes);
        if (message.RequestId <= lastRequest)
            throw new FormatException("Player request IDs must strictly increase within a connection.");
        switch (message.Kind)
        {
            case PlayerMessageKind.Readiness:
                return new(message, Ready: PlayerGameplayCodec.ReadReadiness(message),
                    Control: message.Payload.Length == 1 ? null : PlayerControlCodec.Decode(message.Payload[1..]));
            case PlayerMessageKind.Movement:
                (int x, int y) = PlayerGameplayCodec.ReadMovement(message);
                return new(message, X: x, Y: y);
            case PlayerMessageKind.Command:
                return new(message, Command: PlayerGameplayCodec.ReadCommand(message));
            case PlayerMessageKind.Query:
                return new(message, Query: PlayerQueryCodec.Decode(message.Payload));
            case PlayerMessageKind.Ping:
            case PlayerMessageKind.Observation:
            case PlayerMessageKind.Goodbye:
                if (!message.Payload.IsEmpty)
                    throw new FormatException("This player message must have an empty payload.");
                return new(message);
            default:
                throw new FormatException("Unsupported player input message.");
        }
    }
}
