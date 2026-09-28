using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>
/// Explicit continued-care interpretation: retained records with unchanged record formats, cumulative
/// totals and the exact commitment of their closed boundary. Restoring requires that same context.
/// </summary>
public static class CareCodecV3
{
    public const int MaximumEncodedByteLength = CareCodecV2.MaximumEncodedByteLength;
    public const int MaximumContinuationByteLength = 4_096;
    private const int MaximumAftermathBytes = CareCodecV1.MaximumEncodedByteLength;
    private const int MaximumOrdinaryBytes = 512 * 1_024;
    private const int MaximumDefinitionBytes = 512;
    private const int MaximumAcknowledgmentBytes = 2_560;
    private const int MaximumTreatmentBytes = 512;
    private const int MaximumUseDefinitionBytes = 2_048;
    private const int MaximumPermissionBytes = 1_024;
    private const int MaximumUseBytes = 1_024;

    public static byte[] Encode(CareSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Continuation is not { } continuation) throw new ArgumentException("Care V3 requires a continued owner.");
        byte[] bytes = Frame("care.snapshot", Number(3), state.WorldId.ToByteArray(), state.BranchId.ToByteArray(),
            Number(state.Revision), continuation.Commitment,
            Collection("care.remedies", state.Remedies.Select(CareCodecV1.EncodeRemedy)),
            Collection("care.agreements", state.Agreements.Select(CareCodecV1.EncodeAgreement)),
            EncodeAftermath(state), state.Ordinary is { } ordinary ? EncodeOrdinary(ordinary) : []);
        if (bytes.Length > MaximumEncodedByteLength) throw new ArgumentException("Continued care exceeds its archive bound.");
        return bytes;
    }

    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes, CareContinuationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 10);
            reader.Tag("care.snapshot"u8);
            if (reader.Number() != 3) throw new FormatException("Unsupported continued care version.");
            EntityId world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            if (!reader.Field(MaximumContinuationByteLength).SequenceEqual(context.Commitment.Span)
                || world != context.WorldId || branch != context.BranchId)
                throw new FormatException("Continued care requires its exact continuation context.");
            var remedies = ReadEntries(reader.Field(MaximumAftermathBytes), "care.remedies"u8,
                CareSnapshot.MaximumRemedies, MaximumDefinitionBytes, CareCodecV1.DecodeRemedy);
            var agreements = ReadEntries(reader.Field(MaximumAftermathBytes), "care.agreements"u8,
                CareSnapshot.MaximumAgreements, MaximumDefinitionBytes, CareCodecV1.DecodeAgreement);
            var aftermath = new WorldFrameReader(reader.Field(MaximumAftermathBytes), MaximumAftermathBytes, 6);
            var ordinaryBytes = reader.Field(MaximumOrdinaryBytes);
            reader.Complete();
            aftermath.Tag("care.aftermath"u8);
            aftermath.Version();
            ulong totalAcknowledgments = aftermath.Number(), totalTreatments = aftermath.Number();
            var acknowledgments = ReadEntries(aftermath.Field(MaximumAftermathBytes), "care.acknowledgments"u8,
                CareSnapshot.MaximumAcknowledgments, MaximumAcknowledgmentBytes, CareCodecV1.DecodeAcknowledgment);
            var treatments = ReadEntries(aftermath.Field(MaximumAftermathBytes), "care.treatments"u8,
                CareSnapshot.MaximumTreatments, MaximumTreatmentBytes, CareCodecV1.DecodeTreatment);
            aftermath.Complete();
            var ordinary = ordinaryBytes.IsEmpty ? null : DecodeOrdinary(ordinaryBytes, context);
            var state = new CareSnapshot(context, revision, remedies, agreements, acknowledgments, treatments, ordinary);
            if (state.TotalAcknowledgments != totalAcknowledgments || state.TotalTreatments != totalTreatments)
                throw new FormatException("Continued totals must equal their closed base and retained operations.");
            if (!Encode(state).AsSpan().SequenceEqual(bytes)) throw new FormatException("Continued care archive is not canonical.");
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Continued care archive violates its bounded ownership contract.", error);
        }
    }

    /// <summary>Canonical commitment of one closed boundary; catalogs and the pending record are pinned, not copied.</summary>
    public static byte[] EncodeContinuation(CareContinuationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.ToCommitment();
    }

    internal static byte[] ComputeContinuation(CareContinuationContext context)
    {
        byte[] bytes = Frame("care.continuation.v1", context.WorldId.ToByteArray(), context.BranchId.ToByteArray(),
            Number(context.Ordinal), context.ClosingCareHash.ToByteArray(), Number(context.ClosingRevision),
            Number(context.ArchivedAcknowledgments), Number(context.ArchivedTreatments),
            Number(context.ArchivedOrdinaryUses), Number(context.Frontier.WorldSequence),
            Pin(Collection("care.remedies", context.Remedies.Select(CareCodecV1.EncodeRemedy))),
            Pin(Collection("care.agreements", context.Agreements.Select(CareCodecV1.EncodeAgreement))),
            context.Definitions is { } definitions && context.Permissions is { } permissions
                ? Frame("care.continuation-ordinary.v1",
                    Pin(Collection("care.use-definitions", definitions.Select(CareCodecV2.EncodeUseDefinition))),
                    Pin(Collection("care.permissions", permissions.Select(CareCodecV2.EncodePermission))))
                : [],
            context.Pending is { } pending ? Pin(CareCodecV1.EncodeAcknowledgment(pending)) : [],
            Collection("care.patient-witnesses", context.PatientWitnesses.Select(witness => Frame("care.patient-witness.v1",
                witness.PatientActorId.ToByteArray(), CareCodecV1.EncodeTreatment(witness.Treatment)))));
        if (context.PatientWitnesses.Count > CareContinuationContext.MaximumPatientWitnesses
            || bytes.Length > MaximumContinuationByteLength)
            throw new ArgumentException("A care continuation exceeds its bounded frontier.");
        return bytes;
    }

    private static byte[] EncodeAftermath(CareSnapshot state) => Frame("care.aftermath", Number(1),
        Number(state.TotalAcknowledgments), Number(state.TotalTreatments),
        Collection("care.acknowledgments", state.Acknowledgments.Select(CareCodecV1.EncodeAcknowledgment)),
        Collection("care.treatments", state.Treatments.Select(CareCodecV1.EncodeTreatment)));

    private static byte[] EncodeOrdinary(OrdinaryCareSnapshot state)
    {
        byte[] bytes = Frame("care.ordinary", Number(2), Number(state.Revision), Number(state.TotalUses),
            Collection("care.use-definitions", state.Definitions.Select(CareCodecV2.EncodeUseDefinition)),
            Collection("care.permissions", state.Permissions.Select(CareCodecV2.EncodePermission)),
            Collection("care.ordinary-uses", state.Uses.Select(CareCodecV2.EncodeUse)));
        if (bytes.Length > MaximumOrdinaryBytes) throw new ArgumentException("Continued ordinary history exceeds its bounded archive.");
        return bytes;
    }

    private static OrdinaryCareSnapshot DecodeOrdinary(ReadOnlySpan<byte> bytes, CareContinuationContext context)
    {
        var reader = new WorldFrameReader(bytes, MaximumOrdinaryBytes, 7);
        reader.Tag("care.ordinary"u8);
        if (reader.Number() != 2) throw new FormatException("Unsupported continued ordinary care version.");
        ulong revision = reader.Number(), totalUses = reader.Number();
        var definitions = ReadEntries(reader.Field(MaximumOrdinaryBytes), "care.use-definitions"u8,
            OrdinaryCareSnapshot.MaximumDefinitions, MaximumUseDefinitionBytes, CareCodecV2.DecodeUseDefinition);
        var permissions = ReadEntries(reader.Field(MaximumOrdinaryBytes), "care.permissions"u8,
            OrdinaryCareSnapshot.MaximumPermissions, MaximumPermissionBytes, CareCodecV2.DecodePermission);
        var uses = ReadEntries(reader.Field(MaximumOrdinaryBytes), "care.ordinary-uses"u8,
            OrdinaryCareSnapshot.MaximumUses, MaximumUseBytes, CareCodecV2.DecodeUse);
        reader.Complete();
        var value = new OrdinaryCareSnapshot(definitions, permissions, uses, context);
        if (value.Revision != revision || value.TotalUses != totalUses)
            throw new FormatException("Continued ordinary totals must equal their closed base and retained operations.");
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
    private static byte[] Pin(byte[] bytes) => ContentHash.Compute(bytes).ToByteArray();
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
    private static byte[] Collection(string tag, IEnumerable<byte[]> values) =>
        Frame(tag, values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
}
