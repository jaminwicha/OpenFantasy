using OpenFantasy.Core.Care;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

public static class WorldActionCodec
{
    public const int MaximumCommandByteLength = 16 * 1024;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Intent switch
        {
            UseRestorativeItemIntent or RevokeCarePermissionIntent => WorldActionCodecV14.Encode(command),
            AcknowledgeCareIntent or AdministerRemedyIntent => WorldActionCodecV13.Encode(command),
            EnterEncounterIntent or ReleaseEncounterIntent => WorldActionCodecV12.Encode(command),
            QueueEncounterAbilityIntent => WorldActionCodecV11.Encode(command),
            QueueEncounterWaitIntent => WorldActionCodecV10.Encode(command),
            ClimbIntent => WorldActionCodecV9.Encode(command),
            ExcavateIntent => WorldActionCodecV8.Encode(command),
            ScheduleRunoffTransportIntent => WorldActionCodecV7.Encode(command),
            RunoffCommitmentIntent => WorldActionCodecV6.Encode(command),
            WorkConstructionIntent => WorldActionCodecV5.Encode(command),
            DeliveryQuestIntent => WorldActionCodecV4.Encode(command),
            CraftIntent => WorldActionCodecV3.Encode(command),
            GatherIntent => WorldActionCodecV2.Encode(command),
            _ => WorldActionCodecV1.Encode(command)
        };
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "world.actor-action"u8, MaximumCommandByteLength, 10) switch
        {
            14 => WorldActionCodecV14.Decode(bytes),
            13 => WorldActionCodecV13.Decode(bytes),
            12 => WorldActionCodecV12.Decode(bytes),
            11 => WorldActionCodecV11.Decode(bytes),
            10 => WorldActionCodecV10.Decode(bytes),
            1 => WorldActionCodecV1.Decode(bytes),
            2 => WorldActionCodecV2.Decode(bytes),
            3 => WorldActionCodecV3.Decode(bytes),
            4 => WorldActionCodecV4.Decode(bytes),
            5 => WorldActionCodecV5.Decode(bytes),
            9 => WorldActionCodecV9.Decode(bytes),
            8 => WorldActionCodecV8.Decode(bytes),
            7 => WorldActionCodecV7.Decode(bytes),
            6 => WorldActionCodecV6.Decode(bytes),
            _ => throw new FormatException("Unsupported world action version.")
        };
}
