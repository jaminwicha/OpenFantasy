namespace OpenFantasy.Core.Worlds;

/// <summary>Profile-34 batches: the shared step frame, including committed-retreat encounter decisions.</summary>
public static class WorldStepCodecV34
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldEncounterRetreatAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, WorldEncounterRetreatAdmission.ProfileVersion);
}
