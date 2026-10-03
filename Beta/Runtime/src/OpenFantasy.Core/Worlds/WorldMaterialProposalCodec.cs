using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Bounded canonical frames of the saved proposal owner. Decoding validates shape and local
/// invariants only; it authenticates neither participant and publishes nothing.
/// </summary>
public static class WorldMaterialProposalCodec
{
    private const int MaximumSettlementByteLength = 2_048;
    private const int MaximumListByteLength = WorldMaterialProposalBook.MaximumByteLength;

    public static byte[] Encode(WorldMaterialProposalBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        byte[] bytes = CanonicalEncodingV1.Frame("world.material-proposals.v1",
            CanonicalEncodingV1.Frame("world.material-proposals.open",
                book.Open.Select(value => (ReadOnlyMemory<byte>)Bounded(value.Encode(),
                    WorldMaterialProposal.MaximumByteLength)).ToArray()),
            CanonicalEncodingV1.Frame("world.material-proposals.outcomes",
                book.Outcomes.Select(value => (ReadOnlyMemory<byte>)Bounded(value.Encode(),
                    WorldMaterialProposalOutcome.MaximumByteLength)).ToArray()));
        return Bounded(bytes, WorldMaterialProposalBook.MaximumByteLength);
    }

    public static WorldMaterialProposalBook Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldMaterialProposalBook.MaximumByteLength, 3);
            reader.Tag("world.material-proposals.v1"u8);
            ReadOnlySpan<byte> openBytes = reader.Field(MaximumListByteLength);
            ReadOnlySpan<byte> outcomeBytes = reader.Field(MaximumListByteLength);
            reader.Complete();

            var openReader = new WorldFrameReader(openBytes, MaximumListByteLength, WorldMaterialProposalBook.MaximumOpen + 1);
            openReader.Tag("world.material-proposals.open"u8);
            List<WorldMaterialProposal> open = [];
            while (openReader.Remaining != 0)
            {
                open.Add(DecodeProposal(openReader.Field(WorldMaterialProposal.MaximumByteLength)));
            }
            openReader.Complete();

            var outcomeReader = new WorldFrameReader(outcomeBytes, MaximumListByteLength,
                WorldMaterialProposalBook.MaximumOutcomes + 1);
            outcomeReader.Tag("world.material-proposals.outcomes"u8);
            List<WorldMaterialProposalOutcome> outcomes = [];
            while (outcomeReader.Remaining != 0)
            {
                outcomes.Add(DecodeOutcome(outcomeReader.Field(WorldMaterialProposalOutcome.MaximumByteLength)));
            }
            outcomeReader.Complete();

            var book = new WorldMaterialProposalBook(open, outcomes);
            if (!Encode(book).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Saved proposals are not canonical.");
            }
            return book;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException
            or AuthorityFormatException)
        {
            throw new FormatException("Saved proposals violate their bounded owner contract.", error);
        }
    }

    private static WorldMaterialProposal DecodeProposal(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldMaterialProposal.MaximumByteLength, 8);
        bool witnessed = Version(ref reader, "world.material-proposal.v1"u8, "world.material-proposal.v2"u8);
        ConsentedMaterialCommand grant = ConsentedMaterialCodecV1.DecodeCommand(
            reader.Field(ConsentedMaterialCodecV1.MaximumCommandByteLength));
        var delivery = new TrustedCommandDelivery(reader.Identity(), reader.Identity());
        ulong sequence = reader.Number();
        ulong step = reader.Number();
        ulong expiry = reader.Number();
        WorldControlWitness? witness = witnessed ? WorldControlWitnessCodecV1.Decode(reader.Field(WorldControlWitnessCodecV1.MaximumByteLength)) : null;
        reader.Complete();
        return new WorldMaterialProposal(grant, delivery, sequence, step, expiry, witness);
    }

    private static WorldMaterialProposalOutcome DecodeOutcome(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldMaterialProposalOutcome.MaximumByteLength, 7);
        bool attributed = Version(ref reader, "world.material-proposal-outcome.v1"u8, "world.material-proposal-outcome.v2"u8);
        WorldMaterialProposal proposal = DecodeProposal(reader.Field(WorldMaterialProposal.MaximumByteLength));
        ulong kind = reader.Number();
        ulong sequence = reader.Number();
        ReadOnlySpan<byte> terminal = reader.Field(16);
        ReadOnlySpan<byte> settlement = reader.Field(MaximumSettlementByteLength);
        WorldMaterialProposalTermination? termination = attributed
            ? DecodeTermination(reader.Field(WorldMaterialProposalTermination.MaximumByteLength)) : null;
        reader.Complete();
        if (kind is 0 or > 4 || terminal.Length is not (0 or 16))
        {
            throw new FormatException("Unsupported proposal outcome.");
        }
        return new WorldMaterialProposalOutcome(proposal, (WorldMaterialProposalOutcomeKind)kind, sequence,
            terminal.IsEmpty ? null : EntityId.FromBytes(terminal),
            settlement.IsEmpty ? null : DecodeSettlement(settlement), termination);
    }

    private static WorldMaterialProposalSettlement DecodeSettlement(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumSettlementByteLength, 15);
        bool witnessed = Version(ref reader, "world.material-proposal-settlement.v1"u8, "world.material-proposal-settlement.v2"u8);
        uint index = checked((uint)reader.Number());
        ConsentedMaterialAcceptance acceptance = ConsentedMaterialCodecV1.DecodeAcceptance(
            reader.Field(ConsentedMaterialCodecV1.MaximumAcceptanceByteLength));
        var recipient = new TrustedCommandDelivery(reader.Identity(), reader.Identity());
        EntityId movedLot = reader.Identity();
        EntityId rightsOwner = reader.Identity();
        uint outputIndex = checked((uint)reader.Number());
        ulong start = reader.Number();
        ulong end = reader.Number();
        ContentHash originalInventory = ReadHash(ref reader);
        ContentHash originalProduction = ReadHash(ref reader);
        ContentHash candidateInventory = ReadHash(ref reader);
        ContentHash candidateProduction = ReadHash(ref reader);
        WorldControlWitness? witness = witnessed ? WorldControlWitnessCodecV1.Decode(reader.Field(WorldControlWitnessCodecV1.MaximumByteLength)) : null;
        reader.Complete();
        return new WorldMaterialProposalSettlement(index, acceptance, recipient, movedLot, rightsOwner, outputIndex,
            start, end, originalInventory, originalProduction, candidateInventory, candidateProduction, witness);
    }

    private static WorldMaterialProposalTermination DecodeTermination(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldMaterialProposalTermination.MaximumByteLength, 5);
        reader.Tag("world.material-proposal-termination.v1"u8);
        EntityId command = reader.Identity();
        var delivery = new TrustedCommandDelivery(reader.Identity(), reader.Identity());
        ReadOnlySpan<byte> witness = reader.Field(WorldControlWitnessCodecV1.MaximumByteLength);
        reader.Complete();
        return new(command, delivery, witness.IsEmpty ? null : WorldControlWitnessCodecV1.Decode(witness));
    }

    private static bool Version(ref WorldFrameReader reader, ReadOnlySpan<byte> oldTag, ReadOnlySpan<byte> witnessedTag)
    {
        ReadOnlySpan<byte> tag = reader.Field(witnessedTag.Length);
        if (tag.SequenceEqual(oldTag)) return false;
        if (tag.SequenceEqual(witnessedTag)) return true;
        throw new FormatException("Unsupported saved material provenance version.");
    }

    private static ContentHash ReadHash(ref WorldFrameReader reader) =>
        new(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());

    private static byte[] Bounded(byte[] bytes, int maximum) => bytes.Length <= maximum ? bytes
        : throw new ArgumentException("A saved proposal frame exceeds its bound.");
}
