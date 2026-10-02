namespace OpenFantasy.Core.Worlds;

/// <summary>Profile-35 batches: the shared step frame, including outcome acknowledgments and completion boundaries.</summary>
public static class WorldStepCodecV35
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldEncounterOutcomeAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, WorldEncounterOutcomeAdmission.ProfileVersion);
}
