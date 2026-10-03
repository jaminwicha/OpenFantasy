using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Networking.PrivateHistoryDirectory;

public sealed class PrivateHistoryDirectoryCodecTests
{
    private static readonly EntityId World = new(71, 1);
    private static readonly EntityId Branch = new(71, 2);
    private static readonly LocalActorSelection Home = new(new(71, 3), new(71, 4), new(71, 4), 0, null);

    [Fact]
    public void New_request_versions_preserve_every_legacy_selected_query_byte()
    {
        var legacy = PlayerSelectedInput.Query(Home, LocalSelectedQueryOperation.InspectPaidQuoteHistory, subjectId: new(71, 8));
        byte[] bytes = PlayerSelectedInputCodec.Encode(legacy);
        byte[] selection = PlayerSelectionCodec.Encode(Home);
        byte[] expected = new byte[4 + selection.Length + 19];
        expected[0] = 1; expected[1] = (byte)PlayerSelectedInputKind.Query;
        BinaryPrimitives.WriteUInt16BigEndian(expected.AsSpan(2), checked((ushort)selection.Length));
        selection.CopyTo(expected, 4);
        int start = 4 + selection.Length;
        expected[start] = 5; expected[start + 1] = 1; expected[start + 2] = 4;
        new EntityId(71, 8).WriteBytes(expected.AsSpan(start + 3));
        Assert.Equal(expected, bytes);
        Assert.Equal(bytes, PlayerSelectedInputCodec.Encode(PlayerSelectedInputCodec.Decode(bytes)));
        bytes[0] = 2;
        Assert.Throws<FormatException>(() => PlayerSelectedInputCodec.Decode(bytes));
    }

    [Fact]
    public void Directory_request_has_no_subject_and_exact_first_and_continuation_shapes()
    {
        LocalPrivateHistoryPageRequest[] requests = [new(), new(new(12, 9, LocalPrivateHistoryKind.MaterialProposal, new(71, 9)))];
        foreach (var request in requests)
        {
            byte[] bytes = PlayerSelectedInputCodec.Encode(PlayerSelectedInput.ReadPrivateHistory(Home, request));
            Assert.Equal(2, bytes[0]);
            Assert.Equal(4 + PlayerSelectionCodec.Encode(Home).Length + 1 + (request.Before is null ? 1 : 34), bytes.Length);
            var decoded = PlayerSelectedInputCodec.Decode(bytes);
            Assert.Equal(request, decoded.DirectoryRequest);
            Assert.Null(decoded.SubjectId);
            Assert.Equal(bytes, PlayerSelectedInputCodec.Encode(decoded));
            Assert.Throws<FormatException>(() => PlayerSelectedInputCodec.Decode([.. bytes, 0]));
            bytes[0] = 1;
            Assert.Throws<FormatException>(() => PlayerSelectedInputCodec.Decode(bytes));
        }
        Assert.Throws<FormatException>(() => PlayerSelectedInputCodec.Encode(PlayerSelectedInput.ReadPrivateHistory(Home,
            new(new(2, 3, LocalPrivateHistoryKind.PaidQuote, new(71, 1))))));
    }

