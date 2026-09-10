using OpenFantasy.Core.Gathering;

namespace OpenFantasy.Core.Worlds;

public static class WorldActionCodec
{
    public const int MaximumCommandByteLength = WorldActionCodecV1.MaximumCommandByteLength;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Intent is GatherIntent ? WorldActionCodecV2.Encode(command) : WorldActionCodecV1.Encode(command);
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "world.actor-action"u8, MaximumCommandByteLength, 10) switch
        {
            1 => WorldActionCodecV1.Decode(bytes),
            2 => WorldActionCodecV2.Decode(bytes),
            _ => throw new FormatException("Unsupported world action version.")
        };
}
