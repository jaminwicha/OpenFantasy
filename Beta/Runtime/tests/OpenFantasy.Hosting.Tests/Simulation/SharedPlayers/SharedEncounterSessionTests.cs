using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.Simulation.EncounterOutcome.TwoHumanOutcomeWorld;
using static OpenFantasy.Hosting.Tests.Worlds.EncounterOutcome.EncounterOutcomeJourneyTests;

namespace OpenFantasy.Hosting.Tests.Simulation.SharedPlayers;

public sealed class SharedEncounterSessionTests
{
    [Fact]
    public async Task Actual_player_ports_hold_choose_read_and_reopen_one_shared_encounter()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = SharedOutcomeCalibration.WithObserverCare(
            CargoOutcomeWorldAdmission.CreateTwoHumanWolves("shared-players", "Traveler", 1));
        var scenario = genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        EntityId player = scenario.InitiatingActorId;
        EntityId wolf = scenario.TargetActorId;
        EntityId bystander = genesis.EncounterLifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        ContentHash final;
        byte[] finalBytes;
        WorldActionCommand wolfReading;
        byte[] wolfReceipt;
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using (var host = new LocalWorldSession(authority, player, DeliveryOf(genesis, player), time))
        {
            await using LocalPlayerConnection peer = await host.ConnectPlayerAsync(wolf, DeliveryOf(genesis, wolf));
            await using LocalPlayerConnection observer = await host.ConnectPlayerAsync(bystander, DeliveryOf(genesis, bystander));
            host.SetReady(true);
            peer.SetReady(true);
            var entry = host.ReadView().EncounterLifecycle!.Entries.Single(value => value.ScenarioId == scenario.DefinitionId);
            Task<WorldActionReceipt> entered = host.SubmitAction(Command(player, new EnterEncounterIntent(scenario.DefinitionId,
                entry.EncounterId, genesis.EncounterLifecycle.EncounterCount, entry.TargetRevision)));
            await Tick(time);
            Assert.Equal(WorldActionRejection.None, (await entered).Rejection);
            observer.SetReady(true);
            CareAgreement observerPermission = genesis.Care!.Agreements.Single(value => value.PatientActorId == bystander);
            foreach (EncounterLifecyclePhase expected in new[] { EncounterLifecyclePhase.Staging, EncounterLifecyclePhase.Active })
            {
                WorldSnapshot staging = authority.ReadStateForHost();
                Task<WorldActionReceipt> noise = observer.SubmitAction(Command(bystander,
                    new AcknowledgeCareIntent(observerPermission.AgreementId, staging.EncounterActions!.Scope.EncounterId,
                        staging.EncounterLifecycle!.Revision, staging.EncounterActions.ContentHash)));
                await Tick(time);
                Assert.NotEqual(WorldActionRejection.None, (await noise).Rejection);
                Assert.Equal(expected, authority.ReadStateForHost().EncounterLifecycle!.Phase);
            }
            observer.SetReady(false);
            // One connected human has not made its required choice. The other cannot spend that hold.
            peer.SetReady(false);
            WorldSnapshot ready = authority.ReadStateForHost();
            Task<WorldActionReceipt> ownWait = host.SubmitAction(Command(player,
                new QueueEncounterWaitIntent(ready.Pacing!.Encounter!.EncounterId, ready.Pacing.Encounter.Revision)));
            await Tick(time);
            Assert.Equal(WorldActionRejection.None, (await ownWait).Rejection);
            WorldSnapshot held = authority.ReadStateForHost();
            for (int count = 0; count < 8; count++) await Tick(time);
            Assert.Equal(held.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(LocalWorldRunState.Waiting, host.ReadRunState());
            peer.SetReady(true);
            ready = authority.ReadStateForHost();
            Task<WorldActionReceipt> peerWait = peer.SubmitAction(Command(wolf,
                new QueueEncounterWaitIntent(ready.Pacing!.Encounter!.EncounterId, ready.Pacing.Encounter.Revision)));
            await Tick(time);
            Assert.Equal(WorldActionRejection.None, (await peerWait).Rejection);

            for (int cutoff = 0; authority.ReadStateForHost().EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; cutoff++)
            {
                Assert.True(cutoff < 2_000, "The shared-port fight did not reach its real outcome.");
                WorldSnapshot state = authority.ReadStateForHost();
                var timing = state.Pacing!.Encounter!;
                var queued = new List<Task<WorldActionReceipt>>();
                if (AutomaticChoicesSettled(state))
                {
                    foreach (var actor in WorldEncounterActions.DueActors(state).Where(actor => actor.Control == EncounterActorControl.RequiredHuman
                        && timing.Decisions.All(value => value.ActorId != actor.ActorId)))
                    {
                        WorldActionIntent intent = actor.ActorId == player ? Strike(state, wolf)
                            : new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision);
                        queued.Add((actor.ActorId == player ? host.Player : peer).SubmitAction(Command(actor.ActorId, intent)));
                    }
                }
                await Tick(time);
                foreach (Task<WorldActionReceipt> result in queued) Assert.Equal(WorldActionRejection.None, (await result).Rejection);
                if (queued.Count == 2)
                {
                    Assert.Equal((await queued[0]).CommitSequence, (await queued[1]).CommitSequence);
                }
            }
            Assert.Equal(LocalOutcomeStanding.YourSideStands, host.ReadView().EncounterOutcome!.Standing);
            Assert.Equal(LocalOutcomeStanding.OpponentStands, peer.ReadView().EncounterOutcome!.Standing);
            Assert.Equal(LocalOutcomeStanding.Observer, observer.ReadView().EncounterOutcome!.Standing);
            Assert.Null(observer.ReadView().EncounterOutcome!.Acknowledgment);
            WorldSnapshot outcome = authority.ReadStateForHost();
            Task<WorldActionReceipt> firstRead = host.SubmitAction(Command(player, host.ReadView().EncounterOutcome!.Acknowledgment!));
            await Tick(time);
            Assert.Equal(WorldActionRejection.None, (await firstRead).Rejection);
            Assert.Equal(LocalOutcomeStage.AwaitingOthers, host.ReadView().EncounterOutcome!.Stage);
            Assert.Equal(LocalOutcomeStage.AwaitingYourReading, peer.ReadView().EncounterOutcome!.Stage);
            host.SetReady(false);
            wolfReading = Command(wolf, peer.ReadView().EncounterOutcome!.Acknowledgment!);
            Task<WorldActionReceipt> secondRead = peer.SubmitAction(wolfReading);
            await Tick(time);
            wolfReceipt = WorldReceiptCodecV1.Encode(await secondRead);
            Assert.Equal(WorldActionRejection.None, (await secondRead).Rejection);
            peer.SetReady(false);
            ContentHash noReady = authority.ReadStateForHost().ContentHash;
            for (int count = 0; count < 8; count++) await Tick(time);
            Assert.Equal(noReady, authority.ReadStateForHost().ContentHash);
            peer.SetReady(true);
            observer.SetReady(true);
            CareAgreement observerAgreement = genesis.Care!.Agreements.Single(value => value.PatientActorId == bystander);
            Task<WorldActionReceipt> unrelatedCare = observer.SubmitAction(Command(bystander,
                new AcknowledgeCareIntent(observerAgreement.AgreementId, outcome.EncounterActions!.Scope.EncounterId,
                    outcome.EncounterLifecycle!.Revision, outcome.EncounterActions.ContentHash)));
            await Tick(time);
            Assert.NotEqual(WorldActionRejection.None, (await unrelatedCare).Rejection);
            observer.SetReady(false);
            WorldSnapshot completed = authority.ReadStateForHost();
            Assert.Equal(EncounterLifecyclePhase.Exploration, completed.EncounterLifecycle!.Phase);
            Assert.Equal(outcome.Calendar, completed.Calendar);
            Assert.Equal(new[] { player, wolf }.Order(), completed.EncounterOutcome!.Last!.AcknowledgedActorIds);
            Assert.Equal(LocalOutcomeStage.Completed, host.ReadView().EncounterOutcome!.Stage);
            Assert.True(peer.ReadView().EncounterOutcome!.IsTerminal);
            peer.SetReady(false);
            host.SetReady(true);
            var oldReading = WorldEncounterOutcomes.Determine(outcome)!;
            ulong advancingBefore = completed.AdvancingSteps;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Task<WorldActionReceipt> stale = host.SubmitAction(Command(player,
                    new AcknowledgeEncounterOutcomeIntent(oldReading.EncounterId, oldReading.ActionHash)));
                await Tick(time);
                Assert.Equal(WorldActionRejection.Unavailable, (await stale).Rejection);
            }
            Assert.Equal(advancingBefore + 3, authority.ReadStateForHost().AdvancingSteps);
            var agreement = outcome.Care!.Agreements.First(value => value.PatientActorId == player);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Task<WorldActionReceipt> staleCare = host.SubmitAction(Command(player,
                    new AcknowledgeCareIntent(agreement.AgreementId, oldReading.EncounterId,
                        outcome.EncounterLifecycle!.Revision, oldReading.ActionHash)));
                await Tick(time);
                Assert.NotEqual(WorldActionRejection.None, (await staleCare).Rejection);
                Assert.Null(authority.ReadStateForHost().Care!.Pending);
            }
            Assert.Equal(advancingBefore + 6, authority.ReadStateForHost().AdvancingSteps);
            Task<WorldActionReceipt> unknownReading = host.SubmitAction(Command(player,
                new AcknowledgeEncounterOutcomeIntent(Retreat.RetreatJourneyWorld.NewId(), oldReading.ActionHash)));
            await Tick(time);
            Assert.Equal(WorldActionRejection.Unavailable, (await unknownReading).Rejection);
            Assert.Equal(advancingBefore + 7, authority.ReadStateForHost().AdvancingSteps);
            host.SetReady(false);
            final = authority.ReadStateForHost().ContentHash;
            finalBytes = WorldSnapshotCodec.Encode(authority.ReadStateForHost());
            Assert.True((await host.VerifyRecordedHistoryAsync()).ReproducesHead);
        }
        // A fresh host and fresh peer recover the same state, and exact retry retrieves the original receipt even while unready.
        var reopenedAuthority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        await using var reopened = new LocalWorldSession(reopenedAuthority, player, DeliveryOf(genesis, player), new ManualSessionTimeProvider());
        await using LocalPlayerConnection returned = await reopened.ConnectPlayerAsync(wolf, DeliveryOf(genesis, wolf));
        Assert.Equal(final, reopenedAuthority.ReadStateForHost().ContentHash);
        Assert.Equal(finalBytes, WorldSnapshotCodec.Encode(reopenedAuthority.ReadStateForHost()));
        Assert.Equal(wolfReceipt, WorldReceiptCodecV1.Encode(await returned.SubmitAction(wolfReading)));
        Assert.Equal(final, reopenedAuthority.ReadStateForHost().ContentHash);

        WorldActionCommand Command(EntityId actor, WorldActionIntent intent)
        {
            WorldSnapshot state = authority.ReadStateForHost();
            TrustedCommandDelivery delivery = DeliveryOf(state, actor);
            return new WorldActionCommand(Hosting.Tests.Simulation.Retreat.RetreatJourneyWorld.NewId(), delivery.PrincipalId,
                delivery.SessionId, actor, state.WorldId, state.BranchId, state.Space.GetActor(actor).Revision, intent);
        }
    }

    [Fact]
    public async Task Simultaneous_choices_use_one_presented_timing_revision_and_inquiries_stay_with_their_owner()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateTwoHumanWolves("shared-choice", "Traveler", 1);
        var scenario = genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        EntityId player = scenario.InitiatingActorId;
        EntityId wolf = scenario.TargetActorId;
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, player, DeliveryOf(genesis, player), time);
        await using LocalPlayerConnection peer = await host.ConnectPlayerAsync(wolf, DeliveryOf(genesis, wolf));
        var market = genesis.Coordination!.Markets!.Markets.First();
        var firstInquiry = new LocalTradeInquiry(market.MarketId, market.Commodity, LocalTradeDirection.Buy, 1);
        var secondInquiry = new LocalTradeInquiry(market.MarketId, market.Commodity, LocalTradeDirection.Buy, 2);
        await host.SubmitTradeInquiryAsync(firstInquiry);
        await peer.SubmitTradeInquiryAsync(secondInquiry);
        Assert.Equal(firstInquiry, host.ReadTradeInquiry()!.Inquiry);
        Assert.Equal(secondInquiry, peer.ReadTradeInquiry()!.Inquiry);
        await peer.CancelTradeInquiryAsync();
        Assert.Equal(LocalTradeInquiryState.Waiting, host.ReadTradeInquiry()!.State);
        Assert.Equal(LocalTradeInquiryState.Cancelled, peer.ReadTradeInquiry()!.State);
        await host.CancelTradeInquiryAsync();
        host.SetReady(true);
        peer.SetReady(true);
        var entry = host.ReadView().EncounterLifecycle!.Entries.Single(value => value.ScenarioId == scenario.DefinitionId);
        Task<WorldActionReceipt> entered = host.SubmitAction(Command(player, new EnterEncounterIntent(scenario.DefinitionId,
            entry.EncounterId, genesis.EncounterLifecycle.EncounterCount, entry.TargetRevision)));
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await entered).Rejection);
        for (int count = 0; authority.ReadStateForHost().EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; count++)
        {
            Assert.True(count < 20);
            await Tick(time);
        }
        WorldSnapshot captured = authority.ReadStateForHost();
        var timing = captured.Pacing!.Encounter!;
        Assert.Contains(WorldEncounterActions.DueActors(captured), actor => actor.ActorId == player);
        Assert.Contains(WorldEncounterActions.DueActors(captured), actor => actor.ActorId == wolf);
        var intent = new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision);
        Task<WorldActionReceipt> first = host.SubmitAction(Command(player, intent));
        Task<WorldActionReceipt> second = peer.SubmitAction(Command(wolf, intent));
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await first).Rejection);
        Assert.Equal(WorldActionRejection.None, (await second).Rejection);
        Assert.Equal((await first).CommitSequence, (await second).CommitSequence);
        Assert.Equal(captured.Calendar, authority.ReadStateForHost().Calendar);
        Assert.Equal(2, authority.ReadStateForHost().Pacing!.Encounter!.Decisions.Count(decision => decision.ActorId == player || decision.ActorId == wolf));
        EntityId observerActor = genesis.EncounterLifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        await using LocalPlayerConnection observer = await host.ConnectPlayerAsync(observerActor, DeliveryOf(genesis, observerActor));
        observer.SetReady(true);
        ulong beforeSpam = authority.ReadStateForHost().Revision;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            var current = authority.ReadStateForHost().Pacing!.Encounter!;
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => observer.SubmitAction(Command(observerActor,
                new QueueEncounterWaitIntent(current.EncounterId, current.Revision))));
            Task<WorldActionReceipt> entryNoise = observer.SubmitAction(Command(observerActor,
                new EnterEncounterIntent(scenario.DefinitionId, entry.EncounterId, genesis.EncounterLifecycle.EncounterCount, entry.TargetRevision)));
            await Tick(time);
            Assert.NotEqual(WorldActionRejection.None, (await entryNoise).Rejection);
            current = authority.ReadStateForHost().Pacing!.Encounter!;
            Task<WorldActionReceipt> readingNoise = observer.SubmitAction(Command(observerActor,
                new AcknowledgeEncounterOutcomeIntent(current.EncounterId, authority.ReadStateForHost().EncounterActions!.ContentHash)));
            await Tick(time);
            Assert.NotEqual(WorldActionRejection.None, (await readingNoise).Rejection);
        }
        Assert.True(authority.ReadStateForHost().Revision > beforeSpam);
        Assert.True(authority.ReadStateForHost().EncounterActions!.LastResolution is not null);
        await peer.DisposeAsync();
        await using LocalPlayerConnection fresh = await host.ConnectPlayerAsync(wolf, DeliveryOf(genesis, wolf));
        Assert.Null(fresh.ReadTradeInquiry());
        Assert.Throws<ObjectDisposedException>(() => peer.ReadTradeInquiry());

        WorldActionCommand Command(EntityId actor, WorldActionIntent action)
        {
            WorldSnapshot state = authority.ReadStateForHost();
            TrustedCommandDelivery delivery = DeliveryOf(state, actor);
            return new WorldActionCommand(Hosting.Tests.Simulation.Retreat.RetreatJourneyWorld.NewId(), delivery.PrincipalId,
                delivery.SessionId, actor, state.WorldId, state.BranchId, state.Space.GetActor(actor).Revision, action);
        }
    }

    [Fact]
    public async Task Four_real_connections_are_bounded_and_host_stop_cancels_all_inputs()
    {
        using var files = new SessionFiles();
        WorldSnapshot source = RoadThreatWorldAdmission.CreateEmbedded("four-ports", "Traveler");
        EntityId player = source.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId[] actors = source.Residents.Where(resident => resident.ControllerKind == ResidentControllerKind.Host)
            .OrderBy(resident => resident.ActorId).Take(4).Select(resident => resident.ActorId).ToArray();
        Assert.Equal(4, actors.Length);
        WorldSnapshot genesis = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(source, new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), actors));
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, player, DeliveryOf(genesis, player), time);
        List<LocalPlayerConnection> peers = [];
        foreach (EntityId actor in actors.Take(3)) peers.Add(await host.ConnectPlayerAsync(actor, DeliveryOf(genesis, actor)));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => host.ConnectPlayerAsync(actors[3], DeliveryOf(genesis, actors[3])));
        foreach (LocalPlayerConnection port in peers) port.SetReady(true);
        await Tick(time);
        Assert.Equal(1UL, authority.ReadStateForHost().AdvancingSteps);
        Assert.All(peers, port => Assert.Equal(host.ReadView().Calendar, port.ReadView().Calendar));
        host.SetReady(true);
        List<Task<WorldActionReceipt>> pending = [];
        foreach (LocalPlayerConnection port in peers.Prepend(host.Player))
        {
            EntityId target = port.ActorId == player ? actors[0] : player;
            var state = authority.ReadStateForHost();
            var delivery = DeliveryOf(state, port.ActorId);
            pending.Add(port.SubmitAction(new WorldActionCommand(Hosting.Tests.Simulation.Retreat.RetreatJourneyWorld.NewId(),
                delivery.PrincipalId, delivery.SessionId, port.ActorId, state.WorldId, state.BranchId,
                state.Space.GetActor(port.ActorId).Revision, new SpeakToActorIntent(target, state.Space.GetActor(target).Revision))));
        }
        await host.StopAsync();
        foreach (Task<WorldActionReceipt> request in pending)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.All(peers, port => Assert.Equal(LocalWorldRunState.Unavailable, port.ReadRunState()));
        foreach (LocalPlayerConnection port in peers) await port.DisposeAsync();
    }
}
