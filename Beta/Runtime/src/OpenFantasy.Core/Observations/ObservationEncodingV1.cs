using System.Buffers.Binary;

namespace OpenFantasy.Core.Observations;

/// <summary>Canonical encoding of the public field whitelist; never encodes a knowledge journal.</summary>
public static class ObservationEncodingV1
{
    private const ulong ContractVersion = 1;

    public static byte[] Encode(ResourceObservationProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ReadOnlyMemory<byte>[] entries = new ReadOnlyMemory<byte>[projection.Entries.Count + 1];
        entries[0] = CanonicalEncodingV1.Unsigned64(ContractVersion);
        for (int index = 0; index < projection.Entries.Count; index++)
        {
            entries[index + 1] = EncodeEntry(projection.Entries[index]);
        }

        return CanonicalEncodingV1.Frame("observation.snapshot", CanonicalEncodingV1.Unsigned64(ContractVersion),
            EncodeAudience(projection.Audience), projection.DisclosureEpoch.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(projection.DisclosureRevision),
            CanonicalEncodingV1.Frame("observation.resources", entries));
    }

    private static byte[] EncodeAudience(ObservationAudience audience) => CanonicalEncodingV1.Frame(
        "observation.audience", CanonicalEncodingV1.Unsigned64(ContractVersion),
        audience.PrincipalId.ToByteArray(), audience.SessionId.ToByteArray(),
        audience.Scope.WorldId.ToByteArray(), audience.Scope.BranchId.ToByteArray(),
        audience.Scope.ActorId.ToByteArray(), CanonicalEncodingV1.Unsigned64(audience.ControlRevision));

    private static byte[] EncodeEntry(ResourceObservationView entry)
    {
        byte[] days = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(days, entry.AcquiredAt.Days);
        return CanonicalEncodingV1.Frame("observation.resource", CanonicalEncodingV1.Unsigned64(ContractVersion),
            entry.DisclosureId.ToByteArray(), CanonicalEncodingV1.Unsigned64(entry.DisclosureRevision),
            entry.KnownSubjectId.ToByteArray(), entry.KnownMaterial.DefinitionId.ToByteArray(),
            entry.KnownMaterial.Version.ToByteArray(), CanonicalEncodingV1.Unsigned64(entry.Quantity),
            CanonicalEncodingV1.Unsigned64((ulong)entry.Basis), CanonicalEncodingV1.Unsigned64(entry.Confidence),
            days, CanonicalEncodingV1.Unsigned64((ulong)entry.AcquiredAt.MillisecondsWithinDay),
            entry.KnownReporterId?.ToByteArray() ?? []);
    }
}
