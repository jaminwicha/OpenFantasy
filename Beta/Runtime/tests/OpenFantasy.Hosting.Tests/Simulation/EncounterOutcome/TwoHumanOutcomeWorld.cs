using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.EncounterOutcome;

/// <summary>
/// A declared two-human managed fixture over one durable outcome-completion save: the local session drives its own
/// player at ordinary cutoffs, and every other human acts only through its own control binding as another writer of the
/// same authority between those cutoffs. Nothing is injected into the world; no network or device is implied.
/// </summary>
internal sealed class TwoHumanOutcomeWorld : IAsyncDisposable
{
    private bool _stopped;

    internal TwoHumanOutcomeWorld(string path, WorldSnapshot genesis, bool reopen = false)
    {
        Authority = reopen ? SqliteWorldAuthority.OpenExisting(path, WorldSnapshotCodec.Encode(genesis))
            : SqliteWorldAuthority.CreateNew(path, genesis);
        WorldSnapshot state = Authority.ReadStateForHost();
        EncounterLifecycleSnapshot lifecycle = state.EncounterLifecycle!;
        Scenario = lifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        Player = Scenario.InitiatingActorId;
        FirstWolf = Scenario.TargetActorId;
        SecondWolf = lifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        Delivery = DeliveryOf(state, Player);
        Session = new LocalWorldSession(Authority, Player, Delivery, Time);
        Session.SetReady(true);
    }

    internal SqliteWorldAuthority Authority { get; }
    internal ManualSessionTimeProvider Time { get; } = new();
    internal LocalWorldSession Session { get; }
    internal TrustedCommandDelivery Delivery { get; }
    internal EncounterScenarioDefinition Scenario { get; }
    internal EntityId Player { get; }
    internal EntityId FirstWolf { get; }
    internal EntityId SecondWolf { get; }
    internal WorldSnapshot State => Authority.ReadStateForHost();

    internal Task Tick() => SessionFixture.Tick(Time);

    internal static TrustedCommandDelivery DeliveryOf(WorldSnapshot state, EntityId actor)
    {
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
    }

    /// <summary>A command for the actor, from its own controller unless another (forged) delivery is named.</summary>
    internal WorldActionCommand Command(EntityId actor, WorldActionIntent intent, TrustedCommandDelivery? delivery = null)
    {
        WorldSnapshot state = State;
        TrustedCommandDelivery origin = delivery ?? DeliveryOf(state, actor);
        return new WorldActionCommand(RetreatJourneyWorld.NewId(), origin.PrincipalId, origin.SessionId, actor, state.WorldId,
            state.BranchId, state.Space.GetActor(actor).Revision, intent);
    }

    internal WorldStepBatch Batch(WorldActionCommand? command, WorldBatchKind kind = WorldBatchKind.EncounterLifecycle)
    {
        WorldSnapshot state = State;
        return new WorldStepBatch(RetreatJourneyWorld.NewId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            kind, [], command is null ? [] : [command], state.ProfileVersion);
    }

    /// <summary>Another human's own writer commits one of its own commands between session cutoffs.</summary>
    internal WorldActionReceipt CommitOwn(EntityId actor, WorldActionIntent intent, WorldBatchKind kind = WorldBatchKind.EncounterLifecycle) =>
        CommitCommand(Command(actor, intent), kind);

    /// <summary>Commits an exact, already built command of another human's own writer (kept for exact-retry checks).</summary>
    internal WorldActionReceipt CommitCommand(WorldActionCommand command, WorldBatchKind kind = WorldBatchKind.EncounterLifecycle) =>
        Authority.Commit(Batch(command, kind)).Receipts.Single(value => value.CommandId == command.CommandId);

    internal async Task<WorldActionReceipt> Submit(WorldActionCommand command)
    {
        Task<WorldActionReceipt> pending = Session.SubmitAction(command);
        await Tick();
        await SessionFixture.Until(() => pending.IsCompleted);
        return await pending;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stopped) return;
        _stopped = true;
        await Session.StopAsync();
    }
}
