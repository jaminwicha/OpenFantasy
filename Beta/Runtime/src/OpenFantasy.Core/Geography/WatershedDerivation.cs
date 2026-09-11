using System.Buffers.Binary;
using System.Security.Cryptography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

internal sealed class WatershedDerivation
{
    private readonly byte[] _context;
    private readonly WatershedRules _rules;

    internal WatershedDerivation(WatershedRequest request)
    {
        _rules = request.Rules;
        // Branch custody is explicit in the saved request, but common genesis geography is branch-independent.
        _context = CanonicalEncodingV1.Frame("geography.watershed.context.v1", request.Scope.WorldId.ToByteArray(),
            request.Scope.RealmId.ToByteArray(), request.Seed.ToByteArray(), WatershedCodecV1.EncodeBounds(request.Bounds),
            WatershedCodecV1.EncodeRules(request.Rules), CanonicalEncodingV1.Unsigned64((ulong)request.OutletIndex),
            request.ExternalBoundaryId.ToByteArray());
    }

    internal long NaturalElevation(int index)
    {
        byte[] digest = SHA256.HashData(CanonicalEncodingV1.Frame("geography.watershed.relief.v1", _context,
            CanonicalEncodingV1.Unsigned64((ulong)index)));
        int sample = (int)(BinaryPrimitives.ReadUInt64BigEndian(digest) % (ulong)(2 * _rules.Relief + 1));
        return checked(_rules.BaseElevation + (sample - _rules.Relief));
    }

    internal EntityId Identity(string role, ulong ordinal)
    {
        byte[] digest = SHA256.HashData(CanonicalEncodingV1.Frame("geography.watershed.identity.v1", _context,
            CanonicalEncodingV1.Text(role, nameof(role)), CanonicalEncodingV1.Unsigned64(ordinal)));
        return EntityId.FromBytes(digest.AsSpan(0, EntityId.ByteLength));
    }
}
