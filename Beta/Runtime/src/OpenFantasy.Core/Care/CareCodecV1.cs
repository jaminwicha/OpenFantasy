using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Canonical complete permission and treatment history, independent of rendering and transport.</summary>
public static class CareCodecV1
{
    public const int MaximumEncodedByteLength = 1_024 * 1_024;
    private const int MaximumDefinitionBytes = 512;
    private const int MaximumAcknowledgmentBytes = 2_560;
    private const int MaximumTreatmentBytes = 512;

    public static byte[] Encode(CareSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SupportsOrdinary) throw new ArgumentException("Care V1 cannot discard ordinary care ownership.");
        byte[] bytes = Frame("care.snapshot", Number(1), state.WorldId.ToByteArray(), state.BranchId.ToByteArray(),
            Number(state.Revision), Collection("care.remedies", state.Remedies.Select(EncodeRemedy)),
            Collection("care.agreements", state.Agreements.Select(EncodeAgreement)),
            Collection("care.acknowledgments", state.Acknowledgments.Select(EncodeAcknowledgment)),
            Collection("care.treatments", state.Treatments.Select(EncodeTreatment)));
        if (bytes.Length > MaximumEncodedByteLength) throw new ArgumentException("Care history exceeds its archive bound.");
        return bytes;
    }

    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 9);
            reader.Tag("care.snapshot"u8);
            reader.Version();
            EntityId world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var remedies = ReadEntries(reader.Field(MaximumEncodedByteLength), "care.remedies"u8,
                CareSnapshot.MaximumRemedies, MaximumDefinitionBytes, DecodeRemedy);
            var agreements = ReadEntries(reader.Field(MaximumEncodedByteLength), "care.agreements"u8,
                CareSnapshot.MaximumAgreements, MaximumDefinitionBytes, DecodeAgreement);
            var acknowledgments = ReadEntries(reader.Field(MaximumEncodedByteLength), "care.acknowledgments"u8,
                CareSnapshot.MaximumAcknowledgments, MaximumAcknowledgmentBytes, DecodeAcknowledgment);
            var treatments = ReadEntries(reader.Field(MaximumEncodedByteLength), "care.treatments"u8,
                CareSnapshot.MaximumTreatments, MaximumTreatmentBytes, DecodeTreatment);
            reader.Complete();
            var state = new CareSnapshot(world, branch, revision, remedies, agreements, acknowledgments, treatments);
            if (!Encode(state).AsSpan().SequenceEqual(bytes)) throw new FormatException("Care archive is not canonical.");
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Care archive violates its bounded ownership contract.", error);
        }
    }

    internal static byte[] EncodeRemedy(RestorativeRemedyDefinition value) => Frame("care.remedy.v1",
        value.DefinitionId.ToByteArray(), value.Item.DefinitionId.ToByteArray(), value.Item.Version.ToByteArray(),
        Number(value.RestoredHp), Number(value.RangeSubunits));

    internal static byte[] EncodeAgreement(CareAgreement value) => Frame("care.agreement.v1",
        value.AgreementId.ToByteArray(), value.PatientActorId.ToByteArray(), value.HelperActorId.ToByteArray(),
        value.RemedyDefinitionId.ToByteArray(), value.RemedyVersion.ToByteArray());

    internal static byte[] EncodeAcknowledgment(CareAcknowledgment value) => Frame("care.acknowledgment.v1",
        value.AcknowledgmentId.ToByteArray(), value.CommandId.ToByteArray(), value.AgreementId.ToByteArray(),
        value.EncounterId.ToByteArray(), Number(value.WorldSequence), value.ActionHash.ToByteArray(),
        value.TimingHash.ToByteArray(), EncounterActionCodecV1.EncodeResolution(value.Defeat));

    internal static byte[] EncodeTreatment(CareTreatment value) => Frame("care.treatment.v1",
        value.TreatmentId.ToByteArray(), value.CommandId.ToByteArray(), value.AcknowledgmentId.ToByteArray(),
        value.ConsumptionId.ToByteArray(), Number(value.WorldSequence), Number(value.TimingRevision),
        Number(value.PatientRevisionBefore), Number((ulong)value.MaximumHp), Number((ulong)value.HpAfter),
        Number(value.HelperNormalizedSpeed), Number(value.HelperDelay));

    internal static RestorativeRemedyDefinition DecodeRemedy(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumDefinitionBytes, 6);
        reader.Tag("care.remedy.v1"u8);
        var value = new RestorativeRemedyDefinition(reader.Identity(), new CountedItemDefinition(reader.Identity(),
            Hash(reader.Exact(32))), checked((uint)reader.Number()), reader.Number());
        reader.Complete();
        return value;
    }

    internal static CareAgreement DecodeAgreement(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumDefinitionBytes, 6);
        reader.Tag("care.agreement.v1"u8);
        var value = new CareAgreement(reader.Identity(), reader.Identity(), reader.Identity(), reader.Identity(), Hash(reader.Exact(32)));
        reader.Complete();
        return value;
    }

    internal static CareAcknowledgment DecodeAcknowledgment(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumAcknowledgmentBytes, 9);
        reader.Tag("care.acknowledgment.v1"u8);
        var value = new CareAcknowledgment(reader.Identity(), reader.Identity(), reader.Identity(), reader.Identity(),
            reader.Number(), Hash(reader.Exact(32)), Hash(reader.Exact(32)),
            EncounterActionCodecV1.DecodeResolution(reader.Field(2_048)));
        reader.Complete();
        return value;
    }

    internal static CareTreatment DecodeTreatment(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumTreatmentBytes, 12);
        reader.Tag("care.treatment.v1"u8);
        var value = new CareTreatment(reader.Identity(), reader.Identity(), reader.Identity(), reader.Identity(),
            reader.Number(), reader.Number(), reader.Number(), checked((int)reader.Number()),
            checked((int)reader.Number()), checked((uint)reader.Number()), reader.Number());
        reader.Complete();
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
