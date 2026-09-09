using System.Buffers.Binary;
using System.Security.Cryptography;

namespace OpenFantasy.Core.Terrain;

/// <summary>Stateless generation of fresh substrate. Existing snapshots must be loaded and edited, never regenerated over.</summary>
public sealed class StratifiedTerrainGenerator
{
    public TerrainRegionSnapshot Generate(TerrainScope scope, EntityId seed, StratifiedTerrainRules rules,
        TerrainRegionBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(bounds);
        byte[] context = GenerationContext(scope, seed, rules);
        TerrainCell[] cells = new TerrainCell[bounds.CellCount];
        for (int y = 0; y < bounds.Depth; y++)
        {
            for (int x = 0; x < bounds.Width; x++)
            {
                CellCoordinate column = bounds.Minimum.Offset(x, y, 0);
                long surface = SurfaceHeight(context, rules, column.X, column.Y);
                for (int z = 0; z < bounds.Height; z++)
                {
                    CellCoordinate coordinate = column.Offset(0, 0, z);
                    TerrainStratum stratum = Classify(coordinate.Z, surface, rules.SoilDepth);
                    TerrainCell cell = CreateCell(context, rules, coordinate, stratum);
                    cells[bounds.IndexOf(coordinate)] = cell;
                }
            }
        }
        return new TerrainRegionSnapshot(scope, seed, rules, bounds, 0, cells);
    }

    private static byte[] GenerationContext(TerrainScope scope, EntityId seed, StratifiedTerrainRules rules) =>
        CanonicalEncodingV1.Frame("terrain.generation.v1", scope.WorldId.ToByteArray(), scope.RealmId.ToByteArray(),
            seed.ToByteArray(), rules.Version.ToByteArray());

    private static long SurfaceHeight(byte[] context, StratifiedTerrainRules rules, long x, long y)
    {
        const int spacing = StratifiedTerrainRules.LatticeSpacing;
        (long latticeX, int fractionX) = DivideFloor(x, spacing);
        (long latticeY, int fractionY) = DivideFloor(y, spacing);
        int left = spacing - fractionX;
        int lower = spacing - fractionY;
        int weighted = LatticeHeight(context, rules.Relief, latticeX, latticeY) * left * lower
            + LatticeHeight(context, rules.Relief, latticeX + 1, latticeY) * fractionX * lower
            + LatticeHeight(context, rules.Relief, latticeX, latticeY + 1) * left * fractionY
            + LatticeHeight(context, rules.Relief, latticeX + 1, latticeY + 1) * fractionX * fractionY;
        // Interpolate nonnegative samples, floor once, then center. No platform float rounding.
        int offset = weighted / (spacing * spacing) - rules.Relief;
        return checked(rules.BaseSurfaceZ + offset);
    }

    private static (long Quotient, int Remainder) DivideFloor(long coordinate, int divisor)
    {
        long quotient = coordinate / divisor;
        int remainder = (int)(coordinate % divisor);
        if (remainder < 0)
        {
            quotient--;
            remainder += divisor;
        }
        return (quotient, remainder);
    }

    private static int LatticeHeight(byte[] context, int relief, long x, long y)
    {
        byte[] digest = SHA256.HashData(CanonicalEncodingV1.Frame("terrain.relief.v1", context,
            TerrainRegionCodecV1.Signed64(x), TerrainRegionCodecV1.Signed64(y)));
        return (int)(BinaryPrimitives.ReadUInt64BigEndian(digest) % (ulong)(2 * relief + 1));
    }

    private static TerrainStratum Classify(long z, long surface, int soilDepth)
    {
        if (z > surface)
        {
            return TerrainStratum.Void;
        }
        if (z == surface)
        {
            return TerrainStratum.Surface;
        }
        return z >= surface - soilDepth ? TerrainStratum.Soil : TerrainStratum.Stone;
    }

    private static TerrainCell CreateCell(byte[] context, StratifiedTerrainRules rules,
        CellCoordinate coordinate, TerrainStratum stratum)
    {
        if (stratum == TerrainStratum.Void)
        {
            return new TerrainCell(coordinate, stratum, null, null, null);
        }
        byte[] formationDigest = SHA256.HashData(CanonicalEncodingV1.Frame("terrain.formation.v1", context,
            TerrainRegionCodecV1.Signed64(coordinate.X), TerrainRegionCodecV1.Signed64(coordinate.Y),
            CanonicalEncodingV1.Unsigned64((ulong)stratum)));
        EntityId formation = EntityId.FromBytes(formationDigest.AsSpan(0, EntityId.ByteLength));
        byte[] sourceDigest = SHA256.HashData(CanonicalEncodingV1.Frame("terrain.source.v1", formation.ToByteArray(),
            TerrainRegionCodecV1.Signed64(coordinate.Z)));
        EntityId source = EntityId.FromBytes(sourceDigest.AsSpan(0, EntityId.ByteLength));
        if (source == formation)
        {
            throw new InvalidOperationException("A generated source identity collided with its formation identity.");
        }
        return new TerrainCell(coordinate, stratum, rules.Materials.For(stratum), formation, source);
    }
}
