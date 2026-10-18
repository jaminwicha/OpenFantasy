using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Custody;

/// <summary>Canonical bounded snapshot bytes and validation, independent of durable storage.</summary>
public static class CustodyEncodingV1
{
    // Exact maxima of the already established v1 layout, including every optional reservation.
    public const int MaximumSnapshotByteLength = 942_753;
    private const int MaximumStorageCollectionBytes = 21_027;
    private const int MaximumLotCollectionBytes = 921_631;
    private const int StorageRecordBytes = 78;
    private const int MaximumLotRecordBytes = 221;

    public static CustodySnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        CanonicalCustodyReader reader = new(bytes, MaximumSnapshotByteLength, 6);
        reader.ExpectTag("custody.snapshot.v1"u8);
        EntityId world = reader.ReadIdentity();
        EntityId branch = reader.ReadIdentity();
        ulong revision = reader.ReadUnsigned64();
        List<CustodianStorage> storages = ReadStorages(reader.ReadField(MaximumStorageCollectionBytes));
        List<CountedSourceLot> lots = ReadLots(reader.ReadField(MaximumLotCollectionBytes));
        reader.Complete();
        try
        {
            return new CustodySnapshot(world, branch, revision, storages, lots);
        }
        catch (ArgumentException)
        {
            throw CanonicalCustodyReader.Failure(CustodyFormatFailure.InvalidValue,
                "The snapshot violates custody invariants.");
        }
    }

    public static byte[] Encode(CustodySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var storages = snapshot.Storages.Select(storage => (ReadOnlyMemory<byte>)EncodeStorage(storage)).ToArray();
        var lots = snapshot.Lots.Select(lot => (ReadOnlyMemory<byte>)EncodeLot(lot)).ToArray();
        return CanonicalEncodingV1.Frame("custody.snapshot.v1", snapshot.WorldId.ToByteArray(),
            snapshot.BranchId.ToByteArray(), CanonicalEncodingV1.Unsigned64(snapshot.Revision),
            CanonicalEncodingV1.Frame("custody.storages.v1", storages),
            CanonicalEncodingV1.Frame("custody.lots.v1", lots));
    }

    private static List<CustodianStorage> ReadStorages(ReadOnlySpan<byte> bytes)
    {
        CanonicalCustodyReader reader = new(bytes, MaximumStorageCollectionBytes,
            CustodySnapshot.MaximumCustodians + 1);
        reader.ExpectTag("custody.storages.v1"u8);
        var storages = new List<CustodianStorage>();
        while (reader.RemainingFields > 0)
        {
            CanonicalCustodyReader entry = new(reader.ReadField(StorageRecordBytes), StorageRecordBytes, 4);
            entry.ExpectTag("custody.storage.v1"u8);
            var storage = new CustodianStorage(entry.ReadIdentity(), entry.ReadUnsigned64(), entry.ReadUnsigned64());
            entry.Complete();
            if (storages.Count > 0 && storages[^1].CustodianId >= storage.CustodianId)
            {
                throw CanonicalCustodyReader.Failure(CustodyFormatFailure.NonCanonical,
                    "Custodian identities must be unique and strictly sorted.");
            }
            storages.Add(storage);
        }
        reader.Complete();
        return storages;
    }

    private static List<CountedSourceLot> ReadLots(ReadOnlySpan<byte> bytes)
    {
        CanonicalCustodyReader reader = new(bytes, MaximumLotCollectionBytes, CustodySnapshot.MaximumLots + 1);
        reader.ExpectTag("custody.lots.v1"u8);
        var lots = new List<CountedSourceLot>();
        while (reader.RemainingFields > 0)
        {
            CountedSourceLot lot = ReadLot(reader.ReadField(MaximumLotRecordBytes));
            if (lots.Count > 0 && lots[^1].LotId >= lot.LotId)
            {
                throw CanonicalCustodyReader.Failure(CustodyFormatFailure.NonCanonical,
                    "Lot identities must be unique and strictly sorted.");
            }
            lots.Add(lot);
        }
        reader.Complete();
        return lots;
    }

    private static CountedSourceLot ReadLot(ReadOnlySpan<byte> bytes)
    {
        CanonicalCustodyReader reader = new(bytes, MaximumLotRecordBytes, 11);
        reader.ExpectTag("custody.source-lot.v1"u8);
        EntityId lotId = reader.ReadIdentity();
        EntityId custodian = reader.ReadIdentity();
        EntityId definition = reader.ReadIdentity();
        ReadOnlySpan<byte> hashBytes = reader.ReadField(32);
        if (hashBytes.Length != 32)
        {
            throw CanonicalCustodyReader.Failure(CustodyFormatFailure.InvalidValue,
                "Definition versions require exactly 32 hash bytes.");
        }
        var hash = new ContentHash(Convert.ToHexString(hashBytes).ToLowerInvariant());
        EntityId source = reader.ReadIdentity();
        ulong start = reader.ReadUnsigned64();
        ulong end = reader.ReadUnsigned64();
        ulong revision = reader.ReadUnsigned64();
        ulong protection = reader.ReadUnsigned64();
        if (protection > 15)
        {
            throw CanonicalCustodyReader.Failure(CustodyFormatFailure.InvalidValue,
                "The lot carries unsupported protection bits.");
        }
        ReadOnlySpan<byte> reservationBytes = reader.ReadField(EntityId.ByteLength);
        if (reservationBytes.Length is not (0 or EntityId.ByteLength))
        {
            throw CanonicalCustodyReader.Failure(CustodyFormatFailure.InvalidValue,
                "A reservation must be absent or one complete identity.");
        }
        EntityId? reservedBy = reservationBytes.IsEmpty ? null : EntityId.FromBytes(reservationBytes);
        reader.Complete();
        try
        {
            return new CountedSourceLot(lotId, custodian, new CountedItemDefinition(definition, hash),
                new SourceInterval(source, start, end), revision, (LotProtection)protection, reservedBy);
        }
        catch (ArgumentException)
        {
            throw CanonicalCustodyReader.Failure(CustodyFormatFailure.InvalidValue,
                "The lot violates its source or designation invariants.");
        }
    }

    internal static byte[] EncodeStorage(CustodianStorage storage) => CanonicalEncodingV1.Frame(
        "custody.storage.v1", storage.CustodianId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(storage.UnitCapacity), CanonicalEncodingV1.Unsigned64(storage.Revision));

    internal static byte[] EncodeLot(CountedSourceLot lot) => CanonicalEncodingV1.Frame("custody.source-lot.v1",
        lot.LotId.ToByteArray(), lot.CustodianId.ToByteArray(), lot.Definition.DefinitionId.ToByteArray(),
        lot.Definition.Version.ToByteArray(), lot.Source.SourceId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(lot.Source.Start), CanonicalEncodingV1.Unsigned64(lot.Source.End),
        CanonicalEncodingV1.Unsigned64(lot.Revision), CanonicalEncodingV1.Unsigned64((ulong)lot.Protection),
        lot.ReservedBy?.ToByteArray() ?? []);
}
