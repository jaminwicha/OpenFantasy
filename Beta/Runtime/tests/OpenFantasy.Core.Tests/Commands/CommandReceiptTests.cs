using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Tests.Commands;

public sealed class CommandReceiptTests
{
    [Fact]
    public void AppliedAndRejectedReceiptsMatchIndependentBytesAndResultIdentities()
    {
        CommandEnvelope command = CommandFixture.Create();
        CommandCommitIdentity commit = new(command.WorldId, 42);
        var receipts = new[]
        {
            (Value: CommandReceipt.CreateApplied(command, commit), Prefix: "applied"),
            (Value: CommandReceipt.CreateRejected(command, commit, CommandRejectionReason.RevisionConflict), Prefix: "rejected")
        };
        foreach (var receipt in receipts)
        {
            byte[] expected = CommandFixture.Bytes(receipt.Prefix + "ReceiptHex");
            Assert.Equal(expected, CommandReceiptCodecV1.Encode(receipt.Value));
            Assert.Equal(CommandFixture.Read().GetProperty(receipt.Prefix + "ResultId").GetString(),
                receipt.Value.ResultId.ToString());
            CommandReceipt decoded = CommandReceiptCodecV1.Decode(expected);
            Assert.Equal(expected, CommandReceiptCodecV1.Encode(decoded));
            Assert.Equal(receipt.Value.ResultId, decoded.ResultId);
            Assert.Equal(receipt.Value.Status, decoded.Status);
            Assert.Equal(receipt.Value.RejectionReason, decoded.RejectionReason);
            Assert.Equal(receipt.Value.Commit, decoded.Commit);
            Assert.Equal(CommandRetryComparison.SameContent, decoded.CompareRetry(command));
        }
    }

    [Fact]
    public void DecodedRejectedReceiptKeepsOriginalResultForIdenticalRetry()
    {
        CommandReceipt original = CommandReceiptCodecV1.Decode(CommandFixture.Bytes("rejectedReceiptHex"));
        CommandEnvelope retry = CommandCodecV1.Decode(CommandFixture.Bytes("commandHex"));
        Assert.Equal(CommandRetryComparison.SameContent, original.CompareRetry(retry));
        Assert.Equal(CommandReceiptStatus.Rejected, original.Status);
        Assert.Equal(CommandRejectionReason.RevisionConflict, original.RejectionReason);
        Assert.Equal(42UL, original.Commit.Sequence);
        Assert.Equal(CommandFixture.Bytes("rejectedReceiptHex"), CommandReceiptCodecV1.Encode(original));
    }

    [Fact]
    public void SameKeyWithAnyChangedContentConflictsIncludingOriginSession()
    {
        CommandEnvelope command = CommandFixture.Create();
        CommandReceipt receipt = CommandReceipt.CreateApplied(command, new CommandCommitIdentity(command.WorldId, 42));
        CommandEnvelope[] changed =
        [
            CommandFixture.Create(8),
            CommandFixture.Create(originSession: new EntityId(0, 999)),
            CommandFixture.Create(actorId: new EntityId(0, 999)),
            CommandFixture.Create(branchId: new EntityId(0, 999), revisions: []),
            CommandFixture.Create(revisions: [])
        ];
        foreach (CommandEnvelope retry in changed)
        {
            Assert.Equal(CommandRetryComparison.ConflictingContent, receipt.CompareRetry(retry));
        }
    }

    [Fact]
    public void DifferentWorldPrincipalOrCommandIsNotTheSameRetryKey()
    {
        CommandEnvelope command = CommandFixture.Create();
        CommandReceipt receipt = CommandReceipt.CreateApplied(command, new CommandCommitIdentity(command.WorldId, 42));
        foreach (CommandEnvelope other in new[]
        {
            CommandFixture.Create(commandId: new EntityId(0, 999)),
            CommandFixture.Create(principalId: new EntityId(0, 999)),
            CommandFixture.Create(worldId: new EntityId(0, 999))
        })
        {
            Assert.Equal(CommandRetryComparison.DifferentCommand, receipt.CompareRetry(other));
        }
    }

    [Fact]
    public void ReceiptWithMismatchedBranchCannotMatchItsSuppliedCommandHash()
    {
        CommandReceipt receipt = CommandReceiptCodecV1.Decode(CommandFixture.Bytes("branchMismatchReceiptHex"));
        CommandEnvelope command = CommandFixture.Create();
        Assert.Equal(command.ComputeContentHash(), receipt.ContentHash);
        Assert.Equal(CommandRetryComparison.ConflictingContent, receipt.CompareRetry(command));
    }

    [Fact]
    public void ReceiptFactoryRejectsInconsistentCommitAndRejection()
    {
        CommandEnvelope command = CommandFixture.Create();
        CommandCommitIdentity commit = new(command.WorldId, 42);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandCommitIdentity(command.WorldId, 0));
        Assert.Throws<ArgumentException>(() => CommandReceipt.CreateApplied(command,
            new CommandCommitIdentity(new EntityId(0, 999), 42)));
        Assert.Throws<ArgumentException>(() => CommandReceipt.CreateRejected(command, commit, CommandRejectionReason.None));
        Assert.Throws<ArgumentException>(() => CommandReceipt.CreateRejected(command, commit, (CommandRejectionReason)999));
    }

    [Fact]
    public void IndependentlyConstructedMalformedReceiptsReject()
    {
        foreach (var fixture in CommandFixture.Read().GetProperty("invalidReceipts").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(fixture.GetProperty("hex").GetString()!);
            var failure = Assert.Throws<CommandFormatException>(() => CommandReceiptCodecV1.Decode(bytes));
            Assert.True(failure.Failure.ToString() == fixture.GetProperty("failure").GetString(),
                $"{fixture.GetProperty("name").GetString()}: {failure.Failure}");
        }
    }

    [Fact]
    public void EveryReceiptPrefixRejectsAndOversizedReceiptRejectsBeforeParsing()
    {
        byte[] bytes = CommandFixture.Bytes("appliedReceiptHex");
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] prefix = bytes[..length];
            Assert.Throws<CommandFormatException>(() => CommandReceiptCodecV1.Decode(prefix));
        }

        var failure = Assert.Throws<CommandFormatException>(() => CommandReceiptCodecV1.Decode(
            new byte[CommandReceiptCodecV1.MaximumReceiptByteLength + 1]));
        Assert.Equal(CommandFormatFailure.LimitExceeded, failure.Failure);
    }

    [Fact]
    public void ReceiptDecodeCopiesMutableWireData()
    {
        byte[] bytes = CommandFixture.Bytes("appliedReceiptHex");
        CommandReceipt decoded = CommandReceiptCodecV1.Decode(bytes);
        EntityId result = decoded.ResultId;
        Array.Fill(bytes, (byte)0);
        byte[] hashBytes = decoded.ContentHash.ToByteArray();
        Array.Fill(hashBytes, (byte)0xff);
        Assert.Equal(result, decoded.ResultId);
        Assert.Equal(CommandFixture.Bytes("appliedReceiptHex"), CommandReceiptCodecV1.Encode(decoded));
    }
}
