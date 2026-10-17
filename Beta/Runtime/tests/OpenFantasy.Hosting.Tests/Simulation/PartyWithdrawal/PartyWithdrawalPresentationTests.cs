using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Retreat;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalPresentationTests
{
    [Fact]
    public void Each_observer_gets_only_their_exact_choice_and_complete_public_side_in_both_wire_formats()
    {
        var run = WithdrawalWorldFixture.Active();
        SettleNpcChoices(run);
        var commands = run.Humans.Select(run.Retreat).ToArray();
        run.Choose(commands);
        foreach (var actor in run.Humans)
        {
            var view = new LocalWorldView(run.State, actor);
            var own = commands.Single(command => command.ActorId == actor);
            var other = commands.Single(command => command.ActorId != actor);
            var bytes = PlayerViewCodec.EncodeWithCollectiveWithdrawal(view);
            Assert.Equal(PlayerViewCodec.CollectiveWithdrawalVersion, JsonNode.Parse(bytes)!["Version"]!.GetValue<int>());
            var restored = PlayerViewCodec.Decode(bytes);
            Assert.Equal(bytes, PlayerViewCodec.EncodeWithCollectiveWithdrawal(restored));
            var withdrawal = Assert.IsType<LocalWithdrawalView>(restored.Retreat!.Withdrawal);
            Assert.Equal(own.CommandId, withdrawal.Choice!.CommandId);
            Assert.Equal(own.CommandId, withdrawal.Cancellation!.WithdrawalCommandId);
            Assert.Equal(run.Humans.Order(), withdrawal.Members.Select(member => member.ActorId));
            Assert.All(withdrawal.Members, member => Assert.Equal(LocalWithdrawalReadiness.Walking, member.Readiness));
            var json = JsonNode.Parse(bytes);
            Assert.True(ContainsIdentity(json, JsonSerializer.SerializeToNode(own.CommandId)!));
            Assert.False(ContainsIdentity(json, JsonSerializer.SerializeToNode(other.CommandId)!));
            Assert.Throws<FormatException>(() => PlayerViewCodec.Encode(view));
            Assert.Throws<FormatException>(() => PlayerViewCodec.EncodeWithCarePermissionDescriptions(view));

            var frame = HomeFrame(view).WithConstruction().WithCarePermissionDescriptions().WithCollectiveWithdrawal();
            var selected = PlayerSelectedObservationCodec.Encode(frame);
            Assert.Equal(PlayerSelectedObservationCodec.CollectiveWithdrawalVersion,
                PlayerSelectedObservationCodec.Decode(selected).PresentationVersion);
            Assert.Equal(selected, PlayerSelectedObservationCodec.Encode(PlayerSelectedObservationCodec.Decode(selected)));
            Assert.Throws<FormatException>(() => PlayerSelectedObservationCodec.Encode(frame with { PresentationVersion = 4 }));
        }
    }

    [Fact]
    public void Foreign_commands_observers_missing_members_and_downgraded_versions_are_rejected()
    {
        var run = WithdrawalWorldFixture.Active();
        SettleNpcChoices(run);
        run.Choose(run.Humans.Select(run.Retreat).ToArray());
        var bytes = PlayerViewCodec.EncodeWithCollectiveWithdrawal(new LocalWorldView(run.State, run.Humans[0]));
        var options = PlayerViewCodec.CreateOptions();
        LocalWorldView Read(JsonNode json) => JsonSerializer.Deserialize<LocalWorldView>(json["View"]!.ToJsonString(), options)!;
        WithdrawalObservationValidation.Validate(Read(JsonNode.Parse(bytes)!), included: true);
        void Reject(Action<JsonNode> mutate)
        {
            var json = JsonNode.Parse(bytes)!;
            mutate(json);
            Assert.Throws<FormatException>(() => WithdrawalObservationValidation.Validate(Read(json), json["Version"]!.GetValue<int>() == 8));
            Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(Encoding.UTF8.GetBytes(json.ToJsonString())));
        }
        Reject(json => json["Version"] = 7);
        Reject(json => json["View"]!["Retreat"]!["Withdrawal"]!["ActorId"] = JsonSerializer.SerializeToNode(run.Humans[1], options));
        Reject(json => json["View"]!["Retreat"]!["Withdrawal"]!["Cancellation"]!["WithdrawalCommandId"] = JsonSerializer.SerializeToNode(run.NextId(), options));
        Reject(json => json["View"]!["Retreat"]!["Withdrawal"]!["Cancellation"]!["ExpectedTimingRevision"] = 0);
        Reject(json => json["View"]!["Retreat"]!["Withdrawal"]!["Members"]!.AsArray().RemoveAt(0));
        Reject(json => json["View"]!["Retreat"]!["Withdrawal"]!["Members"]![0]!["Readiness"] = (int)LocalWithdrawalReadiness.Incapacitated);
        Reject(json => json["View"]!["Retreat"]!.AsObject().Remove("Withdrawal"));
    }

    [Fact]
    public void The_retained_shared_adventure_observation_reencodes_byte_for_byte()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "withdrawal-legacy-home-v4.json"));
        Assert.Equal("63ea4a594696c253d0984012ac017ea323f7151a7bf40210e0e98b538194c9e5", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        Assert.Equal(bytes, PlayerViewCodec.Encode(PlayerViewCodec.Decode(bytes)));
    }

    [Fact]
    public void Older_worlds_keep_their_exact_shape_and_new_versions_cannot_fabricate_collective_state()
    {
        var view = new LocalWorldView(WithdrawalWorldFixture.Legacy, WithdrawalWorldFixture.Legacy.Controllers!.Assignments[0].ActorId);
        var bytes = PlayerViewCodec.EncodeWithCarePermissionDescriptions(view);
        Assert.Equal(bytes, PlayerViewCodec.EncodeWithCollectiveWithdrawal(view));
        Assert.DoesNotContain("Withdrawal", Encoding.UTF8.GetString(bytes));
        var frame = HomeFrame(view).WithConstruction().WithCarePermissionDescriptions();
        Assert.Equal(PlayerSelectedObservationCodec.Encode(frame), PlayerSelectedObservationCodec.Encode(frame.WithCollectiveWithdrawal()));
        Assert.Throws<FormatException>(() => PlayerSelectedObservationCodec.Encode(frame with { PresentationVersion = 5 }));
        var json = JsonNode.Parse(bytes)!;
        json["Version"] = 8;
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void The_existing_wheel_slot_cancels_the_exact_choice_and_announces_its_end_once()
    {
        var run = WithdrawalWorldFixture.Active();
        SettleNpcChoices(run);
        var actor = run.Humans[0];
        var before = new LocalWorldView(run.State, actor);
        var notice = new RetreatOutcomeNotice();
        Assert.Null(notice.Observe(before.Retreat));
        var route = run.Retreat(actor);
        run.Choose(route);
        var view = new LocalWorldView(run.State, actor);
        var offered = RetreatOffers.Create(view.Retreat!, FarmActionScope.From(run.Delivery(actor), view));
        Assert.True(offered.CanExecute, offered.BlockingReason);
        Assert.Equal("Stop retreat", offered.Label);
        Assert.Equal(FarmSemanticAction.Retreat, offered.Action);
        var cancellation = Assert.IsType<CancelEncounterWithdrawalIntent>(offered.Intent);
        Assert.Equal(route.CommandId, cancellation.WithdrawalCommandId);
        Assert.Contains("need a route", offered.DetailText);
        Assert.Contains("does not move you or refund", offered.DetailText);
        run.Choose(run.Command(actor, cancellation));
        var ended = new LocalWorldView(run.State, actor);
        Assert.Contains("Paid recovery remains", notice.Observe(ended.Retreat));
        Assert.Null(notice.Observe(ended.Retreat));
        Assert.Null(new RetreatOutcomeNotice().Observe(ended.Retreat));
    }

    [Fact]
    public void The_selected_wheel_hides_withdrawal_outside_encounters()
    {
        using var fixture = new OpenFantasy.Hosting.Tests.Networking.Selection.SelectedCodecFixture();
        var observation = fixture.Frame.Observation;
        Assert.Null(observation.ControlledView!.Combat!.Encounter);
        var scope = FarmActionScope.From(fixture.Source.Delivery(fixture.Source.Grantee), observation);
        var offers = FarmSelectedActionOffers.Create(observation, scope, null, fixture.Source.Other);
        Assert.False(offers.ContainsKey(FarmSemanticAction.Retreat));
    }

    [Theory]
    [InlineData(WorldControlCapabilities.Combat | WorldControlCapabilities.Traversal)]
    [InlineData(WorldControlCapabilities.Combat)]
    public void Delegated_disclosure_requires_both_capabilities_and_never_includes_completed_history(WorldControlCapabilities capabilities)
    {
        var run = WithdrawalWorldFixture.Active();
        SettleNpcChoices(run);
        var owner = run.Humans[0];
        var grantee = run.Humans[1];

        var offer = run.Command(owner, new OfferControlPermissionIntent(grantee, 0, capabilities));
        Assert.Equal(WorldActionRejection.None, Assert.Single(run.Apply(WorldBatchKind.ControlPermission, [offer]).Receipts).Rejection);
        var grant = run.State.ControlPermissions!.Find(owner, grantee)!;
        Assert.Equal(WorldActionRejection.None, Assert.Single(run.Apply(WorldBatchKind.ControlPermission,
            [run.Command(grantee, new AcceptControlPermissionIntent(owner, grant.Revision, offer.CommandId, grant.TermsHash!))]).Receipts).Rejection);
        run.ControllerEvent(owner, WorldControllerEventKind.Disconnected);
        // Explicit Core expiry input; this test proves projection, not elapsed host allowance.
        run.ControllerEvent(owner, WorldControllerEventKind.AllowanceExpired);
        grant = run.State.ControlPermissions!.Find(owner, grantee)!;
        var controller = run.State.Controllers!.Get(owner);
        var witness = new WorldControlWitness(owner, grantee, grant.GrantCommandId!.Value, grant.AcceptanceCommandId!.Value,
            grant.Revision, capabilities, grant.TermsHash!, run.NextId(), 1, run.State.Revision + 1,
            controller.DisconnectId!.Value, controller.Revision);
        run.Apply(WorldBatchKind.ControlAssignment, assignmentEvent: new(owner, WorldControlAssignmentEventKind.Acquire, 0, witness));
        var delivery = run.Delivery(grantee);
        var own = run.Retreat(owner);
        var route = new WorldActionCommand(own.CommandId, delivery.PrincipalId, delivery.SessionId, owner,
            own.WorldId, own.BranchId, own.ExpectedActorRevision, own.Intent, witness);
        if ((capabilities & WorldControlCapabilities.Traversal) != 0) run.Choose(route);
        LocalControlledActorView Project() => LocalControlledActorView.Project(run.State, grantee, delivery, witness);
        var scoped = Project();
        var home = new LocalWorldView(run.State, grantee);
        var frame = HomeFrame(home);
        frame = frame with { Observation = frame.Observation with
        {
            Selection = frame.Observation.Selection with { ActorId = owner, ControlWitness = witness }, ControlledView = scoped
        }};
        frame = frame.WithConstruction().WithCarePermissionDescriptions().WithCollectiveWithdrawal();
        var bytes = PlayerSelectedObservationCodec.Encode(frame);
        var restored = PlayerSelectedObservationCodec.Decode(bytes).Observation.ControlledView!;
        Assert.Equal(bytes, PlayerSelectedObservationCodec.Encode(PlayerSelectedObservationCodec.Decode(bytes)));
        if ((capabilities & WorldControlCapabilities.Traversal) == 0)
        {
            Assert.Null(restored.Retreat);
            return;
        }
        Assert.Equal(route.CommandId, restored.Retreat!.Withdrawal!.Cancellation!.WithdrawalCommandId);
        var selectedOffers = FarmSelectedActionOffers.Create(frame.Observation,
            FarmActionScope.From(delivery, frame.Observation), null, grantee);
        Assert.Equal("Stop retreat", selectedOffers[FarmSemanticAction.Retreat].Label);
        Assert.Equal(restored.Retreat.Withdrawal.Cancellation, selectedOffers[FarmSemanticAction.Retreat].Intent);
        var cancel = restored.Retreat.Withdrawal.Cancellation;
        run.Choose(new WorldActionCommand(run.NextId(), delivery.PrincipalId, delivery.SessionId, owner,
            run.State.WorldId, run.State.BranchId, run.State.Space.GetActor(owner).Revision, cancel, witness));
        Assert.NotNull(new LocalWorldView(run.State, owner).Retreat!.Withdrawal!.Last);
        Assert.Null(Project().Retreat!.Withdrawal!.Last);
        var revoke = run.Command(owner, new RevokeControlPermissionIntent(grantee, grant.Revision));
        Assert.Equal(WorldActionRejection.None, Assert.Single(run.Apply(WorldBatchKind.ControlPermission, [revoke]).Receipts).Rejection);
        Assert.Throws<ArgumentException>(() => Project());
    }

    private static bool ContainsIdentity(JsonNode? node, JsonNode identity) =>
        JsonNode.DeepEquals(node, identity) || node switch
        {
            JsonObject value => value.Any(property => ContainsIdentity(property.Value, identity)),
            JsonArray value => value.Any(item => ContainsIdentity(item, identity)),
            _ => false
        };

    private static void SettleNpcChoices(WithdrawalWorldFixture run)
    {
        var waiting = WorldEncounterActions.DueActors(run.State)
            .Where(actor => !run.Humans.Contains(actor.ActorId)).Select(actor => run.Wait(actor.ActorId)).ToArray();
        if (waiting.Length != 0) run.Choose(waiting);
    }

    private static PlayerSelectedObservation HomeFrame(LocalWorldView view)
    {
        EntityId port = new(0x5749544856494557, 1);
        return new(new(0x5749544856494557, 2), 1,
            new(new(port, view.PlayerActorId, view.PlayerActorId, 1, null), view, null,
                new(PlayerControlState.Available, port, view.PlayerActorId, 0), false));
    }
}
