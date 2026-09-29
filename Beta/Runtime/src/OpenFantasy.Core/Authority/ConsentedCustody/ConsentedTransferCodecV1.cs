using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Authority.ConsentedCustody;

/// <summary>
/// Canonical fixed-layout frames for the sender command, recipient acceptance and decision receipt.
/// Decoding validates shape and local invariants only; it authenticates nobody and commits nothing.
/// </summary>
public static class ConsentedTransferCodecV1
{
    private const int FrameHeaderBytes = 12;
    private const int LengthPrefixBytes = sizeof(uint);
    private const int IdentityFieldBytes = LengthPrefixBytes + EntityId.ByteLength;
    private const int IntegerFieldBytes = LengthPrefixBytes + sizeof(ulong);
    private const int HashFieldBytes = LengthPrefixBytes + CommandContentHash.ByteLength;

    private const string TermsTag = "authority.consented-transfer.terms";
    private const string CommandTag = "authority.consented-transfer.command";
    private const string AcceptanceTag = "authority.consented-transfer.acceptance";
    private const string ReceiptTag = "authority.consented-transfer.receipt";
    private const int TermsTagBytes = 34;
    private const int CommandTagBytes = 36;
    private const int AcceptanceTagBytes = 39;
    private const int ReceiptTagBytes = 36;

    // Field counts include the leading tag field.
    private const uint TermsFieldCount = 14;
    private const uint CommandFieldCount = 6;
    private const uint AcceptanceFieldCount = 8;
    private const uint ReceiptFieldCount = 14;
    private const ulong ContractVersion = 1;

    /// <summary>Version, seven identities, the amount and four revisions.</summary>
    public const int TermsByteLength = FrameHeaderBytes + LengthPrefixBytes + TermsTagBytes
        + (7 * IdentityFieldBytes) + (6 * IntegerFieldBytes);

    public const int CommandByteLength = FrameHeaderBytes + LengthPrefixBytes + CommandTagBytes
        + IntegerFieldBytes + (3 * IdentityFieldBytes) + LengthPrefixBytes + TermsByteLength;

    public const int AcceptanceByteLength = FrameHeaderBytes + LengthPrefixBytes + AcceptanceTagBytes
        + IntegerFieldBytes + (4 * IdentityFieldBytes) + HashFieldBytes + LengthPrefixBytes + TermsByteLength;

    /// <summary>An applied receipt; a rejected receipt omits the sixteen moved-lot bytes.</summary>
    public const int MaximumReceiptByteLength = FrameHeaderBytes + LengthPrefixBytes + ReceiptTagBytes
        + (4 * IntegerFieldBytes) + (6 * IdentityFieldBytes) + (2 * HashFieldBytes) + IdentityFieldBytes;

    private static ReadOnlySpan<byte> TermsTagUtf8 => "authority.consented-transfer.terms"u8;
    private static ReadOnlySpan<byte> CommandTagUtf8 => "authority.consented-transfer.command"u8;
    private static ReadOnlySpan<byte> AcceptanceTagUtf8 => "authority.consented-transfer.acceptance"u8;
    private static ReadOnlySpan<byte> ReceiptTagUtf8 => "authority.consented-transfer.receipt"u8;

    public static byte[] Encode(ConsentedTransferCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CanonicalEncodingV1.Frame(CommandTag, CanonicalEncodingV1.Unsigned64(command.Version),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(),
            command.OriginSessionId.ToByteArray(), EncodeTerms(command.Terms));
    }

