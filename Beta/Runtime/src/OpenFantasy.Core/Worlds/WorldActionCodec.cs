using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

public static class WorldActionCodec
{
    public const int MaximumCommandByteLength = 16 * 1024;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Intent switch
        {
            DeliveryQuestIntent => WorldActionCodecV4.Encode(command),
            CraftIntent => WorldActionCodecV3.Encode(command),
            GatherIntent => WorldActionCodecV2.Encode(command),
            _ => WorldActionCodecV1.Encode(command)
        };
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "world.actor-action"u8, MaximumCommandByteLength, 10) switch
        {
            1 => WorldActionCodecV1.Decode(bytes),
            2 => WorldActionCodecV2.Decode(bytes),
            3 => WorldActionCodecV3.Decode(bytes),
            4 => WorldActionCodecV4.Decode(bytes),
            _ => throw new FormatException("Unsupported world action version.")
        };
}
