using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Terrain;

/// <summary>
/// Canonical bounded snapshot encoding: pinned generation inputs plus sorted persistent excavation indices.
/// Decoding restores all edits after baseline realization; callers must still verify content admission and storage ownership.
/// </summary>
public static class TerrainRegionCodecV1
{
    // A region is immutable. Cache by its object identity, never bounds/revision, and retain it only while
    // the snapshot is alive. Every public export copies so a caller cannot corrupt later encodings.
    private static readonly ConditionalWeakTable<TerrainRegionSnapshot, CanonicalRegion> CanonicalRegions = new();

    public const int MaximumEncodedByteLength = 70_000;
    private const int MaximumRulesByteLength = 512;
    private const int MaximumScopeByteLength = 128;
    private const int MaximumBoundsByteLength = 128;
    private const int MaximumPinByteLength = 128;

    public static byte[] Encode(TerrainRegionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return CanonicalRegions.GetValue(snapshot, static region => new CanonicalRegion(region)).Copy();
    }

    private static byte[] EncodeUncached(TerrainRegionSnapshot snapshot)
    {
        int[] indices = snapshot.Cells.Where(cell => cell.IsExcavated)
            .Select(cell => snapshot.Bounds.IndexOf(cell.Coordinate)).ToArray();
        byte[] edits = new byte[checked(indices.Length * sizeof(uint))];
        for (int index = 0; index < indices.Length; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(edits.AsSpan(index * sizeof(uint)), (uint)indices[index]);
        }
        return CanonicalEncodingV1.Frame("terrain.region.v1", EncodeScope(snapshot.Scope), snapshot.Seed.ToByteArray(),
            EncodeRules(snapshot.Rules), EncodeBounds(snapshot.Bounds),
            CanonicalEncodingV1.Unsigned64(snapshot.Revision), edits);
    }

    private sealed class CanonicalRegion
    {
        private readonly byte[] _bytes;

        internal CanonicalRegion(TerrainRegionSnapshot snapshot) => _bytes = EncodeUncached(snapshot);

        internal byte[] Copy() => (byte[])_bytes.Clone();
    }

    public static TerrainRegionSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new CanonicalTerrainReader(bytes, MaximumEncodedByteLength, 7, "terrain.region.v1"u8);
            TerrainScope scope = ReadScope(reader.ReadField(MaximumScopeByteLength));
            EntityId seed = reader.ReadIdentity();
            StratifiedTerrainRules rules = ReadRules(reader.ReadField(MaximumRulesByteLength));
            TerrainRegionBounds bounds = ReadBounds(reader.ReadField(MaximumBoundsByteLength));
            ulong revision = reader.ReadUnsigned64();
            ReadOnlySpan<byte> edits = reader.ReadField(checked(bounds.CellCount * sizeof(uint)));
            reader.Complete();
            if (edits.Length % sizeof(uint) != 0)
            {
                throw new FormatException("Terrain edit indices require whole four-byte unsigned integers.");
            }
            int[] indices = new int[edits.Length / sizeof(uint)];
            int previous = -1;
            for (int index = 0; index < indices.Length; index++)
            {
                uint value = BinaryPrimitives.ReadUInt32BigEndian(edits[(index * sizeof(uint))..]);
                if (value >= bounds.CellCount || (long)value <= previous)
                {
                    throw new FormatException("Terrain edit indices must be unique, ordered and within the region.");
                }
                indices[index] = (int)value;
                previous = (int)value;
            }
            var initial = new StratifiedTerrainGenerator().Generate(scope, seed, rules, bounds);
            return initial.RestoreExcavations(revision, indices);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The terrain snapshot contains invalid values.", exception);
        }
        catch (OverflowException exception)
        {
            throw new FormatException("The terrain snapshot exceeds its numeric envelope.", exception);
        }
    }

    public static byte[] EncodeRules(StratifiedTerrainRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return CanonicalEncodingV1.Frame("terrain.rules.v1", Signed64(rules.BaseSurfaceZ),
            CanonicalEncodingV1.Unsigned64((ulong)rules.Relief), CanonicalEncodingV1.Unsigned64((ulong)rules.SoilDepth),
            EncodePin(rules.Materials.Surface), EncodePin(rules.Materials.Soil), EncodePin(rules.Materials.Stone));
    }

    internal static byte[] Signed64(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] EncodeScope(TerrainScope scope) => CanonicalEncodingV1.Frame("terrain.scope.v1",
        scope.WorldId.ToByteArray(), scope.BranchId.ToByteArray(), scope.RealmId.ToByteArray());

    private static byte[] EncodeBounds(TerrainRegionBounds bounds) => CanonicalEncodingV1.Frame("terrain.bounds.v1",
        Signed64(bounds.Minimum.X), Signed64(bounds.Minimum.Y), Signed64(bounds.Minimum.Z),
        CanonicalEncodingV1.Unsigned64((ulong)bounds.Width), CanonicalEncodingV1.Unsigned64((ulong)bounds.Depth),
        CanonicalEncodingV1.Unsigned64((ulong)bounds.Height));

    private static byte[] EncodePin(CountedItemDefinition pin) => CanonicalEncodingV1.Frame("terrain.material-pin.v1",
        pin.DefinitionId.ToByteArray(), pin.Version.ToByteArray());

    private static TerrainScope ReadScope(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumScopeByteLength, 4, "terrain.scope.v1"u8);
        var result = new TerrainScope(reader.ReadIdentity(), reader.ReadIdentity(), reader.ReadIdentity());
        reader.Complete();
        return result;
    }

    private static StratifiedTerrainRules ReadRules(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumRulesByteLength, 7, "terrain.rules.v1"u8);
        long baseSurface = reader.ReadSigned64();
        int relief = reader.ReadSmallInteger(StratifiedTerrainRules.MaximumRelief);
        int soilDepth = reader.ReadSmallInteger(StratifiedTerrainRules.MaximumSoilDepth);
        var materials = new TerrainMaterialPalette(ReadPin(reader.ReadField(MaximumPinByteLength)),
            ReadPin(reader.ReadField(MaximumPinByteLength)), ReadPin(reader.ReadField(MaximumPinByteLength)));
        reader.Complete();
        return new StratifiedTerrainRules(baseSurface, relief, soilDepth, materials);
    }

    private static CountedItemDefinition ReadPin(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumPinByteLength, 3, "terrain.material-pin.v1"u8);
        var result = new CountedItemDefinition(reader.ReadIdentity(), reader.ReadHash());
        reader.Complete();
        return result;
    }

    private static TerrainRegionBounds ReadBounds(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumBoundsByteLength, 7, "terrain.bounds.v1"u8);
        var minimum = new CellCoordinate(reader.ReadSigned64(), reader.ReadSigned64(), reader.ReadSigned64());
        var result = new TerrainRegionBounds(minimum, reader.ReadSmallInteger(TerrainRegionBounds.MaximumWidth),
            reader.ReadSmallInteger(TerrainRegionBounds.MaximumDepth), reader.ReadSmallInteger(TerrainRegionBounds.MaximumHeight));
        reader.Complete();
        return result;
    }
}
