using OpenFantasy.Core.Cognition;

namespace OpenFantasy.Core.Tests.Cognition;

/// <summary>Identities and evidence for Wren's delayed-supply episode, matching the Python oracle's fixture.</summary>
internal static class CognitionFixture
{
    internal static readonly EntityId World = Id(1), Branch = Id(2), Wren = Id(10), Player = Id(20), Quest = Id(300),
        Project = Id(400), Source = Id(501), OtherSource = Id(502), ObserveCommand = Id(701), GatherCommand = Id(702),
        DeliveryCommand = Id(703), Observed = Id(801), Delivered = Id(802);

    internal static EntityId Id(ulong value) => new(0, value);
    internal static WorldInstant At(int milliseconds) => new(0, milliseconds);

    internal static CognitionEvidence Observation(EntityId source, ulong remaining, int at, EntityId? id = null) =>
        new(id ?? Observed, Wren, EvidenceKind.SourceObserved, ObserveCommand, null, source, remaining, At(at));

    internal static CognitionEvidence Delivery(int at = 5000) =>
        new(Delivered, Wren, EvidenceKind.QuestDeliveryReceived, DeliveryCommand, Player, Quest, 4, At(at));

    internal static CognitionEvidence Gathered(EntityId source, ulong yield, int at, EntityId id) =>
        new(id, Wren, EvidenceKind.GatherOutcome, GatherCommand, null, source, yield, At(at));

    internal static CognitionDecision Decision(CognitionIntention? intention, CognitionDeferral deferral, ulong step = 0,
        bool completed = false) =>
        new(CognitionPolicy.Version, intention, deferral, new CognitionWake(step + CognitionPolicy.FallbackSteps,
            WakeCause.Fallback), completed);

    internal static CognitionSituation Situation(ulong step = 0, bool needsWork = true, bool tool = true, ulong stock = 0, ulong target = 2,
        bool effort = true, bool hold = false, IEnumerable<EntityId>? reachable = null) =>
        new(step, Project, Quest, needsWork, tool, stock, target, effort, hold, (reachable ?? []).ToHashSet());

    /// <summary>The oracle's "wren_working" state, built through the domain operations rather than by hand.</summary>
    internal static CognitionSnapshot WrenWorking()
    {
        ActorCognition wren = ActorCognition.Empty(Wren);
        wren = wren.Adopt(Decision(new CognitionIntention(IntentionKind.AwaitSupply, Project, At(0), []),
            CognitionDeferral.MissingTool), At(0));
        wren = wren.Acquire(Observation(Source, 3, 1000), 20).Acquire(Delivery(), 100);
        wren = wren.Adopt(CognitionPolicy.Decide(wren, Situation(step: 100, tool: false), At(5000)), At(5000));
        var snapshot = new CognitionSnapshot(World, Branch, 0, []);
        return new CognitionSnapshot(World, Branch, 5, snapshot.With(wren).With(ActorCognition.Empty(Player)).Actors);
    }
}
