using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Tests.Care.Dispositions;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Networking.Selection;

public sealed class CareSelectedCodecTests
{
    [Theory]
    [InlineData(false, WorldControlCapabilities.Recovery)]
    [InlineData(true, WorldControlCapabilities.Recovery)]
    [InlineData(false, WorldControlCapabilities.CareDisposition)]
    [InlineData(true, WorldControlCapabilities.CareDisposition)]
    public void Actual_pending_care_roundtrips_only_the_selected_participants_permitted_decisions(
        bool selectHelper, WorldControlCapabilities capabilities)
    {
        // Both participants are humans in the original admission. This is a managed authority and
        // codec journey; it does not establish live transport custody, native presentation or input.
        var fixture = new CareDispositionJournalFixture(humanHelper: true);
        var genesis = WorldDelegatedControlAdmission.CreateFresh(
            WorldControlPermissionAdmission.CreateFresh(fixture.Genesis));
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        EntityId selectedActor = selectHelper ? fixture.Journey.Helper : fixture.Journey.Patient;
        EntityId homeActor = selectHelper ? fixture.Journey.Patient : fixture.Journey.Helper;
        var commands = fixture.Journey.Commands;

        var offer = commands.Command(genesis, selectedActor,
            new OfferControlPermissionIntent(homeActor, 0, capabilities));
        var state = fixture.Journey.Commit(authority, WorldBatchKind.ControlPermission, [offer]).Candidate;
        var grant = state.ControlPermissions!.Find(selectedActor, homeActor)!;
        state = fixture.Journey.Commit(authority, WorldBatchKind.ControlPermission,
            [commands.Command(state, homeActor, new AcceptControlPermissionIntent(selectedActor,
                grant.Revision, offer.CommandId, grant.TermsHash!))]).Candidate;

        // Real travel, combat and the patient's accepted acknowledgment create the request. No view
        // or root is patched to manufacture a pending decision or an owned dose.
        state = fixture.RequestAfterDefeat(authority);
        var pending = Assert.IsType<CareAcknowledgment>(state.Care!.Pending);
        Assert.Equal(0, state.Progression!.GetActor(fixture.Journey.Patient).CurrentHp);
        Assert.Equal(WorldControllerMode.Human, state.Controllers!.Get(homeActor).Mode);
        var dose = Assert.IsType<AdministerRemedyIntent>(WorldCareActions.PrepareRemedy(state));
        byte[] pendingCare = state.Care.ContentHash.ToByteArray();
        var inventoryHash = state.Inventory.ContentHash;
        var progressionHash = state.Progression.ContentHash;

        state = ControllerEvent(WorldControllerEventKind.Disconnected);
        var segment = authority.BeginControllerRecoverySegment();
        authority.CheckpointControllerAllowances(segment.Generation, segment.Allowances.Select(row =>
            row.ActorId == selectedActor ? row with { ElapsedHostMilliseconds = 30_000 } : row).ToArray());
        state = ControllerEvent(WorldControllerEventKind.AllowanceExpired);
        grant = state.ControlPermissions!.Find(selectedActor, homeActor)!;
        var controller = state.Controllers!.Get(selectedActor);
        var witness = new WorldControlWitness(selectedActor, homeActor, grant.GrantCommandId!.Value,
            grant.AcceptanceCommandId!.Value, grant.Revision, capabilities, grant.TermsHash!, commands.NextId(),
            1, state.Revision + 1, controller.DisconnectId!.Value, controller.Revision);
        state = authority.Commit(new WorldStepBatch(witness.AssignmentId, state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.ControlAssignment, [], [], state.ProfileVersion,
            controlAssignmentEvent: new(selectedActor, WorldControlAssignmentEventKind.Acquire, 0, witness))).Candidate;

        var delivery = ConstructionUpgradeFixture.Delivery(state, homeActor);
        var selection = new LocalActorSelection(commands.NextId(), homeActor, selectedActor, 1, witness);
        var home = new LocalWorldView(state, homeActor, regions: new LocalRegionObserver(homeActor));
        var controlled = LocalControlledActorView.Project(state, homeActor, delivery, witness,
            regions: new LocalRegionObserver(selectedActor));
        var frame = new PlayerSelectedObservation(commands.NextId(), 1, new(selection, home, controlled,
            new(PlayerControlState.Available, commands.NextId(), homeActor, 0), false));
        byte[] homeBytes = PlayerViewCodec.Encode(home);
        byte[] encoded = PlayerSelectedObservationCodec.Encode(frame);
        var decoded = PlayerSelectedObservationCodec.Decode(encoded);
        Assert.Equal(encoded, PlayerSelectedObservationCodec.Encode(decoded));
        Assert.Equal(homeBytes, PlayerViewCodec.Encode(decoded.Observation.HomeView));
        var restored = Assert.IsType<LocalControlledActorView>(decoded.Observation.ControlledView);
        Assert.Equal(selectedActor, restored.ActorId);
        Assert.Equal(capabilities, restored.Capabilities);
        Assert.Null(restored.Inventory);
        Assert.Null(restored.MaterialProposals);
        Assert.Null(restored.Trade);
        Assert.Null(restored.Production);
        Assert.Null(restored.Conversation);
        Assert.Null(restored.Combat);

        LocalCareView care;
        if (capabilities == WorldControlCapabilities.Recovery)
        {
            Assert.Null(restored.CareDisposition);
            care = Assert.IsType<LocalCareView>(Assert.IsType<LocalControlledRecoveryView>(restored.Recovery).Care);
            Assert.Null(care.Decision!.Cancellation);
            Assert.Null(care.Decision.Decline);
            if (selectHelper)
            {
                Assert.Equal(dose, Assert.IsType<AdministerRemedyIntent>(care.Decision.Treatment));
                Assert.Equal(WorldActionRejection.None, WorldCareActions.EvaluateRemedy(state, selectedActor,
                    care.Decision.Treatment!));
                AssertExactSelectedCommand(care.Decision.Treatment!);
            }
            else Assert.Null(care.Decision.Treatment);
        }
        else
        {
            Assert.Null(restored.Recovery);
            care = Assert.IsType<LocalCareView>(Assert.IsType<LocalControlledCareDispositionView>(restored.CareDisposition).Care);
            Assert.Null(care.Decision!.Treatment);
            PendingCareDispositionIntent decision;
            if (selectHelper)
            {
                Assert.Null(care.Decision.Cancellation);
                decision = Assert.IsType<DeclinePendingCareIntent>(care.Decision.Decline);
            }
            else
            {
                Assert.Null(care.Decision.Decline);
                decision = Assert.IsType<CancelPendingCareIntent>(care.Decision.Cancellation);
            }
            Assert.Equal(WorldActionRejection.None, WorldCareActions.EvaluateDisposition(state, selectedActor, decision));
            AssertExactSelectedCommand(decision);
        }
        Assert.Equal(pending.AcknowledgmentId, care.PendingAcknowledgmentId);
        Assert.Equal(fixture.Journey.Helper, care.HelperActorId);
        Assert.Equal(fixture.Journey.Patient, care.Decision!.PatientActorId);
        Assert.Empty(care.Offers);
        Assert.Null(care.Closure);

        byte[] json = ObservationCompression.IsCompressed(encoded) ? ObservationCompression.Decode(encoded) : encoded;
        JsonNode selectedJson = JsonNode.Parse(json)!["Value"]!["Observation"]!["ControlledView"]!;
        JsonNode doseIdentity = JsonSerializer.SerializeToNode(dose.Ingredient.LotId, PlayerViewCodec.CreateOptions())!;
        if (selectHelper && capabilities == WorldControlCapabilities.Recovery)
            Assert.True(ContainsValue(selectedJson, doseIdentity), "The permitted helper treatment must disclose its exact dose identity.");
        else
            Assert.False(ContainsValue(selectedJson, doseIdentity), "Withheld helper dose identity must not occur anywhere in the controlled body.");
        // The full home remains its own observation. In the helper-selected case the patient's home
        // also has no private helper dose; when home is the helper, its own inventory is retained.
        if (selectHelper) Assert.Null(decoded.Observation.HomeView.Care!.Decision!.Treatment);
        else Assert.Equal(dose, decoded.Observation.HomeView.Care!.Decision!.Treatment);
        Assert.Equal(pendingCare, state.Care!.ContentHash.ToByteArray());
        Assert.Equal(inventoryHash, state.Inventory.ContentHash);
        Assert.Equal(progressionHash, state.Progression!.ContentHash);
        Assert.Same(state, authority.ReadStateForHost());
        Assert.Empty(state.Care.Treatments);
        Assert.Empty(state.Care.Dispositions);

        WorldSnapshot ControllerEvent(WorldControllerEventKind kind)
        {
            var current = authority.ReadStateForHost();
            var row = current.Controllers!.Get(selectedActor);
            var input = kind == WorldControllerEventKind.Disconnected
                ? new WorldControllerEvent(selectedActor, kind, row.Revision, reason: WorldControllerDisconnectReason.TransportLost)
                : new WorldControllerEvent(selectedActor, kind, row.Revision, row.DisconnectId, 30_000);
            return authority.Commit(new WorldStepBatch(commands.NextId(), current.WorldId, current.BranchId,
                current.Revision, current.AdvancingSteps, WorldBatchKind.ControllerLifecycle, [], [],
                current.ProfileVersion, input)).Candidate;
        }

        void AssertExactSelectedCommand(WorldActionIntent intent)
        {
            var command = new WorldActionCommand(commands.NextId(), delivery.PrincipalId, delivery.SessionId,
                selectedActor, state.WorldId, state.BranchId, state.Space.GetActor(selectedActor).Revision, intent, witness);
            var roundtrip = PlayerSelectedInputCodec.Decode(PlayerSelectedInputCodec.Encode(
                PlayerSelectedInput.Command(selection, command))).Action!;
            Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(roundtrip));
            Assert.Equal(homeActor, roundtrip.ControlWitness!.GranteeActorId);
            Assert.Equal(selectedActor, roundtrip.ActorId);
            Assert.Equal(delivery.PrincipalId, roundtrip.PrincipalId);
        }
    }

    private static bool ContainsValue(JsonNode? node, JsonNode expected) => JsonNode.DeepEquals(node, expected)
        || node is JsonObject properties && properties.Any(property => ContainsValue(property.Value, expected))
        || node is JsonArray values && values.Any(value => ContainsValue(value, expected));
}
