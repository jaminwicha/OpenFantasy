using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Markets;

/// <summary>
/// Canonical frames for spot quotes, acceptances and the quote book. Decoding validates shape and local
/// invariants only; a content hash is structural and authenticates, admits or commits nothing.
/// </summary>
public static class SpotExchangeCodecV1
{
    private const string QuoteTag = "markets.spot.quote";
    private const string AcceptanceTag = "markets.spot.acceptance";
    private const string EntryTag = "markets.spot.entry";
    private const string BookTag = "markets.spot.book";
    private const ulong ContractVersion = 1;
    private const int ContentHashByteLength = 32;

    // Field counts include the leading tag field.
    private const uint QuoteFixedFieldCount = 26;
    private const uint AcceptanceFieldCount = 10;
    private const uint EntryFieldCount = 12;

    public const int MaximumQuoteByteLength = 1024;
    public const int MaximumAcceptanceByteLength = 512;
    public const int MaximumEntryByteLength = 1536;
    public const int MaximumBookByteLength = 1 << 20;

    public static byte[] Encode(SpotQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        List<ReadOnlyMemory<byte>> fields =
        [
            U64(ContractVersion), quote.QuoteId.ToByteArray(), quote.WorldId.ToByteArray(), quote.BranchId.ToByteArray(),
            quote.IssuerActorId.ToByteArray(), quote.IssuerPrincipalId.ToByteArray(), quote.IssuerSessionId.ToByteArray(),
            quote.CounterpartyActorId.ToByteArray(), U64((ulong)quote.Side), U64((ulong)quote.LotKind),
            quote.LotId.ToByteArray(), quote.Definition.DefinitionId.ToByteArray(), quote.Definition.Version.ToByteArray(),
            U64(quote.ExpectedLotRevision), quote.SellerCustodianId.ToByteArray(), quote.BuyerCustodianId.ToByteArray(),
            quote.CurrencyDefinitionId.ToByteArray(), quote.CurrencyVersion.ToByteArray(), quote.SellerAccountId.ToByteArray(),
            quote.BuyerAccountId.ToByteArray(), U64(quote.UnitPrice), U64(quote.QuantityAllowance),
            U64(quote.MarketRevision), U64(quote.IssuedAtStep), U64(quote.ExpiresAtStep), U64((ulong)quote.Fees.Count)
        ];
        foreach (SpotFee fee in quote.Fees)
        {
            fields.Add(fee.PayeeAccountId.ToByteArray());
            fields.Add(U64(fee.UnitAmount));
        }

        return CanonicalEncodingV1.Frame(QuoteTag, [.. fields]);
    }

