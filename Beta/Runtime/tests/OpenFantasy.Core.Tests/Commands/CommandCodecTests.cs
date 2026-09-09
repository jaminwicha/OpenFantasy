using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Tests.Commands;

public sealed class CommandCodecTests
{
    [Fact]
    public void CommandEncodingAndHashMatchIndependentPythonBytes()
    {
        CommandEnvelope command = CommandFixture.Create();
        byte[] golden = CommandFixture.Bytes("commandHex");
        Assert.Equal(golden, CommandCodecV1.Encode(command));
        Assert.Equal(CommandFixture.Read().GetProperty("contentHash").GetString(), command.ComputeContentHash().ToString());
        Assert.Equal(golden, CommandCodecV1.Encode(CommandCodecV1.Decode(golden)));
    }

    [Fact]
    public void IndependentlyConstructedMalformedCommandsAreRejectedWithSpecificDiagnostics()
    {
        foreach (var fixture in CommandFixture.Read().GetProperty("invalidCommands").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(fixture.GetProperty("hex").GetString()!);
            CommandFormatException failure = Assert.Throws<CommandFormatException>(() => CommandCodecV1.Decode(bytes));
            Assert.True(failure.Failure.ToString() == fixture.GetProperty("failure").GetString(),
                $"{fixture.GetProperty("name").GetString()}: {failure.Failure}");
        }
    }

    [Fact]
    public void EveryTruncatedPrefixFailsWithoutReturningPartialCommand()
    {
        byte[] bytes = CommandFixture.Bytes("commandHex");
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] prefix = bytes[..length];
            Assert.Throws<CommandFormatException>(() => CommandCodecV1.Decode(prefix));
        }
    }

    [Fact]
    public void OversizedInputRejectsBeforeParsing()
    {
        byte[] bytes = new byte[CommandCodecV1.MaximumEnvelopeByteLength + 1];
        var failure = Assert.Throws<CommandFormatException>(() => CommandCodecV1.Decode(bytes));
        Assert.Equal(CommandFormatFailure.LimitExceeded, failure.Failure);
    }

    [Fact]
    public void MutatingInputAndEncodedOutputCannotChangeDecodedCommand()
    {
        byte[] bytes = CommandFixture.Bytes("commandHex");
        CommandEnvelope decoded = CommandCodecV1.Decode(bytes);
        CommandContentHash hash = decoded.ComputeContentHash();
        Array.Fill(bytes, (byte)0xff);
        byte[] encoded = CommandCodecV1.Encode(decoded);
        Array.Fill(encoded, (byte)0xaa);
        byte[] hashBytes = hash.ToByteArray();
        Array.Fill(hashBytes, (byte)0);
        Assert.Equal(hash, decoded.ComputeContentHash());
        Assert.Equal(CommandFixture.Bytes("commandHex"), CommandCodecV1.Encode(decoded));
    }

    [Fact]
    public void RevisionPermutationHasOneCanonicalRepresentationAndSourceMutationCannotChangeIt()
    {
        List<ExpectedRevision> revisions = [
            new(RevisionTargetKind.Entity, new EntityId(0, 90), 5),
            new(RevisionTargetKind.Entity, new EntityId(0, 2), 1),
            new(RevisionTargetKind.Branch, CommandFixture.Id("branch"), 3)
        ];
        CommandEnvelope first = CommandFixture.Create(revisions: revisions);
        revisions.Reverse();
        CommandEnvelope second = CommandFixture.Create(revisions: revisions);
        revisions.Clear();
        Assert.Equal(3, first.ExpectedRevisions.Count);
        Assert.Equal(CommandCodecV1.Encode(first), CommandCodecV1.Encode(second));
        Assert.Equal(first.ComputeContentHash(), second.ComputeContentHash());
        Assert.False((object)first.ExpectedRevisions is IList<ExpectedRevision>);
    }

    [Fact]
    public void RepeatedRevisionOwnersRejectEvenWhenValuesAgree()
    {
        ExpectedRevision revision = new(RevisionTargetKind.Entity, new EntityId(0, 1), 5);
        Assert.Throws<ArgumentException>(() => new ExpectedRevisionSet([revision, revision]));
        Assert.Throws<ArgumentException>(() => new ExpectedRevisionSet([revision,
            new ExpectedRevision(revision.TargetKind, revision.TargetId, 6)]));
    }

    [Fact]
    public void ExactlySixtyFourRevisionsAndMaximumQuantityRoundTrip()
    {
        ExpectedRevision[] revisions = Enumerable.Range(0, ExpectedRevisionSet.MaximumCount)
            .Select(index => new ExpectedRevision(RevisionTargetKind.Entity, new EntityId(0, (ulong)index), ulong.MaxValue))
            .ToArray();
        CommandEnvelope command = CommandFixture.Create(ulong.MaxValue, revisions: revisions);
        byte[] bytes = CommandCodecV1.Encode(command);
        Assert.True(bytes.Length <= CommandCodecV1.MaximumEnvelopeByteLength);
        CommandEnvelope decoded = CommandCodecV1.Decode(bytes);
        Assert.Equal(64, decoded.ExpectedRevisions.Count);
        Assert.Equal(ulong.MaxValue, decoded.Payload.Quantity);
        Assert.Equal(ulong.MaxValue, decoded.ExpectedRevisions[63].Revision);
        Assert.Throws<ArgumentException>(() => new ExpectedRevisionSet(revisions.Append(
            new ExpectedRevision(RevisionTargetKind.Entity, new EntityId(0, 64), 0))));
    }

    [Fact]
    public void UnboundedRevisionEnumerationStopsAfterTheFirstExcessElement()
    {
        int yielded = 0;
        IEnumerable<ExpectedRevision> Endless()
        {
            while (true)
            {
                yielded++;
                yield return new ExpectedRevision(RevisionTargetKind.Entity, new EntityId(0, (ulong)yielded), 0);
            }
        }

        Assert.Throws<ArgumentException>(() => new ExpectedRevisionSet(Endless()));
        Assert.Equal(ExpectedRevisionSet.MaximumCount + 1, yielded);
    }

    [Fact]
    public void EmptyRevisionSetIsRepresentableWithoutInventingGameplayApproval()
    {
        CommandEnvelope command = CommandFixture.Create(revisions: []);
        Assert.Empty(CommandCodecV1.Decode(CommandCodecV1.Encode(command)).ExpectedRevisions);
    }

    [Fact]
    public void InvalidPayloadsAndRevisionOwnershipRejectAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CommandFixture.Create(0));
        Assert.Throws<ArgumentException>(() => new TransferLotIntent(default, default, default, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpectedRevision((RevisionTargetKind)0, default, 0));
        Assert.Throws<ArgumentException>(() => CommandFixture.Create(revisions:
            [new ExpectedRevision(RevisionTargetKind.Branch, new EntityId(0, 99), 0)]));
        Assert.Throws<ArgumentNullException>(() => new ExpectedRevisionSet([null!]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("GG112233445566778899aabbccddeeff00112233445566778899aabbccddeeff00")]
    [InlineData("00112233445566778899AABBCCDDEEFF00112233445566778899aabbccddeeff00")]
    public void NonCanonicalHashTextRejects(string value)
    {
        Assert.Throws<FormatException>(() => CommandContentHash.Parse(value));
    }
}
