using System.Buffers.Binary;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Transient input/refusal payloads; world commands and receipts retain their own canonical codecs.</summary>
public static class PlayerGameplayCodec
{
    public static PlayerMessage Readiness(ulong requestId, bool ready) =>
        new(PlayerMessageKind.Readiness, requestId, new byte[] { ready ? (byte)1 : (byte)0 });

    public static PlayerMessage Movement(ulong requestId, int x, int y)
    {
        ValidateMovement(x, y);
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(bytes, x);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(4), y);
        return new PlayerMessage(PlayerMessageKind.Movement, requestId, bytes);
    }

    public static PlayerMessage Command(ulong requestId, WorldActionCommand command) =>
        new(PlayerMessageKind.Command, requestId, WorldActionCodec.Encode(command));

    public static PlayerRequestRefusal ReadRefusal(PlayerMessage message)
    {
        if (message.Kind != PlayerMessageKind.Refusal || message.Payload.Length != 1
            || !Enum.IsDefined((PlayerRequestRefusal)message.Payload[0])) throw new FormatException("Invalid player refusal.");
        return (PlayerRequestRefusal)message.Payload[0];
    }

    internal static bool ReadReadiness(PlayerMessage message)
    {
        if (message.Payload.Length != 1 || message.Payload[0] > 1) throw new FormatException("Invalid readiness input.");
        return message.Payload[0] == 1;
    }

    internal static (int X, int Y) ReadMovement(PlayerMessage message)
    {
        if (message.Payload.Length != 8) throw new FormatException("Invalid movement input.");
        int x = BinaryPrimitives.ReadInt32BigEndian(message.Payload);
        int y = BinaryPrimitives.ReadInt32BigEndian(message.Payload[4..]);
        ValidateMovement(x, y);
        return (x, y);
    }

    internal static WorldActionCommand ReadCommand(PlayerMessage message)
    {
        if (message.Payload.Length > WorldActionCodec.MaximumCommandByteLength) throw new FormatException("Oversized world command.");
        WorldActionCommand command = WorldActionCodec.Decode(message.Payload);
        if (!message.Payload.SequenceEqual(WorldActionCodec.Encode(command)))
            throw new FormatException("A network command must preserve its exact canonical bytes.");
        return command;
    }

    private static void ValidateMovement(int x, int y)
    {
        if (!(x == 0 && y == 0 || x == 0 && y is -32 or 32 || y == 0 && x is -32 or 32))
            throw new FormatException("Movement must select one fixed cardinal sample or stop.");
    }
}

/// <summary>Connection-local preflight feedback. Neither value is a committed rejection or action receipt.</summary>
public enum PlayerRequestRefusal : byte { Busy = 1, NotAdmitted = 2 }
