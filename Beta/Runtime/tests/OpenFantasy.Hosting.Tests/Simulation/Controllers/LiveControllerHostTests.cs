using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Tests.Worlds.Controllers;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers;

public sealed class LiveControllerHostTests
{
    [Fact]
    public async Task Allowance_uses_the_confirmed_episode_and_expires_at_thirty_seconds_with_no_ready_port()
    {
        await using var fixture = await Fixture.Open();
        await using var first = await fixture.Join();
        WorldSnapshot before = fixture.State;
        await first.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.TransportLost);
        WorldSnapshot lost = fixture.State;
        Assert.Equal(WorldControllerMode.ReconnectAllowance, fixture.Control.Mode);
        Assert.Equal(LocalWorldRunState.Unavailable, first.Player.ReadRunState());
        ControllerJournalTests.AssertGameplayUnchanged(before, lost);
        await Tick(fixture.Time, TimeSpan.FromMilliseconds(29_950));
        fixture.Time.Advance(TimeSpan.FromMilliseconds(49));
        Assert.Equal(lost.ContentHash, fixture.State.ContentHash);
        await Tick(fixture.Time, TimeSpan.FromMilliseconds(1));
        Assert.Equal(WorldControllerMode.Autonomous, fixture.Control.Mode);
        Assert.Equal(lost.Controllers!.Get(fixture.Actor).DisconnectId, fixture.Control.DisconnectId);
        Assert.Equal(2UL, fixture.State.Revision);
        ControllerJournalTests.AssertGameplayUnchanged(before, fixture.State);
        await Tick(fixture.Time, TimeSpan.FromHours(2));
        Assert.Equal(2UL, fixture.State.Revision);
        byte[] saved = WorldSnapshotCodec.Encode(fixture.State);
        await fixture.Host.StopAsync();
        using var reopened = SqliteWorldAuthority.OpenExisting(fixture.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(saved, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    [Fact]
    public async Task Connected_idle_and_ordinary_disposal_never_start_an_allowance()
    {
        await using var fixture = await Fixture.Open();
        await using var first = await fixture.Join();
        await Tick(fixture.Time, TimeSpan.FromHours(3));
        Assert.Equal(fixture.Genesis.ContentHash, fixture.State.ContentHash);
        await first.DisposeAsync();
        await Tick(fixture.Time, TimeSpan.FromHours(3));
        Assert.Equal(fixture.Genesis.ContentHash, fixture.State.ContentHash);
    }

    [Fact]
    public async Task A_late_transport_callback_after_host_shutdown_is_not_a_disconnect_event()
    {
        await using var fixture = await Fixture.Open();
        await using var channel = await fixture.Join();
        await fixture.Host.StopAsync();
        await channel.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.TransportLost);
        using var reopened = SqliteWorldAuthority.OpenExisting(fixture.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(fixture.Genesis.ContentHash, reopened.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task Disconnect_and_rejoin_preserve_an_actual_reading_hold()
    {
        await using var fixture = await Fixture.Open();
        await using var channel = await fixture.Join();
        channel.Player.SetReady(true);
        EntityId wren = fixture.State.Quests!.Definitions[0].GiverActorId;
        var commands = fixture.Commands;
        var speak = commands.Command(fixture.State, fixture.Actor,
            new SpeakToActorIntent(wren, fixture.State.Space.GetActor(wren).Revision));
        Task<WorldActionReceipt> speaking = channel.Player.SubmitAction(speak);
        await Tick(fixture.Time);
        Assert.Equal(WorldActionRejection.None, (await speaking).Rejection);
        WorldSnapshot held = fixture.State;
        Assert.True(held.HasReadingHold);
        await channel.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.PeerClosed);
        await channel.DisposeAsync();
        await using var rejoined = await fixture.Join();
        ControllerJournalTests.AssertGameplayUnchanged(held, fixture.State);
        Assert.True(rejoined.Player.ReadView().HasReadingHold);
        Assert.Equal(LocalWorldRunState.Paused, rejoined.Player.ReadRunState());
        rejoined.Player.SetReady(true);
        Assert.Equal(LocalWorldRunState.Waiting, rejoined.Player.ReadRunState());
    }

    [Fact]
    public async Task Connected_ready_human_has_unlimited_combat_wait()
    {
        await using var fixture = await Fixture.Open(combat: true);
        await using var channel = await fixture.Join();
        channel.Player.SetReady(true);
        WorldSnapshot waiting = fixture.State;
        Assert.True(WorldEncounterActions.HasRequiredDecisions(waiting));
        await Tick(fixture.Time, TimeSpan.FromMinutes(5));
        Assert.Equal(waiting.ContentHash, fixture.State.ContentHash);
        Assert.Equal(WorldControllerMode.Human, fixture.Control.Mode);
        Assert.Equal(LocalWorldRunState.Waiting, channel.Player.ReadRunState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejoin_before_or_after_expiry_preserves_identity_and_starts_unready(bool afterExpiry)
    {
        await using var fixture = await Fixture.Open();
        await using var first = await fixture.Join();
        await first.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.PeerClosed);
        await first.DisposeAsync();
        if (afterExpiry) await Tick(fixture.Time, TimeSpan.FromSeconds(30));
        await using var replacement = await fixture.Join();
        Assert.Equal(WorldControllerMode.Human, fixture.Control.Mode);
        Assert.Null(fixture.Control.DisconnectId);
        Assert.Equal(LocalWorldRunState.Paused, replacement.Player.ReadRunState());
        Assert.Equal(PlayerViewCodec.Encode(new LocalWorldView(fixture.State, fixture.Actor)),
            PlayerViewCodec.Encode(replacement.Player.ReadView()));
        ControllerJournalTests.AssertGameplayUnchanged(fixture.Genesis, fixture.State);
        ulong revision = fixture.State.Revision;
        await first.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.TransportLost);
        await first.Player.DisposeAsync();
        await Tick(fixture.Time, TimeSpan.FromMinutes(1));
        Assert.Equal(revision, fixture.State.Revision);
        Assert.Equal(LocalWorldRunState.Paused, replacement.Player.ReadRunState());
        replacement.Player.SetReady(true);
        Assert.Equal(LocalWorldRunState.Running, replacement.Player.ReadRunState());
    }

    [Fact]
    public async Task A_new_loss_has_its_own_full_allowance_and_old_callback_cannot_restart_it()
    {
        await using var fixture = await Fixture.Open();
        await using var first = await fixture.Join();
        await first.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.PeerClosed);
        EntityId? previous = fixture.Control.DisconnectId;
        await Tick(fixture.Time, TimeSpan.FromSeconds(20));
        await first.DisposeAsync();
        await using var second = await fixture.Join();
        await second.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.TransportLost);
        EntityId? current = fixture.Control.DisconnectId;
        Assert.NotEqual(previous, current);
        await Tick(fixture.Time, TimeSpan.FromSeconds(10));
        await first.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.ProtocolFailure);
        Assert.Equal(current, fixture.Control.DisconnectId);
        Assert.Equal(WorldControllerMode.ReconnectAllowance, fixture.Control.Mode);
        await Tick(fixture.Time, TimeSpan.FromSeconds(20));
        Assert.Equal(WorldControllerMode.Autonomous, fixture.Control.Mode);
        Assert.Equal(4UL, fixture.Control.Revision);
    }

