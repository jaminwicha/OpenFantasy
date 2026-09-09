namespace OpenFantasy.Core.Custody;

/// <summary>Canonical bounded snapshot hashing. Durable decoding/migration belongs to a later storage contract.</summary>
public static class CustodyEncodingV1
{
    public static byte[] Encode(CustodySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var storages = snapshot.Storages.Select(storage => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame(
            "custody.storage.v1", storage.CustodianId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(storage.UnitCapacity), CanonicalEncodingV1.Unsigned64(storage.Revision))).ToArray();
        var lots = snapshot.Lots.Select(lot => (ReadOnlyMemory<byte>)EncodeLot(lot)).ToArray();
        return CanonicalEncodingV1.Frame("custody.snapshot.v1", snapshot.WorldId.ToByteArray(),
            snapshot.BranchId.ToByteArray(), CanonicalEncodingV1.Unsigned64(snapshot.Revision),
            CanonicalEncodingV1.Frame("custody.storages.v1", storages),
            CanonicalEncodingV1.Frame("custody.lots.v1", lots));
    }

    private static byte[] EncodeLot(CountedSourceLot lot) => CanonicalEncodingV1.Frame("custody.source-lot.v1",
        lot.LotId.ToByteArray(), lot.CustodianId.ToByteArray(), lot.Definition.DefinitionId.ToByteArray(),
        lot.Definition.Version.ToByteArray(), lot.Source.SourceId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(lot.Source.Start), CanonicalEncodingV1.Unsigned64(lot.Source.End),
        CanonicalEncodingV1.Unsigned64(lot.Revision), CanonicalEncodingV1.Unsigned64((ulong)lot.Protection),
        lot.ReservedBy?.ToByteArray() ?? []);
}
