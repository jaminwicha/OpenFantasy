using System.Text;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Networking.Selection;

public sealed class SelectedCodecTests
{
    [Theory]
    [InlineData(WorldControlCapabilities.All)]
    [InlineData(WorldControlCapabilities.PrivateHistory)]
    [InlineData(WorldControlCapabilities.Production | WorldControlCapabilities.ConversationAndQuests)]
    public void Actual_scoped_observation_roundtrips_without_a_fake_inventory_or_full_controlled_view(WorldControlCapabilities capabilities)
    {
        using var fixture = new SelectedCodecFixture(capabilities);
        byte[] homeBytes = PlayerViewCodec.Encode(fixture.Frame.Observation.HomeView);
        byte[] encoded = PlayerSelectedObservationCodec.Encode(fixture.Frame);
        Assert.InRange(encoded.Length, 1, PlayerViewCodec.MaximumBodyBytes);
        var decoded = PlayerSelectedObservationCodec.Decode(encoded);
        Assert.Equal(encoded, PlayerSelectedObservationCodec.Encode(decoded));
        Assert.Equal(homeBytes, PlayerViewCodec.Encode(decoded.Observation.HomeView));
        Assert.Equal(fixture.Selection, decoded.Observation.Selection);
        Assert.Equal(fixture.Source.Grantee, decoded.Observation.HomeView.PlayerActorId);
        var controlled = Assert.IsType<LocalControlledActorView>(decoded.Observation.ControlledView);
        Assert.Equal(fixture.Source.Owner, controlled.ActorId);
        Assert.Equal(capabilities, controlled.Capabilities);
        if ((capabilities & WorldControlCapabilities.Inventory) == 0) Assert.Null(controlled.Inventory);
        if (controlled.Production?.CraftPreparation is { } craft)
        {
            var original = fixture.Frame.Observation.ControlledView!.Production!.CraftPreparation!;
            Assert.Equal(original.ProductionRevision, craft.ProductionRevision);
            Assert.Equal(original.CustodianId, craft.CustodianId);
            Assert.Equal(original.Ingredients, craft.Ingredients);
            foreach (var recipe in original.Recipes)
            {
                var expected = original.Prepare(recipe.RecipeId);
                var actual = craft.Prepare(recipe.RecipeId);
                Assert.Equal(expected?.RecipeId, actual?.RecipeId);
                Assert.Equal(expected?.ExpectedProductionRevision, actual?.ExpectedProductionRevision);
            }
        }
        if (controlled.Conversation?.Requests is { } requests)
            foreach (var quest in requests.Quests)
                Assert.Equal(fixture.Frame.Observation.ControlledView!.Conversation!.Requests!.Prepare(quest.DefinitionId),
                    requests.Prepare(quest.DefinitionId));
        Assert.Null(typeof(LocalControlledActorView).GetProperty("Progression"));
        Assert.DoesNotContain(typeof(LocalControlledActorView).GetFields(System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic), field => field.FieldType == typeof(LocalWorldView));
    }

