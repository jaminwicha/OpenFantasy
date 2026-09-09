namespace OpenFantasy.Core.Commands;

public static class CommandReceiptCodecV1
{
    public const int MaximumReceiptByteLength = 1024;

    public static byte[] Encode(CommandReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return CanonicalEncodingV1.Frame("command.receipt",
            CanonicalEncodingV1.Unsigned64(receipt.Version),
            receipt.CommandId.ToByteArray(), receipt.PrincipalId.ToByteArray(),
            receipt.WorldId.ToByteArray(), receipt.BranchId.ToByteArray(), receipt.ContentHash.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(receipt.Commit.Sequence),
            CanonicalEncodingV1.Unsigned64((ulong)receipt.Status),
            CanonicalEncodingV1.Unsigned64((ulong)receipt.RejectionReason));
    }

    public static CommandReceipt Decode(ReadOnlySpan<byte> bytes)
    {
        CanonicalCommandReader reader = new(bytes, MaximumReceiptByteLength, 11);
        reader.ExpectTag("command.receipt"u8);
        reader.ExpectVersion(CommandReceipt.CurrentVersion);
        EntityId commandId = reader.ReadIdentity();
        EntityId principalId = reader.ReadIdentity();
        EntityId worldId = reader.ReadIdentity();
        EntityId branchId = reader.ReadIdentity();
        ReadOnlySpan<byte> hashBytes = reader.ReadField(CommandContentHash.ByteLength);
        if (hashBytes.Length != CommandContentHash.ByteLength)
        {
            throw CanonicalCommandReader.Failure(CommandFormatFailure.InvalidValue,
                "A receipt content hash requires exactly thirty-two bytes.");
        }

        CommandContentHash contentHash = CommandContentHash.FromBytes(hashBytes);
        ulong sequence = reader.ReadUnsigned64();
        ulong status = reader.ReadUnsigned64();
        ulong reason = reader.ReadUnsigned64();
        reader.Complete();
        if (status is not (ulong)CommandReceiptStatus.Applied and not (ulong)CommandReceiptStatus.Rejected ||
            reason > (ulong)CommandRejectionReason.InvalidAction)
        {
            throw CanonicalCommandReader.Failure(CommandFormatFailure.UnknownType,
                "The receipt status or rejection reason is unsupported.");
        }

        try
        {
            return new CommandReceipt(commandId, principalId, worldId, branchId, contentHash,
                new CommandCommitIdentity(worldId, sequence), (CommandReceiptStatus)status,
                (CommandRejectionReason)reason);
        }
        catch (ArgumentException)
        {
            throw CanonicalCommandReader.Failure(CommandFormatFailure.InvalidValue,
                "Receipt fields violate result invariants.");
        }
    }
}
