namespace OpenFantasy.Core.Worlds;

/// <summary>Pending-care disposition world boundaries retain the existing Care commit kind.</summary>
public static class WorldStepCodecV38
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldCareDispositionAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, WorldCareDispositionAdmission.ProfileVersion);
}
