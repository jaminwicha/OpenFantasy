using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Bounded historical claims. Canonical decoding never authenticates either participant.</summary>
public static class WorldConsentedMaterialCodec
{
    public static byte[] Encode(WorldConsentedMaterialHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        byte[] bytes = CanonicalEncodingV1.Frame("world.consented-material-history.v1",
            history.Transfers.Select(value => (ReadOnlyMemory<byte>)value.Encode()).ToArray());
        if (bytes.Length > WorldConsentedMaterialHistory.MaximumByteLength)
        {
            throw new ArgumentException("Consent history exceeds its bounded frame.", nameof(history));
        }
        return bytes;
    }

    public static WorldConsentedMaterialHistory Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldConsentedMaterialHistory.MaximumByteLength,
                WorldConsentedMaterialHistory.MaximumTransfers + 1);
            reader.Tag("world.consented-material-history.v1"u8);
            List<WorldConsentedTransferEvidence> transfers = [];
            while (reader.Remaining != 0)
            {
                transfers.Add(DecodeTransfer(reader.Field(WorldConsentedTransferEvidence.MaximumByteLength)));
            }
            reader.Complete();
            var history = new WorldConsentedMaterialHistory(transfers);
            if (!Encode(history).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Consent history is not canonical.");
            }
            return history;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Consent history violates its bounded evidence contract.", error);
        }
    }

    private static WorldConsentedTransferEvidence DecodeTransfer(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldConsentedTransferEvidence.MaximumByteLength, 19);
        reader.Tag("world.consented-transfer.v1"u8);
        ulong sequence = reader.Number();
        uint offerIndex = checked((uint)reader.Number());
        uint acceptanceIndex = checked((uint)reader.Number());
        ConsentedMaterialCommand command = ConsentedMaterialCodecV1.DecodeCommand(
            reader.Field(ConsentedMaterialCodecV1.MaximumCommandByteLength));
        var sender = new TrustedCommandDelivery(reader.Identity(), reader.Identity());
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
        reader.Complete();
        return new WorldConsentedTransferEvidence(sequence, offerIndex, acceptanceIndex,
            new ConsentedMaterialApproval(command, sender, acceptance, recipient), movedLot, rightsOwner,
            outputIndex, start, end, originalInventory, originalProduction, candidateInventory, candidateProduction);
    }

    private static ContentHash ReadHash(ref WorldFrameReader reader) =>
        new(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
}
