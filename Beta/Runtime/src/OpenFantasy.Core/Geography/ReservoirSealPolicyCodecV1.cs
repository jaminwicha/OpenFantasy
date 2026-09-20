using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Geography;

/// <summary>Canonical sealing policy; world authority requires explicit complete-root admission of this archive.</summary>
public static class ReservoirSealPolicyCodecV1
{
    public const int MaximumEncodedByteLength = 4000;
    private const int RuleRowBytes = 56;

    public static byte[] Encode(ReservoirSealPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        using var rows = new MemoryStream();
        foreach (var rule in policy.Rules)
        {
            rows.Write(Pin(rule.Material));
            rows.Write(CanonicalEncodingV1.Unsigned64(rule.MinimumThicknessMillimetres));
        }
        return CanonicalEncodingV1.Frame("geography.reservoir-seal-policy.v1", Pin(policy.Liquid), rows.ToArray());
    }

    public static ReservoirSealPolicy Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 3);
            reader.Tag("geography.reservoir-seal-policy.v1"u8);
            var liquid = ReadPin(reader.Exact(48));
            var rows = reader.Field(ReservoirSealPolicy.MaximumRules * RuleRowBytes);
            if (rows.Length == 0 || rows.Length % RuleRowBytes != 0)
            {
                throw new FormatException("Sealing rows must be nonempty, complete and bounded.");
            }
            List<ReservoirSealRule> rules = [];
            for (int offset = 0; offset < rows.Length; offset += RuleRowBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rules.Add(new(ReadPin(rows.Slice(offset, 48)), checked((uint)BinaryPrimitives.ReadUInt64BigEndian(rows[(offset + 48)..]))));
            }
            reader.Complete();
            var policy = new ReservoirSealPolicy(liquid, rules);
            if (!Encode(policy).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The sealing policy is not canonical.");
            }
            return policy;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid finite sealing policy.", error);
        }
    }

    private static byte[] Pin(CountedItemDefinition item) => item.DefinitionId.ToByteArray().Concat(item.Version.ToByteArray()).ToArray();
    private static CountedItemDefinition ReadPin(ReadOnlySpan<byte> bytes) => new(EntityId.FromBytes(bytes[..16]),
        new ContentHash(Convert.ToHexString(bytes[16..]).ToLowerInvariant()));
}
