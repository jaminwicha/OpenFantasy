using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Authority.ConsentedMaterial;

/// <summary>
/// Canonical fixed-layout frames for the material sender command and recipient acceptance. The tags
/// are distinct from the raw consented custody contract, whose bytes never decode here. Decoding
/// validates shape and local invariants only; it authenticates nobody and commits nothing.
/// </summary>
public static class ConsentedMaterialCodecV1
{
    private const string TermsTag = "authority.consented-material.terms";
    private const string CommandTag = "authority.consented-material.command";
    private const string AcceptanceTag = "authority.consented-material.acceptance";

    // Field counts include the leading tag field.
    private const uint TermsFieldCount = 14;
    private const uint CommandFieldCount = 6;
    private const uint AcceptanceFieldCount = 8;
    private const ulong ContractVersion = 1;
    private const int ContentHashByteLength = 32;

    /// <summary>Bounds for fixed-layout frames; exact field widths are enforced while reading.</summary>
    public const int MaximumTermsByteLength = 512;
    public const int MaximumCommandByteLength = 768;
    public const int MaximumAcceptanceByteLength = 832;

    private static ReadOnlySpan<byte> TermsTagUtf8 => "authority.consented-material.terms"u8;
    private static ReadOnlySpan<byte> CommandTagUtf8 => "authority.consented-material.command"u8;
    private static ReadOnlySpan<byte> AcceptanceTagUtf8 => "authority.consented-material.acceptance"u8;

    public static byte[] Encode(ConsentedMaterialCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CanonicalEncodingV1.Frame(CommandTag, CanonicalEncodingV1.Unsigned64(command.Version),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(),
            command.OriginSessionId.ToByteArray(), EncodeTerms(command.Terms));
    }

    public static ConsentedMaterialCommand DecodeCommand(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, MaximumCommandByteLength, CommandFieldCount);
        reader.ExpectTag(CommandTagUtf8);
        RequireVersion(ref reader);
        EntityId commandId = reader.ReadIdentity();
        EntityId principalId = reader.ReadIdentity();
        EntityId originSessionId = reader.ReadIdentity();
        ConsentedMaterialTerms terms = DecodeTerms(reader.ReadField(MaximumTermsByteLength));
        reader.Complete();
        return new ConsentedMaterialCommand(commandId, principalId, originSessionId, terms);
    }

    public static byte[] Encode(ConsentedMaterialAcceptance acceptance)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        return CanonicalEncodingV1.Frame(AcceptanceTag, CanonicalEncodingV1.Unsigned64(acceptance.Version),
            acceptance.AcceptanceId.ToByteArray(), acceptance.PrincipalId.ToByteArray(),
            acceptance.OriginSessionId.ToByteArray(), acceptance.CommandId.ToByteArray(),
            acceptance.CommandHash.ToByteArray(), EncodeTerms(acceptance.Terms));
    }

    public static ConsentedMaterialAcceptance DecodeAcceptance(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, MaximumAcceptanceByteLength, AcceptanceFieldCount);
        reader.ExpectTag(AcceptanceTagUtf8);
        RequireVersion(ref reader);
        EntityId acceptanceId = reader.ReadIdentity();
        EntityId principalId = reader.ReadIdentity();
        EntityId originSessionId = reader.ReadIdentity();
        EntityId commandId = reader.ReadIdentity();
        ReadOnlySpan<byte> hash = reader.ReadField(CommandContentHash.ByteLength);
        if (hash.Length != CommandContentHash.ByteLength)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue,
                "A content hash requires exactly thirty-two bytes.");
        }

        CommandContentHash commandHash = CommandContentHash.FromBytes(hash);
        ConsentedMaterialTerms terms = DecodeTerms(reader.ReadField(MaximumTermsByteLength));
        reader.Complete();
        return new ConsentedMaterialAcceptance(acceptanceId, principalId, originSessionId, commandId,
            commandHash, terms);
    }

    /// <summary>The exact approved terms as one frame, shared by both approvals and their world submissions.</summary>
    public static byte[] EncodeTerms(ConsentedMaterialTerms terms) => CanonicalEncodingV1.Frame(TermsTag,
        CanonicalEncodingV1.Unsigned64(ContractVersion), terms.WorldId.ToByteArray(), terms.BranchId.ToByteArray(),
        terms.SenderActorId.ToByteArray(), terms.RecipientActorId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64((ulong)terms.LotKind), terms.LotId.ToByteArray(),
        terms.Definition.DefinitionId.ToByteArray(), terms.Definition.Version.ToByteArray(),
        terms.SourceCustodianId.ToByteArray(), terms.DestinationCustodianId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(terms.Quantity),
        CanonicalEncodingV1.Unsigned64(terms.ExpectedLotRevision));

    public static ConsentedMaterialTerms DecodeTerms(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, MaximumTermsByteLength, TermsFieldCount);
        reader.ExpectTag(TermsTagUtf8);
        RequireVersion(ref reader);
        EntityId worldId = reader.ReadIdentity();
        EntityId branchId = reader.ReadIdentity();
        EntityId senderActorId = reader.ReadIdentity();
        EntityId recipientActorId = reader.ReadIdentity();
        ulong lotKind = reader.ReadUnsigned64();
        EntityId lotId = reader.ReadIdentity();
        EntityId definitionId = reader.ReadIdentity();
        ReadOnlySpan<byte> definitionVersion = reader.ReadField(ContentHashByteLength);
        EntityId sourceCustodianId = reader.ReadIdentity();
        EntityId destinationCustodianId = reader.ReadIdentity();
        ulong quantity = reader.ReadUnsigned64();
        ulong lotRevision = reader.ReadUnsigned64();
        reader.Complete();
        if (definitionVersion.Length != ContentHashByteLength)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue,
                "A definition version requires exactly thirty-two bytes.");
        }

        if (lotKind is not ((ulong)ConsentedMaterialLotKind.CountedSource or (ulong)ConsentedMaterialLotKind.Produced))
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.UnknownType,
                "The consented lot kind is unsupported.");
        }

        var definition = new CountedItemDefinition(definitionId,
            new ContentHash(Convert.ToHexString(definitionVersion).ToLowerInvariant()));
        try
        {
            return new ConsentedMaterialTerms(worldId, branchId, senderActorId, recipientActorId,
                (ConsentedMaterialLotKind)lotKind, lotId, definition, sourceCustodianId, destinationCustodianId,
                quantity, lotRevision);
        }
        catch (ArgumentException)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue,
                "Material transfer terms violate their invariants.");
        }
    }

    private static void RequireVersion(ref AuthorityFrameReader reader)
    {
        if (reader.ReadUnsigned64() != ContractVersion)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.UnsupportedVersion,
                "The consented material contract version is unsupported.");
        }
    }
}