    [Fact]
    public void Legacy_result_shape_remains_exact_and_directory_cannot_use_its_version()
    {
        var legacy = new PlayerSelectedQueryResponse(World, Branch, Home, LocalSelectedQueryOperation.ReadTradeInquiry, null);
        byte[] bytes = PlayerSelectedQueryResponseCodec.Encode(legacy);
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(1, document.RootElement.GetProperty("Version").GetInt32());
        Assert.Equal(new[] { "WorldId", "BranchId", "Selection", "Operation", "SubjectId", "Inquiry", "PaidQuote", "MaterialProposal" },
            document.RootElement.GetProperty("Result").EnumerateObject().Select(property => property.Name));
        Assert.Equal(bytes, PlayerSelectedQueryResponseCodec.Encode(PlayerSelectedQueryResponseCodec.Decode(bytes)));
        Assert.Throws<FormatException>(() => PlayerSelectedQueryResponseCodec.Decode(Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(bytes).Replace("\"Version\":1", "\"Version\":2", StringComparison.Ordinal))));
        var directory = Page();
        byte[] newBytes = PlayerSelectedQueryResponseCodec.Encode(directory);
        Assert.Throws<FormatException>(() => PlayerSelectedQueryResponseCodec.Decode(Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(newBytes).Replace("\"Version\":2", "\"Version\":1", StringComparison.Ordinal))));
        Assert.Throws<FormatException>(() => PlayerSelectedQueryResponseCodec.Encode(legacy with { Directory = directory.Directory }));
    }

    [Fact]
    public void Directory_response_correlates_request_cap_cursor_world_branch_and_complete_selection()
    {
        var response = Page();
        var request = PlayerSelectedInput.ReadPrivateHistory(Home, response.Directory!.Request);
        var decoded = PlayerSelectedQueryResponseCodec.Decode(PlayerSelectedQueryResponseCodec.Encode(response));
        decoded.ValidateFor(request, World, Branch, Home);
        Assert.Equal(response.Directory.Entries, decoded.Directory!.Entries);
        Assert.Throws<FormatException>(() => decoded.ValidateFor(request, World, new(71, 50), Home));
        Assert.Throws<FormatException>(() => decoded.ValidateFor(request, new(71, 50), Branch, Home));
        Assert.Throws<FormatException>(() => decoded.ValidateFor(request, World, Branch, Home with { Generation = 2 }));
        var changed = PlayerSelectedInput.ReadPrivateHistory(Home, new(new(13, 11, LocalPrivateHistoryKind.PaidQuote, new(71, 20))));
        Assert.Throws<FormatException>(() => decoded.ValidateFor(changed, World, Branch, Home));
        changed = PlayerSelectedInput.ReadPrivateHistory(Home, new(new(12, 11, LocalPrivateHistoryKind.PaidQuote, new(71, 21))));
        Assert.Throws<FormatException>(() => decoded.ValidateFor(changed, World, Branch, Home));
    }

    [Fact]
    public void Zero_subject_identity_is_preserved_in_directory_cursor_and_page_roundtrips()
    {
        var request = new LocalPrivateHistoryPageRequest(new(12, 11, LocalPrivateHistoryKind.PaidQuote, default));
        var input = PlayerSelectedInput.ReadPrivateHistory(Home, request);
        byte[] requestBytes = PlayerSelectedInputCodec.Encode(input);
        var decodedInput = PlayerSelectedInputCodec.Decode(requestBytes);
        Assert.Equal(request, decodedInput.DirectoryRequest);
        Assert.Equal(default(EntityId), decodedInput.DirectoryRequest!.Before!.SubjectId);
        Assert.Equal(requestBytes, PlayerSelectedInputCodec.Encode(decodedInput));

        var page = new LocalPrivateHistoryPage(request, 12,
            [new(LocalPrivateHistoryKind.PaidQuote, default, new(71, 30), 10, PaidQuoteStatus.Withdrawn)], null);
        var response = new PlayerSelectedQueryResponse(World, Branch, Home,
            LocalSelectedQueryOperation.ReadPrivateHistory, null, Directory: page);
        byte[] responseBytes = PlayerSelectedQueryResponseCodec.Encode(response);
        var decodedResponse = PlayerSelectedQueryResponseCodec.Decode(responseBytes);
        decodedResponse.ValidateFor(decodedInput, World, Branch, Home);
        Assert.Equal(default(EntityId), Assert.Single(decodedResponse.Directory!.Entries).SubjectId);
        Assert.Equal(page.Entries, decodedResponse.Directory.Entries);
        Assert.Equal(responseBytes, PlayerSelectedQueryResponseCodec.Encode(decodedResponse));
    }

    [Theory]
    [InlineData("open")]
    [InlineData("unknown-status")]
    [InlineData("both-statuses")]
    [InlineData("malformed-id")]
    [InlineData("duplicate")]
    [InlineData("order")]
    [InlineData("future-entry")]
    [InlineData("future-cap")]
    [InlineData("counterparty")]
    [InlineData("bad-next")]
    [InlineData("unknown-property")]
    [InlineData("missing-request")]
    public void Malformed_directory_frames_are_refused(string mutation)
    {
        var node = JsonNode.Parse(PlayerSelectedQueryResponseCodec.Encode(Page()))!;
        var result = node["Result"]!;
        var page = result["Directory"]!;
        var entries = page["Entries"]!.AsArray();
        switch (mutation)
        {
            case "open": entries[0]!["PaidQuoteStatus"] = (int)PaidQuoteStatus.Open; break;
            case "unknown-status": entries[0]!["PaidQuoteStatus"] = 99; break;
            case "both-statuses": entries[0]!["MaterialProposalStatus"] = (int)MaterialProposalStatus.Withdrawn; break;
            case "malformed-id": entries[0]!["SubjectId"] = "not-an-entity-id"; break;
            case "duplicate": entries[1]!["SubjectId"] = entries[0]!["SubjectId"]!.DeepClone(); break;
            case "order": entries[1]!["ClosedAtSequence"] = 11UL; break;
            case "future-entry": entries[0]!["ClosedAtSequence"] = 13UL; break;
            case "future-cap": page["ThroughRevision"] = 13UL; break;
            case "counterparty": entries[0]!["CounterpartyActorId"] = Home.ActorId.ToString(); break;
            case "bad-next": page["Next"] = page["Request"]!["Before"]!.DeepClone(); break;
            case "unknown-property": page["Balance"] = 1; break;
            case "missing-request": page.AsObject().Remove("Request"); break;
        }
        Assert.Throws<FormatException>(() => PlayerSelectedQueryResponseCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(node)));
    }

    private static PlayerSelectedQueryResponse Page()
    {
        var request = new LocalPrivateHistoryPageRequest(new(12, 11, LocalPrivateHistoryKind.PaidQuote, new(71, 20)));
        var page = new LocalPrivateHistoryPage(request, 12,
        [new(LocalPrivateHistoryKind.PaidQuote, new(71, 21), new(71, 30), 10, PaidQuoteStatus.Withdrawn),
            new(LocalPrivateHistoryKind.MaterialProposal, new(71, 22), new(71, 31), 9,
                MaterialProposalStatus: MaterialProposalStatus.Accepted)], null);
        return new(World, Branch, Home, LocalSelectedQueryOperation.ReadPrivateHistory, null, Directory: page);
    }
}
