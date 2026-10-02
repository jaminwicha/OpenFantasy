using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerQueries;

public sealed class PlayerQueryCodecTests(QueryCodecValues values) : IClassFixture<QueryCodecValues>
{
    [Fact]
    public void Every_request_kind_has_one_exact_binary_shape_and_identity_order()
    {
        var definition = new CountedItemDefinition(new(3, 4), ContentHash.Compute("query"u8));
        PlayerQuery[] requests =
        [
            new(PlayerQueryKind.SubmitTradeInquiry, new(new(1, 2), definition, LocalTradeDirection.Buy, 7)),
            new(PlayerQueryKind.SubmitTradeInquiry, new(new(1, 2), definition, LocalTradeDirection.Sell, 8, new(5, 6))),
            new(PlayerQueryKind.CancelTradeInquiry), new(PlayerQueryKind.ReadTradeInquiry),
            new(PlayerQueryKind.InspectPaidQuote, subjectId: new(7, 8)),
            new(PlayerQueryKind.InspectMaterialProposal, subjectId: new(9, 10))
        ];
        int[] lengths = [75, 91, 2, 2, 18, 18];
        for (int index = 0; index < requests.Length; index++)
        {
            byte[] bytes = PlayerQueryCodec.Encode(requests[index]);
            Assert.Equal(lengths[index], bytes.Length);
            Assert.Equal(requests[index], PlayerQueryCodec.Decode(bytes));
            Assert.Equal(bytes, PlayerQueryCodec.Encode(PlayerQueryCodec.Decode(bytes)));
            for (int length = 0; length < bytes.Length; length++)
            {
                byte[] truncated = bytes[..length];
                Assert.Throws<FormatException>(() => PlayerQueryCodec.Decode(truncated));
            }
            Assert.Throws<FormatException>(() => PlayerQueryCodec.Decode([.. bytes, 0]));
        }
        byte[] buy = PlayerQueryCodec.Encode(requests[0]);
        Assert.Equal(new EntityId(1, 2).ToByteArray(), buy[2..18]);
        Assert.Equal(definition.Version.ToByteArray(), buy[34..66]);
        Assert.Equal(7UL, BinaryPrimitives.ReadUInt64BigEndian(buy.AsSpan(67)));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("kind")]
    [InlineData("direction")]
    [InlineData("zero")]
    [InlineData("quantity")]
    [InlineData("shape")]
    public void Requests_refuse_invalid_values_and_contradictory_fields(string mutation)
    {
        byte[] bytes = PlayerQueryCodec.Encode(new(PlayerQueryKind.SubmitTradeInquiry, values.Inquiry));
        switch (mutation)
        {
            case "version": bytes[0] = 2; break;
            case "kind": bytes[1] = 255; break;
            case "direction": bytes[66] = 255; break;
            case "zero": BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(67), 0); break;
            case "quantity": BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(67), SpotQuote.MaximumQuantity + 1); break;
            case "shape": bytes[66] = (byte)LocalTradeDirection.Sell; break;
        }
        Assert.Throws<FormatException>(() => PlayerQueryCodec.Decode(bytes));
        Assert.Throws<ArgumentException>(() => new PlayerQuery(PlayerQueryKind.ReadTradeInquiry, values.Inquiry));
        Assert.Throws<ArgumentException>(() => new PlayerQuery(PlayerQueryKind.InspectPaidQuote));
    }

    [Fact]
    public void Genuine_paid_and_proposal_values_round_trip_without_changing_diagnostic_bytes_or_prepared_terms()
    {
        var paid = AssertParity(values.Paid);
        var proposal = AssertParity(values.Proposal);
        Assert.Equal(values.Proposal.MaterialProposal!.PrepareAcceptance(), proposal.MaterialProposal!.PrepareAcceptance());
        Assert.Equal(values.Paid.PaidQuote!.QuoteHash, paid.PaidQuote!.QuoteHash);
        Assert.Throws<NotSupportedException>(() => ((IList<SpotFee>)paid.PaidQuote.Fees).Clear());
        var open = AssertParity(values.OpenPaid);
        var beforeAcceptance = values.OpenPaid.PaidQuote!.PrepareAcceptance(new(8, 9), values.Delivery, 1);
        var afterAcceptance = open.PaidQuote!.PrepareAcceptance(new(8, 9), values.Delivery, 1);
        Assert.Equal(JsonSerializer.Serialize(beforeAcceptance), JsonSerializer.Serialize(afterAcceptance));
        foreach (PlayerQueryKind kind in new[] { PlayerQueryKind.InspectPaidQuote, PlayerQueryKind.InspectMaterialProposal })
        {
            var missing = new PlayerQueryResult(values.WorldId, values.BranchId, values.ActorId, kind, new EntityId(9, 9));
            AssertParity(missing).ValidateFor(new(kind, subjectId: new EntityId(9, 9)), values.WorldId, values.BranchId, values.ActorId);
        }
    }

    [Fact]
    public async Task Genuine_inquiry_waiting_cancellation_and_empty_reads_round_trip()
    {
        using var world = new ScheduledTradingWorld();
        var time = new ManualSessionTimeProvider();
        await using var session = world.Open(time, ScheduledTradingWorld.Merchants(world.Wren));
        var request = new LocalTradeInquiry(world.Fixture.WrenMarket.MarketId, world.Berries, LocalTradeDirection.Buy, 1);
        var query = new PlayerQuery(PlayerQueryKind.SubmitTradeInquiry, request);
        LocalTradeInquiryView waiting = await session.SubmitTradeInquiryAsync(request);
        PlayerQueryResult Make(PlayerQueryKind kind, LocalTradeInquiryView? inquiry) =>
            new(world.State.WorldId, world.State.BranchId, world.Player, kind, inquiry: inquiry);
        AssertParity(Make(PlayerQueryKind.SubmitTradeInquiry, waiting)).ValidateFor(query,
            world.State.WorldId, world.State.BranchId, world.Player);
        var cancelled = await session.CancelTradeInquiryAsync();
        Assert.Equal(LocalTradeInquiryState.Cancelled, AssertParity(Make(PlayerQueryKind.CancelTradeInquiry, cancelled)).Inquiry!.State);
        AssertParity(Make(PlayerQueryKind.ReadTradeInquiry, session.ReadTradeInquiry()));
        AssertParity(Make(PlayerQueryKind.ReadTradeInquiry, null));
        AssertParity(Make(PlayerQueryKind.CancelTradeInquiry, null));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("version")]
    [InlineData("kind")]
    [InlineData("actor")]
    [InlineData("subject")]
    [InlineData("null-definition")]
    [InlineData("side")]
    [InlineData("paid-status")]
    [InlineData("total")]
    [InlineData("closure")]
    [InlineData("observation")]
    [InlineData("extra-inquiry")]
    public void Paid_results_refuse_malformed_or_contradictory_values(string mutation)
    {
        byte[] bytes = PlayerQueryResultCodec.Encode(values.Paid);
        if (mutation == "duplicate")
        {
            bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\"Version\":1,", "\"Version\":1,\"Version\":1,", StringComparison.Ordinal));
        }
        else
        {
            JsonNode body = JsonNode.Parse(bytes)!;
            JsonNode result = body["Result"]!;
            JsonNode quote = result["PaidQuote"]!;
            switch (mutation)
            {
                case "missing": result.AsObject().Remove("Inquiry"); break;
                case "unknown": result["Root"] = "private"; break;
                case "version": body["Version"] = 2; break;
                case "kind": result["Kind"] = 255; break;
                case "actor": result["ActorId"]!["Low"] = 999; break;
                case "subject": result["SubjectId"]!["Low"] = 999; break;
                case "null-definition": quote["Definition"] = null; break;
                case "side": quote["Side"] = 255; break;
                case "paid-status": quote["Status"] = 255; break;
                case "total": quote["FilledPayment"] = ulong.MaxValue; break;
                case "closure": quote["ClosedAtSequence"] = null; break;
                case "observation": quote["ObservedIssuerRevision"] = 1; break;
                case "extra-inquiry": result["Inquiry"] = JsonSerializer.SerializeToNode(new LocalTradeInquiryView(values.Inquiry, LocalTradeInquiryState.Waiting, 10, null)); break;
            }
            bytes = JsonSerializer.SerializeToUtf8Bytes(body);
        }
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode(bytes));
    }

    [Theory]
    [InlineData("world")]
    [InlineData("branch")]
    [InlineData("participant")]
    [InlineData("enum")]
    [InlineData("null-terms")]
    [InlineData("open-closure")]
    public void Proposal_results_refuse_cross_scope_and_lifecycle_contradictions(string mutation)
    {
        JsonNode body = JsonNode.Parse(PlayerQueryResultCodec.Encode(values.Proposal))!;
        JsonNode proposal = body["Result"]!["MaterialProposal"]!;
        switch (mutation)
        {
            case "world": proposal["Terms"]!["WorldId"]!["Low"] = 999; break;
            case "branch": proposal["Terms"]!["BranchId"]!["Low"] = 999; break;
            case "participant": proposal["ViewerIsSender"] = true; break;
            case "enum": proposal["Status"] = 255; break;
            case "null-terms": proposal["Terms"] = null; break;
            case "open-closure": proposal["ClosedAtSequence"] = 1; break;
        }
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(body);
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode(bytes));
    }

    [Fact]
    public void Results_enforce_byte_depth_canonical_and_request_scope_bounds()
    {
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode([]));
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode(new byte[PlayerQueryResultCodec.MaximumBodyBytes + 1]));
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode(Encoding.UTF8.GetBytes(new string('[', 49) + new string(']', 49))));
        byte[] bytes = PlayerQueryResultCodec.Encode(values.Paid);
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode([.. bytes, (byte)' ']));
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode(bytes[..^1]));
        var request = new PlayerQuery(PlayerQueryKind.InspectPaidQuote, subjectId: values.Paid.SubjectId);
        values.Paid.ValidateFor(request, values.WorldId, values.BranchId, values.ActorId);
        Assert.Throws<FormatException>(() => values.Paid.ValidateFor(request, new(9, 9), values.BranchId, values.ActorId));
        Assert.Throws<FormatException>(() => values.Paid.ValidateFor(request, values.WorldId, new(9, 9), values.ActorId));
        Assert.Throws<FormatException>(() => values.Paid.ValidateFor(request, values.WorldId, values.BranchId, new(9, 9)));
        Assert.Throws<FormatException>(() => values.Paid.ValidateFor(new(PlayerQueryKind.InspectPaidQuote, subjectId: new(9, 9)),
            values.WorldId, values.BranchId, values.ActorId));
        var buffer = new PlayerQueryJsonBuffer();
        buffer.Advance(PlayerQueryResultCodec.MaximumBodyBytes);
        Assert.Throws<FormatException>(() => buffer.Advance(1));
    }

    [Fact]
    public void Submitted_inquiry_results_must_restate_the_request_and_cannot_be_missing()
    {
        var result = new PlayerQueryResult(values.WorldId, values.BranchId, values.ActorId,
            PlayerQueryKind.SubmitTradeInquiry, inquiry: new(values.Inquiry, LocalTradeInquiryState.Waiting, 10, null));
        var other = new LocalTradeInquiry(values.Inquiry.MarketId, values.Inquiry.Commodity, LocalTradeDirection.Buy, 2);
        Assert.Throws<FormatException>(() => result.ValidateFor(new(PlayerQueryKind.SubmitTradeInquiry, other),
            values.WorldId, values.BranchId, values.ActorId));
        Assert.Throws<FormatException>(() => new PlayerQueryResult(values.WorldId, values.BranchId, values.ActorId,
            PlayerQueryKind.SubmitTradeInquiry));
    }

    [Theory]
    [InlineData(LocalTradeInquiryState.Waiting, true)]
    [InlineData(LocalTradeInquiryState.Offered, false)]
    [InlineData((LocalTradeInquiryState)255, false)]
    public void Inquiry_state_and_quote_identity_must_agree(LocalTradeInquiryState state, bool hasQuote)
    {
        Assert.Throws<FormatException>(() => new PlayerQueryResult(values.WorldId, values.BranchId, values.ActorId,
            PlayerQueryKind.ReadTradeInquiry, inquiry: new(values.Inquiry, state, 10, hasQuote ? new EntityId(9, 9) : null)));
    }

    private static PlayerQueryResult AssertParity(PlayerQueryResult original)
    {
        string diagnostic = JsonSerializer.Serialize(original);
        byte[] bytes = PlayerQueryResultCodec.Encode(original);
        PlayerQueryResult decoded = PlayerQueryResultCodec.Decode(bytes);
        Assert.Equal(bytes, PlayerQueryResultCodec.Encode(decoded));
        Assert.Equal(diagnostic, JsonSerializer.Serialize(decoded));
        Assert.Equal(diagnostic, JsonSerializer.Serialize(original));
        Assert.True(bytes.Length <= PlayerQueryResultCodec.MaximumBodyBytes);
        return decoded;
    }
}
