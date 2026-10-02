namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit recovery-world boundaries; controller events retain their existing ownership.</summary>
public static class WorldStepCodecV37
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldRecoveryAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, WorldRecoveryAdmission.ProfileVersion);
}
