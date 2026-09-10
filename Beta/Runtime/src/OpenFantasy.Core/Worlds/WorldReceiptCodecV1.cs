using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

public static class WorldReceiptCodecV1
{
    public const int MaximumReceiptByteLength = 512;

    public static byte[] Encode(WorldActionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return CanonicalEncodingV1.Frame("world.action-receipt", Number(1), receipt.CommandId.ToByteArray(),
            receipt.PrincipalId.ToByteArray(), receipt.WorldId.ToByteArray(), receipt.BranchId.ToByteArray(),
            receipt.CommandHash.ToByteArray(), Number(receipt.CommitSequence), Number((ulong)receipt.Status),
            Number((ulong)receipt.Rejection));
    }

    public static WorldActionReceipt Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumReceiptByteLength, 10);
        reader.Tag("world.action-receipt"u8);
        reader.Version();
        EntityId command = reader.Identity();
        EntityId principal = reader.Identity();
        EntityId world = reader.Identity();
        EntityId branch = reader.Identity();
        var hash = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
        ulong sequence = reader.Number();
        ulong status = reader.Number();
        ulong rejection = reader.Number();
        reader.Complete();
        if (status is not 1 and not 2 || rejection > 5)
        {
            throw new FormatException("Unsupported world receipt status.");
        }
        try
        {
            return new WorldActionReceipt(command, principal, world, branch, hash, sequence,
                (CommandReceiptStatus)status, (WorldActionRejection)rejection);
        }
        catch (ArgumentException error)
        {
            throw new FormatException("The world receipt violates its invariants.", error);
        }
    }

    public static byte[] EncodeBatch(IEnumerable<WorldActionReceipt> receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        List<WorldActionReceipt> values = [];
        HashSet<EntityId> ids = [];
        foreach (WorldActionReceipt receipt in receipts)
        {
            ArgumentNullException.ThrowIfNull(receipt);
            if (values.Count == WorldStepBatch.MaximumActorsPerBatch || !ids.Add(receipt.CommandId))
            {
                throw new ArgumentException("A world receipt set exceeds its bounds or repeats a command.");
            }
            values.Add(receipt);
        }
        return CanonicalEncodingV1.Frame("world.action-outcomes", values.OrderBy(value => value.CommandId)
            .Select(value => (ReadOnlyMemory<byte>)Encode(value)).ToArray());
    }

    public static IReadOnlyList<WorldActionReceipt> DecodeBatch(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 256 * 1024, WorldStepBatch.MaximumActorsPerBatch + 1);
        reader.Tag("world.action-outcomes"u8);
        List<WorldActionReceipt> values = [];
        while (reader.Remaining != 0)
        {
            WorldActionReceipt value = Decode(reader.Field(MaximumReceiptByteLength));
            if (values.Count != 0 && value.CommandId.CompareTo(values[^1].CommandId) <= 0)
            {
                throw new FormatException("World outcomes must have strictly sorted command identities.");
            }
            values.Add(value);
        }
        reader.Complete();
        return values.AsReadOnly();
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
