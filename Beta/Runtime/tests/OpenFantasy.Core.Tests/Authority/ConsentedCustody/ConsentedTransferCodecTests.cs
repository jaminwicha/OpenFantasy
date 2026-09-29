using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedCustody;
using OpenFantasy.Core.Commands;
using static OpenFantasy.Core.Tests.Authority.ConsentedCustody.ConsentedTransferFixture;

namespace OpenFantasy.Core.Tests.Authority.ConsentedCustody;

/// <summary>Expected bytes come from this file's own frame writer, never from the runtime encoder.</summary>
public sealed class ConsentedTransferCodecTests
{
    private const string TermsTag = "authority.consented-transfer.terms";
    private const string CommandTag = "authority.consented-transfer.command";
    private const string AcceptanceTag = "authority.consented-transfer.acceptance";
    private const string ReceiptTag = "authority.consented-transfer.receipt";
    private static readonly byte[] SplitChild = Convert.FromHexString("b7250ae9037ee65abcdb9083b0d96574");

    [Fact]
    public void Command_and_acceptance_match_independent_bytes_and_hashes()
    {
        ConsentedTransferCommand command = Command(Terms(Create()));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        byte[] commandBytes = ExpectedCommand();
        byte[] acceptanceBytes = ExpectedAcceptance();

        Assert.Equal(262, ExpectedTerms().Length);
        Assert.Equal(390, commandBytes.Length);
        Assert.Equal(449, acceptanceBytes.Length);
        Assert.Equal(262, ConsentedTransferCodecV1.TermsByteLength);
        Assert.Equal(390, ConsentedTransferCodecV1.CommandByteLength);
        Assert.Equal(449, ConsentedTransferCodecV1.AcceptanceByteLength);
        Assert.Equal(commandBytes, ConsentedTransferCodecV1.Encode(command));
        Assert.Equal(acceptanceBytes, ConsentedTransferCodecV1.Encode(acceptance));
        Assert.Equal(Hex(SHA256.HashData(commandBytes)), command.ComputeContentHash().ToString());
        Assert.Equal(Hex(SHA256.HashData(acceptanceBytes)), acceptance.ComputeContentHash().ToString());
        Assert.Equal(Hex(SHA256.HashData(commandBytes)), acceptance.CommandHash.ToString());
    }

    [Fact]
    public void Command_frame_begins_with_the_literal_header_tag_version_and_identity()
    {
        byte[] literal =
        [
            .. Convert.FromHexString("4f464300" + "00000001" + "00000006" + "00000024"),
            .. Encoding.ASCII.GetBytes("authority.consented-transfer.command"),
            .. Convert.FromHexString("00000008" + "0000000000000001"),
            .. Convert.FromHexString("00000010" + "00000000000000000000000000000014")
        ];
        byte[] encoded = ConsentedTransferCodecV1.Encode(Command(Terms(Create())));
        Assert.Equal(84, literal.Length);
        Assert.Equal(literal, encoded[..literal.Length]);
    }

    [Fact]
    public void Applied_and_rejected_receipts_match_independent_bytes()
    {
        CountedAuthorityState state = Create();
        ConsentedTransferCommand command = Command(Terms(state));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        byte[] applied = ExpectedReceipt(1, 0, SplitChild);
        byte[] rejected = ExpectedReceipt(2, 2, []);

        Assert.Equal(312, applied.Length);
        Assert.Equal(296, rejected.Length);
        Assert.Equal(312, ConsentedTransferCodecV1.MaximumReceiptByteLength);
        Assert.Equal(applied, ConsentedTransferCodecV1.Encode(Decide(state, command, acceptance).Receipt));
        Assert.Equal(rejected, ConsentedTransferCodecV1.Encode(
            Decide(state, command, acceptance, recipientDelivery: SenderDelivery).Receipt));
    }