    public static SpotQuote DecodeQuote(ReadOnlySpan<byte> bytes)
    {
        var reader = new AuthorityFrameReader(bytes, MaximumQuoteByteLength,
            QuoteFixedFieldCount + 1 + 2 * (uint)SpotQuote.MaximumFees);
        reader.ExpectTag("markets.spot.quote"u8);
        RequireVersion(ref reader);
        EntityId quoteId = reader.ReadIdentity();
        EntityId worldId = reader.ReadIdentity();
        EntityId branchId = reader.ReadIdentity();
        EntityId issuer = reader.ReadIdentity();
        EntityId principal = reader.ReadIdentity();
        EntityId session = reader.ReadIdentity();
        EntityId counterparty = reader.ReadIdentity();
        ulong side = reader.ReadUnsigned64();
        ulong lotKind = reader.ReadUnsigned64();
        EntityId lotId = reader.ReadIdentity();
        EntityId definitionId = reader.ReadIdentity();
        ContentHash definitionVersion = ReadHash(ref reader);
        ulong lotRevision = reader.ReadUnsigned64();
        EntityId seller = reader.ReadIdentity();
        EntityId buyer = reader.ReadIdentity();
        EntityId currencyId = reader.ReadIdentity();
        ContentHash currencyVersion = ReadHash(ref reader);
        EntityId sellerAccount = reader.ReadIdentity();
        EntityId buyerAccount = reader.ReadIdentity();
        ulong unitPrice = reader.ReadUnsigned64();
        ulong allowance = reader.ReadUnsigned64();
        ulong marketRevision = reader.ReadUnsigned64();
        ulong issuedAt = reader.ReadUnsigned64();
        ulong expiresAt = reader.ReadUnsigned64();
        ulong feeCount = reader.ReadUnsigned64();
        if (feeCount > SpotQuote.MaximumFees)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.LimitExceeded, "A quote names too many fees.");
        }

        var fees = new List<SpotFee>();
        EntityId? previous = null;
        for (ulong index = 0; index < feeCount; index++)
        {
            EntityId payee = reader.ReadIdentity();
            ulong amount = reader.ReadUnsigned64();
            if (previous is { } prior && prior.CompareTo(payee) >= 0 || amount == 0)
            {
                throw AuthorityFrameReader.Failure(AuthorityFormatFailure.NonCanonical,
                    "Fees require ascending distinct payees and positive amounts.");
            }

            previous = payee;
            fees.Add(new SpotFee(payee, amount));
        }

        reader.Complete();
        if (side is not ((ulong)SpotQuoteSide.IssuerSells or (ulong)SpotQuoteSide.IssuerBuys)
            || lotKind is not ((ulong)SpotLotKind.CountedSource or (ulong)SpotLotKind.Produced))
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.UnknownType, "The quote side or lot kind is unsupported.");
        }

        try
        {
            return new SpotQuote(quoteId, worldId, branchId, issuer, principal, session, counterparty, (SpotQuoteSide)side,
                (SpotLotKind)lotKind, lotId, new CountedItemDefinition(definitionId, definitionVersion), lotRevision, seller,
                buyer, currencyId, currencyVersion, sellerAccount, buyerAccount, unitPrice, fees, allowance, marketRevision,
                issuedAt, expiresAt);
        }
        catch (ArgumentException)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "Quote terms violate their invariants.");
        }
    }

    public static byte[] Encode(SpotAcceptance acceptance)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        return CanonicalEncodingV1.Frame(AcceptanceTag, U64(ContractVersion), acceptance.AcceptanceId.ToByteArray(),
            acceptance.PrincipalId.ToByteArray(), acceptance.OriginSessionId.ToByteArray(), acceptance.QuoteId.ToByteArray(),
            acceptance.QuoteHash.ToByteArray(), U64(acceptance.Quantity), U64(acceptance.Payment), U64(acceptance.Fees));
    }

    public static SpotAcceptance DecodeAcceptance(ReadOnlySpan<byte> bytes)
    {
        var reader = new AuthorityFrameReader(bytes, MaximumAcceptanceByteLength, AcceptanceFieldCount);
        reader.ExpectTag("markets.spot.acceptance"u8);
        RequireVersion(ref reader);
        EntityId acceptanceId = reader.ReadIdentity();
        EntityId principal = reader.ReadIdentity();
        EntityId session = reader.ReadIdentity();
        EntityId quoteId = reader.ReadIdentity();
        ContentHash quoteHash = ReadHash(ref reader);
        ulong quantity = reader.ReadUnsigned64();
        ulong payment = reader.ReadUnsigned64();
        ulong fees = reader.ReadUnsigned64();
        reader.Complete();
        try
        {
            return new SpotAcceptance(acceptanceId, principal, session, quoteId, quoteHash, quantity, payment, fees);
        }
        catch (ArgumentException)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "Acceptance violates its invariants.");
        }
    }

    public static byte[] Encode(SpotQuoteBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        List<ReadOnlyMemory<byte>> fields =
        [
            U64(ContractVersion), book.WorldId.ToByteArray(), book.BranchId.ToByteArray(), U64(book.Revision),
            U64((ulong)book.IssuerRevisions.Count)
        ];
        foreach ((EntityId issuer, ulong revision) in book.IssuerRevisions)
        {
            fields.Add(issuer.ToByteArray());
            fields.Add(U64(revision));
        }

        fields.Add(U64((ulong)book.Entries.Count));
        foreach (SpotQuoteEntry entry in book.Entries)
        {
            fields.Add(EncodeEntry(entry));
        }

        return CanonicalEncodingV1.Frame(BookTag, [.. fields]);
    }

    public static SpotQuoteBook DecodeBook(ReadOnlySpan<byte> bytes)
    {
        uint maximumFields = 7 + 3 * (uint)SpotQuoteBook.MaximumQuotes;
        var reader = new AuthorityFrameReader(bytes, MaximumBookByteLength, maximumFields);
        reader.ExpectTag("markets.spot.book"u8);
        RequireVersion(ref reader);
        EntityId worldId = reader.ReadIdentity();
        EntityId branchId = reader.ReadIdentity();
        ulong revision = reader.ReadUnsigned64();
        List<KeyValuePair<EntityId, ulong>> issuers = [];
        foreach (ulong _ in Count(ref reader))
        {
            EntityId issuer = reader.ReadIdentity();
            if (issuers.Count > 0 && issuers[^1].Key.CompareTo(issuer) >= 0)
            {
                throw AuthorityFrameReader.Failure(AuthorityFormatFailure.NonCanonical, "Issuers must be ascending and distinct.");
            }

            issuers.Add(new(issuer, reader.ReadUnsigned64()));
        }

        List<SpotQuoteEntry> entries = [];
        foreach (ulong _ in Count(ref reader))
        {
            SpotQuoteEntry entry = DecodeEntry(reader.ReadField(MaximumEntryByteLength));
            if (entries.Count > 0 && entries[^1].Quote.QuoteId.CompareTo(entry.Quote.QuoteId) >= 0)
            {
                throw AuthorityFrameReader.Failure(AuthorityFormatFailure.NonCanonical, "Quotes must be ascending and distinct.");
            }

            entries.Add(entry);
        }

        reader.Complete();
        try
        {
            return new SpotQuoteBook(worldId, branchId, revision, entries, issuers);
        }
        catch (ArgumentException)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "Quote book violates its invariants.");
        }
    }

    private static byte[] EncodeEntry(SpotQuoteEntry entry)
    {
        SpotFill? fill = entry.Fill;
        return CanonicalEncodingV1.Frame(EntryTag, U64(ContractVersion), Encode(entry.Quote),
            entry.IssuerDelivery.PrincipalId.ToByteArray(), entry.IssuerDelivery.SessionId.ToByteArray(),
            U64(fill?.Sequence ?? 0), (fill?.AcceptanceId ?? default).ToByteArray(),
            fill?.AcceptanceHash.ToByteArray() ?? new byte[ContentHashByteLength], U64(fill?.Quantity ?? 0),
            (fill?.MovedLotId ?? default).ToByteArray(), U64(fill?.Payment ?? 0), U64(fill?.Fees ?? 0));
    }

    private static SpotQuoteEntry DecodeEntry(ReadOnlySpan<byte> bytes)
    {
        var reader = new AuthorityFrameReader(bytes, MaximumEntryByteLength, EntryFieldCount);
        reader.ExpectTag("markets.spot.entry"u8);
        RequireVersion(ref reader);
        SpotQuote quote = DecodeQuote(reader.ReadField(MaximumQuoteByteLength));
        var delivery = new TrustedCommandDelivery(reader.ReadIdentity(), reader.ReadIdentity());
        ulong sequence = reader.ReadUnsigned64();
        EntityId acceptanceId = reader.ReadIdentity();
        ReadOnlySpan<byte> hash = reader.ReadField(ContentHashByteLength);
        ulong quantity = reader.ReadUnsigned64();
        EntityId movedLotId = reader.ReadIdentity();
        ulong payment = reader.ReadUnsigned64();
        ulong fees = reader.ReadUnsigned64();
        reader.Complete();
        if (hash.Length != ContentHashByteLength)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "A content hash requires thirty-two bytes.");
        }

        // An open entry encodes one exact all-zero fill; any other absent-fill bytes are non-canonical.
        if (sequence == 0)
        {
            if (acceptanceId != default || hash.IndexOfAnyExcept((byte)0) >= 0 || quantity != 0 || movedLotId != default
                || payment != 0 || fees != 0)
            {
                throw AuthorityFrameReader.Failure(AuthorityFormatFailure.NonCanonical, "An open quote carries no fill.");
            }

            try
            {
                return new SpotQuoteEntry(quote, delivery);
            }
            catch (ArgumentException)
            {
                throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "Recorded issuer delivery must match the quote.");
            }
        }

        try
        {
            return new SpotQuoteEntry(quote, delivery, new SpotFill(sequence, acceptanceId,
                new ContentHash(Convert.ToHexString(hash).ToLowerInvariant()), quantity, movedLotId, payment, fees));
        }
        catch (ArgumentException)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "A fill violates its quote.");
        }
    }

    private static IEnumerable<ulong> Count(ref AuthorityFrameReader reader)
    {
        ulong count = reader.ReadUnsigned64();
        if (count > SpotQuoteBook.MaximumQuotes)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.LimitExceeded, "A quote book collection is too large.");
        }

        return Enumerable.Range(0, (int)count).Select(value => (ulong)value).ToArray();
    }

    private static ContentHash ReadHash(ref AuthorityFrameReader reader)
    {
        ReadOnlySpan<byte> bytes = reader.ReadField(ContentHashByteLength);
        if (bytes.Length != ContentHashByteLength)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "A content hash requires thirty-two bytes.");
        }

        return new ContentHash(Convert.ToHexString(bytes).ToLowerInvariant());
    }

    private static void RequireVersion(ref AuthorityFrameReader reader)
    {
        if (reader.ReadUnsigned64() != ContractVersion)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.UnsupportedVersion,
                "The spot exchange contract version is unsupported.");
        }
    }

    private static byte[] U64(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
