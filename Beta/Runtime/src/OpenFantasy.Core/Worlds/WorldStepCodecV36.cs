namespace OpenFantasy.Core.Worlds;

/// <summary>Controller-aware boundaries, including recorded external session decisions.</summary>
public static class WorldStepCodecV36
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldControllerAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, WorldControllerAdmission.ProfileVersion);
}
