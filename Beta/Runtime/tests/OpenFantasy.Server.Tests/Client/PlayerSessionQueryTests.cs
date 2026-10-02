using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Server.Tests;

public sealed class PlayerSessionQueryTests
{
    [Theory]
    [InlineData(PlayerQueryKind.ReadPresentationBootstrap)]
    [InlineData(PlayerQueryKind.ReadParticipantStatus)]
    public void Extensions_have_exact_requests_and_do_not_accept_other_versions_or_trailing_data(PlayerQueryKind kind)
    {
        byte[] bytes = [1, (byte)kind];
        Assert.Equal(bytes, PlayerQueryCodec.Encode(new(kind)));
        Assert.Equal(kind, PlayerQueryCodec.Decode(bytes).Kind);
        Assert.Throws<FormatException>(() => PlayerQueryCodec.Decode([2, (byte)kind]));
        Assert.Throws<FormatException>(() => PlayerQueryCodec.Decode([.. bytes, 0]));
    }

    [Fact]
    public void Old_result_shape_and_diagnostic_fields_remain_exact_while_new_kind_requires_version_two()
    {
        var old = new PlayerQueryResult(new(1, 2), new(3, 4), new(5, 6), PlayerQueryKind.ReadTradeInquiry);
        byte[] bytes = PlayerQueryResultCodec.Encode(old);
        using var json = JsonDocument.Parse(bytes);
        Assert.Equal(1, json.RootElement.GetProperty("Version").GetInt32());
        string[] oldProperties = ["WorldId", "BranchId", "ActorId", "Kind", "SubjectId", "Inquiry", "PaidQuote", "MaterialProposal"];
        Assert.Equal(oldProperties, json.RootElement.GetProperty("Result").EnumerateObject().Select(value => value.Name));
        using var diagnostic = JsonDocument.Parse(JsonSerializer.Serialize(old));
        Assert.Equal(oldProperties, diagnostic.RootElement.EnumerateObject().Select(value => value.Name));
        var status = new PlayerQueryResult(old.WorldId, old.BranchId, old.ActorId, PlayerQueryKind.ReadParticipantStatus,
            participantStatus: new(LocalWorldRunState.Paused));
        byte[] encoded = PlayerQueryResultCodec.Encode(status);
        Assert.Equal(status.ParticipantStatus, PlayerQueryResultCodec.Decode(encoded).ParticipantStatus);
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(encoded).Replace("\"Version\":2", "\"Version\":1"))));
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\"Version\":1", "\"Version\":2"))));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign-kind")]
    [InlineData("run-state")]
    [InlineData("extra")]
    public void Participant_results_refuse_incomplete_or_contradictory_payloads(string mutation)
    {
        var result = new PlayerQueryResult(new(1, 1), new(1, 2), new(1, 3), PlayerQueryKind.ReadParticipantStatus,
            participantStatus: new(LocalWorldRunState.Paused));
        JsonObject doc = JsonNode.Parse(PlayerQueryResultCodec.Encode(result))!.AsObject();
        JsonObject body = doc["Result"]!.AsObject();
        if (mutation == "missing") body.Remove("ParticipantStatus");
        if (mutation == "foreign-kind") body["Kind"] = (int)PlayerQueryKind.ReadPresentationBootstrap;
        if (mutation == "run-state") body["ParticipantStatus"]!["RunState"] = 255;
        if (mutation == "extra") body["SubjectId"] = null;
        Assert.Throws<FormatException>(() => PlayerQueryResultCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(doc)));
    }
}
