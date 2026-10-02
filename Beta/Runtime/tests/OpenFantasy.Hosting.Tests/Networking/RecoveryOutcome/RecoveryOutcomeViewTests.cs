using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Client;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Networking.RecoveryOutcome;

public sealed class RecoveryOutcomeViewTests
{
    [Fact]
    public void Recoverable_defeat_keeps_the_consequence_of_declining_current_aftermath_care_visible()
    {
        var outcome = View(LocalOutcomeStage.AwaitingYourReading).EncounterOutcome!;
        var care = Client.EncounterOutcome.EncounterOutcomeTextTests.Care(LocalCareStatus.Available,
            Client.EncounterOutcome.EncounterOutcomeTextTests.Offer(true, WorldActionRejection.None));
        string choice = EncounterOutcomeText.ChoiceDetail(outcome, care);
        Assert.Contains("accepting declines that care", choice, StringComparison.Ordinal);
        foreach (string text in new[] { EncounterOutcomeText.ReadDetail(outcome, care), EncounterOutcomeText.Card(outcome, care) })
        {
            Assert.Contains("Wren can help now: ask for care first", text, StringComparison.Ordinal);
            Assert.Contains("Accepting declines that care", text, StringComparison.Ordinal);
            Assert.Contains("every required character has acknowledged it and no care is pending", text, StringComparison.Ordinal);
            Assert.Contains("Rescue is not guaranteed", text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(LocalOutcomeStage.AwaitingYourReading)]
    [InlineData(LocalOutcomeStage.AwaitingOthers)]
    [InlineData(LocalOutcomeStage.Completing)]
    [InlineData(LocalOutcomeStage.CarePending)]
    [InlineData(LocalOutcomeStage.Completed)]
    public void Recovery_interpretation_survives_wire_roundtrip_without_promising_a_rescue(LocalOutcomeStage stage)
    {
        LocalWorldView view = View(stage);
        byte[] bytes = PlayerViewCodec.Encode(view);
        Assert.Equal(PlayerViewCodec.AssistedRecoveryVersion, JsonNode.Parse(bytes)!["Version"]!.GetValue<int>());
        LocalWorldView restored = PlayerViewCodec.Decode(bytes);
        Assert.True(restored.EncounterOutcome!.AllowsAssistedRecovery);
        Assert.False(restored.EncounterOutcome.IsTerminal);
        Assert.Equal(bytes, PlayerViewCodec.Encode(restored));
        Assert.Contains("Rescue is not guaranteed", EncounterOutcomeText.ReadDetail(restored.EncounterOutcome), StringComparison.Ordinal);
        Assert.DoesNotContain("ordinary care cannot treat", EncounterOutcomeText.Card(restored.EncounterOutcome, null), StringComparison.Ordinal);
        if (stage == LocalOutcomeStage.Completed)
        {
            Assert.Contains("prior recovery permission", EncounterOutcomeText.Card(restored.EncounterOutcome, null), StringComparison.Ordinal);
            Assert.Contains("Incapacitated", EncounterOutcomeText.Status(restored.EncounterOutcome, null), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("version")]
    [InlineData("omitted")]
    [InlineData("false")]
    [InlineData("human")]
    public void Recovery_cannot_be_silently_downgraded_or_invented_in_legacy_interpretation(string mutation)
    {
        JsonNode json = JsonNode.Parse(PlayerViewCodec.Encode(View(LocalOutcomeStage.Completed)))!;
        JsonObject outcome = json["View"]!["EncounterOutcome"]!.AsObject();
        switch (mutation)
        {
            case "version": json["Version"] = PlayerViewCodec.ControllerAcceptanceVersion; break;
            case "omitted": outcome.Remove("AllowsAssistedRecovery"); break;
            case "false": outcome["AllowsAssistedRecovery"] = false; break;
            case "human": outcome["AcknowledgmentMode"] = 0; break;
        }
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Fact]
    public void Replica_delta_enters_and_leaves_recovery_interpretation_without_retaining_a_stale_permission_claim()
    {
        LocalWorldView baseline = new(Genesis(), Id(10));
        LocalWorldView outcome = View(LocalOutcomeStage.Completed);
        var stream = new PlayerObservationStream();
        var replica = new PlayerObservationReplica(baseline.WorldId, baseline.BranchId, baseline.PlayerActorId);
        foreach (LocalWorldView current in new[] { baseline, outcome, baseline })
        {
            var frame = stream.Capture(current)!;
            Assert.Equal(PlayerObservationApplyResult.Applied,
                replica.Apply(PlayerObservationFrameCodec.Decode(PlayerObservationFrameCodec.Encode(frame))));
            Assert.Equal(PlayerViewCodec.Encode(current), PlayerViewCodec.Encode(replica.ReadView()!));
        }
        Assert.Null(replica.ReadView()!.EncounterOutcome);
    }

    private static LocalWorldView View(LocalOutcomeStage stage)
    {
        var outcome = new LocalEncounterOutcomeView(stage, Id(900), EncounterOutcomeResult.Victory, 2,
            LocalOutcomeStanding.OpponentStands,
            [new(Id(10), "Traveler", 1, false, true, new LocalOutcomeCondition(0, 100)),
                new(Id(11), "Opponent", 2, true, false, null)],
            stage == LocalOutcomeStage.AwaitingYourReading
                ? new AcknowledgeEncounterOutcomeIntent(Id(900), ContentHash.Compute("recovery-view"u8)) : null,
            1, stage is LocalOutcomeStage.Completing or LocalOutcomeStage.Completed ? 0 : 1,
            stage is LocalOutcomeStage.Completing or LocalOutcomeStage.Completed,
            LocalOutcomeRestriction.Incapacitated, 100, stage == LocalOutcomeStage.Completed ? 10UL : null,
            LocalOutcomeAcknowledgmentMode.ControllerAcceptance, true);
        LocalWorldView basis = new(Genesis(), Id(10));
        return (WorldViewData.From(basis) with { EncounterOutcome = outcome }).ToView();
    }
}