    [Fact]
    public void Selected_inputs_preserve_witness_command_bytes_and_reject_cross_actor_or_trailing_payloads()
    {
        using var fixture = new SelectedCodecFixture();
        var selection = fixture.Selection;
        var command = fixture.Source.Delegated(fixture.State, new SpeakToActorIntent(fixture.Source.Other,
            fixture.State.Space.GetActor(fixture.Source.Other).Revision));
        var market = fixture.Frame.Observation.HomeView.Markets!.Stalls.First();
        PlayerSelectedInput[] inputs =
        [
            PlayerSelectedInput.SelectActor(selection, fixture.Source.Grantee), PlayerSelectedInput.Pause(selection),
            PlayerSelectedInput.Resume(selection, fixture.Frame.StreamId, fixture.Frame.Revision),
            PlayerSelectedInput.Movement(selection, 32, 0), PlayerSelectedInput.Command(selection, command),
            PlayerSelectedInput.Query(selection, LocalSelectedQueryOperation.SubmitTradeInquiry,
                new(market.MarketId, market.Commodity, LocalTradeDirection.Buy, 1)),
            PlayerSelectedInput.Query(selection, LocalSelectedQueryOperation.InspectPaidQuoteHistory, subjectId: new EntityId(0x5e1ec7c0dec, 4))
        ];
        foreach (var input in inputs)
        {
            byte[] bytes = PlayerSelectedInputCodec.Encode(input);
            var decoded = PlayerSelectedInputCodec.Decode(bytes);
            Assert.Equal(bytes, PlayerSelectedInputCodec.Encode(decoded));
            Assert.Equal(selection, decoded.Selection);
            Assert.Throws<FormatException>(() => PlayerSelectedInputCodec.Decode(bytes.Concat(new byte[] { 0 }).ToArray()));
        }
        var decodedCommand = PlayerSelectedInputCodec.Decode(PlayerSelectedInputCodec.Encode(inputs[4])).Action!;
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(decodedCommand));
        Assert.Equal(fixture.Source.Delivery(fixture.Source.Grantee).PrincipalId, decodedCommand.PrincipalId);
        Assert.Throws<FormatException>(() => PlayerSelectedInputCodec.Encode(PlayerSelectedInput.Command(
            selection with { ActorId = fixture.Source.Grantee, ControlWitness = null }, command)));
        Assert.Throws<FormatException>(() => PlayerSelectedInputCodec.Encode(PlayerSelectedInput.Movement(selection, 32, 32)));
        Assert.Throws<FormatException>(() => PlayerSelectedInputCodec.Encode(PlayerSelectedInput.Resume(selection, fixture.Frame.StreamId, 0)));
    }

    [Fact]
    public void Selected_observation_rejects_duplicate_unknown_missing_foreign_and_capability_leaking_fields()
    {
        using var fixture = new SelectedCodecFixture();
        byte[] encoded = PlayerSelectedObservationCodec.Encode(fixture.Frame);
        byte[] json = ObservationCompression.IsCompressed(encoded) ? ObservationCompression.Decode(encoded) : encoded;
        var original = JsonNode.Parse(json)!;
        var unknown = original.DeepClone(); unknown["Unexpected"] = 1;
        Refuse(unknown);
        var missing = original.DeepClone(); missing["Value"]!.AsObject().Remove("Revision"); Refuse(missing);
        var foreign = original.DeepClone(); foreign["Value"]!["Observation"]!["ControlledView"]!["ActorId"] =
            original["Value"]!["Observation"]!["Selection"]!["HomeActorId"]!.DeepClone(); Refuse(foreign);
        var leak = original.DeepClone(); leak["Value"]!["Observation"]!["ControlledView"]!["Capabilities"] = (int)WorldControlCapabilities.PrivateHistory;
        Refuse(leak);
        var preparation = original.DeepClone();
        preparation["Value"]!["Observation"]!["ControlledView"]!["Production"]!["CraftPreparation"]!["StorageRevision"] = ulong.MaxValue;
        Refuse(preparation);
        string text = Encoding.UTF8.GetString(json);
        RefuseBytes(Encoding.UTF8.GetBytes(text.Replace("\"Version\":1,", "\"Version\":1,\"Version\":1,", StringComparison.Ordinal)));
        RefuseBytes(Encoding.UTF8.GetBytes(text.Replace("\"Version\":1,", "\"Version\":1,\"\\u0056ersion\":1,", StringComparison.Ordinal)));
        Assert.Throws<FormatException>(() => PlayerSelectedObservationCodec.Decode(new byte[PlayerSelectedObservationCodec.MaximumBodyBytes + 1]));

        static void Refuse(JsonNode node) => RefuseBytes(Encoding.UTF8.GetBytes(node.ToJsonString()));
        static void RefuseBytes(byte[] bytes) => Assert.Throws<FormatException>(() => PlayerSelectedObservationCodec.Decode(
            bytes.Length > PlayerViewCodec.MaximumBodyBytes ? ObservationCompression.Encode(bytes) : bytes));
    }

    [Fact]
    public void Selected_query_response_cannot_survive_an_actor_cycle_or_mismatched_operation()
    {
        using var fixture = new SelectedCodecFixture();
        var id = new EntityId(0x5e1ec7c0dec, 7);
        var input = PlayerSelectedInput.Query(fixture.Selection, LocalSelectedQueryOperation.InspectPaidQuoteHistory, subjectId: id);
        var result = new LocalSelectedQueryResult<PaidQuoteView?>(fixture.State.WorldId, fixture.State.BranchId,
            fixture.Selection, LocalSelectedQueryOperation.InspectPaidQuoteHistory, id, null);
        var response = PlayerSelectedQueryResponseCodec.Decode(PlayerSelectedQueryResponseCodec.Encode(PlayerSelectedQueryResponse.From(result)));
        response.ValidateFor(input, fixture.State.WorldId, fixture.State.BranchId, fixture.Selection);
        Assert.Throws<FormatException>(() => response.ValidateFor(input, fixture.State.WorldId, fixture.State.BranchId,
            fixture.Selection with { Generation = fixture.Selection.Generation + 2 }));
        Assert.Throws<FormatException>(() => response.ValidateFor(PlayerSelectedInput.Query(fixture.Selection,
            LocalSelectedQueryOperation.InspectOpenPaidQuote, subjectId: id), fixture.State.WorldId, fixture.State.BranchId, fixture.Selection));
        Assert.Null(response.PaidQuote);
        Assert.Throws<FormatException>(() => PlayerSelectedQueryResponseCodec.Decode(
            PlayerSelectedQueryResponseCodec.Encode(response).Concat(new byte[] { 0 }).ToArray()));
    }
}
