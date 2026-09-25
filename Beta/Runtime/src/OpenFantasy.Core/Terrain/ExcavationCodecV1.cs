using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

public static class ExcavationCodecV1
{
    public const int MaximumEncodedByteLength = 32_768;
    internal const int MaximumSiteByteLength = 1000;
    public static byte[] Encode(ExcavationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return CanonicalEncodingV1.Frame("terrain.excavation.v1", snapshot.Sites.Select(s => (ReadOnlyMemory<byte>)EncodeSite(s, false)).ToArray());
    }
    private static byte[] Pin(CountedItemDefinition item) => [..item.DefinitionId.ToByteArray(), ..item.Version.ToByteArray()];
    private static byte[] Position(long x, long y, long z) => [..TerrainRegionCodecV1.Signed64(x), ..TerrainRegionCodecV1.Signed64(y), ..TerrainRegionCodecV1.Signed64(z)];
    internal static byte[] EncodeSite(ExcavationSite s, bool includeReachMode)
    {
        if (!includeReachMode && s.ReachMode != ExcavationReachMode.SameLevelSide)
        {
            throw new ArgumentException("The legacy excavation codec cannot omit an explicit raised-bank reach mode.", nameof(s));
        }
        ReadOnlyMemory<byte>[] fields = [s.SiteId.ToByteArray(), Position(s.Cell.X, s.Cell.Y, s.Cell.Z),
            s.TerrainSourceId.ToByteArray(), s.CountedSourceId.ToByteArray(), s.OwnerActorId.ToByteArray(),
            Pin(s.Material.Item), MaterialRecipeCodecV1.EncodeMaterial(s.Material.Physical), Pin(s.RequiredTool),
            CanonicalEncodingV1.Unsigned64(s.RequiredWork), CanonicalEncodingV1.Unsigned64(s.CompletedWork),
            Position(s.Stance.X, s.Stance.Y, s.Stance.Z)];
        return includeReachMode
            ? CanonicalEncodingV1.Frame("terrain.excavation-site.v2", [..fields, CanonicalEncodingV1.Unsigned64((ulong)s.ReachMode)])
            : CanonicalEncodingV1.Frame("terrain.excavation-site.v1", fields);
    }
    public static ExcavationSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, ExcavationSnapshot.MaximumSites + 1);
            reader.Tag("terrain.excavation.v1"u8);
            List<ExcavationSite> sites = [];
            while (reader.Remaining != 0)
            {
                sites.Add(DecodeSite(reader.Field(MaximumSiteByteLength), false));
            }
            reader.Complete();
            var result = new ExcavationSnapshot(sites);
            if (!Encode(result).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Noncanonical excavation state.");
            }
            return result;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid excavation state.", error);
        }
    }
    internal static ExcavationSite DecodeSite(ReadOnlySpan<byte> bytes, bool includesReachMode)
    {
        var row = new WorldFrameReader(bytes, MaximumSiteByteLength, includesReachMode ? 13 : 12);
        row.Tag(includesReachMode ? "terrain.excavation-site.v2"u8 : "terrain.excavation-site.v1"u8);
        var id = row.Identity();
        var cell = ReadPosition(row.Exact(24));
        var terrain = row.Identity(); var source = row.Identity(); var owner = row.Identity();
        var item = ReadPin(row.Exact(48));
        var physical = MaterialRecipeCodecV1.DecodeMaterial(row.Field(512));
        var tool = ReadPin(row.Exact(48));
        ulong required = row.Number(), completed = row.Number();
        var stance = ReadPosition(row.Exact(24));
        ulong mode = includesReachMode ? row.Number() : (ulong)ExcavationReachMode.SameLevelSide;
        if (mode is not 1 and not 2)
        {
            throw new FormatException("Unknown excavation reach mode.");
        }
        row.Complete();
        return new(id, new(cell.X, cell.Y, cell.Z), terrain, source, owner, new(item, physical), tool,
            required, completed, new(stance.X, stance.Y, stance.Z), (ExcavationReachMode)mode);
    }
    private static CountedItemDefinition ReadPin(ReadOnlySpan<byte> bytes) => new(EntityId.FromBytes(bytes[..16]),
        new ContentHash(Convert.ToHexString(bytes[16..]).ToLowerInvariant()));
    private static (long X, long Y, long Z) ReadPosition(ReadOnlySpan<byte> bytes) =>
        (BinaryPrimitives.ReadInt64BigEndian(bytes), BinaryPrimitives.ReadInt64BigEndian(bytes[8..]), BinaryPrimitives.ReadInt64BigEndian(bytes[16..]));
}