    [Fact]
    public async Task Rejoin_waits_for_actual_paid_choice_resolution_and_does_not_replay_early_readiness()
    {
        await using var fixture = await Fixture.Open(combat: true);
        await using var first = await fixture.Join();
        var commands = fixture.Commands;
        first.Player.SetReady(true);
        var paidWait = commands.Wait(fixture.State, fixture.Actor);
        Task<WorldActionReceipt> receipt = first.Player.SubmitAction(paidWait);
        await Tick(fixture.Time);
        Assert.Equal(WorldActionRejection.None, (await receipt).Rejection);
        await first.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.PeerClosed);
        await first.DisposeAsync();
        await using var replacement = await fixture.Join();
        Assert.False(WorldControllerBoundaries.CanRestoreHuman(fixture.State, fixture.Actor));
        Assert.Equal(WorldControllerMode.ReconnectAllowance, fixture.Control.Mode);
        replacement.Player.SetReady(true);
        replacement.Player.SetMovement(32, 0);
        Assert.Equal(LocalWorldRunState.Paused, replacement.Player.ReadRunState());
        Assert.Equal(0L, replacement.Player.MovementX);
        Assert.Equal(WorldReceiptCodecV1.Encode(await receipt),
            WorldReceiptCodecV1.Encode(await replacement.Player.SubmitAction(paidWait)));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => replacement.Player.SubmitAction(commands.Wait(fixture.State, fixture.Actor)));
        EntityId wolf = fixture.State.EncounterLifecycle!.GetScenario(fixture.State.EncounterLifecycle.ScenarioId!.Value).TargetActorId;
        await using LocalPlayerConnection opponent = await fixture.Host.ConnectPlayerAsync(wolf, ConstructionUpgradeFixture.Delivery(fixture.State, wolf));
        opponent.SetReady(true);
        Task<WorldActionReceipt> opponentWait = opponent.SubmitAction(commands.Wait(fixture.State, wolf));
        await Tick(fixture.Time);
        Assert.Equal(WorldActionRejection.None, (await opponentWait).Rejection);
        for (int count = 0; fixture.Control.Mode != WorldControllerMode.Human; count++)
        {
            Assert.True(count < 8, "Accepted choices must reach their ordinary safe restoration boundary.");
            await Tick(fixture.Time);
        }
        opponent.SetReady(false);
        Assert.Equal(LocalWorldRunState.Paused, replacement.Player.ReadRunState());
        Assert.DoesNotContain(fixture.State.Pacing!.Encounter!.Decisions, row => row.ActorId == fixture.Actor);
        Assert.True(fixture.State.Pacing.Encounter.Actors.Single(row => row.ActorId == fixture.Actor).RemainingDelay > 0);
        Assert.NotNull(fixture.Authority.FindReceipt(paidWait, ConstructionUpgradeFixture.Delivery(fixture.State, fixture.Actor)));
        await Tick(fixture.Time, TimeSpan.FromMinutes(1));
        Assert.Equal(LocalWorldRunState.Paused, replacement.Player.ReadRunState());
        replacement.Player.SetReady(true);
        Assert.NotEqual(LocalWorldRunState.Paused, replacement.Player.ReadRunState());
    }

    [Fact]
    public async Task Sole_returning_player_finishes_accepted_combat_without_automatic_readiness()
    {
        await using var fixture = await Fixture.Open(combat: true, singleHuman: true);
        await using var channel = await fixture.Join();
        channel.Player.SetReady(true);
        var wait = fixture.Commands.Wait(fixture.State, fixture.Actor);
        Task<WorldActionReceipt> receipt = channel.Player.SubmitAction(wait);
        await Tick(fixture.Time);
        Assert.Equal(WorldActionRejection.None, (await receipt).Rejection);
        Assert.Contains(fixture.State.Pacing!.Encounter!.Decisions, row => row.ActorId == fixture.Actor);
        await channel.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.TransportLost);
        await channel.DisposeAsync();
        await using var returning = await fixture.Join();
        returning.Player.SetReady(true); // Discarded while the paid choice still owns the character.
        Assert.Equal(LocalWorldRunState.Paused, returning.Player.ReadRunState());
        for (int count = 0; fixture.Control.Mode != WorldControllerMode.Human; count++)
        {
            Assert.True(count < 8, "No second ready player is required to finish an accepted choice.");
            await Tick(fixture.Time);
        }
        Assert.Equal(LocalWorldRunState.Paused, returning.Player.ReadRunState());
        Assert.True(fixture.State.Pacing!.Encounter!.Actors.Single(row => row.ActorId == fixture.Actor).RemainingDelay > 0);
        Assert.Equal(WorldReceiptCodecV1.Encode(await receipt),
            WorldReceiptCodecV1.Encode(await returning.Player.SubmitAction(wait)));
        WorldSnapshot restored = fixture.State;
        await Tick(fixture.Time, TimeSpan.FromMinutes(1));
        Assert.Equal(restored.ContentHash, fixture.State.ContentHash);
    }

    [Fact]
    public async Task Sole_returning_player_finishes_existing_staging_then_requires_new_readiness()
    {
        await using var fixture = await Fixture.Open(combat: true, singleHuman: true, staging: true);
        await using var channel = await fixture.Join();
        await channel.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.PeerClosed);
        await channel.DisposeAsync();
        await using var returning = await fixture.Join();
        Assert.Equal(WorldControllerMode.ReconnectAllowance, fixture.Control.Mode);
        for (int count = 0; fixture.Control.Mode != WorldControllerMode.Human; count++)
        {
            Assert.True(count < 5);
            await Tick(fixture.Time);
        }
        Assert.Equal(EncounterLifecyclePhase.Active, fixture.State.EncounterLifecycle!.Phase);
        Assert.True(WorldEncounterActions.HasRequiredDecisions(fixture.State));
        Assert.Equal(LocalWorldRunState.Paused, returning.Player.ReadRunState());
        WorldSnapshot waiting = fixture.State;
        await Tick(fixture.Time, TimeSpan.FromMinutes(1));
        Assert.Equal(waiting.ContentHash, fixture.State.ContentHash);
    }

    [Fact]
    public async Task Sole_returning_patient_finishes_accepted_finite_care_without_free_healing_or_readiness()
    {
        await using var fixture = await Fixture.Open(care: true);
        await using var channel = await fixture.Join();
        Assert.NotNull(fixture.State.Care!.Pending);
        Assert.Equal(0, fixture.State.Progression!.GetActor(fixture.Actor).CurrentHp);
        var encounter = fixture.State.EncounterActions!.Scope.EncounterId;
        await channel.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.PeerClosed);
        await channel.DisposeAsync();
        await using var returning = await fixture.Join();
        returning.Player.SetReady(true);
        Assert.False(returning.Player.Ready);
        for (int count = 0; fixture.Control.Mode != WorldControllerMode.Human; count++)
        {
            Assert.True(count < 5);
            await Tick(fixture.Time);
        }
        Assert.Null(fixture.State.Care!.Pending);
        Assert.True(fixture.State.Progression!.GetActor(fixture.Actor).CurrentHp > 0);
        Assert.Single(fixture.State.Production!.ConsumedItems);
        Assert.Equal(encounter, fixture.State.EncounterActions!.Scope.EncounterId);
        Assert.Equal(EncounterLifecyclePhase.Active, fixture.State.EncounterLifecycle!.Phase);
        Assert.Equal(LocalWorldRunState.Paused, returning.Player.ReadRunState());
        WorldSnapshot restored = fixture.State;
        await Tick(fixture.Time, TimeSpan.FromMinutes(1));
        Assert.Equal(restored.ContentHash, fixture.State.ContentHash);
    }

    internal static async Task Tick(ManualSessionTimeProvider time, TimeSpan? elapsed = null)
    {
        await Until(() => time.HasPendingTimer);
        time.Advance(elapsed ?? LocalWorldSession.StepInterval);
        await Until(() => time.HasPendingTimer);
    }

    private static async Task Until(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!ready()) await Task.Delay(1, timeout.Token);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openfantasy-controller-" + Guid.NewGuid().ToString("N"));
        private readonly List<PlayerMessageTransport> _clients = [];
        private readonly PlayerAdmissionService _admission;
        private readonly PlayerJoinCredential _credential;
        internal string Path => System.IO.Path.Combine(_directory, "world.db");
        internal WorldSnapshot Genesis { get; }
        internal SqliteWorldAuthority Authority { get; }
        internal EncounterJourneyWorldFixture Commands { get; } = new();
        internal ManualSessionTimeProvider Time { get; } = new();
        internal LocalWorldSession Host { get; }
        internal EntityId Actor { get; }
        internal WorldSnapshot State => Authority.ReadStateForHost();
        internal WorldControllerAssignment Control => State.Controllers!.Get(Actor);

        private Fixture(bool combat, bool singleHuman, bool staging, bool care)
        {
            Directory.CreateDirectory(_directory);
            Genesis = WorldControllerAdmission.CreateFresh(combat && !singleHuman
                ? CargoOutcomeWorldAdmission.CreateTwoHumanWolves("live-controller", "Traveler", 1)
                : CargoOutcomeWorldAdmission.CreateEmbedded("live-controller", "Traveler"));
            if (care)
            {
                WorldSnapshot basis = RoadThreatWorldAdmission.CreateEmbedded("live-controller", "Traveler");
                EntityId patient = basis.Residents.Single(row => row.ControllerKind == ResidentControllerKind.Player).ActorId;
                Genesis = WorldControllerAdmission.CreateFresh(WorldEncounterOutcomeAdmission.CreateCalibratedFresh(basis,
                    new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [patient] = 1 }, [])));
            }
            Actor = Genesis.EncounterLifecycle!.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.FirstScenarioKey).InitiatingActorId;
            Authority = SqliteWorldAuthority.CreateNew(Path, Genesis);
            if (combat || care) EnterEncounter(staging);
            if (care) PrepareAcceptedCare();
            Host = new LocalWorldSession(Authority, Actor, ConstructionUpgradeFixture.Delivery(Genesis, Actor), Time);
            _admission = new PlayerAdmissionService(Host, []);
            _credential = _admission.Issue(Actor, ConstructionUpgradeFixture.Delivery(Genesis, Actor), TimeSpan.FromHours(1));
        }

        internal static async Task<Fixture> Open(bool combat = false, bool singleHuman = false, bool staging = false, bool care = false)
        {
            var result = new Fixture(combat, singleHuman, staging, care);
            await result.Host.Player.DisposeAsync();
            await Until(() => result.Time.HasPendingTimer);
            return result;
        }

        internal async Task<AuthenticatedPlayerChannel> Join()
        {
            var pair = InMemoryPlayerTransport.CreatePair();
            _clients.Add(pair.Client);
            await pair.Client.SendAsync(PlayerMessageCodec.Encode(PlayerHandshakeCodec.Hello(1, _admission.Compatibility, _credential.Export())));
            return await _admission.AcceptAsync(pair.Server);
        }

        private void EnterEncounter(bool staging)
        {
            var scenario = Genesis.EncounterLifecycle!.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.FirstScenarioKey);
            var walking = new ConstructionUpgradeFixture();
            for (int count = 0; State.Space.QueryInteraction(Actor, scenario.TargetActorId) != SpatialInteraction.Reachable; count++)
            {
                Assert.True(count < 512);
                var here = State.Space.GetActor(Actor).Feet;
                var target = State.Space.GetActor(scenario.TargetActorId).Feet;
                long dx = Math.Sign(target.X - here.X) * WorldMovementInput.DisplacementPerStep;
                long dy = dx == 0 ? Math.Sign(target.Y - here.Y) * WorldMovementInput.DisplacementPerStep : 0;
                Authority.Commit(walking.Batch(State, [], [new(ConstructionUpgradeFixture.Delivery(State, Actor), Actor, dx, dy)]));
            }
            var commands = Commands;
            Authority.Commit(commands.Batch(State, WorldBatchKind.EncounterLifecycle, [commands.Enter(State, scenario.DefinitionId)]));
            for (int count = 0; !staging && State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; count++)
            {
                Assert.True(count < 3);
                Authority.Commit(commands.Batch(State, WorldBatchKind.EncounterLifecycle));
            }
        }

        private void PrepareAcceptedCare()
        {
            var npc = new LocalEncounterController();
            for (int step = 0; State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; step++)
            {
                Assert.True(step < 800);
                if (WorldEncounterActions.HasRequiredDecisions(State))
                    Authority.Commit(Commands.Batch(State, WorldBatchKind.EncounterDecision, [Commands.Wait(State, Actor)]));
                var automatic = npc.SelectChoices(State, Commands.NextId);
                if (automatic.Count != 0)
                    Authority.Commit(Commands.Batch(State, WorldBatchKind.EncounterDecision, automatic));
                var kind = WorldEncounterActions.DueActors(State).Count == 0
                    ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
                Authority.Commit(Commands.Batch(State, kind));
            }
            var agreement = State.Care!.Agreements.Single(row => row.PatientActorId == Actor);
            var acknowledge = Commands.Command(State, Actor, new AcknowledgeCareIntent(agreement.AgreementId,
                State.EncounterActions!.Scope.EncounterId, State.EncounterLifecycle.Revision, State.EncounterActions.ContentHash));
            var accepted = Authority.Commit(Commands.Batch(State, WorldBatchKind.Care, [acknowledge]));
            Assert.Equal(WorldActionRejection.None, Assert.Single(accepted.Receipts).Rejection);
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            foreach (var client in _clients) await client.DisposeAsync();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
