using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Networking.ControllerOutcome;

public sealed class ControllerOutcomeViewCodecTests
{
    [Fact]
    public void Observation_deltas_enter_and_leave_controller_acceptance_without_losing_the_interpretation()
    {
        LocalWorldView exploring = new(Genesis(), Id(10));
        LocalWorldView outcome = View(LocalOutcomeStage.Completed, LocalOutcomeAcknowledgmentMode.ControllerAcceptance);
        var stream = new PlayerObservationStream();
        var replica = new PlayerObservationReplica(exploring.WorldId, exploring.BranchId, exploring.PlayerActorId);
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(Wire(stream.Capture(exploring)!)));
        PlayerObservationFrame entering = Wire(stream.Capture(outcome)!);
        Assert.False(entering.IsFull);
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(entering));
        Assert.Equal(LocalOutcomeAcknowledgmentMode.ControllerAcceptance, replica.ReadView()!.EncounterOutcome!.AcknowledgmentMode);
        Assert.Equal(PlayerViewCodec.Encode(outcome), PlayerViewCodec.Encode(replica.ReadView()!));
        PlayerObservationFrame leaving = Wire(stream.Capture(exploring)!);
        Assert.False(leaving.IsFull);
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(leaving));
        Assert.Null(replica.ReadView()!.EncounterOutcome);
        Assert.Equal(PlayerViewCodec.Encode(exploring), PlayerViewCodec.Encode(replica.ReadView()!));

        static PlayerObservationFrame Wire(PlayerObservationFrame frame) =>
            PlayerObservationFrameCodec.Decode(PlayerObservationFrameCodec.Encode(frame));
    }

    [Theory]
    [InlineData(LocalOutcomeStage.AwaitingYourReading)]
    [InlineData(LocalOutcomeStage.AwaitingOthers)]
    [InlineData(LocalOutcomeStage.Completing)]
    [InlineData(LocalOutcomeStage.CarePending)]
    [InlineData(LocalOutcomeStage.Completed)]
    public void Controller_acceptance_survives_the_actual_observation_codec_at_every_stage(LocalOutcomeStage stage)
    {
        LocalWorldView original = View(stage, LocalOutcomeAcknowledgmentMode.ControllerAcceptance);
        byte[] bytes = PlayerViewCodec.Encode(original);
        Assert.Equal(PlayerViewCodec.ControllerAcceptanceVersion, JsonNode.Parse(bytes)!["Version"]!.GetValue<int>());
        Assert.Contains("\"AcknowledgmentMode\":1", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        LocalWorldView decoded = PlayerViewCodec.Decode(bytes);
        Assert.Equal(LocalOutcomeAcknowledgmentMode.ControllerAcceptance, decoded.EncounterOutcome!.AcknowledgmentMode);
        Assert.Equal(stage, decoded.EncounterOutcome.Stage);
        Assert.Equal(original.EncounterOutcome!.ObserverHasRead, decoded.EncounterOutcome.ObserverHasRead);
        Assert.Equal(original.EncounterOutcome.Acknowledgment, decoded.EncounterOutcome.Acknowledgment);
        Assert.Equal(bytes, PlayerViewCodec.Encode(decoded));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(decoded));
    }

    [Fact]
    public void Acceptance_interpretation_cannot_be_downgraded_or_silently_defaulted_inside_its_version()
    {
        byte[] bytes = PlayerViewCodec.Encode(View(LocalOutcomeStage.Completed, LocalOutcomeAcknowledgmentMode.ControllerAcceptance));
        JsonNode downgraded = JsonNode.Parse(bytes)!;
        downgraded["Version"] = PlayerViewCodec.Version;
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(downgraded)));
        JsonNode omitted = JsonNode.Parse(bytes)!;
        omitted["View"]!["EncounterOutcome"]!.AsObject().Remove("AcknowledgmentMode");
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(omitted)));
        JsonNode legacy = JsonNode.Parse(PlayerViewCodec.Encode(View(LocalOutcomeStage.Completed, LocalOutcomeAcknowledgmentMode.HumanReading)))!;
        legacy["Version"] = PlayerViewCodec.ControllerAcceptanceVersion;
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(legacy)));
    }

    [Fact]
    public void Legacy_default_stays_omitted_and_explicit_default_is_noncanonical()
    {
        LocalWorldView view = View(LocalOutcomeStage.AwaitingYourReading, LocalOutcomeAcknowledgmentMode.HumanReading);
        byte[] original = PlayerViewCodec.Encode(view);
        Assert.DoesNotContain("AcknowledgmentMode", Encoding.UTF8.GetString(original), StringComparison.Ordinal);
        Assert.DoesNotContain("AcknowledgmentMode", JsonSerializer.Serialize(view), StringComparison.Ordinal);
        LocalWorldView decoded = PlayerViewCodec.Decode(original);
        Assert.Equal(LocalOutcomeAcknowledgmentMode.HumanReading, decoded.EncounterOutcome!.AcknowledgmentMode);
        Assert.Equal(original, PlayerViewCodec.Encode(decoded));

        JsonNode body = JsonNode.Parse(original)!;
        body["View"]!["EncounterOutcome"]!["AcknowledgmentMode"] = 0;
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(body)));
    }

    [Theory]
    [InlineData("unknown-positive")]
    [InlineData("negative")]
    [InlineData("null")]
    [InlineData("string")]
    [InlineData("duplicate")]
    public void Invalid_controller_mode_never_falls_back_to_human_reading(string mutation)
    {
        byte[] encoded = PlayerViewCodec.Encode(View(LocalOutcomeStage.Completed, LocalOutcomeAcknowledgmentMode.ControllerAcceptance));
        byte[] malformed;
        if (mutation == "duplicate")
        {
            malformed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(encoded).Replace("\"AcknowledgmentMode\":1",
                "\"AcknowledgmentMode\":1,\"AcknowledgmentMode\":0", StringComparison.Ordinal));
        }
        else
        {
            JsonNode body = JsonNode.Parse(encoded)!;
            JsonNode outcome = body["View"]!["EncounterOutcome"]!;
            switch (mutation)
            {
                case "unknown-positive": outcome["AcknowledgmentMode"] = 2; break;
                case "negative": outcome["AcknowledgmentMode"] = -1; break;
                case "null": outcome["AcknowledgmentMode"] = null; break;
                case "string": outcome["AcknowledgmentMode"] = "ControllerAcceptance"; break;
            }
            malformed = JsonSerializer.SerializeToUtf8Bytes(body);
        }
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(malformed));
    }

    [Fact]
    public void Constructor_refuses_unknown_modes_before_publication()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => View(LocalOutcomeStage.Completed, (LocalOutcomeAcknowledgmentMode)2));
    }

    private static LocalWorldView View(LocalOutcomeStage stage, LocalOutcomeAcknowledgmentMode mode)
    {
        LocalWorldView source = new(Genesis(), Id(10));
        bool pending = stage == LocalOutcomeStage.AwaitingYourReading;
        var outcome = new LocalEncounterOutcomeView(stage, Id(900), EncounterOutcomeResult.Victory, 1,
            LocalOutcomeStanding.YourSideStands,
            [new(Id(10), "Traveler", 1, true, true, new LocalOutcomeCondition(20, 100)),
                new(Id(11), "Opponent", 2, false, false, null)],
            pending ? new AcknowledgeEncounterOutcomeIntent(Id(900), ContentHash.Compute("controller-outcome"u8)) : null,
            2, stage is LocalOutcomeStage.Completing or LocalOutcomeStage.Completed ? 0 : 1,
            stage is LocalOutcomeStage.AwaitingOthers or LocalOutcomeStage.Completing or LocalOutcomeStage.Completed,
            LocalOutcomeRestriction.AwaitingCompletion, 0, stage == LocalOutcomeStage.Completed ? 10UL : null, mode);
        return (WorldViewData.From(source) with { EncounterOutcome = outcome }).ToView();
    }
}
