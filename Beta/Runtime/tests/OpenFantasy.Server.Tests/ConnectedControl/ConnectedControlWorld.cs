using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.ConnectedControl;

/// <summary>Fresh four-human Cargo admission and real preparation; all live choices use the TLS application facade.</summary>
internal sealed class ConnectedControlWorld : IAsyncDisposable
{
    private readonly ServerFiles _files = new();
    private readonly List<Peer> _peers = [];
    private readonly ConcurrentQueue<string> _events = new();
    private readonly List<string> _preparation = [];
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private ulong _identity = 1;
    private bool _stopped;
    internal string Database => Path.Combine(_files.DirectoryPath, "connected-control.sqlite3");
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot Prepared { get; private set; }
    internal EntityId[] Actors { get; }
    internal EntityId Owner => Actors[0];
    internal EntityId Grantee => Actors[1];
    internal EntityId ScenarioId { get; private set; }
    internal EntityId TargetActorId { get; private set; }
    internal (long X, long Y) EntryMovement { get; private set; }
    internal CargoPlayerHost Host { get; private set; } = null!;
    internal PlayerWebSocketServer Server { get; private set; } = null!;
    internal X509Certificate2 Certificate { get; } = ServerWorld.CreateCertificate();

    private ConnectedControlWorld()
    {
        var controllers = WorldControllerAdmission.CreateFresh(CargoOutcomeWorldAdmission.CreateEmbedded(
            CargoOutcomeWorldAdmission.SeedText, ["Amber", "Birch", "Cedar", "Dawn"]));
        var remedy = controllers.Care!.Ordinary!.Definitions.First(value =>
            value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(new(0xcc40404000000000, 1),
            remedy.RemedyDefinitionId, remedy.RemedyVersion, remedy.SupportedForms);
        Genesis = WorldDelegatedControlAdmission.CreateFresh(WorldControlPermissionAdmission.CreateFresh(
            WorldCareDispositionAdmission.CreateFresh(WorldRecoveryAdmission.CreateFresh(controllers, [recovery], []))));
        Prepared = Genesis;
        Actors = Genesis.Controllers!.Assignments.Select(row => row.ActorId).ToArray();
        Assert.Equal(4, Actors.Length);
    }

    internal static async Task<ConnectedControlWorld> Open(bool prepareCombatApproach)
    {
        var world = new ConnectedControlWorld();
        try
        {
            using (var authority = SqliteWorldAuthority.CreateNew(world.Database, world.Genesis))
            {
                if (prepareCombatApproach)
                {
                    world.PrepareApproach(authority);
                    world.PrepareCohort(authority);
                }
                world.Prepared = authority.ReadStateForHost();
                if (prepareCombatApproach) world.CapturePreparedEntry();
            }
            world.Host = await CargoPlayerHost.OpenExistingAsync(world.Database, ServerWorld.Cancellation);
            world.Server = await PlayerWebSocketServer.StartAsync(world.Host.Admission,
                new(IPAddress.Loopback, 0, world.Certificate), ServerWorld.Cancellation);
            return world;
        }
        catch { await world.DisposeAsync(); throw; }
    }

    internal EntityId NextId() => new(0xcc40404100000000, _identity++);

    internal async Task<Peer> Connect(EntityId actor)
    {
        var credential = Host.Issue(actor, TimeSpan.FromMinutes(3));
        var invitation = new PlayerConnectionInvitation(Server.Endpoint, Host.Admission.Compatibility, actor, credential.Export());
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(PlayerWebSocketConnector.Subprotocol);
        socket.Options.RemoteCertificateValidationCallback = (_, peer, _, errors) => peer is not null
            && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None
            && peer.GetCertHashString(HashAlgorithmName.SHA256) == Certificate.GetCertHashString(HashAlgorithmName.SHA256);
        try
        {
            await socket.ConnectAsync(invitation.Endpoint, ServerWorld.Cancellation).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            var tap = new ProtocolTap(new WebSocketPlayerTransport(socket))
            {
                Incoming = message => { Record(actor, "receive", message); return message; },
                AfterSend = message => Record(actor, "send", message)
            };
            var protocol = await PlayerProtocolClient.ConnectAsync(tap, invitation, ServerWorld.Cancellation);
            var application = await RemotePlayerSession.OpenAsync(protocol, ServerWorld.Cancellation);
            var result = new Peer(actor, protocol, application, tap);
            _peers.Add(result);
            Assert.True(result.Control.SupportsControlPermissions);
            Assert.True(result.Control.SupportsActorSelection);
            Assert.Equal(actor, result.Control.ReadSelectedObservation().HomeView.PlayerActorId);
            return result;
        }
        catch { socket.Dispose(); throw; }
    }

    internal WorldActionCommand Permission(Peer peer, LocalControlPermissionBoard board, WorldActionIntent intent) =>
        new(NextId(), peer.Application.Welcome.Delivery.PrincipalId, peer.Application.Welcome.Delivery.SessionId,
            board.HomeActorId, board.WorldId, board.BranchId, board.HomeActorRevision, intent);

    internal WorldActionCommand Selected(Peer peer, LocalSelectedPlayerObservation observation, WorldActionIntent intent)
    {
        var actor = observation.ControlledView is { } operated
            ? operated.Actors.Single(row => row.ActorId == observation.Selection.ActorId)
            : observation.HomeView.Actors.Single(row => row.ActorId == observation.Selection.ActorId);
        return new(NextId(), peer.Application.Welcome.Delivery.PrincipalId, peer.Application.Welcome.Delivery.SessionId,
            observation.Selection.ActorId, observation.HomeView.WorldId, observation.HomeView.BranchId,
            actor.PresenceRevision, intent, observation.Selection.ControlWitness);
    }

    internal async Task Until(Func<bool> predicate, string label)
    {
        var timer = Stopwatch.StartNew();
        while (!predicate() && timer.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10, ServerWorld.Cancellation);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5) && predicate(), label + "\n" + Describe());
    }

    internal async Task<LocalControlAcquisition> AwaitOriginalAllowance(Peer grantee)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(35))
        {
            var board = await grantee.Control.ReadControlPermissionsAsync();
            if (board.Slots.Single(row => row.OwnerActorId == Owner && row.GranteeActorId == Grantee).Acquisition is { } opportunity)
            {
                Assert.True(timer.Elapsed < TimeSpan.FromSeconds(35), Describe());
                return opportunity;
            }
            await Task.Delay(100, ServerWorld.Cancellation);
        }
        throw new TimeoutException("The original 30-second allowance did not expose acquisition within 35 seconds.\n" + Describe());
    }

    internal string Describe()
    {
        var states = _peers.Where(peer => !peer.Disposed).Select(peer =>
        {
            try
            {
                var current = peer.Control.ReadSelectedObservation();
                return $"{peer.Actor}: protocol={peer.Protocol.Completion.Status}; selection={current.Selection.ActorId}/{current.Selection.Generation}; "
                    + $"ready={current.IsReady}; control={current.ControlStatus.State}; "
                    + $"combat={current.ControlledView?.Combat?.Lifecycle?.Phase}; calendar={current.HomeView.Calendar}";
            }
            catch (Exception error) { return $"{peer.Actor}: {error.GetType().Name}: {error.Message}"; }
        });
        return string.Join("\n", _preparation.Concat(states).Concat(_events));
    }

    internal string DescribeEntry(LocalSelectedPlayerObservation observed)
    {
        var view = observed.ControlledView ?? throw new InvalidOperationException("The entry diagnostic requires its selected actor.");
        var scenario = Prepared.EncounterLifecycle!.GetScenario(ScenarioId);
        var actors = view.Actors.ToDictionary(actor => actor.ActorId);
        // This geometry-only diagnostic uses observed feet with the unchanged prepared terrain and
        // forms. It never publishes the reconstructed geometry or creates an authority snapshot.
        var geometry = Prepared.Space.WithActors(Prepared.Space.Actors.Select(actor =>
            actors.TryGetValue(actor.ActorId, out var current)
                ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope, current.Feet,
                    current.PresenceRevision, actor.HomePlaceId) : actor));
        var lifecycle = view.Combat!.Lifecycle!;
        var entry = lifecycle.Entries.Single(value => value.ScenarioId == ScenarioId);
        var lines = new List<string>
        {
            $"Entry {scenario.Key}/{ScenarioId}: range={scenario.EntryRangeSubunits}; phase={lifecycle.Phase}; "
                + $"hold={view.HasReadingHold}; playerCanAct={lifecycle.PlayerCanAct}; recovery={lifecycle.PlayerRecoveryDelay}; "
                + $"projectedCanEnter={entry.CanEnter}; owner={Owner}; target={TargetActorId}; sample={EntryMovement}."
        };
        bool cooperative = true;
        foreach (var participant in scenario.Combatants.Where(actor => actor.Side != 0))
        {
            var actor = actors[participant.ActorId];
            var reach = participant.ActorId == Owner ? SpatialInteraction.Reachable
                : geometry.QueryInteraction(Owner, participant.ActorId, scenario.EntryRangeSubunits);
            cooperative &= !actor.IsIncapacitated && reach == SpatialInteraction.Reachable;
            lines.Add($"Required {actor.DisplayName}/{actor.ActorId}: side={participant.Side}; feet={actor.Feet}; "
                + $"preparedFeet={Prepared.Space.GetActor(actor.ActorId).Feet}; reach={reach}; "
                + $"observedIncapacitated={actor.IsIncapacitated}; preparedHp={Prepared.Progression!.GetActor(actor.ActorId).CurrentHp}.");
        }
        lines.Add($"Recomputed entry conditions: phase={lifecycle.Phase == EncounterLifecyclePhase.Exploration}; "
            + $"unheld={!view.HasReadingHold}; everyRequiredParticipantCooperativeAndReachable={cooperative}.");
        return string.Join("\n", lines) + "\n" + Describe();
    }

    private void CapturePreparedEntry()
    {
        var scenario = Prepared.EncounterLifecycle!.GetScenario(ScenarioId);
        var owner = Prepared.Space.GetActor(Owner);
        var move = Prepared.Space.PrepareMove(Owner, owner.Revision, EntryMovement.X, EntryMovement.Y);
        Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
        var candidate = Prepared.Space.WithActors(Prepared.Space.Actors.Select(actor =>
            actor.ActorId == Owner ? move.Candidate! : actor));
        _preparation.Add($"Prepared {scenario.Key}: range={scenario.EntryRangeSubunits}; owner={owner.Feet}; "
            + $"oneSampleCandidate={move.Candidate!.Feet}; journalRevision={Prepared.Revision}; steps={Prepared.AdvancingSteps}.");
        foreach (var participant in scenario.Combatants.Where(actor => actor.Side != 0))
            _preparation.Add($"Prepared required {participant.ActorId}: side={participant.Side}; "
                + $"feet={Prepared.Space.GetActor(participant.ActorId).Feet}; "
                + $"hp={Prepared.Progression!.GetActor(participant.ActorId).CurrentHp}; "
                + $"oneSampleReach={(participant.ActorId == Owner ? SpatialInteraction.Reachable : candidate.QueryInteraction(Owner, participant.ActorId, scenario.EntryRangeSubunits))}.");
    }

    private void Record(EntityId actor, string direction, PlayerMessage message)
    {
        _events.Enqueue($"{_elapsed.Elapsed.TotalSeconds:F3}s {actor} {direction} {message.Kind} request={message.RequestId} bytes={message.Payload.Length}");
        while (_events.Count > 32) _events.TryDequeue(out _);
    }

    private void PrepareApproach(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var scenarios = state.EncounterLifecycle!.Scenarios.Where(row => row.InitiatingActorId == Owner)
            .OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();
        var scenario = scenarios.FirstOrDefault(row => state.Space.QueryInteraction(Owner, row.TargetActorId,
            row.EntryRangeSubunits) != SpatialInteraction.Reachable) ?? scenarios[0];
        ScenarioId = scenario.DefinitionId;
        TargetActorId = scenario.TargetActorId;
        if (state.Space.QueryInteraction(Owner, TargetActorId, scenario.EntryRangeSubunits) == SpatialInteraction.Reachable)
        {
            PrepareDeparture(authority, scenario);
            return;
        }
        Assert.NotEqual(SpatialInteraction.Reachable, state.Space.QueryInteraction(Owner, TargetActorId, scenario.EntryRangeSubunits));
        var target = state.Space.GetActor(TargetActorId).Feet;
        var goal = new[] { target.Offset(256, 0, 0), target.Offset(-256, 0, 0), target.Offset(0, 256, 0), target.Offset(0, -256, 0) }
            .First(point => state.Space.QueryGroundRoute(Owner, point).Status == GroundRouteStatus.Reachable);
        var binding = state.Inventory.Controls.Single(row => row.ActorId == Owner);
        var delivery = new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
        for (int step = 0; step < 512; step++)
        {
            state = authority.ReadStateForHost();
            var actor = state.Space.GetActor(Owner);
            var route = state.Space.QueryGroundRoute(Owner, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            long dx = Math.Clamp(waypoint.X - actor.Feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - actor.Feet.Y, -32, 32) : 0;
            var move = state.Space.PrepareMove(Owner, actor.Revision, dx, dy);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            // Geometry-only counterfactual chooses the stopping point. Every persisted position below
            // is earned by an ordinary movement commit; the connected player earns the entry step later.
            var candidate = state.Space.WithActors(state.Space.Actors.Select(row => row.ActorId == Owner ? move.Candidate! : row));
            if (candidate.QueryInteraction(Owner, TargetActorId, scenario.EntryRangeSubunits) == SpatialInteraction.Reachable)
            {
                Assert.NotEqual(SpatialInteraction.Reachable, state.Space.QueryInteraction(Owner, TargetActorId, scenario.EntryRangeSubunits));
                EntryMovement = (dx, dy);
                return;
            }
            authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new(delivery, Owner, dx, dy)], [], state.ProfileVersion));
        }
        throw new InvalidOperationException("Actual movement preparation did not reach the outside approach within 512 steps.");
    }

    private void PrepareDeparture(SqliteWorldAuthority authority, EncounterScenarioDefinition scenario)
    {
        var state = authority.ReadStateForHost();
        var target = state.Space.GetActor(scenario.TargetActorId).Feet;
        long distance = scenario.EntryRangeSubunits + 256;
        var goal = new[] { target.Offset(0, distance, 0), target.Offset(0, -distance, 0),
            target.Offset(distance, 0, 0), target.Offset(-distance, 0, 0) }
            .First(point => state.Space.QueryGroundRoute(Owner, point).Status == GroundRouteStatus.Reachable);
        var binding = state.Inventory.Controls.Single(row => row.ActorId == Owner);
        var delivery = new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
        for (int step = 0; step < 512; step++)
        {
            state = authority.ReadStateForHost();
            Assert.Equal(SpatialInteraction.Reachable,
                state.Space.QueryInteraction(Owner, scenario.TargetActorId, scenario.EntryRangeSubunits));
            var actor = state.Space.GetActor(Owner);
            var route = state.Space.QueryGroundRoute(Owner, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            long dx = Math.Clamp(waypoint.X - actor.Feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - actor.Feet.Y, -32, 32) : 0;
            authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new(delivery, Owner, dx, dy)], [], state.ProfileVersion));
            var departed = authority.ReadStateForHost();
            Assert.Equal(actor.Feet.Offset(dx, dy, 0), departed.Space.GetActor(Owner).Feet);
            if (departed.Space.QueryInteraction(Owner, scenario.TargetActorId, scenario.EntryRangeSubunits)
                == SpatialInteraction.Reachable) continue;

            // The saved actor earned this departure through ordinary movement. Its exact previous
            // position is the one-step entry reserved for the connected delegate's single sample.
            EntryMovement = (-dx, -dy);
            var reverse = departed.Space.PrepareMove(Owner, departed.Space.GetActor(Owner).Revision, -dx, -dy);
            Assert.Equal(SpatialMoveStatus.Prepared, reverse.Status);
            Assert.Equal(actor.Feet, reverse.Candidate!.Feet);
            var returned = departed.Space.WithActors(departed.Space.Actors.Select(row =>
                row.ActorId == Owner ? reverse.Candidate! : row));
            Assert.Equal(SpatialInteraction.Reachable,
                returned.QueryInteraction(Owner, scenario.TargetActorId, scenario.EntryRangeSubunits));
            _events.Enqueue($"Preparation: {scenario.Key}, {step + 1} journaled departure steps; TLS entry sample ({-dx}, {-dy}).");
            return;
        }
        throw new InvalidOperationException("Actual movement preparation did not leave the adjacent scenario within 512 steps.");
    }

    private void PrepareCohort(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var scenario = state.EncounterLifecycle!.GetScenario(ScenarioId);
        foreach (var participant in scenario.Combatants.Where(actor => actor.Side != 0 && actor.ActorId != Owner))
        {
            state = authority.ReadStateForHost();
            var entry = PreviewEntryMovement(state.Space);
            if (entry.QueryInteraction(Owner, participant.ActorId, scenario.EntryRangeSubunits) == SpatialInteraction.Reachable)
                continue;
            Assert.Contains(participant.ActorId, Actors); // Move a real teammate, never the encounter's NPC target.
            // Cohort admission is measured from the initiating actor, not from the hostile target.
            // A reachable position behind the party is valid even when the target's nearby cells are enclosed.
            var entryFeet = entry.GetActor(Owner).Feet;
            var target = new SubunitPosition(entryFeet.Cell.X * SubunitPosition.SubunitsPerCell + 128,
                entryFeet.Cell.Y * SubunitPosition.SubunitsPerCell + 128, entryFeet.Z);
            var offsets = new[] { (-256L, 0L), (0L, -256L), (0L, 256L), (256L, 0L),
                (-256L, -256L), (-256L, 256L), (256L, -256L), (256L, 256L) };
            var candidates = offsets.Select(offset => target.Offset(offset.Item1, offset.Item2, 0)).ToArray();
            var goal = candidates.Where(point => AdmitsCohortGoal(state, scenario, participant.ActorId, point))
                .Select(point => (SubunitPosition?)point).FirstOrDefault()
                ?? throw new InvalidOperationException("No admitted cohort destination for " + participant.ActorId + "\n"
                    + string.Join("\n", candidates.Select(point => DescribeCohortGoal(state, scenario, participant.ActorId, point))));
            var binding = state.Inventory.Controls.Single(row => row.ActorId == participant.ActorId);
            var delivery = new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
            var origin = state.Space.GetActor(participant.ActorId).Feet;
            int steps = 0;
            while (state.Space.GetActor(participant.ActorId).Feet != goal && steps < 512)
            {
                var actor = state.Space.GetActor(participant.ActorId);
                var route = state.Space.QueryGroundRoute(participant.ActorId, goal);
                Assert.Equal(GroundRouteStatus.Reachable, route.Status);
                var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
                long dx = Math.Clamp(waypoint.X - actor.Feet.X, -32, 32);
                long dy = dx == 0 ? Math.Clamp(waypoint.Y - actor.Feet.Y, -32, 32) : 0;
                authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                    WorldBatchKind.AdvancingStep, [new(delivery, participant.ActorId, dx, dy)], [], state.ProfileVersion));
                state = authority.ReadStateForHost();
                Assert.Equal(actor.Feet.Offset(dx, dy, 0), state.Space.GetActor(participant.ActorId).Feet);
                steps++;
            }
            Assert.Equal(goal, state.Space.GetActor(participant.ActorId).Feet);
            _preparation.Add($"Cohort travel: {participant.ActorId}, {origin} to {goal}, {steps} actual movement commits.");
        }
        state = authority.ReadStateForHost();
        Assert.NotEqual(SpatialInteraction.Reachable,
            state.Space.QueryInteraction(Owner, TargetActorId, scenario.EntryRangeSubunits));
        var preparedEntry = PreviewEntryMovement(state.Space);
        foreach (var participant in scenario.Combatants.Where(actor => actor.Side != 0))
        {
            Assert.True(WorldActorAvailability.CanCooperate(state, participant.ActorId));
            if (participant.ActorId != Owner)
                Assert.Equal(SpatialInteraction.Reachable,
                    preparedEntry.QueryInteraction(Owner, participant.ActorId, scenario.EntryRangeSubunits));
        }
    }

    private string DescribeCohortGoal(WorldSnapshot state, EncounterScenarioDefinition scenario, EntityId actorId, SubunitPosition goal)
    {
        var reasons = new List<string> { $"goal={goal}; occupancy={state.Space.QueryOccupancy(actorId, goal)}; route={state.Space.QueryGroundRoute(actorId, goal).Status}" };
        if (state.Space.QueryOccupancy(actorId, goal) != SpatialOccupancy.Clear
            || state.Space.QueryGroundRoute(actorId, goal).Status != GroundRouteStatus.Reachable)
            return string.Join("; ", reasons);
        var actor = state.Space.GetActor(actorId);
        var candidate = new ActorPresence(actor.Binding, actor.Condition, actor.Scope, goal, actor.Revision, actor.HomePlaceId);
        var geometry = state.Space.WithActors(state.Space.Actors.Select(value => value.ActorId == actorId ? candidate : value));
        for (int step = 0; step < 4; step++)
        {
            var owner = geometry.GetActor(Owner);
            var move = geometry.PrepareMove(Owner, owner.Revision, EntryMovement.X, EntryMovement.Y);
            reasons.Add($"step={step + 1}; movement={move.Status}");
            if (move.Status != SpatialMoveStatus.Prepared) break;
            geometry = geometry.WithActors(geometry.Actors.Select(value => value.ActorId == Owner ? move.Candidate! : value));
            reasons.Add($"reach={geometry.QueryInteraction(Owner, actorId, scenario.EntryRangeSubunits)}");
        }
        return string.Join("; ", reasons);
    }

    private bool AdmitsCohortGoal(WorldSnapshot state, EncounterScenarioDefinition scenario, EntityId actorId, SubunitPosition goal)
    {
        if (state.Space.QueryOccupancy(actorId, goal) != SpatialOccupancy.Clear
            || state.Space.QueryGroundRoute(actorId, goal).Status != GroundRouteStatus.Reachable) return false;
        var actor = state.Space.GetActor(actorId);
        var candidate = new ActorPresence(actor.Binding, actor.Condition, actor.Scope, goal, actor.Revision, actor.HomePlaceId);
        var geometry = state.Space.WithActors(state.Space.Actors.Select(value => value.ActorId == actorId ? candidate : value));
        // The single sample may cover several ordinary cutoffs before its unchanged lease expires.
        // Keep the teammate reachable and out of that path, rather than relying on exactly one cutoff.
        for (int step = 0; step < 4; step++)
        {
            var owner = geometry.GetActor(Owner);
            var move = geometry.PrepareMove(Owner, owner.Revision, EntryMovement.X, EntryMovement.Y);
            if (move.Status != SpatialMoveStatus.Prepared) return false;
            geometry = geometry.WithActors(geometry.Actors.Select(value => value.ActorId == Owner ? move.Candidate! : value));
            if (geometry.QueryInteraction(Owner, actorId, scenario.EntryRangeSubunits) != SpatialInteraction.Reachable) return false;
        }
        return true;
    }

    private LocalSpatialSnapshot PreviewEntryMovement(LocalSpatialSnapshot geometry)
    {
        var owner = geometry.GetActor(Owner);
        var move = geometry.PrepareMove(Owner, owner.Revision, EntryMovement.X, EntryMovement.Y);
        Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
        return geometry.WithActors(geometry.Actors.Select(actor => actor.ActorId == Owner ? move.Candidate! : actor));
    }

    internal async Task Stop()
    {
        if (_stopped) return;
        foreach (var peer in _peers) await peer.DisposeAsync();
        if (Server is not null) await Server.DisposeAsync();
        if (Host is not null) await Host.DisposeAsync();
        _stopped = true;
    }

    public async ValueTask DisposeAsync()
    {
        try { await Stop(); }
        finally { Certificate.Dispose(); _files.Dispose(); }
    }

    internal sealed class Peer(EntityId actor, PlayerProtocolClient protocol, RemotePlayerSession application, ProtocolTap tap) : IAsyncDisposable
    {
        internal EntityId Actor { get; } = actor;
        internal PlayerProtocolClient Protocol { get; } = protocol;
        internal RemotePlayerSession Application { get; } = application;
        internal ISelectedPlayerSession Control => Application;
        internal ProtocolTap Tap { get; } = tap;
        internal bool Disposed { get; private set; }
        public async ValueTask DisposeAsync()
        {
            if (Disposed) return;
            await Application.DisposeAsync();
            Disposed = true;
        }
    }
}