    [Fact]
    public void Decoded_values_restore_every_field_and_reencode_identically()
    {
        ConsentedTransferCommand command = ConsentedTransferCodecV1.DecodeCommand(ExpectedCommand());
        Assert.Equal(CommandId, command.CommandId);
        Assert.Equal(SenderDelivery.PrincipalId, command.PrincipalId);
        Assert.Equal(SenderDelivery.SessionId, command.OriginSessionId);
        Assert.Equal(new ConsentedTransferTerms(World, Branch, Sender, Recipient, Lot, SenderStorage,
            RecipientStorage, 6, 7, 3, 4, 5), command.Terms);
        Assert.Equal(ExpectedCommand(), ConsentedTransferCodecV1.Encode(command));

        ConsentedTransferAcceptance acceptance = ConsentedTransferCodecV1.DecodeAcceptance(ExpectedAcceptance());
        Assert.Equal(AcceptanceId, acceptance.AcceptanceId);
        Assert.Equal(RecipientDelivery.PrincipalId, acceptance.PrincipalId);
        Assert.Equal(RecipientDelivery.SessionId, acceptance.OriginSessionId);
        Assert.True(acceptance.Binds(command));
        Assert.Equal(ExpectedAcceptance(), ConsentedTransferCodecV1.Encode(acceptance));

        ConsentedTransferReceipt applied = ConsentedTransferCodecV1.DecodeReceipt(ExpectedReceipt(1, 0, SplitChild));
        Assert.Equal(CommandReceiptStatus.Applied, applied.Status);
        Assert.Equal(11UL, applied.Commit.Sequence);
        Assert.Equal(World, applied.Commit.WorldId);
        Assert.Equal(EntityId.FromBytes(SplitChild), applied.MovedLotId);
        Assert.True(applied.Records(command, acceptance));
        Assert.Equal(ExpectedReceipt(1, 0, SplitChild), ConsentedTransferCodecV1.Encode(applied));

        ConsentedTransferReceipt rejected = ConsentedTransferCodecV1.DecodeReceipt(ExpectedReceipt(2, 3, []));
        Assert.Equal(CommandRejectionReason.ResourceUnavailable, rejected.RejectionReason);
        Assert.Null(rejected.MovedLotId);
        Assert.Equal(ExpectedReceipt(2, 3, []), ConsentedTransferCodecV1.Encode(rejected));
    }

