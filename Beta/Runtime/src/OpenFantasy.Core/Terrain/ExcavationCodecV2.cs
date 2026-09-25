using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>Canonical excavation catalog retaining each site's explicit side or raised-bank work face.</summary>
public static class ExcavationCodecV2
{
    public const int MaximumEncodedByteLength = ExcavationCodecV1.MaximumEncodedByteLength;

    public static byte[] Encode(ExcavationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        byte[] bytes = CanonicalEncodingV1.Frame("terrain.excavation.v2", snapshot.Sites
            .Select(site => (ReadOnlyMemory<byte>)ExcavationCodecV1.EncodeSite(site, true)).ToArray());
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The excavation catalog exceeds its finite archive bound.", nameof(snapshot));
        }
        return bytes;
    }

    public static ExcavationSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, ExcavationSnapshot.MaximumSites + 1);
            reader.Tag("terrain.excavation.v2"u8);
            List<ExcavationSite> sites = [];
            while (reader.Remaining != 0)
            {
                sites.Add(ExcavationCodecV1.DecodeSite(reader.Field(ExcavationCodecV1.MaximumSiteByteLength), true));
            }
            reader.Complete();
            var restored = new ExcavationSnapshot(sites);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Noncanonical explicit-reach excavation state.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid explicit-reach excavation state.", error);
        }
    }
}
