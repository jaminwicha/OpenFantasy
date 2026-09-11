using System.Buffers.Binary;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Bounded immutable preparation archive. Decoding verifies every derived byte against the exact v1 algorithm.</summary>
public static class WatershedCodecV1
{
    public const int MaximumEncodedByteLength = 200_000;
    private const int ColumnBytes = 44;
    private const int MaximumRequestBytes = 768;
    private const int MaximumBoundsBytes = 160;
    private const int MaximumRulesBytes = 128;

    public static byte[] Encode(PreparedWatershed plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        byte[] columns = new byte[checked(plan.Columns.Count * ColumnBytes)];
        foreach (WatershedColumn column in plan.Columns)
        {
            Span<byte> bytes = columns.AsSpan(column.Index * ColumnBytes, ColumnBytes);
            BinaryPrimitives.WriteInt64BigEndian(bytes, column.NaturalElevation);
            BinaryPrimitives.WriteInt64BigEndian(bytes[8..], column.SpillElevation);
            BinaryPrimitives.WriteInt32BigEndian(bytes[16..], column.DownstreamIndex ?? -1);
            BinaryPrimitives.WriteInt32BigEndian(bytes[20..], column.DrainageRank);
            BinaryPrimitives.WriteInt32BigEndian(bytes[24..], column.ContributingColumns);
            column.SubstrateParcelId.WriteBytes(bytes[28..]);
        }
        return CanonicalEncodingV1.Frame("geography.watershed.prepared.v1", EncodeRequest(plan.Request),
            plan.BasinId.ToByteArray(), plan.FormationId.ToByteArray(), columns);
    }

    public static PreparedWatershed Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new CanonicalTerrainReader(bytes, MaximumEncodedByteLength, 5, "geography.watershed.prepared.v1"u8);
            WatershedRequest request = ReadRequest(reader.ReadField(MaximumRequestBytes));
            _ = reader.ReadIdentity();
            _ = reader.ReadIdentity();
            _ = reader.ReadExactField(checked(request.Bounds.ColumnCount * ColumnBytes));
            reader.Complete();
            PreparedWatershed plan = new WatershedPlanner().Prepare(request);
            if (!Encode(plan).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The prepared watershed differs from its pinned derivation.");
            }
            return plan;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        {
            throw new FormatException("The watershed preparation contains invalid inputs or identities.", error);
        }
    }

    internal static byte[] EncodeRules(WatershedRules rules) => CanonicalEncodingV1.Frame("geography.watershed.rules.v1",
        TerrainRegionCodecV1.Signed64(rules.BaseElevation), CanonicalEncodingV1.Unsigned64((ulong)rules.Relief));

    internal static byte[] EncodeBounds(WatershedBounds bounds) => CanonicalEncodingV1.Frame("geography.watershed.bounds.v1",
        TerrainRegionCodecV1.Signed64(bounds.MinimumX), TerrainRegionCodecV1.Signed64(bounds.MinimumY),
        CanonicalEncodingV1.Unsigned64((ulong)bounds.Width), CanonicalEncodingV1.Unsigned64((ulong)bounds.Depth));

    private static byte[] EncodeRequest(WatershedRequest request) => CanonicalEncodingV1.Frame("geography.watershed.request.v1",
        request.Scope.WorldId.ToByteArray(), request.Scope.BranchId.ToByteArray(), request.Scope.RealmId.ToByteArray(),
        request.Seed.ToByteArray(), EncodeBounds(request.Bounds), EncodeRules(request.Rules),
        CanonicalEncodingV1.Unsigned64((ulong)request.OutletIndex), request.ExternalBoundaryId.ToByteArray());

    private static WatershedRequest ReadRequest(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumRequestBytes, 9, "geography.watershed.request.v1"u8);
        var scope = new TerrainScope(reader.ReadIdentity(), reader.ReadIdentity(), reader.ReadIdentity());
        EntityId seed = reader.ReadIdentity();
        var boundsReader = new CanonicalTerrainReader(reader.ReadField(MaximumBoundsBytes), MaximumBoundsBytes, 5,
            "geography.watershed.bounds.v1"u8);
        var bounds = new WatershedBounds(boundsReader.ReadSigned64(), boundsReader.ReadSigned64(),
            boundsReader.ReadSmallInteger(WatershedBounds.MaximumAxis), boundsReader.ReadSmallInteger(WatershedBounds.MaximumAxis));
        boundsReader.Complete();
        var rulesReader = new CanonicalTerrainReader(reader.ReadField(MaximumRulesBytes), MaximumRulesBytes, 3,
            "geography.watershed.rules.v1"u8);
        var rules = new WatershedRules(rulesReader.ReadSigned64(), rulesReader.ReadSmallInteger(WatershedRules.MaximumRelief));
        rulesReader.Complete();
        int outlet = reader.ReadSmallInteger(WatershedBounds.MaximumColumns - 1);
        EntityId boundary = reader.ReadIdentity();
        reader.Complete();
        return new WatershedRequest(scope, seed, bounds, rules, outlet, boundary);
    }
}
