using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedCustody;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Tests.Authority.ConsentedMaterial;

public sealed class ConsentedMaterialCodecTests
{
    private static EntityId Id(ulong value) => new(0, value);
    private static readonly CountedItemDefinition Medicine = new(Id(10), ContentHash.Compute("medicine-v1"u8));

    private static ConsentedMaterialTerms Terms(ConsentedMaterialLotKind kind = ConsentedMaterialLotKind.Produced,
        ulong quantity = 3, CountedItemDefinition? definition = null) => new(Id(1), Id(2), Id(23), Id(24), kind, Id(5),
        definition ?? Medicine, Id(3), Id(4), quantity, 13);

    [Fact]
    public void Command_and_acceptance_round_trip_every_term_and_bind_only_the_exact_command()
    {
        var command = new ConsentedMaterialCommand(Id(20), Id(21), Id(22), Terms());
        ConsentedMaterialAcceptance acceptance = ConsentedMaterialAcceptance.Approving(command, Id(30), Id(31), Id(32));
        byte[] commandBytes = ConsentedMaterialCodecV1.Encode(command);
        byte[] acceptanceBytes = ConsentedMaterialCodecV1.Encode(acceptance);
        ConsentedMaterialCommand decoded = ConsentedMaterialCodecV1.DecodeCommand(commandBytes);
        ConsentedMaterialAcceptance accepted = ConsentedMaterialCodecV1.DecodeAcceptance(acceptanceBytes);
        Assert.Equal(commandBytes, ConsentedMaterialCodecV1.Encode(decoded));
        Assert.Equal(acceptanceBytes, ConsentedMaterialCodecV1.Encode(accepted));
        Assert.Equal(command.Terms, decoded.Terms);
        Assert.Equal((ConsentedMaterialLotKind.Produced, Medicine, 3UL, 13UL),
            (decoded.Terms.LotKind, decoded.Terms.Definition, decoded.Terms.Quantity, decoded.Terms.ExpectedLotRevision));
        // Consent names the goods and the approved lot revision; no global or storage revision is a term.
        Assert.DoesNotContain(typeof(ConsentedMaterialTerms).GetProperties(), property =>
            property.Name is "ExpectedProductionRevision" or "ExpectedCustodyRevision"
                or "ExpectedSourceRevision" or "ExpectedDestinationRevision");
        Assert.True(accepted.Binds(decoded));

        ConsentedMaterialCommand[] others =
        [
            new(Id(20), Id(21), Id(22), Terms(ConsentedMaterialLotKind.CountedSource)),
            new(Id(20), Id(21), Id(22), Terms(quantity: 4)),
            new(Id(20), Id(21), Id(22), Terms(definition: new(Id(10), ContentHash.Compute("medicine-v2"u8)))),
            new(Id(20), Id(21), Id(22), Terms(definition: new(Id(11), Medicine.Version))),
            new(Id(20), Id(29), Id(22), Terms()),
            new(Id(28), Id(21), Id(22), Terms())
        ];
        Assert.All(others, other =>
        {
            Assert.False(accepted.Binds(other));
            Assert.NotEqual(command.ComputeContentHash(), other.ComputeContentHash());
        });
    }

    [Fact]
    public void Raw_custody_frames_and_material_frames_never_decode_as_each_other()
    {
        var material = new ConsentedMaterialCommand(Id(20), Id(21), Id(22), Terms(ConsentedMaterialLotKind.CountedSource));
        var rawTerms = new ConsentedTransferTerms(Id(1), Id(2), Id(23), Id(24), Id(5), Id(3), Id(4), 3, 12, 13, 14, 15);
        var raw = new ConsentedTransferCommand(Id(20), Id(21), Id(22), rawTerms);
        ConsentedTransferAcceptance rawAcceptance = ConsentedTransferAcceptance.Approving(raw, Id(30), Id(31), Id(32));
        ConsentedMaterialAcceptance acceptance = ConsentedMaterialAcceptance.Approving(material, Id(30), Id(31), Id(32));
        Assert.Throws<AuthorityFormatException>(() => ConsentedMaterialCodecV1.DecodeCommand(ConsentedTransferCodecV1.Encode(raw)));
        Assert.Throws<AuthorityFormatException>(() => ConsentedMaterialCodecV1.DecodeAcceptance(ConsentedTransferCodecV1.Encode(rawAcceptance)));
        Assert.Throws<AuthorityFormatException>(() => ConsentedTransferCodecV1.DecodeCommand(ConsentedMaterialCodecV1.Encode(material)));
        Assert.Throws<AuthorityFormatException>(() => ConsentedTransferCodecV1.DecodeAcceptance(ConsentedMaterialCodecV1.Encode(acceptance)));
        Assert.NotEqual(raw.ComputeContentHash(), material.ComputeContentHash());
    }

    [Fact]
    public void Truncated_extended_and_unknown_frames_are_refused()
    {
        byte[] bytes = ConsentedMaterialCodecV1.Encode(new ConsentedMaterialCommand(Id(20), Id(21), Id(22), Terms()));
        Assert.Throws<AuthorityFormatException>(() => ConsentedMaterialCodecV1.DecodeCommand(bytes[..^1]));
        Assert.Throws<AuthorityFormatException>(() => ConsentedMaterialCodecV1.DecodeCommand([.. bytes, 0]));
        Assert.Throws<AuthorityFormatException>(() => ConsentedMaterialCodecV1.DecodeAcceptance(bytes));

        // The lot kind is the only eight-byte field holding the produced kind's value after the terms tag.
        byte[] unknownKind = (byte[])bytes.Clone();
        int kind = IndexOfKind(unknownKind);
        unknownKind[kind] = 9;
        Assert.Equal(AuthorityFormatFailure.UnknownType,
            Assert.Throws<AuthorityFormatException>(() => ConsentedMaterialCodecV1.DecodeCommand(unknownKind)).Failure);
        Assert.Throws<ArgumentOutOfRangeException>(() => Terms(quantity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Terms((ConsentedMaterialLotKind)9));
        Assert.Throws<ArgumentException>(() => new ConsentedMaterialTerms(Id(1), Id(2), Id(23), Id(23),
            ConsentedMaterialLotKind.Produced, Id(5), Medicine, Id(3), Id(4), 1, 0));
        Assert.Throws<ArgumentException>(() => new ConsentedMaterialTerms(Id(1), Id(2), Id(23), Id(24),
            ConsentedMaterialLotKind.Produced, Id(5), Medicine, Id(3), Id(3), 1, 0));
    }

    // Terms layout: tag, version, world, branch, sender, recipient, then the lot kind integer.
    private static int IndexOfKind(byte[] command)
    {
        byte[] recipient = Id(24).ToByteArray();
        int index = command.AsSpan().IndexOf(recipient);
        Assert.True(index >= 0);
        return index + recipient.Length + sizeof(uint) + sizeof(ulong) - 1;
    }
}
