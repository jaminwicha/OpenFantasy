using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Explicit ordinary-care ownership alongside unchanged aftermath record formats.</summary>
public static partial class CareCodecV2
{
    public const int MaximumEncodedByteLength = 2 * 1_024 * 1_024;
    private const int MaximumOrdinaryBytes = 512 * 1_024;
    private const int MaximumUseDefinitionBytes = 2_048;
    private const int MaximumPermissionBytes = 1_024;
    private const int MaximumRevocationBytes = 512;
    private const int MaximumUseBytes = 1_024;

    public static byte[] Encode(CareSnapshot state) => EncodeVersion(state, 2);

    internal static byte[] EncodeVersion(CareSnapshot state, int version)
    {
        if (version is not (2 or 4)) throw new ArgumentOutOfRangeException(nameof(version));
        ArgumentNullException.ThrowIfNull(state);
        if (state.Ordinary is not { } ordinary) throw new ArgumentException("Care V2 requires an explicit ordinary owner.");
        if (ordinary.SupportsIncapacitatedAssistance != (version == 4))
            throw new ArgumentException("Care format must select the exact recovery catalog interpretation.");
        if (state.Continuation is not null) throw new ArgumentException("Care V2 cannot discard a continuation frontier.");
        byte[] bytes = Frame("care.snapshot", Number((ulong)version), state.WorldId.ToByteArray(), state.BranchId.ToByteArray(),
            Number(state.Revision), Collection("care.remedies", state.Remedies.Select(CareCodecV1.EncodeRemedy)),
            Collection("care.agreements", state.Agreements.Select(CareCodecV1.EncodeAgreement)),
            Collection("care.acknowledgments", state.Acknowledgments.Select(CareCodecV1.EncodeAcknowledgment)),
            Collection("care.treatments", state.Treatments.Select(CareCodecV1.EncodeTreatment)), EncodeOrdinary(ordinary, version == 4));
        if (bytes.Length > MaximumEncodedByteLength) throw new ArgumentException("Care history exceeds its ordinary archive bound.");
        return bytes;
    }

    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes) => DecodeVersion(bytes, 2);

    internal static CareSnapshot DecodeVersion(ReadOnlySpan<byte> bytes, int version)
    {
        if (version is not (2 or 4)) throw new ArgumentOutOfRangeException(nameof(version));
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 10);
            reader.Tag("care.snapshot"u8);
            if (reader.Number() != (ulong)version) throw new FormatException("Unsupported ordinary care version.");
            EntityId world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var remedies = ReadEntries(reader.Field(CareCodecV1.MaximumEncodedByteLength), "care.remedies"u8,
                CareSnapshot.MaximumRemedies, 512, CareCodecV1.DecodeRemedy);
            var agreements = ReadEntries(reader.Field(CareCodecV1.MaximumEncodedByteLength), "care.agreements"u8,
                CareSnapshot.MaximumAgreements, 512, CareCodecV1.DecodeAgreement);
            var acknowledgments = ReadEntries(reader.Field(CareCodecV1.MaximumEncodedByteLength), "care.acknowledgments"u8,
                CareSnapshot.MaximumAcknowledgments, 2_560, CareCodecV1.DecodeAcknowledgment);
            var treatments = ReadEntries(reader.Field(CareCodecV1.MaximumEncodedByteLength), "care.treatments"u8,
                CareSnapshot.MaximumTreatments, 512, CareCodecV1.DecodeTreatment);
            var ordinary = DecodeOrdinary(reader.Field(MaximumOrdinaryBytes), version == 4);
            reader.Complete();
            var state = new CareSnapshot(world, branch, revision, remedies, agreements, acknowledgments, treatments, ordinary);
            if (!EncodeVersion(state, version).AsSpan().SequenceEqual(bytes)) throw new FormatException("Ordinary care archive is not canonical.");
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Ordinary care archive violates its bounded ownership contract.", error);
        }
    }

    private static byte[] EncodeOrdinary(OrdinaryCareSnapshot state, bool recovery)
    {
        byte[] bytes = Frame("care.ordinary", Number(recovery ? 3UL : 1UL), Number(state.Revision),
            Collection("care.use-definitions", state.Definitions.Select(value => EncodeUseDefinition(value, recovery))),
            Collection("care.permissions", state.Permissions.Select(EncodePermission)),
            Collection("care.ordinary-uses", state.Uses.Select(value => EncodeUse(value, recovery))));
        if (bytes.Length > MaximumOrdinaryBytes) throw new ArgumentException("Ordinary history exceeds its bounded archive.");
        return bytes;
    }

    private static OrdinaryCareSnapshot DecodeOrdinary(ReadOnlySpan<byte> bytes, bool recovery)
    {
        var reader = new WorldFrameReader(bytes, MaximumOrdinaryBytes, 6);
        reader.Tag("care.ordinary"u8);
        if (reader.Number() != (recovery ? 3UL : 1UL)) throw new FormatException("Unsupported inline ordinary interpretation.");
        ulong revision = reader.Number();
        var definitions = ReadEntries(reader.Field(MaximumOrdinaryBytes), "care.use-definitions"u8,
            OrdinaryCareSnapshot.MaximumDefinitions, MaximumUseDefinitionBytes, entry => DecodeUseDefinition(entry, recovery));
        var permissions = ReadEntries(reader.Field(MaximumOrdinaryBytes), "care.permissions"u8,
            OrdinaryCareSnapshot.MaximumPermissions, MaximumPermissionBytes, DecodePermission);
        var uses = ReadEntries(reader.Field(MaximumOrdinaryBytes), "care.ordinary-uses"u8,
            OrdinaryCareSnapshot.MaximumUses, MaximumUseBytes, entry => DecodeUse(entry, recovery));
        reader.Complete();
        var value = new OrdinaryCareSnapshot(definitions, permissions, uses);
        if (value.Revision != revision) throw new FormatException("Ordinary revision must equal its retained operation count.");
        return value;
    }

    private static List<T> ReadEntries<T>(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> tag, int count, int size, Func<byte[], T> decode)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, count + 1);
        reader.Tag(tag);
        List<T> values = [];
        while (reader.Remaining != 0) values.Add(decode(reader.Field(size).ToArray()));
        reader.Complete();
        return values;
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static ContentHash Hash(ReadOnlySpan<byte> bytes) => new(Convert.ToHexString(bytes).ToLowerInvariant());
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
    private static byte[] Collection(string tag, IEnumerable<byte[]> values) =>
        Frame(tag, values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
}
