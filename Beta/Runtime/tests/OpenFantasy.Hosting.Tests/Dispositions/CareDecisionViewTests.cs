using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Dispositions;

public sealed class CareDecisionViewTests
{
    private static readonly EntityId Request = Id(900);
    private static readonly EntityId Encounter = Id(901);

    [Fact]
    public void Explicit_empty_capability_selects_version_four_while_legacy_bytes_omit_it()
    {
        LocalWorldView legacy = new(Genesis(), Id(10));
        byte[] before = PlayerViewCodec.Encode(legacy);
        Assert.DoesNotContain("SupportsCareDispositions", System.Text.Encoding.UTF8.GetString(before), StringComparison.Ordinal);
        LocalWorldView capable = (WorldViewData.From(legacy) with { SupportsCareDispositions = true }).ToView();
        byte[] bytes = PlayerViewCodec.Encode(capable);
        Assert.Equal(4, JsonNode.Parse(bytes)!["Version"]!.GetValue<int>());
        Assert.True(PlayerViewCodec.Decode(bytes).SupportsCareDispositions);
        Assert.Equal(before, PlayerViewCodec.Encode((WorldViewData.From(capable) with { SupportsCareDispositions = false }).ToView()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_participant_choices_roundtrip_and_inspection_does_not_submit_or_substitute(bool helper)
    {
        LocalWorldView view = Pending(helper);
        byte[] bytes = PlayerViewCodec.Encode(view);
        LocalWorldView restored = PlayerViewCodec.Decode(bytes);
        Assert.Equal(bytes, PlayerViewCodec.Encode(restored));
        var scope = FarmActionScope.From(new TrustedCommandDelivery(Id(700), Id(701)), restored);
        var offers = EncounterActionOffers.Create(restored, scope, null)!;
        FarmSemanticAction action = helper ? FarmSemanticAction.DeclineCare : FarmSemanticAction.CancelCare;
        Assert.Single(offers);
        FarmActionOffer offer = offers[action];
        Assert.True(offer.CanExecute);
        Assert.Contains("Health, medicine", offer.DetailText, StringComparison.Ordinal);
        Assert.Contains("separate choice", offer.DetailText, StringComparison.Ordinal);
        var wheel = new FarmQuickWheelModel(offers);
        FarmWheelSlot slot = Assert.Single(wheel.Slots, value => value is not null)!;
        Assert.Equal(offer.Intent, slot.Offer.Intent);
        Assert.Equal(offer.Intent, slot.Offer.ToCommand(Id(799)).Intent);
        LocalCareDecisionView changed = helper
            ? view.Care!.Decision! with { Decline = new DeclinePendingCareIntent(Request, Encounter, 4) }
            : view.Care!.Decision! with { Cancellation = new CancelPendingCareIntent(Request, Encounter, 4) };
        LocalWorldView later = (WorldViewData.From(view) with
        {
            Care = new LocalCareView([], LocalCareStatus.Pending, Request, Id(11), "Helper", changed, null)
        }).ToView();
        wheel.Refresh(_ => EncounterActionOffers.Create(later, scope, null)!);
        Assert.False(slot.Offer.CanExecute);
        Assert.Null(slot.Offer.Intent);
        Assert.Equal(bytes, PlayerViewCodec.Encode(view));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("capability")]
    [InlineData("patient")]
    [InlineData("request")]
    [InlineData("encounter")]
    [InlineData("helper-leak")]
    public void Contradictory_observation_is_rejected(string mutation)
    {
        JsonNode json = JsonNode.Parse(PlayerViewCodec.Encode(Pending(false)))!;
        JsonObject view = json["View"]!.AsObject();
        JsonObject care = view["Care"]!.AsObject();
        JsonObject decision = care["Decision"]!.AsObject();
        switch (mutation)
        {
            case "version": json["Version"] = 3; break;
            case "capability": view.Remove("SupportsCareDispositions"); break;
            case "patient": decision["PatientActorId"] = care["HelperActorId"]!.DeepClone(); break;
            case "request": decision["Cancellation"]!["AcknowledgmentId"] = care["HelperActorId"]!.DeepClone(); break;
            case "encounter": decision["Cancellation"]!["EncounterId"] = care["HelperActorId"]!.DeepClone(); break;
            case "helper-leak":
                JsonNode helper = JsonNode.Parse(PlayerViewCodec.Encode(Pending(true)))!;
                decision["Decline"] = helper["View"]!["Care"]!["Decision"]!["Decline"]!.DeepClone();
                break;
        }
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Theory]
    [InlineData(LocalCareClosureKind.PatientCancelled, LocalCareStatus.Cancelled, "cancelled")]
    [InlineData(LocalCareClosureKind.HelperDeclined, LocalCareStatus.Declined, "declined")]
    [InlineData(LocalCareClosureKind.Treated, LocalCareStatus.Treated, "administered")]
    public void Terminal_record_names_the_actual_result_and_has_no_repeat_choice(LocalCareClosureKind kind,
        LocalCareStatus status, string word)
    {
        LocalWorldView view = (WorldViewData.From(Pending(false)) with
        {
            Care = new LocalCareView([], status, null, helperActorId: Id(11), helperName: "Helper", decision: null,
                closure: new LocalCareClosureView(Request, Id(10), kind))
        }).ToView();
        LocalWorldView restored = PlayerViewCodec.Decode(PlayerViewCodec.Encode(view));
        Assert.Contains(word, EncounterOutcomeText.CareReason(restored.Care), StringComparison.Ordinal);
        if (kind != LocalCareClosureKind.Treated)
            Assert.Contains("no treatment was given", EncounterOutcomeText.CareReason(restored.Care), StringComparison.Ordinal);
        Assert.Null(restored.Care!.Decision);
    }

    [Fact]
    public void Delta_replaces_pending_choice_with_closure_without_retaining_a_stale_cancellation()
    {
        var pending = Pending(false);
        var closed = (WorldViewData.From(pending) with { Care = new LocalCareView([], LocalCareStatus.Cancelled, null,
            helperActorId: Id(11), helperName: "Helper", decision: null, closure: new(Request, Id(10), LocalCareClosureKind.PatientCancelled)) }).ToView();
        var stream = new PlayerObservationStream();
        var replica = new PlayerObservationReplica(pending.WorldId, pending.BranchId, pending.PlayerActorId);
        foreach (var view in new[] { pending, closed })
            Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(stream.Capture(view)!));
        Assert.Null(replica.ReadView()!.Care!.Decision);
        Assert.Equal(LocalCareStatus.Cancelled, replica.ReadView()!.Care!.Status);
    }

    private static LocalWorldView Pending(bool helper)
    {
        LocalWorldView basis = new(Genesis(), Id(10));
        var decision = new LocalCareDecisionView(Id(10), "Patient",
            helper ? null : new CancelPendingCareIntent(Request, Encounter, 3),
            helper ? new DeclinePendingCareIntent(Request, Encounter, 3) : null, null);
        return (WorldViewData.From(basis) with
        {
            PlayerActorId = Id(helper ? 11UL : 10UL), SupportsCareDispositions = true,
            Encounter = new LocalEncounterView(Encounter, 2, 0, false, false, false, true),
            Care = new LocalCareView([], LocalCareStatus.Pending, Request, Id(11), "Helper", decision, null)
        }).ToView();
    }
}
