using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Canonical v1 frames for observation digests. These bytes are hashed in memory only; they are not
/// a saved world component and do not alter any existing codec.
/// </summary>
internal static class WorldMaterialEvidenceEncoding
{
    private const ulong FormatVersion = 1;
    private const string PinsTag = "world.material-owner-pins";
    private const string EvidenceTag = "world.material-action-evidence";

    internal static ContentHash Hash(WorldMaterialOwnerPins pins) => ContentHash.Compute(CanonicalEncodingV1.Frame(PinsTag,
        CanonicalEncodingV1.Unsigned64(FormatVersion), Digest(pins.Inventory), Digest(pins.Production),
        Digest(pins.Gathering), Digest(pins.Excavation), Digest(pins.Construction), Digest(pins.Space),
        Digest(pins.Quests), Digest(pins.Progression), Digest(pins.Care), Digest(pins.Reservoirs),
        Digest(pins.LiquidCells)));

    internal static ContentHash Hash(WorldMaterialActionEvidence evidence) => ContentHash.Compute(CanonicalEncodingV1.Frame(EvidenceTag,
        CanonicalEncodingV1.Unsigned64(FormatVersion), evidence.WorldId.ToByteArray(), evidence.BranchId.ToByteArray(),
        evidence.InputWorldHash.ToByteArray(), evidence.SourceWorldHash.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(evidence.SourceWorldRevision),
        CanonicalEncodingV1.Unsigned64(evidence.PublicationSequence),
        CanonicalEncodingV1.Unsigned64(checked((ulong)evidence.ActionOrdinal)),
        evidence.Command.ContentHash.ToByteArray(), CanonicalEncodingV1.Unsigned64((ulong)evidence.Kind),
        evidence.Before.ContentHash.ToByteArray(), evidence.After.ContentHash.ToByteArray(),
        Identities(evidence.CreatedIds)));

    // An absent owner is an empty field; a present owner is always its complete 32-byte digest.
    private static byte[] Digest(ContentHash? digest) => digest?.ToByteArray() ?? [];

    private static byte[] Identities(IReadOnlyList<EntityId> identities)
    {
        byte[] bytes = new byte[checked(identities.Count * EntityId.ByteLength)];
        for (int index = 0; index < identities.Count; index++)
        {
            identities[index].WriteBytes(bytes.AsSpan(index * EntityId.ByteLength));
        }
        return bytes;
    }
}
