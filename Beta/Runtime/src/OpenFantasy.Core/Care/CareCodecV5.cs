using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Explicit voluntary request closure, with exact bounded per-patient closed-defeat evidence.</summary>
public static class CareCodecV5
{
    public const int MaximumEncodedByteLength = CareCodecV2.MaximumEncodedByteLength;
    public const int MaximumContinuationByteLength = 64 * 1_024;
    private const int MaximumOrdinaryBytes = 512 * 1_024;
    private const int MaximumDispositionBytes = 256;

    public static byte[] Encode(CareSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.SupportsDispositions) throw new ArgumentException("Care V5 requires explicit voluntary-closure admission.");
        bool recovery = state.Ordinary?.SupportsIncapacitatedAssistance == true;
        byte[] ordinary = state.Ordinary is not { } owner ? [] : state.Continuation is null
            ? CareCodecV2.EncodeOrdinary(owner, recovery) : CareCodecV3.EncodeOrdinary(owner, recovery);
        byte[] bytes = Frame("care.snapshot", Number(5), state.WorldId.ToByteArray(), state.BranchId.ToByteArray(),
            Number(state.Revision), CareDispositionPolicy.RulesPin.ToByteArray(),
            Number(state.TotalAcknowledgments), Number(state.TotalTreatments), Number(state.TotalDispositions),
            state.Continuation?.Commitment ?? ReadOnlyMemory<byte>.Empty,
            Collection("care.remedies", state.Remedies.Select(CareCodecV1.EncodeRemedy)),
            Collection("care.agreements", state.Agreements.Select(CareCodecV1.EncodeAgreement)),
            Collection("care.acknowledgments", state.Acknowledgments.Select(CareCodecV1.EncodeAcknowledgment)),
            Collection("care.treatments", state.Treatments.Select(CareCodecV1.EncodeTreatment)),
            Collection("care.dispositions", state.Dispositions.Select(EncodeDisposition)), ordinary);
        // Even bounding all three histories independently gives <900 KiB, plus <=512 KiB ordinary,
        // <=64 KiB continuation and <16 KiB catalogs/framing: strictly below the retained 2-MiB limit.
        if (bytes.Length > MaximumEncodedByteLength) throw new ArgumentException("Care V5 exceeds its bounded archive.");
        return bytes;
    }

    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes) => DecodeCore(bytes, null);
    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes, CareContinuationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return DecodeCore(bytes, context);
    }

    private static CareSnapshot DecodeCore(ReadOnlySpan<byte> bytes, CareContinuationContext? context)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 16);
            reader.Tag("care.snapshot"u8);
            if (reader.Number() != 5) throw new FormatException("Unsupported voluntary-closure version.");
            EntityId world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            if (!reader.Exact(32).SequenceEqual(CareDispositionPolicy.RulesPin.ToByteArray()))
                throw new FormatException("Care V5 requires the exact voluntary-closure policy.");
            ulong acknowledgmentsTotal = reader.Number(), treatmentsTotal = reader.Number(), dispositionsTotal = reader.Number();
            var commitment = reader.Field(MaximumContinuationByteLength);
            if (context is null ? !commitment.IsEmpty : !context.SupportsDispositions
                || world != context.WorldId || branch != context.BranchId || !commitment.SequenceEqual(context.Commitment.Span))
                throw new FormatException("Care V5 requires exactly its captured continuation context.");
            var remedies = ReadEntries(reader.Field(MaximumEncodedByteLength), "care.remedies"u8, CareSnapshot.MaximumRemedies, 512, CareCodecV1.DecodeRemedy);
            var agreements = ReadEntries(reader.Field(MaximumEncodedByteLength), "care.agreements"u8, CareSnapshot.MaximumAgreements, 512, CareCodecV1.DecodeAgreement);
            var acknowledgments = ReadEntries(reader.Field(MaximumEncodedByteLength), "care.acknowledgments"u8, CareSnapshot.MaximumAcknowledgments, 2_560, CareCodecV1.DecodeAcknowledgment);
            var treatments = ReadEntries(reader.Field(MaximumEncodedByteLength), "care.treatments"u8, CareSnapshot.MaximumTreatments, 512, CareCodecV1.DecodeTreatment);
            var dispositions = ReadEntries(reader.Field(MaximumEncodedByteLength), "care.dispositions"u8, CareSnapshot.MaximumDispositions, MaximumDispositionBytes, DecodeDisposition);
            var ordinaryBytes = reader.Field(MaximumOrdinaryBytes);
            reader.Complete();
            OrdinaryCareSnapshot? ordinary = null;
            if (!ordinaryBytes.IsEmpty)
            {
                ulong ordinaryVersion = WorldCodecVersion.Read(ordinaryBytes, "care.ordinary"u8, MaximumOrdinaryBytes, 7);
                bool recovery = ordinaryVersion is 3 or 4;
                ordinary = context is null ? CareCodecV2.DecodeOrdinary(ordinaryBytes, recovery)
                    : CareCodecV3.DecodeOrdinary(ordinaryBytes, context, recovery);
            }
            var state = CareSnapshot.RestoreWithDispositions(context, world, branch, revision, remedies, agreements,
                acknowledgments, treatments, dispositions, ordinary);
            if (state.TotalAcknowledgments != acknowledgmentsTotal || state.TotalTreatments != treatmentsTotal
                || state.TotalDispositions != dispositionsTotal || !Encode(state).AsSpan().SequenceEqual(bytes))
                throw new FormatException("Care V5 requires exact totals and canonical bytes.");
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Care V5 violates its bounded ownership contract.", error);
        }
    }

    public static byte[] EncodeContinuation(CareContinuationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.SupportsDispositions) throw new ArgumentException("Care V5 requires a voluntary-closure continuation.");
        return context.ToCommitment();
    }

    internal static byte[] ComputeContinuation(CareContinuationContext context)
    {
        byte[] bytes = Frame("care.continuation.v3", context.WorldId.ToByteArray(), context.BranchId.ToByteArray(),
            CareDispositionPolicy.RulesPin.ToByteArray(), Number(context.Ordinal), context.ClosingCareHash.ToByteArray(),
            Number(context.ClosingRevision), Number(context.ArchivedAcknowledgments), Number(context.ArchivedTreatments),
            Number(context.ArchivedDispositions), Number(context.ArchivedOrdinaryUses), Number(context.Frontier.WorldSequence),
            Pin(Collection("care.remedies", context.Remedies.Select(CareCodecV1.EncodeRemedy))),
            Pin(Collection("care.agreements", context.Agreements.Select(CareCodecV1.EncodeAgreement))),
            context.Definitions is { } definitions && context.Permissions is { } permissions
                ? Frame("care.continuation-ordinary.v1",
                    Pin(Collection("care.use-definitions", definitions.Select(CareCodec.EncodeUseDefinition))),
                    Pin(Collection("care.permissions", permissions.Select(CareCodecV2.EncodePermission)))) : [],
            context.Pending is { } pending ? Pin(CareCodecV1.EncodeAcknowledgment(pending)) : [],
            Collection("care.patient-witnesses", context.PatientWitnesses.Select(witness => Frame("care.patient-witness.v1",
                witness.PatientActorId.ToByteArray(), CareCodecV1.EncodeTreatment(witness.Treatment)))),
            Collection("care.closed-defeat-witnesses", context.ClosedDefeatWitnesses.Select(witness => Frame("care.closed-defeat-witness.v1",
                witness.PatientActorId.ToByteArray(), CareCodecV1.EncodeAcknowledgment(witness.Acknowledgment),
                witness.Treatment is { } treatment ? CareCodecV1.EncodeTreatment(treatment) : [],
                witness.Disposition is { } disposition ? EncodeDisposition(disposition) : []))));
        // Each of at most eight closed witnesses has a <=2,560-byte acknowledgment and one <=512-byte
        // terminal, plus <128 bytes of framing/identity. Eight independent latest-treatment witnesses
        // cost <8*640 bytes; fixed fields, pins and collections cost <2,048. Total <32,768 bytes, even
        // with a pending request (pinned by 32 bytes). The independently admitted 64-KiB bound retains
        // headroom without changing the legacy V3/V4 4,096-byte contract.
        if (context.PatientWitnesses.Count > CareContinuationContext.MaximumPatientWitnesses
            || context.ClosedDefeatWitnesses.Count > CareContinuationContext.MaximumPatientWitnesses
            || bytes.Length > MaximumContinuationByteLength)
            throw new ArgumentException("Care V5 continuation exceeds its bounded per-patient frontier.");
        return bytes;
    }

    internal static byte[] EncodeDisposition(CareDisposition value) => Frame("care.disposition.v1",
        value.DispositionId.ToByteArray(), value.CommandId.ToByteArray(), value.AcknowledgmentId.ToByteArray(),
        value.DecidingActorId.ToByteArray(), Number((ulong)value.Kind), Number(value.WorldSequence));

    private static CareDisposition DecodeDisposition(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumDispositionBytes, 7);
        reader.Tag("care.disposition.v1"u8);
        var value = new CareDisposition(reader.Identity(), reader.Identity(), reader.Identity(), reader.Identity(),
            (CareDispositionKind)checked((int)reader.Number()), reader.Number());
        reader.Complete();
        return value;
    }

    private static List<T> ReadEntries<T>(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> tag, int maximum, int size, Func<byte[], T> decode)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, maximum + 1);
        reader.Tag(tag);
        List<T> values = [];
        while (reader.Remaining != 0) values.Add(decode(reader.Field(size).ToArray()));
        reader.Complete();
        return values;
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Pin(byte[] bytes) => ContentHash.Compute(bytes).ToByteArray();
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
    private static byte[] Collection(string tag, IEnumerable<byte[]> values) => Frame(tag, values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
}
