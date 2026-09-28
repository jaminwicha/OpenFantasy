using OpenFantasy.Core.Care;
using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

public enum WorldBatchKind { AdvancingStep = 1, HeldInteraction = 2, ConsequenceContinuation = 4, TacticalAdvance = 5, EncounterDecision = 6, EncounterResolution = 7, EncounterLifecycle = 8, Care = 9 }

/// <summary>One control sample selected at the host cutoff; it cannot request extra simulation time.</summary>
public sealed record WorldMovementInput
{
    public const long DisplacementPerStep = 32;

    public WorldMovementInput(TrustedCommandDelivery delivery, EntityId actorId, long deltaX, long deltaY)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        bool horizontal = (deltaX == DisplacementPerStep || deltaX == -DisplacementPerStep) && deltaY == 0;
        bool vertical = (deltaY == DisplacementPerStep || deltaY == -DisplacementPerStep) && deltaX == 0;
        if (!horizontal && !vertical)
        {
            throw new ArgumentException("A movement sample selects one cardinal displacement under the pinned pacing.");
        }
        Delivery = delivery;
        ActorId = actorId;
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    public TrustedCommandDelivery Delivery { get; }
    public EntityId ActorId { get; }
    public long DeltaX { get; }
    public long DeltaY { get; }
}

/// <summary>Typed internal commit input. Discrete actor commands retain their own identities and outcomes.</summary>
public sealed class WorldStepBatch
{
    public const int MaximumActorsPerBatch = 64;

