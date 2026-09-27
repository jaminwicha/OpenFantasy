using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Preserves the original inline archive and explicitly selects retrieved-boundary continuation.</summary>
public static class WorldResourceHistoryCodec
{
    public const int MaximumEncodedByteLength = WorldResourceHistoryCodecV1.MaximumEncodedByteLength + 512;

    public static byte[] Encode(WorldResourceHistory value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.ArchiveAnchor is not { } anchor) return WorldResourceHistoryCodecV1.Encode(value);
        var reference = anchor.Reference;
        // The inner V1 frame remains a structural archive; the outer V2 frame binds its actual basis.
        var body = new WorldResourceHistory(value.AnchorAdmission, value.History, value.Evidence);
        return CanonicalEncodingV1.Frame("world.resource-history", CanonicalEncodingV1.Unsigned64(2),
            CanonicalEncodingV1.Frame("world.resource-archive-reference.v1", reference.WorldId.ToByteArray(),
                reference.BranchId.ToByteArray(), CanonicalEncodingV1.Unsigned64(reference.Ordinal),
                CanonicalEncodingV1.Unsigned64(reference.BoundarySequence), reference.BoundaryHash.ToByteArray()),
            WorldResourceHistoryCodecV1.Encode(body));
    }

    public static WorldResourceHistory Decode(ReadOnlySpan<byte> bytes, WorldResourceArchiveAnchor? archiveAnchor = null)
    {
        try
        {
            ulong version = WorldCodecVersion.Read(bytes, "world.resource-history"u8, MaximumEncodedByteLength, 7);
            if (version == 1)
            {
                if (archiveAnchor is not null) throw new FormatException("An inline resource history cannot select an external anchor.");
                return WorldResourceHistoryCodecV1.Decode(bytes);
            }
            if (version != 2) throw new FormatException("Unsupported world resource history version.");
            if (archiveAnchor is null) throw new FormatException("The continued resource history requires its retrieved archive boundary.");
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 4);
            reader.Tag("world.resource-history"u8);
            if (reader.Number() != 2) throw new FormatException("Unsupported resource continuation version.");
            var referenceReader = new WorldFrameReader(reader.Field(256), 256, 6);
            referenceReader.Tag("world.resource-archive-reference.v1"u8);
            var reference = new WorldResourceArchiveReference(referenceReader.Identity(), referenceReader.Identity(),
                referenceReader.Number(), referenceReader.Number(),
                new ContentHash(Convert.ToHexString(referenceReader.Exact(32)).ToLowerInvariant()));
            referenceReader.Complete();
            if (reference != archiveAnchor.Reference)
                throw new FormatException("The supplied archive boundary does not match the exact retained reference.");
            var body = WorldResourceHistoryCodecV1.Decode(reader.Field(WorldResourceHistoryCodecV1.MaximumEncodedByteLength));
            reader.Complete();
            var value = new WorldResourceHistory(body.AnchorAdmission, body.History, body.Evidence, archiveAnchor);
            if (!Encode(value).AsSpan().SequenceEqual(bytes)) throw new FormatException("Continued resource history is not canonical.");
            return value;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Continued resource history violates its archive boundary.", error);
        }
    }
}