    public static ConsentedTransferCommand DecodeCommand(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, CommandByteLength, CommandFieldCount);
        reader.ExpectTag(CommandTagUtf8);
        RequireVersion(ref reader);
        EntityId commandId = reader.ReadIdentity();
        EntityId principalId = reader.ReadIdentity();
        EntityId originSessionId = reader.ReadIdentity();
        ConsentedTransferTerms terms = DecodeTerms(reader.ReadField(TermsByteLength));
        reader.Complete();
        return new ConsentedTransferCommand(commandId, principalId, originSessionId, terms);
    }

    public static byte[] Encode(ConsentedTransferAcceptance acceptance)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        return CanonicalEncodingV1.Frame(AcceptanceTag, CanonicalEncodingV1.Unsigned64(acceptance.Version),
            acceptance.AcceptanceId.ToByteArray(), acceptance.PrincipalId.ToByteArray(),
            acceptance.OriginSessionId.ToByteArray(), acceptance.CommandId.ToByteArray(),
            acceptance.CommandHash.ToByteArray(), EncodeTerms(acceptance.Terms));
    }

    public static ConsentedTransferAcceptance DecodeAcceptance(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, AcceptanceByteLength, AcceptanceFieldCount);
        reader.ExpectTag(AcceptanceTagUtf8);
        RequireVersion(ref reader);
        EntityId acceptanceId = reader.ReadIdentity();
        EntityId principalId = reader.ReadIdentity();
        EntityId originSessionId = reader.ReadIdentity();
        EntityId commandId = reader.ReadIdentity();
        CommandContentHash commandHash = ReadHash(ref reader);
        ConsentedTransferTerms terms = DecodeTerms(reader.ReadField(TermsByteLength));
        reader.Complete();
        return new ConsentedTransferAcceptance(acceptanceId, principalId, originSessionId, commandId,
            commandHash, terms);
    }

    public static byte[] Encode(ConsentedTransferReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return CanonicalEncodingV1.Frame(ReceiptTag, CanonicalEncodingV1.Unsigned64(receipt.Version),
            receipt.WorldId.ToByteArray(), receipt.BranchId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(receipt.Commit.Sequence),
            CanonicalEncodingV1.Unsigned64((ulong)receipt.Status),
            CanonicalEncodingV1.Unsigned64((ulong)receipt.RejectionReason),
            receipt.CommandId.ToByteArray(), receipt.SenderPrincipalId.ToByteArray(),
            receipt.CommandHash.ToByteArray(), receipt.AcceptanceId.ToByteArray(),
            receipt.RecipientPrincipalId.ToByteArray(), receipt.AcceptanceHash.ToByteArray(),
            receipt.MovedLotId?.ToByteArray() ?? []);
    }

    public static ConsentedTransferReceipt DecodeReceipt(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, MaximumReceiptByteLength, ReceiptFieldCount);
        reader.ExpectTag(ReceiptTagUtf8);
        RequireVersion(ref reader);
        EntityId worldId = reader.ReadIdentity();
        EntityId branchId = reader.ReadIdentity();
        ulong sequence = reader.ReadUnsigned64();
        ulong status = reader.ReadUnsigned64();
        ulong reason = reader.ReadUnsigned64();
        EntityId commandId = reader.ReadIdentity();
        EntityId senderPrincipalId = reader.ReadIdentity();
        CommandContentHash commandHash = ReadHash(ref reader);
        EntityId acceptanceId = reader.ReadIdentity();
        EntityId recipientPrincipalId = reader.ReadIdentity();
        CommandContentHash acceptanceHash = ReadHash(ref reader);
        ReadOnlySpan<byte> movedLotBytes = reader.ReadField(EntityId.ByteLength);
        reader.Complete();
        if (movedLotBytes.Length is not (0 or EntityId.ByteLength))
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue,
                "A moved lot must be absent or one complete identity.");
        }

        if (status is not ((ulong)CommandReceiptStatus.Applied or (ulong)CommandReceiptStatus.Rejected)
            || reason is not ((ulong)CommandRejectionReason.None or (ulong)CommandRejectionReason.NotAuthorized
                or (ulong)CommandRejectionReason.ResourceUnavailable))
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.UnknownType,
                "The receipt status or rejection reason is unsupported.");
        }

        EntityId? movedLotId = movedLotBytes.IsEmpty ? null : EntityId.FromBytes(movedLotBytes);
        try
        {
            return new ConsentedTransferReceipt(worldId, branchId, new CommandCommitIdentity(worldId, sequence),
                (CommandReceiptStatus)status, (CommandRejectionReason)reason, commandId, senderPrincipalId,
                commandHash, acceptanceId, recipientPrincipalId, acceptanceHash, movedLotId);
        }
        catch (ArgumentException)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue,
                "Receipt fields violate result invariants.");
        }
    }

    private static byte[] EncodeTerms(ConsentedTransferTerms terms) => CanonicalEncodingV1.Frame(TermsTag,
        CanonicalEncodingV1.Unsigned64(ContractVersion), terms.WorldId.ToByteArray(), terms.BranchId.ToByteArray(),
        terms.SenderActorId.ToByteArray(), terms.RecipientActorId.ToByteArray(), terms.LotId.ToByteArray(),
        terms.SourceCustodianId.ToByteArray(), terms.DestinationCustodianId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(terms.Quantity),
        CanonicalEncodingV1.Unsigned64(terms.ExpectedBranchRevision),
        CanonicalEncodingV1.Unsigned64(terms.ExpectedLotRevision),
        CanonicalEncodingV1.Unsigned64(terms.ExpectedSourceRevision),
        CanonicalEncodingV1.Unsigned64(terms.ExpectedDestinationRevision));

    private static ConsentedTransferTerms DecodeTerms(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, TermsByteLength, TermsFieldCount);
        reader.ExpectTag(TermsTagUtf8);
        RequireVersion(ref reader);
        EntityId worldId = reader.ReadIdentity();
        EntityId branchId = reader.ReadIdentity();
        EntityId senderActorId = reader.ReadIdentity();
        EntityId recipientActorId = reader.ReadIdentity();
        EntityId lotId = reader.ReadIdentity();
        EntityId sourceCustodianId = reader.ReadIdentity();
        EntityId destinationCustodianId = reader.ReadIdentity();
        ulong quantity = reader.ReadUnsigned64();
        ulong branchRevision = reader.ReadUnsigned64();
        ulong lotRevision = reader.ReadUnsigned64();
        ulong sourceRevision = reader.ReadUnsigned64();
        ulong destinationRevision = reader.ReadUnsigned64();
        reader.Complete();
        try
        {
            return new ConsentedTransferTerms(worldId, branchId, senderActorId, recipientActorId, lotId,
                sourceCustodianId, destinationCustodianId, quantity, branchRevision, lotRevision,
                sourceRevision, destinationRevision);
        }
        catch (ArgumentException)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue,
                "Transfer terms violate their invariants.");
        }
    }

    private static void RequireVersion(ref AuthorityFrameReader reader)
    {
        if (reader.ReadUnsigned64() != ContractVersion)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.UnsupportedVersion,
                "The consented transfer contract version is unsupported.");
        }
    }

    private static CommandContentHash ReadHash(ref AuthorityFrameReader reader)
    {
        ReadOnlySpan<byte> bytes = reader.ReadField(CommandContentHash.ByteLength);
        if (bytes.Length != CommandContentHash.ByteLength)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue,
                "A content hash requires exactly thirty-two bytes.");
        }

        return CommandContentHash.FromBytes(bytes);
    }
}