    public WorldStepBatch(EntityId causeId, EntityId worldId, EntityId branchId, ulong expectedRevision,
        ulong expectedAdvancingSteps, WorldBatchKind kind, IEnumerable<WorldMovementInput> movement,
        IEnumerable<WorldActionCommand> actions, ushort profileVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(actions);
        if (profileVersion is not 1 and not 2 and not 3 and not 4 and not 5 and not 6 and not 7 and not 8 and not 9 and not 10 and not 11 and not 12 and not 13 and not 14 and not 15 and not 16 and not 17 and not 18 and not 19 and not 20 and not 21 and not 22 and not 23 and not 24 and not 25 and not 26 and not 27 || !Enum.IsDefined(kind) || causeId == worldId || causeId == branchId)
        {
            throw new ArgumentException("The world batch kind or cause identity is invalid.");
        }
        var moving = new SortedDictionary<EntityId, WorldMovementInput>();
        foreach (WorldMovementInput input in movement)
        {
            ArgumentNullException.ThrowIfNull(input);
            if (moving.Count == MaximumActorsPerBatch || !moving.TryAdd(input.ActorId, input))
            {
                throw new ArgumentException("A world step accepts at most one movement sample per actor.");
            }
        }
        var acting = new SortedDictionary<EntityId, WorldActionCommand>();
        HashSet<EntityId> commandIds = [];
        foreach (WorldActionCommand command in actions)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (command.Intent is UseRestorativeItemIntent or RevokeCarePermissionIntent
                && (profileVersion is not 26 and not 27 || kind != WorldBatchKind.AdvancingStep))
            {
                throw new ArgumentException("Ordinary care requires its explicit advancing exploration profile.");
            }
            if (command.Intent is AcknowledgeCareIntent or AdministerRemedyIntent
                && (profileVersion is not 25 and not 26 and not 27 || kind != WorldBatchKind.Care))
            {
                throw new ArgumentException("Care choices require their explicit profile and care boundary.");
            }
            if (command.Intent is QueueEncounterWaitIntent && (profileVersion is not 22 and not 23 and not 24 and not 25 and not 26 and not 27 || kind != WorldBatchKind.EncounterDecision))
            {
                throw new ArgumentException("Encounter choices require their explicit decision boundary.");
            }
            if (command.Intent is QueueEncounterAbilityIntent && (profileVersion is not 23 and not 24 and not 25 and not 26 and not 27 || kind != WorldBatchKind.EncounterDecision))
            {
                throw new ArgumentException("An ability choice requires its admitted combat decision boundary.");
            }
            if (command.Intent is EnterEncounterIntent or ReleaseEncounterIntent
                && (profileVersion is not 24 and not 25 and not 26 and not 27 || kind != WorldBatchKind.EncounterLifecycle))
            {
                throw new ArgumentException("Lifecycle choices require the explicit lifecycle boundary.");
            }
            if (command.Intent is ClimbIntent && profileVersion is not 11 and not 12 and not 13 and not 14 and not 15 and not 16 and not 17 and not 18)
            {
                throw new ArgumentException("Assisted climbing requires its explicit world profile.");
            }
            if (command.Intent is ExcavateIntent && profileVersion is not 10 and not 11 and not 12 and not 13 and not 14 and not 15 and not 16 and not 17 and not 18 and not 21 and not 22 and not 23 and not 24 and not 25 and not 26 and not 27)
            {
                throw new ArgumentException("Excavation requires its explicit world profile.");
            }
            if (command.Intent is ScheduleRunoffTransportIntent && profileVersion != 9)
            {
                throw new ArgumentException("Scheduled reservoir transport requires its explicit world profile.");
            }
            if (command.Intent is RunoffCommitmentIntent && profileVersion is not 8 and not 9 and not 19)
            {
                throw new ArgumentException("Runoff commitments require the physical reservoir world profile.");
            }
            if (command.Intent is WorkConstructionIntent && profileVersion < 5)
            {
                throw new ArgumentException("Construction work requires its admitted world profile.");
            }
            if (command.Intent is DeliveryQuestIntent && profileVersion < 4)
            {
                throw new ArgumentException("Quest actions require the funded quest world profile.");
            }
            if (command.Intent is CraftIntent && profileVersion < 3)
            {
                throw new ArgumentException("Crafting actions require the production world profile.");
            }
            if (acting.Count == MaximumActorsPerBatch || !acting.TryAdd(command.ActorId, command)
                || !commandIds.Add(command.CommandId) || command.CommandId == causeId
                || command.WorldId != worldId || command.BranchId != branchId)
            {
                throw new ArgumentException("A batch needs unique commands, one action per actor, in its own world/branch.");
            }
        }
        if (kind == WorldBatchKind.Care && (profileVersion is not 25 and not 26 and not 27 || moving.Count != 0 || acting.Count != 1
            || acting.Values.Any(command => command.Intent is not AcknowledgeCareIntent and not AdministerRemedyIntent)))
        {
            throw new ArgumentException("A care boundary requires exactly one care command and no movement.");
        }
        if (kind == WorldBatchKind.EncounterLifecycle && (profileVersion is not 24 and not 25 and not 26 and not 27 || moving.Count != 0 || acting.Count > 1
            || acting.Values.Any(command => command.Intent is not EnterEncounterIntent and not ReleaseEncounterIntent)))
        {
            throw new ArgumentException("A lifecycle boundary has one explicit command or one system staging transition.");
        }
        if (kind == WorldBatchKind.HeldInteraction && (moving.Count != 0 || acting.Count == 0))
        {
            throw new ArgumentException("A held interaction has discrete actions and no movement samples.");
        }
        if (kind == WorldBatchKind.ConsequenceContinuation
            && (profileVersion is not 16 and not 17 and not 18 || moving.Count != 0 || acting.Count != 0))
        {
            throw new ArgumentException("Consequence continuation accepts no new controls or commands.");
        }
        if (kind is WorldBatchKind.TacticalAdvance or WorldBatchKind.EncounterResolution or WorldBatchKind.EncounterDecision)
        {
            if (profileVersion is not 22 and not 23 and not 24 and not 25 and not 26 and not 27 || moving.Count != 0
                || kind == WorldBatchKind.EncounterDecision && (acting.Count == 0 || acting.Values.Any(command => command.Intent is not QueueEncounterWaitIntent and not QueueEncounterAbilityIntent))
                || kind != WorldBatchKind.EncounterDecision && acting.Count != 0)
            {
                throw new ArgumentException("Encounter boundaries require complete decision-only or actionless tactical inputs.");
            }
        }
        CauseId = causeId;
        WorldId = worldId;
        BranchId = branchId;
        ExpectedRevision = expectedRevision;
        ExpectedAdvancingSteps = expectedAdvancingSteps;
        Kind = kind;
        ProfileVersion = profileVersion;
        Movement = Array.AsReadOnly(moving.Values.ToArray());
        Actions = Array.AsReadOnly(acting.Values.ToArray());
    }

    public EntityId CauseId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong ExpectedRevision { get; }
    public ulong ExpectedAdvancingSteps { get; }
    public WorldBatchKind Kind { get; }
    public ushort ProfileVersion { get; }
    public ReadOnlyCollection<WorldMovementInput> Movement { get; }
    public ReadOnlyCollection<WorldActionCommand> Actions { get; }
}