    [Fact]
    public void Maximum_field_values_round_trip_within_the_fixed_bounds()
    {
        var wide = new EntityId(ulong.MaxValue, ulong.MaxValue - 1);
        var terms = new ConsentedTransferTerms(wide, new(1, 2), new(3, 4), new(5, 6), new(7, 8), new(9, 10),
            new(11, 12), ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var command = new ConsentedTransferCommand(new(13, 14), new(15, 16), new(17, 18), terms);
        byte[] encoded = ConsentedTransferCodecV1.Encode(command);
        ConsentedTransferCommand decoded = ConsentedTransferCodecV1.DecodeCommand(encoded);
        Assert.Equal(ConsentedTransferCodecV1.CommandByteLength, encoded.Length);
        Assert.Equal(terms, decoded.Terms);
        Assert.Equal(command.ComputeContentHash(), decoded.ComputeContentHash());
    }

    [Fact]
    public void Every_incomplete_prefix_and_oversized_input_fails_closed()
    {
        byte[][] frames = [ExpectedCommand(), ExpectedAcceptance(), ExpectedReceipt(1, 0, SplitChild),
            ExpectedReceipt(2, 2, [])];
        Action<byte[]>[] decoders =
        [
            bytes => ConsentedTransferCodecV1.DecodeCommand(bytes),
            bytes => ConsentedTransferCodecV1.DecodeAcceptance(bytes),
            bytes => ConsentedTransferCodecV1.DecodeReceipt(bytes),
            bytes => ConsentedTransferCodecV1.DecodeReceipt(bytes)
        ];
        for (int index = 0; index < frames.Length; index++)
        {
            for (int length = 0; length < frames[index].Length; length++)
            {
                byte[] prefix = frames[index][..length];
                Assert.Throws<AuthorityFormatException>(() => decoders[index](prefix));
            }

            byte[] oversized = [.. frames[0], .. new byte[ConsentedTransferCodecV1.AcceptanceByteLength]];
            Assert.Equal(AuthorityFormatFailure.LimitExceeded,
                Assert.Throws<AuthorityFormatException>(() => decoders[index](oversized)).Failure);
        }
    }

    [Fact]
    public void Malformed_frames_fail_with_specific_diagnostics()
    {
        byte[] wrongMagic = ExpectedCommand();
        wrongMagic[0] = (byte)'X';
        byte[] wrongFraming = ExpectedCommand();
        wrongFraming[7] = 2;
        (string Name, byte[] Bytes, AuthorityFormatFailure Failure)[] commands =
        [
            ("magic", wrongMagic, AuthorityFormatFailure.InvalidFrame),
            ("framing version", wrongFraming, AuthorityFormatFailure.UnsupportedVersion),
            ("tag", CommandFrame(tag: ReceiptTag), AuthorityFormatFailure.UnknownType),
            ("contract version", CommandFrame(version: 2), AuthorityFormatFailure.UnsupportedVersion),
            ("terms version", CommandFrame(terms: TermsFrame(version: 2)), AuthorityFormatFailure.UnsupportedVersion),
            ("terms tag", CommandFrame(terms: TermsFrame(tag: "authority.consented-transfer.termz")),
                AuthorityFormatFailure.UnknownType),
            ("missing terms", Frame(CommandTag, UInt64(1), Identity(20), Identity(21), Identity(22)),
                AuthorityFormatFailure.Truncated),
            ("extra field", Frame(CommandTag, UInt64(1), Identity(20), Identity(21), Identity(22), ExpectedTerms(), []),
                AuthorityFormatFailure.LimitExceeded),
            ("trailing byte", [.. ExpectedCommand(), 0], AuthorityFormatFailure.LimitExceeded),
            ("short identity", Frame(CommandTag, UInt64(1), Identity(20)[1..], Identity(21), Identity(22),
                ExpectedTerms()), AuthorityFormatFailure.InvalidValue),
            ("short integer", CommandFrame(terms: TermsFrame(quantityField: new byte[7])),
                AuthorityFormatFailure.InvalidValue),
            ("oversized terms", CommandFrame(terms: [.. ExpectedTerms(), 0]), AuthorityFormatFailure.LimitExceeded),
            ("zero amount", CommandFrame(terms: TermsFrame(quantityField: UInt64(0))),
                AuthorityFormatFailure.InvalidValue),
            ("same participants", CommandFrame(terms: TermsFrame(recipient: 23)), AuthorityFormatFailure.InvalidValue),
            ("same storages", CommandFrame(terms: TermsFrame(destination: 3)), AuthorityFormatFailure.InvalidValue),
            ("lot is a storage", CommandFrame(terms: TermsFrame(lot: 3)), AuthorityFormatFailure.InvalidValue)
        ];
        foreach ((string name, byte[] bytes, AuthorityFormatFailure failure) in commands)
        {
            AuthorityFormatException error = Assert.Throws<AuthorityFormatException>(
                () => ConsentedTransferCodecV1.DecodeCommand(bytes));
            Assert.True(failure == error.Failure, $"{name}: {error.Failure}");
        }

        byte[] shortHash = Frame(AcceptanceTag, UInt64(1), Identity(30), Identity(31), Identity(32), Identity(20),
            SHA256.HashData(ExpectedCommand())[1..], ExpectedTerms());
        Assert.Equal(AuthorityFormatFailure.InvalidValue, Assert.Throws<AuthorityFormatException>(
            () => ConsentedTransferCodecV1.DecodeAcceptance(shortHash)).Failure);
        Assert.Equal(AuthorityFormatFailure.UnknownType, Assert.Throws<AuthorityFormatException>(
            () => ConsentedTransferCodecV1.DecodeAcceptance(ExpectedCommand())).Failure);
    }

    [Fact]
    public void Receipts_with_inconsistent_outcomes_fail_closed()
    {
        (string Name, byte[] Bytes, AuthorityFormatFailure Failure)[] receipts =
        [
            ("unknown status", ExpectedReceipt(3, 0, SplitChild), AuthorityFormatFailure.UnknownType),
            ("unknown reason", ExpectedReceipt(2, 5, []), AuthorityFormatFailure.UnknownType),
            ("applied with reason", ExpectedReceipt(1, 2, SplitChild), AuthorityFormatFailure.InvalidValue),
            ("rejected without reason", ExpectedReceipt(2, 0, []), AuthorityFormatFailure.InvalidValue),
            ("applied without lot", ExpectedReceipt(1, 0, []), AuthorityFormatFailure.InvalidValue),
            ("rejected with lot", ExpectedReceipt(2, 2, SplitChild), AuthorityFormatFailure.InvalidValue),
            ("partial lot identity", ExpectedReceipt(1, 0, SplitChild[..8]), AuthorityFormatFailure.InvalidValue),
            ("zero commit sequence", ExpectedReceipt(1, 0, SplitChild, sequence: 0), AuthorityFormatFailure.InvalidValue),
            ("trailing byte", [.. ExpectedReceipt(2, 2, []), 0], AuthorityFormatFailure.TrailingData),
            ("command frame", ExpectedTerms(), AuthorityFormatFailure.UnknownType)
        ];
        foreach ((string name, byte[] bytes, AuthorityFormatFailure failure) in receipts)
        {
            AuthorityFormatException error = Assert.Throws<AuthorityFormatException>(
                () => ConsentedTransferCodecV1.DecodeReceipt(bytes));
            Assert.True(failure == error.Failure, $"{name}: {error.Failure}");
        }
    }

    [Fact]
    public void Mutating_input_or_output_bytes_cannot_change_a_decoded_command()
    {
        byte[] bytes = ExpectedCommand();
        ConsentedTransferCommand decoded = ConsentedTransferCodecV1.DecodeCommand(bytes);
        CommandContentHash hash = decoded.ComputeContentHash();
        Array.Fill(bytes, (byte)0xff);
        Array.Fill(ConsentedTransferCodecV1.Encode(decoded), (byte)0xaa);
        Assert.Equal(hash, decoded.ComputeContentHash());
        Assert.Equal(ExpectedCommand(), ConsentedTransferCodecV1.Encode(decoded));
    }

    [Theory]
    [InlineData(1)] // World
    [InlineData(2)] // Branch
    [InlineData(7)] // Sender principal
    [InlineData(10)] // Recipient principal
    public void Receipt_matching_rejects_substituted_scope_or_participant_even_with_original_hashes(int field)
    {
        ConsentedTransferCommand command = Command(Terms(Create()));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        byte[][] fields =
        [
            UInt64(1), Identity(1), Identity(2), UInt64(11), UInt64(1), UInt64(0),
            Identity(20), Identity(21), SHA256.HashData(ExpectedCommand()), Identity(30),
            Identity(31), SHA256.HashData(ExpectedAcceptance()), SplitChild
        ];
        fields[field] = Identity(99);
        ConsentedTransferReceipt substituted = ConsentedTransferCodecV1.DecodeReceipt(Frame(ReceiptTag, fields));

        Assert.Equal(command.ComputeContentHash(), substituted.CommandHash);
        Assert.Equal(acceptance.ComputeContentHash(), substituted.AcceptanceHash);
        Assert.False(substituted.Records(command, acceptance));
    }

    [Fact]
    public void Receipt_matching_rejects_an_unrelated_moved_lot()
    {
        ConsentedTransferCommand command = Command(Terms(Create()));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        var receipt = ConsentedTransferCodecV1.DecodeReceipt(ExpectedReceipt(1, 0, Identity(99)));
        Assert.False(receipt.Records(command, acceptance));
    }

    [Fact]
    public void Receipt_matching_rejects_applied_outcomes_for_nonbinding_approvals()
    {
        ConsentedTransferCommand command = Command(Terms(Create()));
        ConsentedTransferAcceptance acceptance = Acceptance(Command(Terms(Create(), quantity: 5)));
        Assert.False(acceptance.Binds(command));
        byte[] FrameFor(ulong status, ulong reason, byte[] moved) => Frame(ReceiptTag, UInt64(1),
            Identity(1), Identity(2), UInt64(11), UInt64(status), UInt64(reason), Identity(20), Identity(21),
            SHA256.HashData(ExpectedCommand()), Identity(30), Identity(31),
            acceptance.ComputeContentHash().ToByteArray(), moved);

        var applied = ConsentedTransferCodecV1.DecodeReceipt(FrameFor(1, 0, SplitChild));
        var rejected = ConsentedTransferCodecV1.DecodeReceipt(FrameFor(2, 2, []));
        Assert.False(applied.Records(command, acceptance));
        Assert.True(rejected.Records(command, acceptance));
    }

    [Theory]
    [InlineData(1UL)] // RevisionConflict belongs to the shared enum, but not this contract.
    [InlineData(4UL)] // InvalidAction is likewise never emitted by this authority.
    public void Unemitted_rejection_reasons_fail_closed(ulong reason)
    {
        Assert.Equal(AuthorityFormatFailure.UnknownType, Assert.Throws<AuthorityFormatException>(
            () => ConsentedTransferCodecV1.DecodeReceipt(ExpectedReceipt(2, reason, []))).Failure);
    }

    [Fact]
    public void Receipt_matching_cannot_establish_the_actual_outcome_commit_or_whole_versus_split()
    {
        ConsentedTransferCommand command = Command(Terms(Create()));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        byte[][] possibleClaims =
        [
            ExpectedReceipt(1, 0, SplitChild), ExpectedReceipt(1, 0, Identity(5)),
            ExpectedReceipt(1, 0, SplitChild, sequence: 12), ExpectedReceipt(2, 3, [])
        ];
        foreach (byte[] bytes in possibleClaims)
        {
            Assert.True(ConsentedTransferCodecV1.DecodeReceipt(bytes).Records(command, acceptance));
        }
        // Only an actual authoritative decision or its authenticated journal can establish the result.
        Assert.Equal(EntityId.FromBytes(SplitChild), Decide(Create(), command, acceptance).MovedLotId);
    }

    private static byte[] ExpectedTerms() => TermsFrame();
    private static byte[] ExpectedCommand() => CommandFrame();

    private static byte[] ExpectedAcceptance() => Frame(AcceptanceTag, UInt64(1), Identity(30), Identity(31),
        Identity(32), Identity(20), SHA256.HashData(ExpectedCommand()), ExpectedTerms());

    private static byte[] ExpectedReceipt(ulong status, ulong reason, byte[] movedLot, ulong sequence = 11) =>
        Frame(ReceiptTag, UInt64(1), Identity(1), Identity(2), UInt64(sequence), UInt64(status), UInt64(reason),
            Identity(20), Identity(21), SHA256.HashData(ExpectedCommand()), Identity(30), Identity(31),
            SHA256.HashData(ExpectedAcceptance()), movedLot);

    private static byte[] CommandFrame(string tag = CommandTag, ulong version = 1, byte[]? terms = null) =>
        Frame(tag, UInt64(version), Identity(20), Identity(21), Identity(22), terms ?? ExpectedTerms());

    private static byte[] TermsFrame(string tag = TermsTag, ulong version = 1, ulong recipient = 24, ulong lot = 5,
        ulong destination = 4, byte[]? quantityField = null) => Frame(tag, UInt64(version), Identity(1), Identity(2),
        Identity(23), Identity(recipient), Identity(lot), Identity(3), Identity(destination),
        quantityField ?? UInt64(6), UInt64(7), UInt64(3), UInt64(4), UInt64(5));

    private static byte[] Frame(string tag, params byte[][] fields)
    {
        using MemoryStream stream = new();
        stream.Write("OFC\0"u8);
        stream.Write(UInt32(1));
        stream.Write(UInt32((uint)fields.Length + 1));
        WriteField(stream, Encoding.ASCII.GetBytes(tag));
        foreach (byte[] field in fields)
        {
            WriteField(stream, field);
        }

        return stream.ToArray();
    }

    private static void WriteField(MemoryStream stream, byte[] field)
    {
        stream.Write(UInt32((uint)field.Length));
        stream.Write(field);
    }

    private static byte[] UInt32(uint value)
    {
        byte[] bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] UInt64(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Identity(ulong low) => [.. new byte[sizeof(ulong)], .. UInt64(low)];
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
