using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// Version byte, kind byte, then exact kind-specific fields. Inspection adds a 16-byte subject. Submission adds
/// market ID, commodity ID, 32-byte commodity hash, direction byte and big-endian quantity; selling adds own lot ID.
/// Entity IDs retain their canonical big-endian order. There are no flags, identities or optional trailing fields.
/// </summary>
public static class PlayerQueryCodec
{
    public const byte Version = 1;
    public const int HeaderBytes = 2;
    public const int InspectionBytes = HeaderBytes + EntityId.ByteLength;
    public const int BuyInquiryBytes = HeaderBytes + EntityId.ByteLength * 2 + 32 + 1 + sizeof(ulong);
    public const int SellInquiryBytes = BuyInquiryBytes + EntityId.ByteLength;
    public const int MaximumBodyBytes = SellInquiryBytes;

    public static byte[] Encode(PlayerQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        int length = query.Inquiry is { } inquiry
            ? (inquiry.Direction == LocalTradeDirection.Buy ? BuyInquiryBytes : SellInquiryBytes)
            : query.SubjectId.HasValue ? InspectionBytes : HeaderBytes;
        byte[] bytes = new byte[length];
        bytes[0] = Version;
        bytes[1] = (byte)query.Kind;
        if (query.Inquiry is { } request)
        {
            request.MarketId.WriteBytes(bytes.AsSpan(2));
            request.Commodity.DefinitionId.WriteBytes(bytes.AsSpan(18));
            request.Commodity.Version.ToByteArray().CopyTo(bytes.AsSpan(34));
            bytes[66] = (byte)request.Direction;
            BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(67), request.Quantity);
            request.OwnShownLotId?.WriteBytes(bytes.AsSpan(BuyInquiryBytes));
        }
        else if (query.SubjectId is { } subject)
        {
            subject.WriteBytes(bytes.AsSpan(HeaderBytes));
        }
        return bytes;
    }

    public static PlayerQuery Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.Length < HeaderBytes || bytes.Length > MaximumBodyBytes || bytes[0] != Version)
            {
                throw new FormatException("Invalid query header or byte length.");
            }
            PlayerQueryKind kind = (PlayerQueryKind)bytes[1];
            if (kind == PlayerQueryKind.SubmitTradeInquiry)
            {
                if (bytes.Length is not (BuyInquiryBytes or SellInquiryBytes))
                    throw new FormatException("Invalid inquiry byte length.");
                LocalTradeDirection direction = (LocalTradeDirection)bytes[66];
                if (!Enum.IsDefined(direction) || bytes.Length != (direction == LocalTradeDirection.Buy ? BuyInquiryBytes : SellInquiryBytes))
                    throw new FormatException("The inquiry direction and shape disagree.");
                var commodity = new CountedItemDefinition(EntityId.FromBytes(bytes.Slice(18, 16)),
                    new ContentHash(Convert.ToHexString(bytes.Slice(34, 32)).ToLowerInvariant()));
                EntityId? ownLot = direction == LocalTradeDirection.Sell ? EntityId.FromBytes(bytes.Slice(BuyInquiryBytes, 16)) : null;
                return new PlayerQuery(kind, new LocalTradeInquiry(EntityId.FromBytes(bytes.Slice(2, 16)), commodity,
                    direction, BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(67, 8)), ownLot));
            }
            if (kind is PlayerQueryKind.InspectPaidQuote or PlayerQueryKind.InspectMaterialProposal)
            {
                if (bytes.Length != InspectionBytes) throw new FormatException("Invalid inspection byte length.");
                return new PlayerQuery(kind, subjectId: EntityId.FromBytes(bytes.Slice(HeaderBytes, 16)));
            }
            if (kind is not (PlayerQueryKind.CancelTradeInquiry or PlayerQueryKind.ReadTradeInquiry) || bytes.Length != HeaderBytes)
                throw new FormatException("Unknown query kind or contradictory fields.");
            return new PlayerQuery(kind);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid query values.", exception);
        }
    }
}
