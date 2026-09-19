using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

public enum WorldBatchKind { AdvancingStep = 1, HeldInteraction = 2 }

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
        if (profileVersion is not 1 and not 2 and not 3 and not 4 and not 5 and not 6 and not 7 and not 8 || !Enum.IsDefined(kind) || causeId == worldId || causeId == branchId)
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
            if (command.Intent is RunoffCommitmentIntent && profileVersion != 8)
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
        if (kind == WorldBatchKind.HeldInteraction && (moving.Count != 0 || acting.Count == 0))
        {
            throw new ArgumentException("A held interaction has discrete actions and no movement samples.");
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
