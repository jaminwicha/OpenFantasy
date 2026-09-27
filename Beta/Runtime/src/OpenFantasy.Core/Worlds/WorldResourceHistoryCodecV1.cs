using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;

namespace OpenFantasy.Core.Worlds;

/// <summary>Canonical bounded structure; embedded anchors still require trusted genesis or journal admission.</summary>
public static class WorldResourceHistoryCodecV1
{
    private const int MaximumEvidenceByteLength = 4096;
    private const int MaximumEvidenceCollectionByteLength = (MaximumEvidenceByteLength + 4) * ActorResourceHistory.MaximumBatches + 64;
    public const int MaximumEncodedByteLength = ProgressionCodecV1.MaximumSnapshotByteLength
        + ActorResourceHistoryCodecV1.MaximumEncodedByteLength + MaximumEvidenceCollectionByteLength + 256;

    public static byte[] Encode(WorldResourceHistory value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return CanonicalEncodingV1.Frame("world.resource-history", Number(1), value.AnchorAdmission.ToByteArray(),
            Number(value.History.BasisWorldSequence), ProgressionCodecV1.Encode(value.History.Basis),
            ActorResourceHistoryCodecV1.Encode(value.History), CanonicalEncodingV1.Frame("world.resource-evidence",
                value.Evidence.Select(item => (ReadOnlyMemory<byte>)EncodeEvidence(item)).ToArray()));
    }

    public static WorldResourceHistory Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 7);
            reader.Tag("world.resource-history"u8);
            reader.Version();
            var admission = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
            ulong sequence = reader.Number();
            var basis = ProgressionCodecV1.Decode(reader.Field(ProgressionCodecV1.MaximumSnapshotByteLength));
            var history = ActorResourceHistoryCodecV1.Decode(reader.Field(ActorResourceHistoryCodecV1.MaximumEncodedByteLength), basis, sequence);
            var evidence = new WorldFrameReader(reader.Field(MaximumEvidenceCollectionByteLength),
                MaximumEvidenceCollectionByteLength, ActorResourceHistory.MaximumBatches + 1);
            evidence.Tag("world.resource-evidence"u8);
            List<WorldResourceEvidence> items = [];
            while (evidence.Remaining != 0) items.Add(DecodeEvidence(evidence.Field(MaximumEvidenceByteLength)));
            evidence.Complete();
            reader.Complete();
            var result = new WorldResourceHistory(admission, history, items);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("Resource history is not canonical.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or ProgressionFormatException)
        {
            throw new FormatException("World resource history violates its structural invariants.", error);
        }
    }

    private static byte[] EncodeEvidence(WorldResourceEvidence evidence)
    {
        byte[] bytes = evidence switch
        {
            WorldEncounterResourceEvidence attack => CanonicalEncodingV1.Frame("world.resource-attack.v1",
                attack.ScenarioId.ToByteArray(), WorldActionCodec.Encode(attack.Request),
                EncounterActionCodecV1.EncodeResolution(attack.Resolution)),
            WorldRewardResourceEvidence reward => CanonicalEncodingV1.Frame("world.resource-reward.v1", reward.CommandId.ToByteArray()),
            WorldTreatmentResourceEvidence treatment => CanonicalEncodingV1.Frame("world.resource-treatment.v1",
                treatment.CommandId.ToByteArray(), treatment.TreatmentId.ToByteArray()),
            _ => throw new ArgumentException("Unsupported resource evidence.")
        };
        if (bytes.Length > MaximumEvidenceByteLength) throw new ArgumentException("Resource evidence exceeds its storage envelope.");
        return bytes;
    }

    private static WorldResourceEvidence DecodeEvidence(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumEvidenceByteLength, 4);
        var tag = reader.Field(64);
        WorldResourceEvidence evidence;
        if (tag.SequenceEqual("world.resource-attack.v1"u8))
        {
            var scenario = reader.Identity();
            var request = WorldActionCodec.Decode(reader.Field(EncounterActionSnapshot.MaximumPendingCommandByteLength));
            var resolution = EncounterActionCodecV1.DecodeResolution(reader.Field(EncounterActionCodecV1.MaximumResolutionByteLength));
            evidence = new WorldEncounterResourceEvidence(scenario, request, resolution);
        }
        else if (tag.SequenceEqual("world.resource-reward.v1"u8)) evidence = new WorldRewardResourceEvidence(reader.Identity());
        else if (tag.SequenceEqual("world.resource-treatment.v1"u8)) evidence = new WorldTreatmentResourceEvidence(reader.Identity(), reader.Identity());
        else throw new FormatException("Unsupported resource evidence.");
        reader.Complete();
        return evidence;
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
