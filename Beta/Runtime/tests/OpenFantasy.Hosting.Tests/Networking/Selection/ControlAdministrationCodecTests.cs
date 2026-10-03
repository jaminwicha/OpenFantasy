using System.Text;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.Control;
using OpenFantasy.Hosting.Tests.Simulation.ActorSelection;

namespace OpenFantasy.Hosting.Tests.Networking.Selection;

public sealed class ControlAdministrationCodecTests
{
    [Fact]
    public async Task Real_private_board_acquisition_and_original_receipt_roundtrip_without_delivery_credentials()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var board = await player.ReadControlPermissionsAsync();
        var response = new PlayerControlResponse(board.WorldId, board.BranchId, board.HomeActorId,
            PlayerControlRequestKind.ReadPermissions, Board: board);
        byte[] bytes = PlayerControlResponseCodec.Encode(response);
        var decoded = PlayerControlResponseCodec.Decode(bytes);
        Assert.Equal(bytes, PlayerControlResponseCodec.Encode(decoded));
        Assert.Equal(acquired.Witness, decoded.Board!.HeldWitness);
        Assert.All(decoded.Board.Slots, slot => Assert.True(slot.OwnerActorId == fixture.Grantee || slot.GranteeActorId == fixture.Grantee));
        string json = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("PrincipalId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionId", json, StringComparison.Ordinal);

        var command = fixture.Commands.Command(fixture.State, fixture.Grantee,
            new OfferControlPermissionIntent(fixture.Third, 0, WorldControlCapabilities.Inventory));
        var request = new PlayerControlRequest(PlayerControlRequestKind.SubmitPermission, Command: command);
        var decodedRequest = PlayerControlRequestCodec.Decode(PlayerControlRequestCodec.Encode(request));
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(decodedRequest.Command!));
        var receipt = await player.SubmitControlPermissionAsync(command);
        var reply = new PlayerControlResponse(board.WorldId, board.BranchId, board.HomeActorId,
            PlayerControlRequestKind.SubmitPermission, Receipt: receipt);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
            PlayerControlResponseCodec.Decode(PlayerControlResponseCodec.Encode(reply)).Receipt!));
        var release = new PlayerControlRequest(PlayerControlRequestKind.Release, Witness: acquired.Witness);
        Assert.Equal(acquired.Witness, PlayerControlRequestCodec.Decode(PlayerControlRequestCodec.Encode(release)).Witness);
    }

    [Fact]
    public async Task Control_decoding_rejects_shape_conflicts_foreign_board_and_noncanonical_values()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var player = (await fixture.Join(fixture.Grantee)).Player;
        var board = await player.ReadControlPermissionsAsync();
        var response = new PlayerControlResponse(board.WorldId, board.BranchId, board.HomeActorId,
            PlayerControlRequestKind.ReadPermissions, Board: board);
        byte[] bytes = PlayerControlResponseCodec.Encode(response);
        var json = JsonNode.Parse(bytes)!;
        json["Board"]!["HomeActorId"] = fixture.Third.ToString();
        Assert.Throws<FormatException>(() => PlayerControlResponseCodec.Decode(Encoding.UTF8.GetBytes(json.ToJsonString())));
        Assert.Throws<FormatException>(() => PlayerControlResponseCodec.Encode(response with { Kind = PlayerControlRequestKind.Release }));
        Assert.Throws<FormatException>(() => PlayerControlResponseCodec.Encode(response with { HomeActorId = fixture.Third }));
        string original = Encoding.UTF8.GetString(bytes);
        Assert.Throws<FormatException>(() => PlayerControlResponseCodec.Decode(Encoding.UTF8.GetBytes(
            original.Replace("\"Version\":1,", "\"Version\":1,\"Version\":1,", StringComparison.Ordinal))));
        Assert.Throws<FormatException>(() => PlayerControlResponseCodec.Decode(bytes.Concat(new byte[] { 0 }).ToArray()));
        Assert.Throws<FormatException>(() => PlayerControlRequestCodec.Decode(new byte[PlayerControlRequestCodec.MaximumBodyBytes + 1]));
        Assert.Throws<FormatException>(() => PlayerControlRequestCodec.Encode(new(PlayerControlRequestKind.Acquire)));
        byte[] read = PlayerControlRequestCodec.Encode(new(PlayerControlRequestKind.ReadPermissions));
        Assert.Equal(read, PlayerControlRequestCodec.Encode(PlayerControlRequestCodec.Decode(read)));
    }
}
